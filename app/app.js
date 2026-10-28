'use strict';
const $=id=>document.getElementById(id),editor=$('editor'),paper=$('paper');
const md=window.markdownit({html:false,linkify:true,typographer:true,breaks:false});
let currentText='',currentMode='read',renderTimer,toastTimer,matches=[],matchIndex=-1,documentBase='',headingObserver;
const bridge=window.chrome?.webview;
function send(type,extra={}){bridge?.postMessage({type,...extra});}
function toast(text){$('toast').textContent=text;$('toast').hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').hidden=true,3200);}
function setMode(mode){currentMode=mode;document.body.dataset.mode=mode;document.querySelectorAll('[data-mode]').forEach(b=>{if(b.tagName==='BUTTON'){b.classList.toggle('active',b.dataset.mode===mode);b.setAttribute('aria-pressed',b.dataset.mode===mode)}});if(mode==='edit')editor.focus();}
function render(){
 const tokens=md.parse(editor.value,{});let serial=0;
 for(let i=0;i<tokens.length;i++){if(tokens[i].type==='heading_open')tokens[i].attrSet('id','heading-'+serial++);}
 paper.innerHTML=DOMPurify.sanitize(md.renderer.render(tokens,md.options,{}),{USE_PROFILES:{html:true}});
 paper.querySelectorAll('li').forEach(li=>{const p=li.firstElementChild?.tagName==='P'?li.firstElementChild:li;const text=p.firstChild;if(text?.nodeType===3&&/^\[[ xX]\] /.test(text.textContent)){const checked=text.textContent[1]!==' ';text.textContent=text.textContent.slice(4);const input=document.createElement('input');input.type='checkbox';input.checked=checked;input.disabled=true;input.setAttribute('aria-label',checked?'已完成':'未完成');p.prepend(input);li.classList.add('task')}});
 const outline=$('outline');outline.replaceChildren();let h=0;
 for(let i=0;i<tokens.length;i++)if(tokens[i].type==='heading_open'){const token=tokens[i],b=document.createElement('button');b.textContent=tokens[i+1].children?.filter(t=>['text','code_inline','image'].includes(t.type)).map(t=>t.content).join('')||tokens[i+1].content;b.className='level-'+token.tag.slice(1);const id='heading-'+h++,line=token.map?.[0]||0;b.onclick=()=>{if(currentMode==='edit'){const offset=editor.value.split('\n').slice(0,line).join('\n').length+(line?1:0);editor.focus();editor.setSelectionRange(offset,offset);const lineHeight=parseFloat(getComputedStyle(editor).lineHeight);editor.scrollTop=Math.max(0,line*lineHeight-80);}else $(id)?.scrollIntoView({behavior:'smooth',block:'start'});};outline.append(b)}
 $('headingCount').textContent=h;if(!h){outline.innerHTML='<p class="empty-outline">添加 # 标题后，<br>这里会自动生成目录。</p>'}
 outline.querySelectorAll('button').forEach((b,i)=>{b.dataset.heading='heading-'+i;b.title=b.textContent});
 headingObserver?.disconnect();headingObserver=new IntersectionObserver(()=>updateActiveHeading(),{root:$('readerPane'),rootMargin:'0px 0px -65% 0px'});paper.querySelectorAll('h1,h2,h3,h4,h5,h6').forEach(h=>headingObserver.observe(h));updateActiveHeading();
 const count=editor.value.replace(/\s/g,'').length,lines=editor.value.split('\n').length;$('stats').textContent=`${count.toLocaleString()} 字符　·　${lines} 行　·　约 ${Math.max(1,Math.ceil(count/500))} 分钟`;
 if(!$('searchbar').hidden)search(false);
}
function changed(){currentText=editor.value;send('change',{text:currentText});clearTimeout(renderTimer);renderTimer=setTimeout(render,130);position();}
function position(){const before=editor.value.slice(0,editor.selectionStart).split('\n');$('position').textContent=currentMode==='read'?'Markdown':`行 ${before.length}，列 ${before.at(-1).length+1}`;}
function insert(before,after='',placeholder='文字'){if(currentMode==='read')setMode('split');const old=editor.value,start=editor.selectionStart,end=editor.selectionEnd,selected=editor.value.slice(start,end)||placeholder;editor.focus();editor.setSelectionRange(start,end);document.execCommand('insertText',false,before+selected+after);if(editor.value===old){editor.setRangeText(before+selected+after,start,end,'end');changed()}editor.setSelectionRange(start+before.length,start+before.length+selected.length);}
function format(kind){const map={heading:['## ','','标题'],bold:['**','**'],italic:['*','*'],code:['`','`','代码'],link:['[','](https://example.com)','链接文字'],list:['- ','','列表项'],quote:['> ','','引用内容'],table:['\n| 列一 | 列二 |\n| --- | --- |\n| 内容 | 内容 |\n','','']};insert(...map[kind]);}
function clearMarks(){paper.querySelectorAll('mark').forEach(m=>m.replaceWith(document.createTextNode(m.textContent)));paper.normalize();matches=[];matchIndex=-1;}
function search(scroll=true){clearMarks();const q=$('query').value;if(!q){$('matchCount').textContent='';return}if(currentMode==='edit'){const lower=editor.value.toLowerCase(),needle=q.toLowerCase();let p=0;while((p=lower.indexOf(needle,p))!==-1){matches.push(p);p+=needle.length}}else{const nodes=[],walker=document.createTreeWalker(paper,NodeFilter.SHOW_TEXT,{acceptNode:n=>n.parentElement.closest('pre,code')?NodeFilter.FILTER_ACCEPT:NodeFilter.FILTER_ACCEPT});while(walker.nextNode())nodes.push(walker.currentNode);for(const n of nodes){const str=n.textContent,lower=str.toLowerCase(),needle=q.toLowerCase();let p=0,i,frag=document.createDocumentFragment();while((i=lower.indexOf(needle,p))!==-1){frag.append(document.createTextNode(str.slice(p,i)));const m=document.createElement('mark');m.textContent=str.slice(i,i+q.length);frag.append(m);matches.push(m);p=i+q.length}if(p){frag.append(document.createTextNode(str.slice(p)));n.replaceWith(frag)}}}matchIndex=matches.length?0:-1;showMatch(scroll);}
function showMatch(scroll=true){$('matchCount').textContent=matches.length?`${matchIndex+1} / ${matches.length}`:'无结果';if(!matches.length)return;if(currentMode==='edit'){const start=matches[matchIndex];editor.setSelectionRange(start,start+$('query').value.length);if(scroll){const line=editor.value.slice(0,start).split('\n').length-1;editor.scrollTop=Math.max(0,line*parseFloat(getComputedStyle(editor).lineHeight)-100)}}else{matches.forEach(m=>m.classList.remove('current'));matches[matchIndex].classList.add('current');if(scroll)matches[matchIndex].scrollIntoView({block:'center'})}}
function toggleSearch(show){$('searchbar').hidden=!show;if(show){$('query').focus();$('query').select();search()}else clearMarks();}
document.querySelectorAll('[data-action]').forEach(b=>b.onclick=()=>{send(b.dataset.action);closeMenu()});
document.querySelectorAll('button[data-mode]').forEach(b=>b.onclick=()=>{setMode(b.dataset.mode);position();if(!$('searchbar').hidden)search()});
document.querySelectorAll('[data-format]').forEach(b=>b.onclick=()=>format(b.dataset.format));
function closeMenu(){$('fileMenu').hidden=true;$('more').setAttribute('aria-expanded','false')}
$('more').onclick=()=>{$('fileMenu').hidden=!$('fileMenu').hidden;$('more').setAttribute('aria-expanded',!$('fileMenu').hidden)};document.addEventListener('click',e=>{if(!e.target.closest('.file-actions'))closeMenu()});
$('outlineToggle').onclick=()=>{document.body.classList.toggle('no-outline');$('outlineToggle').setAttribute('aria-expanded',!document.body.classList.contains('no-outline'))};$('searchToggle').onclick=()=>toggleSearch($('searchbar').hidden);$('closeSearch').onclick=()=>toggleSearch(false);
$('query').oninput=()=>search();$('query').onkeydown=e=>{if(e.key==='Enter'){e.preventDefault();nextMatch(e.shiftKey?-1:1)}};function nextMatch(d){if(!matches.length)return;matchIndex=(matchIndex+d+matches.length)%matches.length;showMatch()};$('nextMatch').onclick=()=>nextMatch(1);$('prevMatch').onclick=()=>nextMatch(-1);
try{if(localStorage.theme==='dark')document.body.classList.add('dark');if(localStorage.fontSize)$('fontSize').value=localStorage.fontSize}catch{}
function font(){document.documentElement.style.setProperty('--reading-size',$('fontSize').value+'px');try{localStorage.fontSize=$('fontSize').value}catch{}}font();$('fontSize').onchange=font;
$('theme').onclick=()=>{document.body.classList.toggle('dark');send('theme',{dark:document.body.classList.contains('dark')});try{localStorage.theme=document.body.classList.contains('dark')?'dark':'light'}catch{}};
editor.addEventListener('input',changed);editor.addEventListener('click',position);editor.addEventListener('keyup',position);
editor.addEventListener('keydown',e=>{if(e.key==='Tab'){e.preventDefault();document.execCommand('insertText',false,'  ')}});
document.addEventListener('keydown',e=>{const cmd=e.ctrlKey||e.metaKey;if(cmd){const k=e.key.toLowerCase();if(['s','o','n','p','f','b','i'].includes(k)){e.preventDefault();if(k==='f')toggleSearch(true);else if(k==='b')format('bold');else if(k==='i')format('italic');else send(({s:e.shiftKey?'saveAs':'save',o:'open',n:'new',p:'pdf'})[k])}}if(e.key==='Escape'){toggleSearch(false);closeMenu()}});
paper.addEventListener('click',e=>{const link=e.target.closest('a');if(!link)return;e.preventDefault();const href=link.getAttribute('href');if(href?.startsWith('#')){try{paper.querySelector('#'+CSS.escape(decodeURIComponent(href.slice(1))))?.scrollIntoView()}catch{}}else send('link',{url:new URL(href,documentBase||location.href).href})});
bridge?.addEventListener('message',e=>{const m=e.data;if(m.type==='document'){editor.value=m.text;currentText=m.text;documentBase=m.base||'';let base=document.querySelector('base');if(!base){base=document.createElement('base');document.head.append(base)}base.href=documentBase||'https://document.local/';$('filename').textContent=m.name;$('saveState').textContent=m.dirty?'有未保存的更改':m.path?'已保存 · '+m.path:'未保存的新文档';$('encoding').textContent=m.encoding+' · 本地文件';render();$('readerPane').scrollTop=0;editor.scrollTop=0;position();}else if(m.type==='status'){documentBase=m.base||'';$('filename').textContent=m.name;$('saveState').textContent=m.dirty?'有未保存的更改':m.path?'已保存 · '+m.path:'未保存的新文档';if(m.encoding)$('encoding').textContent=m.encoding+' · 本地文件';}else if(m.type==='toast')toast(m.text);});
// Reveal scrollbars on pointer activity, scroll, or keyboard navigation; never resize the pane.
const scrollSelector='#outline,#readerPane,#editor,.markdown-body pre,.markdown-body table';
const scrollTimers=new WeakMap();let heldScrollPane=null;
function showScroll(el){if(!el?.matches?.(scrollSelector))return;el.classList.add('scroll-active');clearTimeout(scrollTimers.get(el));scrollTimers.set(el,setTimeout(()=>{if(heldScrollPane===el)showScroll(el);else el.classList.remove('scroll-active')},950));}
document.addEventListener('pointermove',e=>{const el=e.target.closest?.(scrollSelector);if(el){el.classList.remove('scroll-keyboard');showScroll(el)}},{passive:true});
document.addEventListener('pointerdown',e=>{heldScrollPane=e.target.closest?.(scrollSelector);showScroll(heldScrollPane)},{passive:true});
document.addEventListener('pointerup',()=>{const old=heldScrollPane;heldScrollPane=null;showScroll(old)},{passive:true});
window.addEventListener('blur',()=>{heldScrollPane=null});
document.addEventListener('scroll',e=>showScroll(e.target),{capture:true,passive:true});
document.addEventListener('keydown',e=>{if(['Tab','ArrowUp','ArrowDown','ArrowLeft','ArrowRight','PageUp','PageDown','Home','End',' '].includes(e.key)){const el=e.target.closest?.(scrollSelector);if(el){el.classList.add('scroll-keyboard');showScroll(el)}}});
function updateActiveHeading(){if(currentMode==='edit')return;const top=$('readerPane').getBoundingClientRect().top+65;let current=paper.querySelector('[id^="heading-"]');for(const h of paper.querySelectorAll('[id^="heading-"]')){if(h.getBoundingClientRect().top<=top)current=h;else break}if(!current)return;for(const b of $('outline').querySelectorAll('button')){const active=b.dataset.heading===current.id;b.classList.toggle('current-heading',active);if(active)b.setAttribute('aria-current','location');else b.removeAttribute('aria-current')}}
let headingFrame=0;$('readerPane').addEventListener('scroll',()=>{if(!headingFrame)headingFrame=requestAnimationFrame(()=>{headingFrame=0;updateActiveHeading()})},{passive:true});
function fileTooltip(){const info=$('saveState');info.title=info.textContent;$('filename').title=$('filename').textContent;}
new MutationObserver(fileTooltip).observe($('saveState'),{childList:true,subtree:true,characterData:true});
document.querySelectorAll('[data-window]').forEach(b=>b.onclick=()=>send('window',{command:b.dataset.window}));
let titlePointer=null;
$('titlebar').addEventListener('pointerdown',e=>{if(e.button===0&&!e.target.closest('button,select,input,label,.mode-switch,.file-actions,.window-controls'))titlePointer={x:e.clientX,y:e.clientY}});
document.addEventListener('pointermove',e=>{if(titlePointer&&e.buttons===1&&Math.hypot(e.clientX-titlePointer.x,e.clientY-titlePointer.y)>5){titlePointer=null;send('window',{command:'drag'})}});
document.addEventListener('pointerup',()=>titlePointer=null);
$('titlebar').addEventListener('dblclick',e=>{if(!e.target.closest('button,select,input,label,.mode-switch,.file-actions,.window-controls'))send('window',{command:'maximize'})});
send('ready');
send('theme',{dark:document.body.classList.contains('dark')});
