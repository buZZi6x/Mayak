const {chromium}=require('../Artifacts/EventsUpgrade/WebQA/node_modules/playwright');
const fs=require('node:fs'),path=require('node:path');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true,args:['--enable-webgl','--ignore-gpu-blocklist']});
 const page=await browser.newPage({viewport:{width:1100,height:900}});let errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('file:///'+path.resolve('Artifacts/EventsUpgrade/web-lab-preview.html').replaceAll('\\','/'));
 const f=page.frameLocator('iframe');await f.locator('select').waitFor();const results=[];
 for(let i=0;i<8;i++){
  await f.locator('select').selectOption(String(i));await f.locator('input').fill('4');await f.locator('input').dispatchEvent('input');
  await page.waitForTimeout(180);await f.locator('canvas').screenshot({path:`Artifacts/EventsUpgrade/Web-${i}.png`});
  results.push({kind:i,label:await f.locator('#lh-detail').textContent(),webgl:await f.locator('canvas').evaluate(c=>c.getContext('webgl').getError())});
 }
 await page.setViewportSize({width:340,height:700});await f.locator('select').selectOption('4');await page.waitForTimeout(100);
 const overflow=await f.locator('#lh-observation-lab').evaluate(el=>el.scrollWidth>el.clientWidth+1);
 fs.writeFileSync('Artifacts/EventsUpgrade/web-verification.json',JSON.stringify({errors,results,narrowOverflow:overflow,passed:!errors.length&&!overflow&&results.every(r=>r.webgl===0)},null,2));
 await browser.close();console.log(JSON.stringify({errors,results,narrowOverflow:overflow}));
})();
