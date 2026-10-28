# 墨页 · InkPage Markdown

一个轻量的 Windows Markdown 阅读与编辑工具。以纸张式排版阅读纯文本，在阅读、双栏实时预览和编辑模式之间切换。

当前版本：**1.2.0**。项目自身暂未授予开源许可，第三方组件适用各自许可证。

## 功能

- 阅读 / 双栏 / 编辑三种模式，支持标题、列表、任务列表、表格、引用、代码块等常用 Markdown 语法。
- 标题目录与当前章节提示；长目录标题自动换行。
- 紧凑工具栏；鼠标移动或滚动时显示滚动条，静止后隐藏。
- 阅读字号、深色主题、查找、格式工具和常用快捷键。
- 新建、打开、保存、另存为；导出 PDF 和 HTML 阅读版。
- 未保存提醒、本地恢复草稿、外部文件更改检测、原子替换保存。
- PerMonitorV2 高 DPI 支持，以及窗口拖动、最小化、最大化 / 还原和边缘缩放。

## 环境

- Windows 10 / 11，x64。
- .NET Framework 4.8 或更高版本。
- Microsoft Edge WebView2 Runtime。
- 构建脚本使用 Windows PowerShell 5.1 或 PowerShell 7。无需安装 Node.js、Python 或 .NET SDK。

## 从源码构建

在项目根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

脚本从 NuGet 官方源下载固定版本 **Microsoft.Web.WebView2 1.0.4258.31**，校验 SHA-256 后编译程序。依赖保存在 `.deps/`，生成文件位于 `dist/InkPage/`；这两个目录均被 Git 忽略。

启动：

```powershell
.\dist\InkPage\InkPage.exe
```

保留输出目录中的配套 DLL、`InkPage.exe.config` 和 `app/`。首次构建需要网络；依赖已缓存后，可给构建脚本添加 `-Offline`。日常阅读与编辑使用本地资源，不依赖 CDN。

## 检查

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-dpi.ps1
```

此检查会短暂启动独立测试窗口，验证窗口控制、PerMonitorV2、中文编辑消息、恢复草稿、文件保存与读取、UTF-16 BOM 和 PDF 导出。测试数据写入被忽略的 `.test-output/` 和构建输出目录，不读取个人笔记。

## 目录结构

```text
app/        本地界面、Markdown 渲染库、图标及通用示例
src/        C# WinForms / WebView2 桌面宿主与应用配置
scripts/    下载依赖、构建和检查脚本
tests/      原生 DPI 检查程序源码
docs/       使用说明、数据与隐私说明
```

详细使用方法见 [使用说明](docs/USAGE.md)，运行时数据说明见 [数据与隐私](docs/PRIVACY.md)。

## 当前限制

- 暂不支持 LaTeX、Mermaid、代码语法着色、插件或多标签。
- 打开 UTF-8 和带 BOM 的 UTF-16 文本；保存为 UTF-8，单文件最多 10 MB。
- 相对图片以 Markdown 文件所在目录为基准，支持该目录与子目录；远程图片默认不加载。
- HTML 导出不打包相对图片，分享时需要保持有效的图片位置。
- 程序未进行代码签名；跨不同缩放比例的显示器交互仍需更多兼容性验证。

## 版权与第三方组件

本项目自身**暂未提供开源许可证**，公开源码不代表授予使用、修改或再发布许可。第三方组件的版权及许可证声明见 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) 和 `app/LICENSE-*` 文件。
