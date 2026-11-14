const {chromium}=require('../Artifacts/EventsUpgrade/WebQA/node_modules/playwright');
const fs=require('node:fs'),path=require('node:path');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true,args:['--enable-webgl','--ignore-gpu-blocklist']});
 const page=await browser.newPage({viewport:{width:1200,height:900}}),errors=[],results=[];
 page.on('pageerror',e=>errors.push(e.message));
 await page.goto('file:///'+path.resolve('ArtSource/observation-vfx-lab.html').replaceAll('\\','/'));
 const out='Artifacts/MarineOverhaul/Web';fs.mkdirSync(out,{recursive:true});
 for(let kind=0;kind<8;kind++){
  await page.locator('select').selectOption(String(kind));await page.locator('input').fill(kind===1?'7.8':'8');await page.locator('input').dispatchEvent('input');
  await page.waitForTimeout(150);const before=await page.locator('canvas').screenshot({path:out+'/event-'+kind+'.png'});
  const glError=await page.locator('canvas').evaluate(c=>c.getContext('webgl').getError());
  await page.locator('input').fill(kind===1?'8.4':'10');await page.locator('input').dispatchEvent('input');await page.waitForTimeout(120);
  const after=await page.locator('canvas').screenshot();results.push({kind,label:await page.locator('#lh-detail').textContent(),glError,moves:!before.equals(after)});
 }
 await page.setViewportSize({width:340,height:720});await page.waitForTimeout(100);
 const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1);
 const passed=!errors.length&&!overflow&&results.every(r=>r.glError===0&&r.moves);
 fs.writeFileSync(out+'/verification.json',JSON.stringify({passed,errors,overflow,results},null,2));await browser.close();console.log(JSON.stringify({passed,errors,overflow,results}));process.exitCode=passed?0:1;
})();
