(() => {
  const dialog = document.getElementById('radio-dialog');
  const wave = document.getElementById('radio-wave');
  const transcript = document.getElementById('radio-transcript');
  const voice = new Audio();
  let powered = true, frequency = 94.2, returnFocus, context, noise, ambient, noiseGain, ambientGain, bulletinTimer;
  let catalog = [];
  fetch('assets/radio/broadcasts.json').then(r => r.json()).then(data => {catalog = data.broadcasts;}).catch(() => {});
  const station = () => Math.abs(frequency-94.2)<.35 ? 'coast' : Math.abs(frequency-101.6)<.35 ? 'weather' : Math.abs(frequency-88.6)<.4 ? 'ambient' : 'static';
  function ensureAudio() {
    if (context) {context.resume(); return;}
    context = new AudioContext();
    const buffer = context.createBuffer(1,context.sampleRate*4,context.sampleRate);
    const samples = buffer.getChannelData(0);
    let smooth=0;
    for(let i=0;i<samples.length;i++) {smooth=.76*smooth+.24*(Math.random()*2-1);samples[i]=smooth;}
    noise=context.createBufferSource();noise.buffer=buffer;noise.loop=true;
    noiseGain=context.createGain();noiseGain.gain.value=0;noise.connect(noiseGain).connect(context.destination);noise.start();
    ambient=new Audio('assets/radio/ambient.wav');ambient.loop=true;ambient.volume=0;
    ambientGain=context.createGain();ambientGain.gain.value=0;
    context.createMediaElementSource(ambient).connect(ambientGain).connect(context.destination);
    ambient.volume=1;ambient.play().catch(()=>{});
  }
  function update() {
    const band=station();
    document.getElementById('radio-frequency').textContent=frequency.toFixed(1).replace('.',',');
    wave.value=frequency;wave.setAttribute('aria-valuetext',frequency.toFixed(1)+' МГц');
    dialog.dataset.powered=powered;
    document.getElementById('radio-on').setAttribute('aria-pressed',String(powered));
    document.getElementById('radio-off').setAttribute('aria-pressed',String(!powered));
    document.getElementById('radio-station').textContent=!powered?'Выключено':({coast:'Береговая служба',weather:'Метеопост',ambient:'Дальний эфир',static:'Помехи'})[band];
    if(context) {noiseGain.gain.setTargetAtTime(powered?(band==='static'?.075:.009):0,context.currentTime,.06);ambientGain.gain.setTargetAtTime(powered&&band==='ambient'?.12:0,context.currentTime,.2);}
  }
  function stopVoice() {voice.pause();voice.currentTime=0;transcript.textContent='';clearTimeout(bulletinTimer);}
  function bulletin() {
    if(!powered || dialog.open===false || !['coast','weather'].includes(station()))return;
    const requested=new URLSearchParams(location.search).get('broadcast');
    const key=requested || (station()==='weather'?'weather-fog':'aurora');
    const entry=catalog.find(item=>item.key===key&&item.station===station());
    if(!entry)return;
    voice.src='assets/radio/'+entry.key+'.wav';voice.volume=.64;transcript.textContent=entry.text;
    voice.play().catch(()=>{transcript.textContent='Не удалось воспроизвести запись.';});
    voice.onended=()=>{transcript.textContent='';};
  }
  function setPower(value) {ensureAudio();powered=value;stopVoice();update();if(value)bulletinTimer=setTimeout(bulletin,1600);}
  document.getElementById('radio-on').onclick=()=>setPower(true);
  document.getElementById('radio-off').onclick=()=>setPower(false);
  wave.addEventListener('input',()=>{ensureAudio();frequency=Number(wave.value);stopVoice();update();bulletinTimer=setTimeout(bulletin,1800);});
  function close(){if(dialog.open)dialog.close();returnFocus?.focus();}
  document.getElementById('radio-close').onclick=close;
  dialog.addEventListener('click',event=>{if(event.target===dialog) {const r=dialog.getBoundingClientRect();if(event.clientX<r.left||event.clientX>r.right||event.clientY<r.top||event.clientY>r.bottom)close();}});
  dialog.addEventListener('cancel',event=>{event.preventDefault();close();});
  dialog.addEventListener('keydown',event=>{event.stopPropagation();if(event.key==='Tab'&&!event.shiftKey) {event.preventDefault();close();window.dispatchEvent(new KeyboardEvent('keydown',{key:'Tab',code:'Tab',bubbles:true}));}});
  window.addEventListener('pagehide',()=>{stopVoice();ambient?.pause();context?.suspend();});
  window.keeperRadio={open(){returnFocus=document.activeElement;ensureAudio();update();dialog.showModal();wave.focus();bulletinTimer=setTimeout(bulletin,1800);},close,stop(){stopVoice();}};
  update();
})();
