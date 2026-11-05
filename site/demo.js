'use strict';
(() => {
  const $ = id => document.getElementById(id);
  const canvas = $('canvas'), ctx = canvas.getContext('2d');
  const source = new Image(), translated = new Image();
  const originalText = 'FIELD NOTES\nMake room for your next good idea.\nA calmer space to read, think and share.\nTurn the page when you are ready.\nExplore the guide\nREAD / THINK / SHARE';
  const translatedText = '灵感手记\n给下一个好点子，留一点空间。\n在这里，安心阅读、思考与分享。\n准备好了，就翻开新的一页。\n阅读指南\n阅读 / 思考 / 分享';
  const tools = [['move','移动 / 缩放'],['arrow','箭头'],['rectangle','连续方框'],['ellipse','连续画圈'],['line','直线'],['pen','画笔'],['text','添加文字'],['number','连续数字'],['mosaic','马赛克'],['protect','保留原图'],['color','颜色'],['weight','粗细 / 马赛克颗粒'],['fontsize','字号'],['undo','撤销 Ctrl+Z'],['redo','重做 Ctrl+Y'],['translate','显示原文 / 译文'],['copyimage','复制截图，成功后关闭'],['copytext','复制文字'],['save','保存图片，成功后关闭'],['stop','停止当前翻译'],['close','关闭 Esc']];
  let marks = [], undo = [], redo = [], selected = -1, tool = 'move', color = '#ff5459', weight = 3, fontSize = 36;
  let showOriginal = true, closed = false, drag = null, tourMode = true, playing = true, step = 0, timer, bubbleTimer, pendingText;
  let selection = null, steps = [], ready = false, operation = 0;
  const clone = value => JSON.parse(JSON.stringify(value));
  const shot = () => ({ marks: clone(marks), selected });
  function remember() { undo.push(shot()); redo = []; }
  function load(s) { marks = clone(s.marks); selected = s.selected; render(); }
  function history(back) {
    const from = back ? undo : redo, to = back ? redo : undo;
    if (from.length) { to.push(shot()); load(from.pop()); }
  }
  function toast(text) {
    $('bubble').textContent = text; $('bubble').hidden = false;
    clearTimeout(bubbleTimer); bubbleTimer = setTimeout(() => { $('bubble').hidden = true; }, 2800);
  }
  const point = e => { const r = canvas.getBoundingClientRect(); return { x: (e.clientX-r.left)*1200/r.width, y: (e.clientY-r.top)*520/r.height }; };
  function bounds(m) {
    if (m.type === 'text') {
      ctx.font = `${m.size}px "Microsoft YaHei UI",sans-serif`;
      return { x:m.x, y:m.y, w:Math.max(...m.text.split('\n').map(t=>ctx.measureText(t).width),20), h:m.text.split('\n').length*m.size*1.25 };
    }
    if (m.type === 'number') return {x:m.x-18,y:m.y-18,w:36,h:36};
    if (m.type === 'pen') { const xs=m.points.map(p=>p.x),ys=m.points.map(p=>p.y);return {x:Math.min(...xs),y:Math.min(...ys),w:Math.max(...xs)-Math.min(...xs),h:Math.max(...ys)-Math.min(...ys)}; }
    return {x:Math.min(m.x,m.x2),y:Math.min(m.y,m.y2),w:Math.abs(m.x2-m.x),h:Math.abs(m.y2-m.y)};
  }
  function drawMark(m) {
    const b = bounds(m); ctx.save();ctx.strokeStyle=m.color;ctx.fillStyle=m.color;ctx.lineWidth=m.weight||3;ctx.lineCap='round';ctx.lineJoin='round';
    if (m.type==='protect') ctx.drawImage(source,b.x,b.y,b.w,b.h,b.x,b.y,b.w,b.h);
    else if (m.type==='mosaic') {
      const pixels = Math.max(5,(m.weight||3)*4), base = showOriginal?source:translated;
      const tiny=document.createElement('canvas');tiny.width=Math.max(1,Math.ceil(b.w/pixels));tiny.height=Math.max(1,Math.ceil(b.h/pixels));
      tiny.getContext('2d').drawImage(base,b.x,b.y,b.w,b.h,0,0,tiny.width,tiny.height);
      ctx.imageSmoothingEnabled=false;ctx.drawImage(tiny,0,0,tiny.width,tiny.height,b.x,b.y,b.w,b.h);
    } else if (m.type==='text') {
      ctx.font=`${m.size}px "Microsoft YaHei UI",sans-serif`;ctx.textBaseline='top';m.text.split('\n').forEach((t,i)=>ctx.fillText(t,m.x,m.y+i*m.size*1.25));
    } else if (m.type==='number') {
      ctx.beginPath();ctx.arc(m.x,m.y,18,0,Math.PI*2);ctx.fill();ctx.strokeStyle='#fff';ctx.lineWidth=1;ctx.stroke();ctx.fillStyle='#fff';ctx.textAlign='center';ctx.textBaseline='middle';ctx.font='bold 23px sans-serif';ctx.fillText(m.number,m.x,m.y);
    } else if (m.type==='rectangle') ctx.strokeRect(b.x,b.y,b.w,b.h);
    else if (m.type==='ellipse') { ctx.beginPath();ctx.ellipse(b.x+b.w/2,b.y+b.h/2,Math.max(.1,b.w/2),Math.max(.1,b.h/2),0,0,Math.PI*2);ctx.stroke(); }
    else {
      ctx.beginPath();ctx.moveTo(m.x,m.y);
      if (m.type==='pen') m.points.forEach(p=>ctx.lineTo(p.x,p.y));else ctx.lineTo(m.x2,m.y2);ctx.stroke();
      if (m.type==='arrow') { const a=Math.atan2(m.y2-m.y,m.x2-m.x),s=18;ctx.beginPath();ctx.moveTo(m.x2,m.y2);ctx.lineTo(m.x2-s*Math.cos(a-.45),m.y2-s*Math.sin(a-.45));ctx.lineTo(m.x2-s*Math.cos(a+.45),m.y2-s*Math.sin(a+.45));ctx.closePath();ctx.fill(); }
    }
    ctx.restore();
  }
  function render(clean=false) {
    if (!ready) return;
    ctx.clearRect(0,0,1200,520);ctx.drawImage(showOriginal?source:translated,0,0);
    marks.forEach(drawMark);
    if (!clean && selected>=0 && marks[selected]) {
      const b=bounds(marks[selected]);ctx.save();ctx.strokeStyle='#59e0c4';ctx.lineWidth=1.5;ctx.setLineDash([5,4]);ctx.strokeRect(b.x-4,b.y-4,b.w+8,b.h+8);ctx.setLineDash([]);
      for (const [x,y] of [[b.x,b.y],[b.x+b.w,b.y],[b.x,b.y+b.h],[b.x+b.w,b.y+b.h]]) {ctx.fillStyle='#fff';ctx.fillRect(x-5,y-5,10,10);ctx.strokeRect(x-5,y-5,10,10);}ctx.restore();
    }
    if (!clean && selection) { const b=selection;ctx.save();ctx.fillStyle='#0007';ctx.fillRect(0,0,1200,520);ctx.drawImage(source,b.x,b.y,b.w,b.h,b.x,b.y,b.w,b.h);ctx.strokeStyle='#54e4c0';ctx.lineWidth=3;ctx.strokeRect(b.x,b.y,b.w,b.h);ctx.restore(); }
    $('closed').hidden=!closed;$('toolbar').hidden=closed || (tourMode && step<2);
    document.querySelectorAll('[data-action]').forEach(b=> {
      const a=b.dataset.action;b.classList.toggle('active',a===tool);
      b.disabled=closed || (a==='undo'&&!undo.length) || (a==='redo'&&!redo.length);
      if(a==='translate') {b.dataset.view=showOriginal?'原文':'译文';b.firstChild.src=`assets/icons/${showOriginal?'translate':'original'}.png`;}
      if(a==='color') b.firstChild.style.background=color;
      if(a==='weight') b.textContent=weight+'⌄';
      if(a==='fontsize') b.textContent=fontSize+'⌄';
    });
    canvas.style.cursor=tool==='move'?'move':tool==='text'?'text':'crosshair';
  }
  function setMode(automatic) {
    clearTimeout(timer);operation++;tourMode=automatic;playing=automatic;selection=null;drag=null;
    $('copy-menu').hidden=true;$('cursor').hidden=!automatic;$('tour').classList.toggle('active',automatic);$('try').classList.toggle('active',!automatic);$('pause').disabled=!automatic;
    if (automatic) startStep(0); else {
      closed=false;showOriginal=false;marks=[];undo=[];redo=[];selected=-1;tool='move';
      $('step-no').textContent='自由试用';$('step-title').textContent='点击工具，在截图上试一试';$('step-caption').textContent='拖动画圈画框；移动工具可调整位置和四角缩放。手机也可触摸操作。';$('pause').textContent='暂停';render();
    }
  }
  function closeCapture(message) { closed=true;selected=-1;$('closed-title').textContent=message;$('copy-menu').hidden=true;render(); }
  function textDialog(x,y,index=-1) {
    pendingText={x,y,index};$('text-value').value=index>=0?marks[index].text:'在这里写下你的想法';$('text-dialog').showModal();$('text-value').focus();$('text-value').select();
  }
  $('text-dialog').addEventListener('close',()=> {
    if ($('text-dialog').returnValue!=='ok'||!pendingText) return;
    const value=$('text-value').value.trim();if(!value)return;remember();
    const p=pendingText;if(p.index>=0) marks[p.index].text=value;else marks.push({type:'text',x:p.x,y:p.y,text:value,size:fontSize,color,weight});
    selected=p.index>=0?p.index:marks.length-1;tool='move';pendingText=null;render();
  });
  function styleCycle(kind) {
    if (kind==='color') {const c=['#ff5459','#50dcbe','#facc15','#ffffff'];color=c[(c.indexOf(color)+1)%c.length];}
    if (kind==='weight') weight=weight===3?6:weight===6?10:3;
    if (kind==='fontsize') fontSize=fontSize===36?48:fontSize===48?24:36;
    if (selected>=0) {remember();const m=marks[selected];m.color=color;m.weight=weight;if(m.type==='text')m.size=fontSize;}
    render();
  }
  async function copyText(which) {
    $('copy-menu').hidden=true;
    try { await navigator.clipboard.writeText(which==='source'?originalText:translatedText);toast(which==='source'?'已复制原文':'已复制译文'); }
    catch {toast('浏览器未允许写入剪贴板。请在下载的程序中使用此功能。');}
  }
  function exportBlob() {render(true);const result=new Promise(resolve=>canvas.toBlob(resolve,'image/png'));render();return result;}
  async function exportImage(copy) {
    try {
      const blob=await exportBlob();if(!blob)throw Error('empty');
      if(copy) {await navigator.clipboard.write([new ClipboardItem({'image/png':blob})]);closeCapture('截图已复制，浮窗自动关闭');}
      else { const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download='快译截图-演示.png';a.click();setTimeout(()=>URL.revokeObjectURL(url),15000);closeCapture('已发起 PNG 下载，演示浮窗关闭'); }
    } catch {toast('浏览器未允许复制图片，浮窗保持打开；可以使用保存按钮下载。');}
  }
  function action(a) {
    if(tourMode) {clearTimeout(timer);playing=false;$('pause').textContent='继续';toast('先点击「自己试用」，再自由操作。');return;}
    $('copy-menu').hidden=a!=='copytext';
    if(['move','arrow','rectangle','ellipse','line','pen','text','number','mosaic','protect'].includes(a)){tool=a;selected=-1;render();return;}
    if(['color','weight','fontsize'].includes(a))return styleCycle(a);
    if(a==='translate'){showOriginal=!showOriginal;render();}
    if(a==='undo')history(true);if(a==='redo')history(false);
    if(a==='copyimage')exportImage(true);if(a==='save')exportImage(false);
    if(a==='copytext')$('copy-menu').hidden=false;
    if(a==='stop')toast('演示译文已经完成，没有正在进行的请求。真实程序可中断翻译。');
    if(a==='close')closeCapture('本次截图已结束');
  }
  tools.forEach(([name,label])=> {
    const b=document.createElement('button');b.type='button';b.dataset.action=name;b.title=label;b.setAttribute('aria-label',label);
    if(name==='color')b.append(document.createElement('span'));else if(!['weight','fontsize'].includes(name)){const img=new Image();img.src=`assets/icons/${name}.png`;img.alt='';b.append(img);}
    b.addEventListener('click',()=>action(name));$('toolbar').append(b);
  });
  document.querySelectorAll('[data-copy]').forEach(b=>b.addEventListener('click',()=>copyText(b.dataset.copy)));
  canvas.addEventListener('pointerdown',e=> {
    if(tourMode||closed)return;const p=point(e);canvas.setPointerCapture(e.pointerId);canvas.focus({preventScroll:true});$('copy-menu').hidden=true;
    if(tool==='text') {textDialog(p.x,p.y);return;}
    if(tool==='move') {
      if(selected>=0) {
        const b=bounds(marks[selected]),corners=[[b.x,b.y],[b.x+b.w,b.y],[b.x,b.y+b.h],[b.x+b.w,b.y+b.h]];
        const index=corners.findIndex(([x,y])=>Math.hypot(x-p.x,y-p.y)<18);
        if(index>=0){remember();drag={kind:'resize',index:selected,corner:index,start:p,b,original:clone(marks[selected])};return;}
      }
      selected=-1;for(let i=marks.length-1;i>=0;i--){const b=bounds(marks[i]);if(p.x>=b.x-10&&p.x<=b.x+b.w+10&&p.y>=b.y-10&&p.y<=b.y+b.h+10){selected=i;break;}}
      if(selected>=0){remember();drag={kind:'move',index:selected,start:p,original:clone(marks[selected])};}render();return;
    }
    remember();selected=-1;
    if(tool==='number'){marks.push({type:'number',...p,number:marks.filter(m=>m.type==='number').length+1,color,weight});render();return;}
    const m={type:tool,...p,x2:p.x,y2:p.y,color,weight,points:[p]};marks.push(m);drag={kind:'draw',index:marks.length-1};render();
  });
  canvas.addEventListener('pointermove',e=> {
    if(!drag)return;const p=point(e),m=marks[drag.index];if(!m)return;
    if(drag.kind==='draw'){m.x2=p.x;m.y2=p.y;if(m.type==='pen')m.points.push(p);}
    else if(drag.kind==='move'){
      const o=drag.original,dx=p.x-drag.start.x,dy=p.y-drag.start.y;m.x=o.x+dx;m.y=o.y+dy;if(o.x2!==undefined){m.x2=o.x2+dx;m.y2=o.y2+dy;}if(o.points)m.points=o.points.map(q=>({x:q.x+dx,y:q.y+dy}));
    } else {
      const o=drag.original,b=drag.b,c=drag.corner,ax=c%2?b.x:b.x+b.w,ay=c<2?b.y+b.h:b.y;
      let x=Math.min(ax,p.x),y=Math.min(ay,p.y),w=Math.max(12,Math.abs(p.x-ax)),h=Math.max(12,Math.abs(p.y-ay));
      if(m.type==='text'){const ratio=Math.max(.25,Math.min(5,Math.max(w/b.w,h/b.h)));m.size=Math.max(10,o.size*ratio);w=b.w*ratio;h=b.h*ratio;m.x=c%2?ax:ax-w;m.y=c<2?ay-h:ay;}
      else if(m.type==='number'){m.x=x+w/2;m.y=y+h/2;}
      else if(m.type==='pen'){m.points=o.points.map(q=>({x:x+(q.x-b.x)*w/Math.max(1,b.w),y:y+(q.y-b.y)*h/Math.max(1,b.h)}));m.x=m.points[0].x;m.y=m.points[0].y;}
      else {m.x=o.x<=o.x2?x:x+w;m.y=o.y<=o.y2?y:y+h;m.x2=o.x<=o.x2?x+w:x;m.y2=o.y<=o.y2?y+h:y;}
    }render();
  });
  const endDrag=()=>{if(drag?.kind==='draw'){const m=marks[drag.index],b=bounds(m);if(b.w<2&&b.h<2){marks.pop();undo.pop();}}drag=null;render();};
  canvas.addEventListener('pointerup',endDrag);canvas.addEventListener('pointercancel',endDrag);canvas.addEventListener('lostpointercapture',endDrag);
  canvas.addEventListener('dblclick',e=>{if(tourMode||closed||tool!=='move')return;const p=point(e);for(let i=marks.length-1;i>=0;i--){const m=marks[i],b=bounds(m);if(m.type==='text'&&p.x>=b.x&&p.x<=b.x+b.w&&p.y>=b.y&&p.y<=b.y+b.h){textDialog(m.x,m.y,i);break;}}});
  document.addEventListener('keydown',e=>{
    if(document.querySelector('dialog[open]')||e.target.matches('input,textarea'))return;
    if(e.key==='Escape'){if(tourMode){playing=false;clearTimeout(timer);}closeCapture('本次截图已结束');}
    if(!tourMode&&(e.ctrlKey||e.metaKey)&&['z','y'].includes(e.key.toLowerCase())){e.preventDefault();history(e.key.toLowerCase()==='z'&&!e.shiftKey);}
    if(!tourMode&&e.key==='Delete'&&selected>=0){remember();marks.splice(selected,1);selected=-1;render();}
  });
  $('reopen').onclick=()=>setMode(false);$('settings').onclick=()=>{if(tourMode){playing=false;clearTimeout(timer);$('pause').textContent='继续';}$('settings-dialog').showModal();};
  $('try').onclick=()=>setMode(false);$('tour').onclick=()=>setMode(true);
  // Tour uses the same canvas renderer and tools as manual mode. Copy/download are
  // explicitly simulated during playback; only a user's manual click writes data.
  function add(type,x,y,x2,y2,extra={}) {marks.push({type,x,y,x2,y2,color:'#ff5459',weight:3,...extra});}
  function tourState(n) {
    marks=[];undo=[];redo=[];selected=-1;tool='move';color='#ff5459';weight=3;fontSize=36;closed=false;showOriginal=n<2||n===3;selection=null;$('copy-menu').hidden=true;$('bubble').hidden=true;
    if(n===1)selection={x:0,y:0,w:1199,h:519};
    if(n>=4)add('arrow',705,220,515,160);
    if(n>=5)add('line',50,318,600,318);
    if(n>=6)add('pen',620,380,730,350,{points:[{x:620,y:380},{x:658,y:360},{x:690,y:390},{x:730,y:350}]});
    if(n>=7){add('rectangle',36,225,605,310);add('rectangle',37,340,340,420);}
    if(n>=8){add('ellipse',835,90,1075,325);add('ellipse',955,218,1083,342);}
    if(n>=9){marks.push({type:'text',x:n>=10?435:390,y:n>=10?412:397,text:'这个位置可以修改',size:n>=17?42:n>=11?41.4:36,color:n>=17?'#facc15':'#50dcbe'});if(n<=11||n===17)selected=marks.length-1;}
    if(n>=12){add('number',810,85,0,0,{number:1});add('number',1100,365,0,0,{number:2});}
    if(n>=13)add('mosaic',880,230,1040,330);
    if(n>=14&&n!==15)add('protect',43,25,250,62);
    const active={4:'arrow',5:'line',6:'pen',7:'rectangle',8:'ellipse',12:'number',13:'mosaic',14:'protect',15:'undo',16:'redo',18:'copytext',19:'copyimage'};tool=active[n]||'move';
    if(n>=4)undo=[{marks:[],selected:-1}];if(n===15)redo=[{marks:clone(marks),selected:-1}];
    if(n===18)$('copy-menu').hidden=false;
    if(n===19)toast('自动演示：复制截图或保存成功后，浮窗自动关闭。');
    if(n===20){closed=true;$('closed-title').textContent='本次截图已结束';}
  }
  function startStep(n) {
    if(!ready)return;clearTimeout(timer);operation++;step=(n+steps.length)%steps.length;tourMode=true;
    $('tour').classList.add('active');$('try').classList.remove('active');$('pause').disabled=false;
    const info=steps[step];$('step-no').textContent=`${String(step+1).padStart(2,'0')} / ${steps.length}`;$('step-title').textContent=info.title.replace(/^\d+\s*·\s*/,'');$('step-caption').textContent=info.caption;
    document.querySelectorAll('[data-step]').forEach(b=>b.classList.toggle('active',+b.dataset.step===step));
    tourState(step);$('cursor').hidden=step===20;
    const points=[[9,15],[98,96],[62,58],[73,94],[47,35],[50,63],[61,71],[49,60],[89,64],[60,84],[65,86],[70,89],[92,70],[85,59],[17,9],[62,94],[66,94],[51,94],[81,94],[77,94],[50,50]];
    $('cursor').style.left=points[step][0]+'%';$('cursor').style.top=points[step][1]+'%';render();
    $('pause').textContent=playing?'暂停':'继续';
    if(playing)timer=setTimeout(()=>startStep(step+1),step===1?1900:step===20?2300:3300);
  }
  $('pause').onclick=()=>{playing=!playing;clearTimeout(timer);$('pause').textContent=playing?'暂停':'继续';if(playing)timer=setTimeout(()=>startStep(step+1),2000);};
  $('previous').onclick=()=>{playing=false;startStep(step-1);};$('next').onclick=()=>{playing=false;startStep(step+1);};$('restart').onclick=()=>{playing=true;startStep(0);};
  document.addEventListener('visibilitychange',()=>{if(document.hidden){clearTimeout(timer);}else if(tourMode&&playing){timer=setTimeout(()=>startStep(step+1),2000);}});
  Promise.all([new Promise((resolve,reject)=>{source.onload=resolve;source.onerror=reject;source.src='assets/source.png';}),new Promise((resolve,reject)=>{translated.onload=resolve;translated.onerror=reject;translated.src='assets/translated.png';}),fetch('assets/steps.json').then(r=>{if(!r.ok)throw Error('steps');return r.json();})]).then(values=>{
    steps=values[2];ready=true;steps.forEach((s,i)=>{const b=document.createElement('button');b.dataset.step=i;b.title=s.title;b.setAttribute('aria-label',s.title);b.onclick=()=>{playing=false;startStep(i);};$('step-dots').append(b);});
    if(matchMedia('(prefers-reduced-motion: reduce)').matches)playing=false;
    if(tourMode)startStep(0);else setMode(false);
  }).catch(()=>{$('step-title').textContent='演示素材加载失败';$('step-caption').textContent='请刷新页面，或从仓库下载完整视频查看。';});
})();
