// Build-time only. NODE_PATH may point to an existing sharp installation.
// node --use-system-ca tools/build-fluent-color.cjs [--offline]
const fs = require('fs'), path = require('path'), sharp = require('sharp');
const root = path.resolve(__dirname, '../source/ui/icons');
const commit = JSON.parse(fs.readFileSync(path.join(root, 'SOURCE.json'))).commit;
const renderSize = 256;
const official = {
  folder: 'Document Folder', refresh: 'Arrow Sync', details: 'Apps List',
  select: 'Checkbox', 'archive-test': 'Shield Checkmark', ftp: 'Cloud', url: 'Link',
  network: 'Org', rename: 'Edit', edit: 'Document Edit', favorite: 'Bookmark',
  notepad: 'Document Text', settings: 'Settings', document: 'Document', lock: 'Document Lock',
  tools: 'Wrench Screwdriver', view: 'Content View', 'new-file': 'Document Add'
};
// Original File Compass vector artwork. Shared palette follows Fluent Color's
// cyan/blue, teal and purple gradients, rounded silhouettes and layered surfaces.
const gradient = (id, a, b) => `<linearGradient id="${id}" x1="0" y1="0" x2="1" y2="1"><stop stop-color="${a}"/><stop offset="1" stop-color="${b}"/></linearGradient>`;
const defs = gradient('blue','#29C3FF','#1554CF') + gradient('teal','#38D5B0','#087F8C') + gradient('purple','#C58AFF','#7041C5') + gradient('paper','#F2FAFF','#B6DDF8') + gradient('gold','#FFE08A','#EF9D22') + gradient('red','#FF8792','#CB254B') + gradient('slate','#B7CBDF','#617999');
const svg = body => `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"><defs>${defs}</defs>${body}</svg>\n`;
const rect = (x,y,w,h,fill,r=2) => `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}"/>`;
const shape = (d,fill) => `<path d="${d}" fill="${fill}"/>`;
const line = (d,color,width=1.6) => `<path d="${d}" stroke="${color}" stroke-width="${width}" stroke-linecap="round" stroke-linejoin="round" fill="none"/>`;
const circle = (x,y,r,fill) => `<circle cx="${x}" cy="${y}" r="${r}" fill="${fill}"/>`;
const paper = (x=5,y=2,w=14,h=19) => rect(x,y,w,h,'url(#paper)') + shape(`M${x+w-5} ${y} v4 q0 1 1 1 h4 Z`,'#FFFFFF');
const folder = () => shape('M2 6 Q2 3 5 3 H9 L12 6 H19 Q22 6 22 9 V18 H2 Z','url(#gold)') + rect(3,8,18,10,'#FFF2C5') + shape('M2 10 H22 L21 19 Q20.8 21 19 21 H5 Q3 21 2.8 19 Z','url(#gold)');
const custom = {};
custom.back = svg(shape('M10.4 3.6 Q9.8 3 9.2 3.6 L1.8 11 Q.8 12 1.8 13 L9.2 20.4 Q9.8 21 10.4 20.4 L12 18.8 Q12.6 18.2 12 17.6 L8.4 14 H21 Q22 14 22 13 V11 Q22 10 21 10 H8.4 L12 6.4 Q12.6 5.8 12 5.2 Z','url(#blue)'));
custom.forward = custom.back.replace('</defs>','</defs><g transform="translate(24 0) scale(-1 1)">').replace('</svg>','</g></svg>');
custom.undo = svg(shape('M9 3 Q9 2 8 3 L2 8 Q1 9 2 10 L8 15 Q9 16 9 15 V12 H14 Q19 12 19 16 Q19 19 15 19 H13 Q12 19 12 20 V21 Q12 22 13 22 H15 Q23 22 23 16 Q23 8 14 8 H9 Z','url(#blue)'));
custom.search = svg(line('M15.6 15.6 L21 21','url(#blue)',3.4) + circle(10,10,8,'url(#blue)') + circle(10,10,5.2,'url(#paper)') + line('M7 9 Q7.5 7 10 7','#FFF',1.5));
custom.sync = svg(shape('M3 5 H16 V2 Q16 1 17 2 L22 7 Q23 8 22 9 L17 14 Q16 15 16 14 V11 H3 Q2 11 2 10 V6 Q2 5 3 5 Z','url(#teal)') + shape('M21 13 H8 V10 Q8 9 7 10 L2 15 Q1 16 2 17 L7 22 Q8 23 8 22 V19 H21 Q22 19 22 18 V14 Q22 13 21 13 Z','url(#blue)'));
custom.copy = svg(rect(2,2,14,17,'url(#blue)') + paper(8,6,14,17) + line('M11 13 H18 M11 16 H18 M11 19 H16','#5895C7',1.3));
custom.move = svg(folder() + circle(17.5,17.5,6,'url(#blue)') + line('M14 17.5 H21 M18 14.5 L21 17.5 L18 20.5','#FFF',1.8));
custom.delete = svg(shape('M5 7 H19 L18 20 Q18 22 16 22 H8 Q6 22 6 20 Z','url(#red)') + rect(3,4,18,3,'url(#red)',1.3) + rect(9,2,6,3,'url(#red)',1) + line('M9 10 L9.5 18 M15 10 L14.5 18','#FFE4E8',1.4));
custom.tree = svg(line('M6 6 V18 H16 M6 12 H16','#8198BA',1.8) + rect(2,2,8,6,'url(#gold)',1.3) + rect(13,9,9,6,'url(#blue)',1.3) + rect(13,17,9,6,'url(#teal)',1.3));
custom.invert = svg(rect(2,2,12,12,'url(#purple)') + line('M5 8 L7 10 L11 6','#FFF',1.8) + rect(11,11,11,11,'url(#paper)') + line('M17 3 H21 V7 M7 21 H3 V17','#8254CB',1.8));
for (const [key,extract] of [['pack',false],['unpack',true]]) custom[key] = svg(folder() + circle(17.5,17.5,6,extract?'url(#teal)':'url(#blue)') + line(extract?'M17.5 21 V14 M14.5 17 L17.5 14 L20.5 17':'M17.5 14 V21 M14.5 18 L17.5 21 L20.5 18','#FFF',1.8));
for (const mode of ['SideBySide','Stacked','Single']) {
  let body = rect(2,3,20,18,'url(#slate)') + rect(3.5,7,17,12.5,'url(#paper)',1);
  if(mode==='SideBySide') body += rect(3.5,7,7.5,12.5,'url(#blue)',1) + rect(13,7,7.5,12.5,'url(#teal)',1);
  else if(mode==='Stacked') body += rect(3.5,7,17,5.2,'url(#blue)',1) + rect(3.5,14.2,17,5.3,'url(#teal)',1);
  else body += rect(3.5,7,17,12.5,'url(#blue)',1);
  custom['panels-'+mode] = svg(body);
}
for (const state of ['visible','partial','hidden']) {
  const ink=state==='visible'?'url(#teal)':state==='partial'?'url(#gold)':'url(#slate)';
  let body=paper(4,1,13,18) + line('M7 6 H11 M7 9 H12','#77A7CA',1.3);
  body += shape('M1 16 Q6 9 12 9 Q18 9 23 16 Q18 23 12 23 Q6 23 1 16 Z',ink) + circle(12,16,4,'#FFF') + circle(12,16,2.2,'url(#blue)');
  if(state==='hidden') body+=line('M4 22 L20 10','#FFF',3.4)+line('M4 22 L20 10','#617999',1.8);
  else {body+=circle(19,20,3.7,state==='visible'?'url(#teal)':'url(#gold)');body+=line(state==='visible'?'M17 20 L18.5 21.5 L21 18.5':'M17 20 H21','#FFF',1.4);}
  custom['hidden-files-'+state]=svg(body);
}
// Four image tiles identify a display mode; a single Image icon means a file.
custom.picture=svg([[2,2,'blue'],[13,2,'teal'],[2,13,'purple'],[13,13,'blue']].map(([x,y,color])=>rect(x,y,9,9,`url(#${color})`,1.8)+circle(x+2.6,y+2.7,1.1,'#FFF3BA')+shape(`M${x+1} ${y+8} L${x+4} ${y+4.5} L${x+6} ${y+6.5} L${x+8} ${y+4.8} V${y+8} Z`,'#FFFFFF')).join(''));

async function main() {
  const entries={};
  for(const [key,name] of Object.entries(official)) {
    let content, url;
    if(process.argv.includes('--offline')) content=fs.readFileSync(path.join(root,key+'.svg'));
    else {
      for(const size of [24,20,32,16,48]) {
        url=`https://raw.githubusercontent.com/microsoft/fluentui-system-icons/${commit}/assets/${encodeURIComponent(name)}/SVG/ic_fluent_${name.toLowerCase().replaceAll(' ','_')}_${size}_color.svg`;
        const response=await fetch(url); if(response.status===404)continue;
        if(!response.ok)throw Error(`${response.status}: ${url}`);
        content=Buffer.from(await response.arrayBuffer());break;
      }
      if(!content)throw Error('Missing Fluent Color icon: '+name);
    }
    fs.writeFileSync(path.join(root,key+'.svg'),content);
    entries[key]={kind:'Microsoft Fluent Color',name,commit};
  }
  for(const [key,content] of Object.entries(custom)) {
    fs.writeFileSync(path.join(root,key+'.svg'),content);
    entries[key]={kind:'File Compass original',style:'Fluent Color companion'};
  }
  for(const key of Object.keys(entries)) {
    await sharp(path.join(root,key+'.svg')).resize(renderSize,renderSize).png().toFile(path.join(root,key+'.png'));
    console.log(key);
  }
  fs.writeFileSync(path.join(root,'COLOR-SOURCE.json'),JSON.stringify({repository:'https://github.com/microsoft/fluentui-system-icons',catalog:'https://icones.js.org/collection/fluent-color',commit,license:'MIT',renderSize,icons:entries},null,2)+'\n');
  fs.writeFileSync(path.join(root,'SOURCE.json'),JSON.stringify({repository:'https://github.com/microsoft/fluentui-system-icons',commit,license:'MIT',renderSize,manifest:'COLOR-SOURCE.json'},null,2)+'\n');
  fs.writeFileSync(path.join(root,'TOOLBAR-SOURCE.json'),JSON.stringify({repository:'https://github.com/microsoft/fluentui-system-icons',commit,license:'MIT',manifest:'COLOR-SOURCE.json',note:'Current per-icon provenance is recorded in COLOR-SOURCE.json.'},null,2)+'\n');
}
main().catch(error=>{console.error(error);process.exitCode=1;});
