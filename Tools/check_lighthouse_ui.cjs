const {chromium} = require('../Artifacts/EventsUpgrade/WebQA/node_modules/playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const out = path.resolve(__dirname,'../Artifacts/UI');
const url = process.env.LIGHTHOUSE_UI_URL || 'http://127.0.0.1:4317';
const reviewUrl = new URL(url);
reviewUrl.searchParams.set('review','1');
fs.mkdirSync(out,{recursive:true});

(async () => {
  const browser = await chromium.launch({channel:'msedge',headless:true});
  const page = await browser.newPage({viewport:{width:1920,height:1080},reducedMotion:'reduce',hasTouch:true});
  page.setDefaultTimeout(10000);
  const errors = [], checks = [];
  page.on('pageerror',error => errors.push(error.message));
  page.on('console',message => {if(message.type()==='error') errors.push(message.text());});
  const check = async (name,fn) => {await fn(); checks.push(name); console.log('PASS '+name);};
  const key = async value => {await page.keyboard.press(value); await page.waitForTimeout(60);};
  const visible = id => page.locator('#'+id).isVisible();
  const text = id => page.locator('#'+id).textContent();
  const photoCount = async () => String(await page.locator('.photo-slot:not(.empty)').count());
  const scene = async id => {await page.locator('#location-toggle').click(); await page.locator(`[data-scene="${id}"]`).click(); await page.waitForTimeout(80);};
  const screenshot = async name => {await page.mouse.move(10,10); await page.waitForTimeout(250); await page.screenshot({path:path.join(out,name+'.png')});};
  try {
    await page.goto(url);
    await page.locator('#scene-image').evaluate(img => img.decode());
    await check('initial inventory: three actual photographs, two empty slots',async () => {
      assert.equal(await page.locator('.photo-slot').count(),5);
      assert.equal(await page.locator('.photo-slot.empty').count(),2);
      assert.equal(await text('score-label'),'480');
      await page.locator('img[src]').evaluateAll(images => Promise.all(images.map(img => img.decode())));
      assert.equal(await page.locator('img[src]').evaluateAll(images => images.every(img => img.complete && img.naturalWidth>0)),true);
    });
    await screenshot('01-desktop');
    await page.goto(reviewUrl.href);
    await check('1–5 and numpad select photographs; empty slot preserves held photo',async () => {
      await key('Digit2'); assert.equal(await text('held-title'),'Атака кракена');
      await key('Numpad3'); assert.equal(await text('held-title'),'Полярная завеса');
      await key('Digit4'); assert.equal(await text('held-title'),'Полярная завеса');
      assert.match(await text('toast'),/Слот 4 свободен/);
      await page.locator('[data-slot="0"]').click(); assert.equal(await text('held-title'),'Корабль');
    });
    await check('Q toggles independent flashlight; repeated key does not flicker',async () => {
      await key('KeyQ'); assert.equal(await page.locator('#torch-toggle').getAttribute('aria-pressed'),'true');
      await page.keyboard.down('KeyQ');
      const current=await page.locator('#torch-toggle').getAttribute('aria-pressed');
      await page.keyboard.down('KeyQ');
      assert.equal(await page.locator('#torch-toggle').getAttribute('aria-pressed'),current);
      await page.keyboard.up('KeyQ');
      assert.equal(await visible('held-photo'),true);
    });
    await check('photo inspection: click, arrows, wheel, numbers, Escape',async () => {
      await page.locator('#held-photo').click();
      assert.equal(await page.locator('#photo-dialog').evaluate(dialog => dialog.open),true);
      await key('ArrowRight'); assert.equal(await text('inspect-title'),'Кракен атакует корабль');
      await key('Digit3'); assert.equal(await text('inspect-title'),'Полярная завеса');
      await page.locator('#inspect-next').hover(); await page.waitForTimeout(220);
      assert.equal(await visible('tooltip'),true);
      assert.match(await text('tooltip'),/Следующий снимок/);
      await page.mouse.move(500,400); await page.mouse.wheel(0,120); await page.waitForTimeout(260);
      assert.equal(await text('inspect-title'),'Корабль в луче маяка');
      await screenshot('02-photo-inspection');
      await key('Escape'); assert.equal(await visible('photo-dialog'),false);
    });
    await check('tooltips show photo and object data and stay within viewport',async () => {
      await page.locator('[data-slot="1"]').hover(); await page.waitForTimeout(250);
      assert.equal(await visible('tooltip'),true);
      assert.match(await text('tooltip'),/300 очков/);
      const b=await page.locator('#tooltip').boundingBox(); assert(b.x>=0 && b.y>=0 && b.x+b.width<=1920 && b.y+b.height<=1080);
      await page.screenshot({path:path.join(out,'03-photo-tooltip.png')});
    });
    await check('drag moves a photo to an empty slot without changing price or selected photo',async () => {
      await key('Digit1');
      await page.locator('[data-slot="0"]').dragTo(page.locator('[data-slot="4"]'));
      assert.match(await page.locator('[data-slot="4"]').getAttribute('aria-label'),/Корабль/);
      assert.equal(await page.locator('[data-slot="4"]').getAttribute('aria-pressed'),'true');
      assert.equal(await text('score-label'),'480');
      assert.equal(await page.locator('.dragging,.drop-target').count(),0);
      await page.locator('[data-slot="4"]').dragTo(page.locator('[data-slot="0"]'));
    });
    await check('cancelled drag keeps slots intact; keyboard reorder preserves identity',async () => {
      const source=await page.locator('[data-slot="1"]').boundingBox();
      await page.mouse.move(source.x+30,source.y+30); await page.mouse.down(); await page.mouse.move(source.x+80,source.y-150,{steps:10});
      await key('Escape'); await page.mouse.up();
      assert.match(await page.locator('[data-slot="1"]').getAttribute('aria-label'),/Кракен/);
      assert.equal(await page.locator('.dragging,.drop-target').count(),0);
      await page.locator('[data-slot="1"]').focus(); await key('Shift+ArrowRight');
      assert.match(await page.locator('[data-slot="2"]').getAttribute('aria-label'),/Кракен/);
      await key('Shift+ArrowLeft');
      assert.match(await page.locator('[data-slot="1"]').getAttribute('aria-label'),/Кракен/);
    });
    await check('E equips camera; HUD clicks never take a photo',async () => {
      await key('KeyE'); assert.equal(await visible('viewfinder'),true); assert.equal(await visible('held-photo'),false);
      await page.locator('#torch-toggle').click(); assert.equal(await photoCount(),'3');
      await screenshot('04-camera');
    });
    await check('Tab hides all HUD and tooltips, ignores hidden inputs, restores camera',async () => {
      await page.locator('#camera-toggle').hover(); await page.waitForTimeout(220);
      await key('Tab');
      assert.equal(await page.locator('#game').evaluate(el => el.classList.contains('clean-view')),true);
      assert.equal(await visible('hud'),false); assert.equal(await visible('tooltip'),false);
      await key('KeyQ'); await key('Digit2'); await key('KeyE');
      await screenshot('05-clean-view');
      await key('Tab'); assert.equal(await visible('viewfinder'),true);
      await key('Tab'); await page.locator('#world').dblclick({position:{x:400,y:300}});
      assert.equal(await visible('hud'),true);
    });
    await check('camera captures first free slot, correct repeat price, hard five-shot limit',async () => {
      await page.locator('#world').click({position:{x:500,y:300}}); assert.equal(await photoCount(),'4'); assert.equal(await text('score-label'),'510');
      await page.locator('#world').click({position:{x:500,y:300}});
      assert.equal(await photoCount(),'5'); assert.equal(await text('score-label'),'525');
      await page.locator('#world').click({position:{x:500,y:300}}); assert.equal(await photoCount(),'5');
      assert.match(await text('toast'),/пять кадров/);
    });
    await check('F radio marks all photos sent only after completing transmission',async () => {
      await scene('radio');
      await page.locator('[data-object="radio"]').hover(); await page.waitForTimeout(250);
      await page.screenshot({path:path.join(out,'06-radio-tooltip.png')});
      await page.mouse.move(10,10);
      await key('KeyF');
      assert.match(await page.locator('.object-action').textContent(),/Передаём/);
      await page.waitForTimeout(4300);
      assert.match(await page.locator('.object-action').textContent(),/Все снимки отправлены/);
      assert.equal(await page.locator('.pending-dot').count(),0);
      assert.equal(await text('score-label'),'525');
    });
    await check('F lamp repairs condition after six seconds without adding fuel',async () => {
      await scene('lamp'); await key('KeyF'); await page.waitForTimeout(6250);
      assert.match(await text('toast'),/Лампа обслужена/); assert.equal(await text('fuel-label'),'01:28');
    });
    await check('F door state toggles and telescope zoom works',async () => {
      await scene('door'); await key('KeyF'); assert.match(await page.locator('.object-action').textContent(),/Открыть дверь/);
      await key('KeyF'); assert.match(await page.locator('.object-action').textContent(),/Закрыть дверь/);
      await scene('telescope'); await key('KeyF'); assert.equal(await page.locator('#game').evaluate(el => el.classList.contains('aiming')),true);
      await key('KeyF'); assert.equal(await page.locator('#game').evaluate(el => el.classList.contains('aiming')),false);
    });
    await check('F bed starts next day, resets five slots, preserves score, grants bonus once',async () => {
      await scene('bed'); await key('KeyF'); await page.waitForTimeout(3300);
      assert.match(await text('toast'),/День 2/); assert.equal(await photoCount(),'0');
      assert.equal(await text('score-label'),'525'); assert.equal(await text('fuel-label'),'02:18');
      await key('KeyF'); assert.match(await text('toast'),/Нужно ещё 5 кадров/);
      assert.equal(await text('fuel-label'),'02:18');
    });
    await check('non-subject photo consumes one attempt for zero points',async () => {
      await scene('lamp'); await key('KeyE'); await page.locator('#world').click({position:{x:500,y:300}});
      assert.equal(await photoCount(),'1'); assert.equal(await text('score-label'),'525');
      await key('Digit1'); assert.equal(await text('held-title'),'Пустой кадр');
    });
    await check('four stations retain tooltips; door has only a compact F command',async () => {
      for (const id of ['door','lamp','radio','bed','telescope']) {
        await scene(id); await page.locator(`[data-object="${id}"]`).hover(); await page.waitForTimeout(220);
        if (id === 'door') {
          assert.equal(await visible('tooltip'),false);
          assert.equal(await page.locator('#object-card h2, #object-card p').count(),0);
          assert.match(await text('object-card'),/F— (Открыть|Закрыть) дверь/);
          await screenshot('07-door-prompt');
        } else {
          const description=await page.locator(`[data-object="${id}"]`).getAttribute('aria-describedby');
          assert(description);
          assert.equal(await visible(description),true);
          assert.match(await text(description),/F/);
        }
        assert.equal(await visible('object-card'),true);
      }
    });
    await page.goto(url); await page.waitForLoadState('networkidle');
    await check('responsive layouts: 1920, 1440, 1280, 1024, 768, 390 and 360 px',async () => {
      for (const [width,height] of [[1920,1080],[1440,900],[1280,720],[1024,768],[768,1024],[390,844],[360,740],[844,390]]) {
        await page.setViewportSize({width,height}); await page.waitForTimeout(120);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth>innerWidth || document.documentElement.scrollHeight>innerHeight),false);
        for (const selector of ['#photo-slots','#torch-toggle','#camera-toggle','#clean-toggle','.watch-stats']) {
          const rect=await page.locator(selector).boundingBox();
          assert(rect && rect.x>=0 && rect.y>=0 && rect.x+rect.width<=width+1 && rect.y+rect.height<=height+1,`${selector} out of viewport ${width}x${height}: ${JSON.stringify(rect)}`);
        }
        if(width===1440 || width===390 || width===844) await screenshot(`layout-${width}x${height}`);
      }
    });
    await check('mobile taps: selection, inspection, camera, clean view and double-tap return',async () => {
      await page.setViewportSize({width:390,height:844});
      await page.locator('[data-slot="1"]').tap(); assert.equal(await text('held-title'),'Атака кракена');
      await page.locator('#held-photo').tap(); assert.equal(await visible('photo-dialog'),true);
      await page.locator('#inspect-close').tap();
      await page.locator('#camera-toggle').tap(); assert.equal(await visible('viewfinder'),true);
      await page.locator('#clean-toggle').tap(); assert.equal(await visible('hud'),false);
      await page.touchscreen.tap(180,300); await page.touchscreen.tap(180,300);
      assert.equal(await visible('hud'),true);
    });
    await check('no browser errors',async () => assert.deepEqual(errors,[]));
    fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({passed:true,url,checks,errors},null,2));
  } catch(error) {
    await page.screenshot({path:path.join(out,'failure.png')}).catch(()=>{});
    fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({passed:false,checks,errors,failure:error.stack},null,2));
    console.error(error); process.exitCode=1;
  } finally { await browser.close(); }
})();
