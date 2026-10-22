// Build-time asset import; the application never downloads icons at runtime.
const https = require('https');
const fs = require('fs');
const path = require('path');
const sharp = require('sharp'); // Build-time dependency (npm install sharp); not shipped with the app.
const output = path.resolve(__dirname, '../source/ui/icons');
const icons = {
  folder: ['Document Folder'], refresh: ['Arrow Sync'], picture: ['Image'],
  details: ['Apps List'], tree: ['Folder', 'regular'], select: ['Checkbox Checked', 'regular'],
  back: ['Arrow Left', 'regular'], forward: ['Arrow Right', 'regular'],
  pack: ['Folder Zip', 'regular', '#b47700'], unpack: ['Archive Arrow Back', 'regular', '#b47700'],
  'archive-test': ['Shield Checkmark', 'regular', '#16834a'], ftp: ['Cloud'], url: ['Link'], network: ['Cloud'],
  search: ['Search', 'regular'], rename: ['Edit'], edit: ['Document Edit'], sync: ['Arrow Swap', 'regular'],
  favorite: ['Bookmark'], notepad: ['Document Text'], copy: ['Copy', 'regular'],
  move: ['Folder Arrow Right', 'regular'], delete: ['Delete', 'regular', '#c42b1c'],
  settings: ['Settings'], document: ['Document'], lock: ['Document Lock']
};
function get(url) { return new Promise((resolve,reject)=>https.get(url,{headers:{'User-Agent':'CommanderCZ-assets'}},r=>{
  if(r.statusCode>=300&&r.statusCode<400) {r.resume();get(r.headers.location).then(resolve,reject);return;}
  if(r.statusCode!==200){r.resume();reject(new Error(r.statusCode+' '+url));return;}
  const chunks=[];r.on('data',c=>chunks.push(c));r.on('end',()=>resolve(Buffer.concat(chunks)));r.on('error',reject);
}).on('error',reject)); }
(async()=>{
  fs.mkdirSync(output,{recursive:true});
  const metadata=path.join(output,'SOURCE.json');
  const commit=fs.existsSync(metadata)?JSON.parse(fs.readFileSync(metadata)).commit:JSON.parse(await get('https://api.github.com/repos/microsoft/fluentui-system-icons/commits/main')).sha;
  fs.writeFileSync(metadata,JSON.stringify({repository:'https://github.com/microsoft/fluentui-system-icons',commit,license:'MIT',renderSize:96},null,2));
  for(const [key,[name,variant='color',tint='#0067c0']] of Object.entries(icons)){
    const url=`https://raw.githubusercontent.com/microsoft/fluentui-system-icons/${commit}/assets/${encodeURIComponent(name)}/SVG/ic_fluent_${name.toLowerCase().replaceAll(' ','_')}_24_${variant}.svg`;
    const original=await get(url);fs.writeFileSync(path.join(output,key+'.svg'),original);
    const rendered=variant==='regular'?original.toString().replace(/#212121/g,tint):original;
    await sharp(Buffer.from(rendered)).resize(96,96).png().toFile(path.join(output,key+'.png'));
    console.log(key);
  }
  fs.writeFileSync(path.join(output,'LICENSE.txt'),await get(`https://raw.githubusercontent.com/microsoft/fluentui-system-icons/${commit}/LICENSE`));
})().catch(e=>{console.error(e);process.exitCode=1;});
