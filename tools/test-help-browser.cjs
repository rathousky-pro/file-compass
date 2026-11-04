// Run against a built installation: node tools/test-help-browser.cjs <build> <results>.
// Requires Playwright; uses the installed Edge browser and no network services.
const {chromium}=require('playwright');
const path=require('node:path'),fs=require('node:fs/promises'),{pathToFileURL}=require('node:url'),assert=require('node:assert/strict');
(async()=>{
 const root=path.resolve(process.argv[2]),out=path.resolve(process.argv[3]);await fs.mkdir(out,{recursive:true});
 const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;
 const check=(ok,label)=>{assert.ok(ok,label);checks++;};
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000},offline:true});
  const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(error.message));
  for(const locale of ['cs','en']){
   const uri=pathToFileURL(path.join(root,'help',locale,'index.html')).href;
   await page.goto(uri+'#copy');
   check(await page.locator('article:visible').count()===1,'One topic is visible');
   check(await page.locator('article:visible').getAttribute('id')==='copy','Topic hash opens copy help');
   const picture=page.locator('#copy figure img').first();await picture.scrollIntoViewIfNeeded();
   await picture.evaluate(image=>image.decode());check(await picture.evaluate(image=>image.naturalWidth>=250&&image.naturalHeight>=100),'External screenshot loads offline');
   await picture.click();check(await page.locator('#zoom').evaluate(dialog=>dialog.open),'Screenshot zoom opens');
   await page.keyboard.press('Escape');check(!await page.locator('#zoom').evaluate(dialog=>dialog.open),'Escape closes zoom');
   await page.locator('[data-topic-link="settings"]').click();check(new URL(page.url()).hash==='#settings','Internal navigation');
   await page.goBack();check(new URL(page.url()).hash==='#copy','Browser back restores topic');
   await page.locator('#help-search').fill(locale==='cs'?'kopirovani':'copy');
   check(await page.locator('article:visible').count()>0,'Search works without accents');
   await page.locator('#help-search').fill('xyzzzzzz');check(await page.locator('article:visible').count()===0&&await page.locator('#no-results').isVisible(),'Empty search');
   await page.keyboard.press('Escape');check(await page.locator('#copy').isVisible(),'Escape clears search');
   await page.locator('[data-language]').filter({hasText:locale==='cs'?'English':'Čeština'}).click();
   check(new URL(page.url()).hash==='#copy','Language switch retains topic');
   await page.goto(uri+'#archives');await page.screenshot({path:path.join(out,locale+'.png'),fullPage:false});
   await page.setViewportSize({width:390,height:844});
   check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Mobile width has no horizontal overflow');
   await page.setViewportSize({width:1440,height:1000});
  }
  check(errors.length===0,'No JavaScript errors: '+errors.join(', '));
  await fs.writeFile(path.join(out,'result.txt'),'PASS '+checks+' offline browser checks\n');console.log('PASS '+checks+' offline browser checks');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
