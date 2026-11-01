(()=>{
 const articles=[...document.querySelectorAll('article[data-topic]')],search=document.querySelector('#help-search'),count=document.querySelector('#result-count'),empty=document.querySelector('#no-results');
 if(!search)return;
 const cs=document.documentElement.lang==='cs-CZ',normalize=text=>text.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase();
 const index=articles.map(article=>({article,text:normalize(article.innerText)}));
 function show(){const terms=normalize(search.value).trim().split(/\s+/).filter(Boolean),id=location.hash.slice(1)||'home';let matches=0;for(const item of index){const visible=terms.length?terms.every(term=>item.text.includes(term)):item.article.id===id;item.article.hidden=!visible;if(visible)matches++;}if(!matches&&!terms.length){articles.find(x=>x.id==='home').hidden=false;matches=1;}empty.hidden=matches>0;count.textContent=terms.length?(cs?'Nalezená témata: ':'Topics found: ')+matches:'';document.querySelectorAll('[data-topic-link]').forEach(link=>{if(link.hash==='#'+id&&!terms.length)link.setAttribute('aria-current','page');else link.removeAttribute('aria-current');});for(const link of document.querySelectorAll('[data-language]'))link.hash=id;}
 addEventListener('hashchange',()=>{search.value='';show();document.querySelector('#content').scrollIntoView({block:'start'});});search.addEventListener('input',show);
 document.addEventListener('click',event=>{const link=event.target.closest('[data-enlarge]');if(!link)return;const dialog=document.querySelector('#zoom');if(!dialog.showModal)return;event.preventDefault();const image=dialog.querySelector('img');image.src=link.href;image.alt=link.querySelector('img').alt;dialog.showModal();});
 document.addEventListener('keydown',event=>{if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='f'){event.preventDefault();search.focus();search.select();}else if(event.key==='Escape'&&search.value){search.value='';show();}});
 show();
})();
