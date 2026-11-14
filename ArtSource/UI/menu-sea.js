/* Actual HDRP ocean capture. Only the red interference is added by this shader. */
(function () {
  'use strict';
  if (new URLSearchParams(location.search).get('context') === 'pause' && parent !== window) {
    document.documentElement.dataset.menuContext = 'pause';
    const video = document.getElementById('sea-video');
    video.pause(); video.removeAttribute('autoplay'); video.removeAttribute('src'); video.load();
    window.MenuSea = Object.freeze({
      configure(values) { parent.postMessage({type:'keeper-settings-preview', brightness:values.brightness}, location.origin); },
      setActive() {}, previewGlitch() { return false; }
    });
    return;
  }
  const canvas = document.getElementById('sea-canvas');
  const source = document.getElementById('sea-fallback');
  const video = document.getElementById('sea-video');
  const motion = matchMedia('(prefers-reduced-motion: reduce)');
  const gl = canvas.getContext('webgl', {alpha: false, antialias: false, depth: false, powerPreference: 'low-power'});
  let settings = {...MenuState.defaults}, active = true, ready = false, forcedUntil = 0;
  let lastFrame = 0, elapsed = 0, glitchStart = -10, glitchDuration = 1, nextGlitch = 8;
  let animationId = 0, locations, program, texture, lastVideoTime = -1;
  const vertex = 'attribute vec2 p; varying vec2 v; void main(){v=p*.5+.5;gl_Position=vec4(p,0.,1.);}';
  const fragment = `
    precision mediump float;
    varying vec2 v;
    uniform sampler2D photo;
    uniform vec2 cover;
    uniform float time, glitch;
    float hash(float p){return fract(sin(p*127.1)*43758.5453);}
    void main(){
      vec2 uv=(v-.5)*cover+.5;
      float band=floor(v.y*31.);
      float cut=step(.77,hash(band+floor(time*5.)))*glitch;
      uv.x+=(hash(band+floor(time*3.))-.5)*.08*cut;
      float split=glitch*.005;
      vec3 col=texture2D(photo,uv).rgb;
      col.r=texture2D(photo,uv+vec2(split,0.)).r;
      col.b=texture2D(photo,uv-vec2(split,0.)).b;
      float luma=dot(col,vec3(.299,.587,.114));
      vec3 red=vec3(luma*1.7+.025,luma*.075,luma*.12);
      red=mix(red,vec3(.32,.007,.019),cut*.35);
      col=mix(col,red,glitch*.95);
      col*=1.-cut*.22;
      gl_FragColor=vec4(col,1.);
    }`;
  function compile(kind, code) {
    const shader = gl.createShader(kind);
    gl.shaderSource(shader, code); gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) { gl.deleteShader(shader); return null; }
    return shader;
  }
  function init() {
    if (ready || !gl || !source.complete || !source.naturalWidth) return;
    const vs = compile(gl.VERTEX_SHADER, vertex), fs = compile(gl.FRAGMENT_SHADER, fragment);
    if (!vs || !fs) return;
    program = gl.createProgram(); gl.attachShader(program, vs); gl.attachShader(program, fs); gl.linkProgram(program);
    gl.deleteShader(vs); gl.deleteShader(fs);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) return;
    gl.useProgram(program);
    const buffer = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1,-1, 1,-1, -1,1, -1,1, 1,-1, 1,1]), gl.STATIC_DRAW);
    const position = gl.getAttribLocation(program, 'p'); gl.enableVertexAttribArray(position); gl.vertexAttribPointer(position, 2, gl.FLOAT, false, 0, 0);
    locations = Object.fromEntries(['cover','time','glitch'].map(name => [name, gl.getUniformLocation(program, name)]));
    texture = gl.createTexture(); gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
    try { gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGB, gl.RGB, gl.UNSIGNED_BYTE, source); }
    catch { canvas.dataset.renderer = 'video'; return; }
    ready = true; lastVideoTime = -1; canvas.classList.add('ready'); canvas.dataset.renderer = 'webgl'; resize();
  }
  function resize() {
    if (!ready) return;
    const width = Math.min(innerWidth * Math.min(devicePixelRatio || 1, 1.25), 1920);
    canvas.width = Math.round(width); canvas.height = Math.round(width * innerHeight / innerWidth);
    gl.viewport(0, 0, canvas.width, canvas.height);
    const displayAspect = canvas.width / canvas.height, imageAspect = source.naturalWidth / source.naturalHeight;
    gl.uniform2f(locations.cover, displayAspect < imageAspect ? displayAspect / imageAspect : 1, displayAspect > imageAspect ? imageAspect / displayAspect : 1);
    draw(0);
  }
  function draw(glitch) {
    if (!ready) return;
    if (!motion.matches && video.readyState >= 2 && video.currentTime !== lastVideoTime) {
      try {
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGB, gl.RGB, gl.UNSIGNED_BYTE, video);
        lastVideoTime = video.currentTime;
      } catch { /* Keep the last valid frame if the decoder has no frame yet. */ }
    }
    gl.uniform1f(locations.time, motion.matches ? 0 : elapsed);
    gl.uniform1f(locations.glitch, glitch);
    gl.drawArrays(gl.TRIANGLES, 0, 6);
  }
  function syncPlayback() {
    lastFrame = 0;
    if (document.hidden || !active || motion.matches) video.pause();
    else video.play().catch(() => { /* Poster remains visible if autoplay is unavailable. */ });
    if (motion.matches) {
      document.body.dataset.background = 'sea';
      if (ready) {
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGB, gl.RGB, gl.UNSIGNED_BYTE, source);
        lastVideoTime = -1; draw(0);
      }
    }
  }
  function frame(now) {
    animationId = requestAnimationFrame(frame);
    const cap = settings.vsync ? 60 : settings.fps || 60;
    if (now - lastFrame < 1000 / cap - 1) return;
    const delta = lastFrame ? Math.min((now - lastFrame) / 1000, .1) : 0;
    lastFrame = now;
    if (document.hidden || !active || motion.matches) return;
    elapsed += delta;
    const allowGlitch = settings.glitches && !motion.matches;
    if (allowGlitch && elapsed > nextGlitch) {
      glitchStart = elapsed; glitchDuration = .85 + Math.random() * .4;
      nextGlitch = elapsed + 13 + Math.random() * 12;
    }
    const phase = (elapsed - glitchStart) / glitchDuration;
    // One red hold followed by a fade. No repetitive full-screen strobing.
    let glitch = allowGlitch && phase >= 0 && phase < 1 ? Math.min(1, phase * 10, (1 - phase) * 4) : 0;
    if (allowGlitch && now < forcedUntil) glitch = 1;
    const mode = glitch > .05 ? 'red' : 'sea';
    if (document.body.dataset.background !== mode) document.body.dataset.background = mode;
    if (!motion.matches || glitch) draw(glitch);
  }
  canvas.addEventListener('webglcontextlost', event => { event.preventDefault(); ready = false; canvas.classList.remove('ready'); canvas.dataset.renderer = 'video'; });
  canvas.addEventListener('webglcontextrestored', init);
  source.addEventListener('load', init, {once: true});
  video.addEventListener('loadeddata', () => { if (!ready) canvas.dataset.renderer = 'video'; syncPlayback(); });
  video.addEventListener('error', () => { if (!ready) canvas.dataset.renderer = 'poster'; });
  if (source.complete) init();
  addEventListener('resize', resize);
  motion.addEventListener('change', syncPlayback);
  document.addEventListener('visibilitychange', syncPlayback);
  canvas.dataset.renderer = ready ? 'webgl' : 'poster';
  syncPlayback();
  animationId = requestAnimationFrame(frame);
  window.MenuSea = Object.freeze({
    configure(value) { settings = {...value}; document.documentElement.style.setProperty('--brightness', value.brightness / 100); if (!settings.glitches) { forcedUntil = 0; glitchStart = -10; document.body.dataset.background = 'sea'; draw(0); } },
    setActive(value) { active = value; syncPlayback(); },
    previewGlitch() { if (!settings.glitches || motion.matches) return false; forcedUntil = performance.now() + 1400; return true; }
  });
  addEventListener('pagehide', () => { cancelAnimationFrame(animationId); video.pause(); });
  addEventListener('pageshow', event => { if (event.persisted) { syncPlayback(); animationId = requestAnimationFrame(frame); } });
})();
