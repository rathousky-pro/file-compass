(async function(){
 const output=document.getElementById("output"), tabs=document.getElementById("tabs"), note=document.getElementById("note");
 document.addEventListener("click",e=>{if(e.target.closest("a"))e.preventDefault();});
 try {
  const filename=location.hash.slice(1);
  if(!/^document\.(docx|xlsx|xls|xlsm|xlsb|ods)$/.test(filename))throw Error();
  const response=await fetch(filename);if(!response.ok)throw Error();
  const bytes=await response.arrayBuffer();
  if(filename.endsWith(".docx")){
   await docx.renderAsync(bytes,output,null,{inWrapper:true,ignoreWidth:true,ignoreHeight:true,breakPages:true,renderAltChunks:false});
  }else{
   if(!XLSX || XLSX.version!=="0.20.3")throw Error("Spreadsheet preview requires the verified SheetJS 0.20.3 dependency. Update the preview dependencies before opening spreadsheets.");
   const book=XLSX.read(bytes,{type:"array",cellHTML:false,cellFormula:false,sheetRows:2001});
   function show(name,button){
    tabs.querySelectorAll("button").forEach(b=>b.classList.toggle("active",b===button));
    const sheet=book.Sheets[name],range=XLSX.utils.decode_range(sheet["!ref"]||"A1");
    const limited=range.e.r-range.s.r>=2000||range.e.c-range.s.c>=100;
    range.e.r=Math.min(range.e.r,range.s.r+1999);range.e.c=Math.min(range.e.c,range.s.c+99);
    if(![range.s.r,range.s.c,range.e.r,range.e.c].every(Number.isSafeInteger)||range.s.r<0||range.s.c<0)throw Error("Invalid worksheet range");
    const table=document.createElement("table"), body=document.createElement("tbody");table.appendChild(body);
    for(let r=range.s.r;r<=range.e.r;r++){
     const row=document.createElement("tr");body.appendChild(row);
     for(let c=range.s.c;c<=range.e.c;c++){
      const td=document.createElement("td"),address=XLSX.utils.encode_cell({r,c});row.appendChild(td);
      const cell=Object.prototype.hasOwnProperty.call(sheet,address)?sheet[address]:null;
      if(cell){const value=Object.prototype.hasOwnProperty.call(cell,"w")?cell.w:cell.v;td.textContent=["string","number","boolean"].includes(typeof value)?String(value):"";}
     }
    }
    output.replaceChildren(table);
    note.textContent=limited?"Náhled je omezen na 2 000 řádků a 100 sloupců.":"";
   }
   book.SheetNames.slice(0,200).forEach((name,i)=>{const b=document.createElement("button");b.textContent=name;b.onclick=()=>show(name,b);tabs.appendChild(b);if(i===0)show(name,b);});
  }
  window.chrome.webview.postMessage("ready");
 }catch(e){output.textContent="";window.chrome.webview.postMessage("error: "+String(e));}
})();
