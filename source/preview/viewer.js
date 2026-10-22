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
   const book=XLSX.read(bytes,{type:"array",cellHTML:false,cellFormula:false,sheetRows:2001});
   function show(name,button){
    tabs.querySelectorAll("button").forEach(b=>b.classList.toggle("active",b===button));
    const sheet=book.Sheets[name],range=XLSX.utils.decode_range(sheet["!ref"]||"A1");
    const limited=range.e.r-range.s.r>=2000||range.e.c-range.s.c>=100;
    range.e.r=Math.min(range.e.r,range.s.r+1999);range.e.c=Math.min(range.e.c,range.s.c+99);
    const preview=Object.assign({},sheet,{"!ref":XLSX.utils.encode_range(range)});
    output.innerHTML=XLSX.utils.sheet_to_html(preview,{editable:false});
    note.textContent=limited?"Náhled je omezen na 2 000 řádků a 100 sloupců.":"";
   }
   book.SheetNames.slice(0,200).forEach((name,i)=>{const b=document.createElement("button");b.textContent=name;b.onclick=()=>show(name,b);tabs.appendChild(b);if(i===0)show(name,b);});
  }
  window.chrome.webview.postMessage("ready");
 }catch(e){output.textContent="";window.chrome.webview.postMessage("error: "+String(e));}
})();
