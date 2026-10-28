using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName=".NET Framework 4.8")]
[assembly: System.Reflection.AssemblyVersion("1.2.0.0")]

namespace InkPage {
 static class Program {
  [STAThread] static void Main(string[] args) {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   bool testing=args.Length>0&&args[0]=="--self-test";
   bool first; using(var mutex=new Mutex(true,testing?"InkPageMarkdown_SelfTest":"InkPageMarkdown_1",out first)) {
    if(!first){MessageBox.Show("墨页已经在运行，请切换到已打开的窗口。","墨页");return;}
    Application.ThreadException+=(s,e)=>MessageBox.Show(e.Exception.Message,"墨页 · 出错了");
    Application.Run(new MainWindow(args.Length>0?args[0]:null));
   }
  }
 }
 public sealed class MainWindow:Form {
  readonly WebView2 web=new WebView2();
  readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue};
  readonly string root=AppDomain.CurrentDomain.BaseDirectory;
  readonly string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"InkPageMarkdown");
  readonly System.Windows.Forms.Timer draftTimer=new System.Windows.Forms.Timer{Interval=1500};
  string path=null,text="",saved="",encoding="UTF-8",initialPath; bool dirty=false,ready=false,busy=false;
  string DraftPath {get{return Path.Combine(data,"draft.json");}}
  public MainWindow(string file){initialPath=file;Text="墨页 Markdown";Size=new Size(1260,860);MinimumSize=new Size(900,620);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(243,244,242);AutoScaleDimensions=new SizeF(96F,96F);AutoScaleMode=AutoScaleMode.Dpi;
   FormBorderStyle=FormBorderStyle.None;BackColor=Color.FromArgb(225,230,224);Padding=new Padding(5);Resize+=(s,e)=>UpdateChrome();
   if(file=="--self-test")data=Path.Combine(root,".test-output");
   try{Icon=new Icon(Path.Combine(root,"app","inkpage.ico"));}catch{}
   web.Dock=DockStyle.Fill;Controls.Add(web);Shown+=async(s,e)=>await Initialize();FormClosing+=HandleClosing;
   draftTimer.Tick+=(s,e)=>{draftTimer.Stop();WriteDraft();};
  }
  [DllImport("user32.dll")] static extern bool ReleaseCapture();
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
  [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h,int flags);
  [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
  [StructLayout(LayoutKind.Sequential)] struct NativePoint {public int X,Y;}
  [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public NativeRect Monitor,Work;public int Flags;}
  [StructLayout(LayoutKind.Sequential)] struct MinMaxInfo {public NativePoint Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
  protected override CreateParams CreateParams {get{var p=base.CreateParams;p.Style|=0x00040000|0x00020000|0x00010000|0x00080000;return p;}}
  void UpdateChrome(){int edge=WindowState==FormWindowState.Maximized?0:Math.Max(4,(int)Math.Round(5.0*DeviceDpi/96));if(Padding.Left!=edge)Padding=new Padding(edge);}
  protected override void WndProc(ref System.Windows.Forms.Message m){
   if(m.Msg==0x83){m.Result=IntPtr.Zero;return;}
   if(m.Msg==0x24){base.WndProc(ref m);var monitor=MonitorFromWindow(Handle,2);var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(GetMonitorInfo(monitor,ref info)){var mm=(MinMaxInfo)Marshal.PtrToStructure(m.LParam,typeof(MinMaxInfo));mm.MaxPosition.X=info.Work.Left-info.Monitor.Left;mm.MaxPosition.Y=info.Work.Top-info.Monitor.Top;mm.MaxSize.X=info.Work.Right-info.Work.Left;mm.MaxSize.Y=info.Work.Bottom-info.Work.Top;Marshal.StructureToPtr(mm,m.LParam,false);}return;}
   base.WndProc(ref m);
   if(m.Msg==0x84&&WindowState==FormWindowState.Normal){long packed=m.LParam.ToInt64();var p=PointToClient(new Point(unchecked((short)(packed&65535)),unchecked((short)((packed>>16)&65535))));int edge=Math.Max(5,Padding.Left);bool left=p.X<edge,right=p.X>=ClientSize.Width-edge,top=p.Y<edge,bottom=p.Y>=ClientSize.Height-edge;int hit=top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:1;m.Result=new IntPtr(hit);}
   if(m.Msg==0x2E0)UpdateChrome();
  }
  void WindowCommand(string command){switch(command){case "minimize":WindowState=FormWindowState.Minimized;break;case "maximize":WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;break;case "close":BeginInvoke(new Action(Close));break;case "drag":ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);break;}}
  async Task Initialize(){try{
   Directory.CreateDirectory(data);
   var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(data,"WebView"));await web.EnsureCoreWebView2Async(env);
   var core=web.CoreWebView2;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreDevToolsEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.IsZoomControlEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;
   core.SetVirtualHostNameToFolderMapping("inkpage.local",Path.Combine(root,"app"),CoreWebView2HostResourceAccessKind.DenyCors);
   core.WebMessageReceived+=Message;
   core.NavigationStarting+=(s,e)=>{Uri u;if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out u)||u.Host!="inkpage.local"||u.Scheme!="https")e.Cancel=true;};
   core.NewWindowRequested+=(s,e)=>{e.Handled=true;};core.DownloadStarting+=(s,e)=>e.Cancel=true;
   core.PermissionRequested+=(s,e)=>e.State=CoreWebView2PermissionState.Deny;
   web.Source=new Uri("https://inkpage.local/index.html");
  }catch(Exception ex){MessageBox.Show("无法启动阅读界面。请确认已安装 Microsoft Edge WebView2 Runtime。\n\n"+ex.Message,"墨页 · 启动失败",MessageBoxButtons.OK,MessageBoxIcon.Error);Close();}}
  new string Name {get{return path==null?"未命名.md":Path.GetFileName(path);}}
  void Post(object obj){if(ready)web.CoreWebView2.PostWebMessageAsJson(json.Serialize(obj));}
  void Toast(string message){Post(new{type="toast",text=message});}
  void Status(){Text=(dirty?"● ":"")+Name+" — 墨页 Markdown";Post(new{type="status",name=Name,path=path,dirty=dirty,encoding=encoding,@base=path==null?"":"https://document.local/"});}
  string MapDocumentFolder(){web.CoreWebView2.ClearVirtualHostNameToFolderMapping("document.local");if(path==null)return "";web.CoreWebView2.SetVirtualHostNameToFolderMapping("document.local",Path.GetDirectoryName(path),CoreWebView2HostResourceAccessKind.DenyCors);return "https://document.local/";}
  void Document(){string b=MapDocumentFolder();Post(new{type="document",name=Name,path=path,text=text,dirty=dirty,encoding=encoding,@base=b});Status();}
  async void Message(object sender,CoreWebView2WebMessageReceivedEventArgs e){
   if(!e.Source.StartsWith("https://inkpage.local/",StringComparison.Ordinal))return;
   try {var m=json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);string type=Convert.ToString(m["type"]);
    if(type=="change"){text=Convert.ToString(m["text"]);dirty=saved==null||text!=saved;Status();draftTimer.Stop();if(dirty)draftTimer.Start();else DeleteDraft();return;}
    if(busy)return;busy=true;
    try{switch(type){
     case "ready":ready=true;if(initialPath=="--self-test")await SelfTest();else Startup();break;
     case "window":WindowCommand(Convert.ToString(m["command"]));break;
     case "theme":BackColor=Convert.ToBoolean(m["dark"])?Color.FromArgb(58,69,62):Color.FromArgb(225,230,224);break;
     case "new":if(ConfirmLeave()){path=null;text="";saved="";encoding="UTF-8";dirty=false;DeleteDraft();Document();}break;
     case "open":if(ConfirmLeave())using(var d=new OpenFileDialog{Filter="Markdown / 文本文件|*.md;*.markdown;*.txt;*.mdown|所有文件|*.*",Title="打开 Markdown 文档"})if(d.ShowDialog(this)==DialogResult.OK)LoadFile(d.FileName);break;
     case "save":Save(false);break;case "saveAs":Save(true);break;
     case "pdf":await ExportPdf();break;case "html":await ExportHtml();break;
     case "help":MessageBox.Show("墨页 Markdown 1.2\n\nCtrl+O 打开 · Ctrl+N 新建\nCtrl+S 保存 · Ctrl+Shift+S 另存为\nCtrl+P 导出 PDF · Ctrl+F 查找\nCtrl+B 粗体 · Ctrl+I 斜体\n\n阅读 / 双栏 / 编辑三种模式；点击目录跳转。\n支持标题、列表、任务列表、表格、引用和代码块。\n本地图片按 Markdown 文件所在文件夹解析；远程图片默认不加载。\n打开 UTF-8 / 带 BOM 的 UTF-16 文本，保存统一为 UTF-8。\n未保存草稿会留在本机，异常退出后可恢复。\n\n此版本暂不支持 LaTeX 公式、Mermaid、插件或多标签。","使用说明",MessageBoxButtons.OK,MessageBoxIcon.Information);break;
     case "link":OpenLink(Convert.ToString(m["url"]));break;
    }}finally{busy=false;}
   }catch(Exception ex){MessageBox.Show("操作未完成：\n"+ex.Message,"墨页",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  }
  void Startup(){bool recovered=false;
   if(File.Exists(DraftPath)){try{var draft=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(DraftPath,Encoding.UTF8));if(MessageBox.Show("发现上次未保存的草稿，是否恢复？","恢复草稿",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes){text=Convert.ToString(draft["text"]);path=draft.ContainsKey("path")?draft["path"] as string:null;saved=draft.ContainsKey("saved")?draft["saved"] as string:null;dirty=true;encoding="UTF-8";recovered=true;Document();}else DeleteDraft();}catch{MessageBox.Show("草稿暂时无法读取，已保留原文件：\n"+DraftPath,"墨页");}}
   if(recovered)return;
   if(initialPath!=null){try{LoadFile(Path.GetFullPath(initialPath));return;}catch(Exception ex){MessageBox.Show(ex.Message,"打开失败");}}
   text=File.ReadAllText(Path.Combine(root,"app","欢迎使用.md"),Encoding.UTF8);saved=text;path=null;dirty=false;Document();
  }
  string ReadText(string p,out string label){using(var r=new StreamReader(p,new UTF8Encoding(false,true),true)){var content=r.ReadToEnd();label=r.CurrentEncoding.WebName.ToUpperInvariant();return content;}}
  void LoadFile(string p){var info=new FileInfo(p);if(info.Length>10*1024*1024)throw new IOException("当前版本支持 10 MB 以内的文本文件。");string enc;string loaded=ReadText(p,out enc);path=Path.GetFullPath(p);text=loaded;saved=loaded;dirty=false;encoding=enc;DeleteDraft();Document();}
  bool ConfirmLeave(){if(!dirty)return true;var result=MessageBox.Show("要保存对「"+Name+"」的更改吗？","未保存的更改",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Warning);if(result==DialogResult.Cancel)return false;if(result==DialogResult.Yes)return Save(false);return true;}
  bool Save(bool choose){
   try{string target=path;if(!choose&&target!=null&&File.Exists(target)){string enc;var disk=ReadText(target,out enc);if(saved==null||disk!=saved){MessageBox.Show("文件已被其他程序修改。请另存为新文件，避免覆盖外部修改。","检测到外部更改");choose=true;}}
    if(choose||target==null)using(var d=new SaveFileDialog{Filter="Markdown 文档|*.md|文本文件|*.txt|所有文件|*.*",FileName=Name,DefaultExt="md",AddExtension=true,OverwritePrompt=true,Title="保存 Markdown 文档"}){if(path!=null)d.InitialDirectory=Path.GetDirectoryName(path);if(d.ShowDialog(this)!=DialogResult.OK)return false;target=d.FileName;}
    AtomicWrite(target,text);path=Path.GetFullPath(target);saved=text;dirty=false;encoding="UTF-8";draftTimer.Stop();DeleteDraft();MapDocumentFolder();Status();Toast("已保存到本地");return true;
   }catch(Exception ex){MessageBox.Show("保存失败，编辑内容仍保留在窗口中。\n\n"+ex.Message,"墨页",MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}
  }
  static void AtomicWrite(string dest,string content){string tmp=dest+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var fs=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=new UTF8Encoding(false).GetBytes(content);fs.Write(bytes,0,bytes.Length);fs.Flush(true);}if(File.Exists(dest))File.Replace(tmp,dest,null);else File.Move(tmp,dest);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
  void WriteDraft(){if(!dirty)return;try{Directory.CreateDirectory(data);AtomicWrite(DraftPath,json.Serialize(new{text=text,path=path,saved=saved}));}catch{Toast("草稿备份失败，请手动保存文档");}}
  void DeleteDraft(){try{if(File.Exists(DraftPath))File.Delete(DraftPath);}catch{}}
  void HandleClosing(object sender,FormClosingEventArgs e){if(busy){e.Cancel=true;return;}if(!ready)return;draftTimer.Stop();if(!ConfirmLeave()){e.Cancel=true;if(dirty)draftTimer.Start();return;}DeleteDraft();}
  async Task RefreshPreview(){await web.ExecuteScriptAsync("(async()=>{render();await Promise.race([Promise.all(Array.from(document.images).map(i=>i.decode().catch(()=>{}))),new Promise(r=>setTimeout(r,2500))]);})()");}
  async Task SelfTest(){
   string log=Path.Combine(data,"result.txt");
   try{
    if(FormBorderStyle!=FormBorderStyle.None)throw new Exception("Duplicate native title bar remains");
    WindowCommand("maximize");await Task.Delay(120);if(WindowState!=FormWindowState.Maximized)throw new Exception("Maximize failed");
    var area=Screen.FromHandle(Handle).WorkingArea;if(Math.Abs(Bounds.Height-area.Height)>4||Math.Abs(Bounds.Width-area.Width)>4)throw new Exception("Maximized window exceeds working area");
    WindowCommand("maximize");await Task.Delay(120);if(WindowState!=FormWindowState.Normal)throw new Exception("Restore failed");
    var before=Size;Size=new Size(1100,760);await Task.Delay(80);if(Size==before)throw new Exception("Resize failed");
    WindowCommand("minimize");await Task.Delay(80);if(WindowState!=FormWindowState.Minimized)throw new Exception("Minimize failed");WindowState=FormWindowState.Normal;
    string test=Path.Combine(data,"中文测试.md");string original=File.ReadAllText(Path.Combine(root,"app","欢迎使用.md"),Encoding.UTF8);AtomicWrite(test,original);LoadFile(test);
    await Task.Delay(300);string title=json.Deserialize<string>(await web.ExecuteScriptAsync("document.querySelector('#paper h1').textContent"));if(!title.Contains("给文字"))throw new Exception("Native rendering failed");
    // Textarea.value normalizes Windows CRLF to LF before sending edit messages.
    string updated=original.Replace("\r\n","\n").Replace("\r","\n")+"\n\n## 原生保存验证\n\n中文、emoji 🌿 与 UTF-8。";
    await web.ExecuteScriptAsync("editor.value="+json.Serialize(updated)+";changed();render();");
    // WebMessageReceived crosses a process boundary; wait for the actual acknowledgement.
    for(int attempt=0;attempt<50&&(!dirty||text!=updated);attempt++)await Task.Delay(100);
    if(!dirty||text!=updated)throw new Exception("Edit bridge or dirty tracking failed: dirty="+dirty+", textLength="+text.Length+", expectedLength="+updated.Length);
    WriteDraft();if(!File.Exists(DraftPath)||!File.ReadAllText(DraftPath).Contains("原生保存验证"))throw new Exception("Draft failed");
    if(!Save(false)||File.ReadAllText(test,Encoding.UTF8)!=updated||dirty)throw new Exception("Native save failed");
    AtomicWrite(test,updated+"\n更新");string label;if(ReadText(test,out label)!=updated+"\n更新")throw new Exception("Atomic replace failed");
    string utf16=Path.Combine(data,"utf16.md");File.WriteAllText(utf16,"# 中文 UTF16",Encoding.Unicode);LoadFile(utf16);if(text!="# 中文 UTF16")throw new Exception("UTF16 read failed");
    LoadFile(test);await RefreshPreview();var settings=web.CoreWebView2.Environment.CreatePrintSettings();settings.ShouldPrintBackgrounds=true;settings.ShouldPrintHeaderAndFooter=false;
    string pdf=Path.Combine(data,"native-export.pdf");if(!await web.CoreWebView2.PrintToPdfAsync(pdf,settings)||new FileInfo(pdf).Length<1000)throw new Exception("Native PDF failed");
    File.WriteAllText(log,"PASS: custom titlebar, maximize to working area, restore, resize, minimize, native WebView2 startup, Chinese rendering, edit bridge, dirty tracking, recovery draft, save/read UTF-8, atomic replace, UTF-16 BOM, native PDF export.",Encoding.UTF8);
   }catch(Exception ex){File.WriteAllText(log,"FAIL: "+ex,Encoding.UTF8);Environment.ExitCode=1;}
   finally{dirty=false;busy=false;BeginInvoke(new Action(Close));}
  }
  async Task ExportPdf(){using(var d=new SaveFileDialog{Filter="PDF 文档|*.pdf",FileName=Path.GetFileNameWithoutExtension(Name)+".pdf",DefaultExt="pdf",AddExtension=true,Title="导出 PDF"}){if(d.ShowDialog(this)!=DialogResult.OK)return;await RefreshPreview();var settings=web.CoreWebView2.Environment.CreatePrintSettings();settings.ShouldPrintBackgrounds=true;settings.ShouldPrintHeaderAndFooter=false;if(await web.CoreWebView2.PrintToPdfAsync(d.FileName,settings))Toast("PDF 导出完成");else throw new IOException("PDF 导出失败，请检查目标文件是否被占用。");}}
  async Task ExportHtml(){using(var d=new SaveFileDialog{Filter="HTML 文档|*.html",FileName=Path.GetFileNameWithoutExtension(Name)+".html",DefaultExt="html",AddExtension=true,Title="导出阅读版 HTML"}){if(d.ShowDialog(this)!=DialogResult.OK)return;await RefreshPreview();var result=await web.ExecuteScriptAsync("JSON.stringify({title:document.getElementById('filename').textContent,html:document.getElementById('paper').innerHTML})");var inner=json.Deserialize<string>(result);var obj=json.Deserialize<Dictionary<string,string>>(inner);string css=File.ReadAllText(Path.Combine(root,"app","style.css"));string body=obj["html"];string exported="<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data: file:;\"><title>"+System.Net.WebUtility.HtmlEncode(obj["title"])+"</title><style>"+css+"\nbody{height:auto;overflow:auto;background:#f3f4f2;padding:40px 20px}.markdown-body{max-width:820px;margin:auto;background:white;padding:60px;min-height:80vh}mark{background:transparent!important;outline:0!important} @media print{body{padding:0!important}.markdown-body{padding:0!important}}</style><article class=\"markdown-body\">"+body+"</article></html>";AtomicWrite(d.FileName,exported);Toast("HTML 导出完成（相对图片路径需保持有效）");}}
  void OpenLink(string url){Uri u;if(!Uri.TryCreate(url,UriKind.Absolute,out u))return;if(u.Host=="document.local"){if(path==null)return;var relative=Uri.UnescapeDataString(u.AbsolutePath).TrimStart('/').Replace('/',Path.DirectorySeparatorChar);string target=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),relative));string folder=Path.GetDirectoryName(path)+Path.DirectorySeparatorChar;if(!target.StartsWith(folder,StringComparison.OrdinalIgnoreCase))return;string ext=Path.GetExtension(target).ToLowerInvariant();if((ext==".md"||ext==".markdown"||ext==".txt")&&ConfirmLeave())LoadFile(target);return;}if(u.Scheme!="https"&&u.Scheme!="http"&&u.Scheme!="mailto")return;if(MessageBox.Show("在默认应用中打开此链接？\n\n"+url,"打开外部链接",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
 }
}
