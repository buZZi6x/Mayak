(() => {
  'use strict';
  const $=id=>document.getElementById(id);
  const dialog=$('radio-dialog'),wave=$('radio-wave'),knob=$('radio-knob');
  const voice=new Audio(),music=new Audio(),ambient=new Audio('assets/radio/ambient.wav');
  voice.id='radio-voice';music.id='radio-track';ambient.id='radio-ambient';
  for(const source of [voice,music,ambient]) {source.hidden=true;dialog.append(source);}
  const tracks=[['ashes-of-the-keep','Ashes of the Keep'],['deckside-jig','Deckside Jig'],['sun-faded-memory','Sun-Faded Memory']];
  let powered=true,frequency=94.2,catalog=[],context,noiseGain,ambientGain,musicGain;
  let returnFocus,bulletinTimer,musicTimer,drag=null,lastMusic=-1,musicBag=[],sentences=[];
  let paused=false,pausedSources=[];
  fetch('assets/radio/broadcasts.json').then(r=>r.json()).then(data=>{catalog=data.broadcasts;}).catch(()=>{});
  const station=()=>Math.abs(frequency-94.2)<.35?'coast':Math.abs(frequency-101.6)<.35?'weather':Math.abs(frequency-88.6)<.4?'ambient':'static';
  const signal=()=>Math.max(0,1-Math.min(Math.abs(frequency-94.2),Math.abs(frequency-101.6),Math.abs(frequency-88.6))/.5);
  function ensureAudio() {
    if(context){context.resume();return;}
    context=new AudioContext();
    const buffer=context.createBuffer(1,context.sampleRate*4,context.sampleRate),samples=buffer.getChannelData(0);
    let smoothed=0;for(let i=0;i<samples.length;i++){smoothed=.76*smoothed+.24*(Math.random()*2-1);samples[i]=smoothed;}
    const noise=context.createBufferSource();noise.buffer=buffer;noise.loop=true;
    noiseGain=context.createGain();noiseGain.gain.value=0;noise.connect(noiseGain).connect(context.destination);noise.start();
    ambientGain=context.createGain();ambientGain.gain.value=0;ambient.loop=true;
    context.createMediaElementSource(ambient).connect(ambientGain).connect(context.destination);
    musicGain=context.createGain();musicGain.gain.value=0;context.createMediaElementSource(music).connect(musicGain).connect(context.destination);
    ambient.play().catch(()=>{});
  }
  function mix(){
    if(!context)return;
    const amount=powered?signal():0,duck=voice.paused?1:.13;
    noiseGain.gain.setTargetAtTime(powered?.009+(1-signal())*.066:0,context.currentTime,.07);
    ambientGain.gain.setTargetAtTime(amount*(music.paused?.15:.015)*duck,context.currentTime,.18);
    musicGain.gain.setTargetAtTime(amount*.48*duck,context.currentTime,.18);
    voice.volume=amount*.68;
  }
  function update(){
    const band=station();
    $('radio-frequency').textContent=frequency.toFixed(1).replace('.',',');
    wave.value=frequency;wave.setAttribute('aria-valuetext',frequency.toFixed(1)+' МГц');
    knob.setAttribute('aria-valuenow',String(frequency));knob.setAttribute('aria-valuetext',frequency.toFixed(1)+' МГц');
    knob.style.setProperty('--knob-angle',((frequency-94.2)*12.5)+'deg');
    dialog.dataset.powered=String(powered);
    $('radio-on').setAttribute('aria-pressed',String(powered));$('radio-off').setAttribute('aria-pressed',String(!powered));
    $('radio-station').textContent=!powered?'Выключено':({coast:'Береговая служба',weather:'Метеопост',ambient:'Дальний эфир',static:'Помехи'})[band];
    $('radio-music').textContent=powered&&band!=='static'&&!music.paused&&lastMusic>=0?tracks[lastMusic][1]:'';
    mix();
  }
  function stopVoice(){voice.pause();voice.currentTime=0;$('radio-transcript').textContent='';clearTimeout(bulletinTimer);mix();}
  function playMusic(){
    if(paused||!powered||station()==='static')return;
    if(!musicBag.length)musicBag=[0,1,2];
    let pick=Math.floor(Math.random()*musicBag.length);
    if(musicBag.length>1&&musicBag[pick]===lastMusic)pick=(pick+1)%musicBag.length;
    lastMusic=musicBag.splice(pick,1)[0];music.src='assets/radio/music/'+tracks[lastMusic][0]+'.ogg';
    music.play().then(update).catch(()=>{});
  }
  music.addEventListener('ended',()=>{update();musicTimer=setTimeout(playMusic,18000+Math.random()*18000);});
  function bulletin(){
    if(paused||!powered||!dialog.open||!['coast','weather'].includes(station()))return;
    const requested=new URLSearchParams(location.search).get('broadcast');
    const key=requested||(station()==='weather'?'weather-fog':'aurora');
    const entry=catalog.find(item=>item.key===key&&item.station===station());if(!entry)return;
    voice.src='assets/radio/'+entry.key+'.wav';
    sentences=entry.text.match(/[^.!?]+[.!?]*/g)||[entry.text];
    $('radio-transcript').textContent=sentences[0].trim();
    voice.play().then(mix).catch(()=>{$('radio-transcript').textContent='Не удалось воспроизвести запись.';});
  }
  voice.addEventListener('timeupdate',()=>{
    if(!voice.duration||voice.paused)return;
    const total=sentences.reduce((sum,s)=>sum+s.length,0);let position=voice.currentTime/voice.duration*total;
    for(const sentence of sentences){position-=sentence.length;if(position<=0){$('radio-transcript').textContent=sentence.trim();break;}}
  });
  voice.addEventListener('ended',()=>{$('radio-transcript').textContent='';mix();});
  function setPower(value){ensureAudio();if(powered===value){update();return;}powered=value;stopVoice();if(!value){music.pause();ambient.pause();clearTimeout(musicTimer);}else{ambient.play().catch(()=>{});musicTimer=setTimeout(playMusic,6000);bulletinTimer=setTimeout(bulletin,1800);}update();}
  function tune(value){
    ensureAudio();const previous=station();frequency=Math.round(Math.max(88,Math.min(108,value))*10)/10;
    if(previous!==station())stopVoice();update();clearTimeout(bulletinTimer);bulletinTimer=setTimeout(bulletin,1800);
    if(powered&&station()==='ambient'&&music.paused){clearTimeout(musicTimer);musicTimer=setTimeout(playMusic,600);}
  }
  $('radio-on').onclick=()=>setPower(true);$('radio-off').onclick=()=>setPower(false);
  wave.addEventListener('input',()=>tune(Number(wave.value)));
  knob.addEventListener('pointerdown',event=>{if(event.button!==0)return;drag={id:event.pointerId,x:event.clientX,y:event.clientY,start:frequency};knob.setPointerCapture(event.pointerId);knob.focus();event.preventDefault();});
  knob.addEventListener('pointermove',event=>{if(drag&&drag.id===event.pointerId)tune(drag.start+(event.clientX-drag.x-drag.y+event.clientY)*.065);});
  function release(){if(drag&&knob.hasPointerCapture(drag.id))knob.releasePointerCapture(drag.id);drag=null;}
  for(const type of ['pointerup','pointercancel','lostpointercapture'])knob.addEventListener(type,release);
  knob.addEventListener('wheel',event=>{event.preventDefault();tune(frequency+(event.deltaY<0?.1:-.1));},{passive:false});
  knob.addEventListener('keydown',event=>{const delta={ArrowRight:.1,ArrowUp:.1,ArrowLeft:-.1,ArrowDown:-.1,PageUp:1,PageDown:-1}[event.key];if(delta||['Home','End'].includes(event.key)){event.preventDefault();tune(event.key==='Home'?88:event.key==='End'?108:frequency+delta);}});
  function close(){release();if(dialog.open)dialog.close();returnFocus?.focus();}
  $('radio-close').onclick=close;
  dialog.addEventListener('cancel',event=>{event.preventDefault();close();});
  dialog.addEventListener('keydown',event=>{event.stopPropagation();if(event.key==='Tab'&&!event.shiftKey){event.preventDefault();close();document.dispatchEvent(new KeyboardEvent('keydown',{key:'Tab',code:'Tab',bubbles:true}));}});
  function silence(){stopVoice();music.pause();ambient.pause();clearTimeout(musicTimer);if(context){noiseGain.gain.value=0;ambientGain.gain.value=0;musicGain.gain.value=0;}}
  function setPaused(value){
    if(paused===value)return;
    paused=value;
    if(value){
      pausedSources=[voice,music,ambient].filter(source=>!source.paused&&!source.ended);
      for(const source of pausedSources)source.pause();
      clearTimeout(bulletinTimer);clearTimeout(musicTimer);context?.suspend();
    }else{
      context?.resume();
      if(powered)for(const source of pausedSources)source.play().catch(()=>{});
      if(powered&&music.paused)musicTimer=setTimeout(playMusic,5000);
      pausedSources=[];mix();
    }
  }
  window.addEventListener('pagehide',()=>{silence();context?.suspend();});
  window.keeperRadio={open(){returnFocus=document.activeElement;ensureAudio();if(powered)ambient.play().catch(()=>{});update();dialog.showModal();wave.focus();bulletinTimer=setTimeout(bulletin,1800);if(music.paused){clearTimeout(musicTimer);musicTimer=setTimeout(playMusic,5000);}},close,stop:silence,setPaused};
  update();
})();
