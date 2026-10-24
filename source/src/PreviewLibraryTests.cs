#if APP_TESTS
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
namespace FileCompass {
 internal static class PreviewLibraryTests {
  static void Wait(Task task){var end=DateTime.UtcNow.AddSeconds(50);while(!task.IsCompleted&&DateTime.UtcNow<end){Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));Thread.Sleep(10);}if(!task.IsCompleted)throw new Exception("Preview timed out");task.GetAwaiter().GetResult();}
  internal static int Run(string root){Directory.CreateDirectory(root);var checks=new List<string>();Window window=null;DocumentPreview preview=null;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add(label);File.WriteAllLines(Path.Combine(root,"result.txt"),checks);};try{
   SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));FluentTheme.Ensure();string[] names={"index.html","viewer.js","docx-preview.min.js","jszip.min.js","xlsx.full.min.js"};foreach(string name in names){using(var stream=PreviewAssets.Open(name))check(stream!=null&&stream.Length>0,"Embedded resource loads: "+name);check(!File.Exists(Path.Combine(DocumentPreview.Assets,name)),"No loose asset required: "+name);}check(PreviewAssets.Open("../viewer.js")==null&&PreviewAssets.Open("https://example.com/a.js")==null,"Resource paths are explicitly allowlisted");check(PluginHashes.Expected.ContainsKey("preview\\preview.dll")&&!PluginHashes.Expected.Keys.Any(x=>x.StartsWith("preview\\")&&(x.EndsWith(".js")||x.EndsWith(".html"))),"Compiled integrity manifest covers the library instead of loose scripts");
   var assembly=Assembly.LoadFile(Path.Combine(DocumentPreview.Assets,"preview.dll"));check(new[]{"docx-preview-LICENSE.txt","JSZip-LICENSE.txt","SheetJS-LICENSE.txt","manifest.json"}.All(x=>assembly.GetManifestResourceNames().Contains("FileCompass.Preview."+x)),"Library retains licenses and source component manifest");
   string[] fixtures=DocumentPreviewTests.CreateFixtures(root);check(DocumentPreview.Available(),"System WebView2 runtime is available");
   foreach(string file in fixtures){preview=new DocumentPreview();window=new Window{Content=preview,Width=900,Height=650,ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000};window.Show();var task=preview.ShowAsync(file,CancellationToken.None);Wait(task);check(task.Result,"Direct rendered preview: "+Path.GetExtension(file)+" "+DocumentPreview.Diagnostic);if(!file.EndsWith(".pdf")){string expression=file.EndsWith(".docx")?"document.querySelector('#output').textContent.includes('Compass formatted document')":"document.querySelector('#output').textContent.includes('Compass table')";var script=preview.view.ExecuteScriptAsync(expression);Wait(script);check(script.Result=="true","Document content rendered from embedded scripts: "+Path.GetExtension(file));if(file.EndsWith(".xlsx")){script=preview.view.ExecuteScriptAsync("document.querySelectorAll('#tabs button')[1].click();document.querySelector('#output').textContent.includes('Second sheet content')");Wait(script);check(script.Result=="true","Spreadsheet sheet switching works from embedded scripts");}script=preview.view.ExecuteScriptAsync("window.blocked=0;fetch('https://example.com/').then(r=>window.blocked=r.status).catch(()=>window.blocked=403)");Wait(script);for(int attempt=0;attempt<200;attempt++){script=preview.view.ExecuteScriptAsync("window.blocked");Wait(script);if(script.Result!="0")break;Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));Thread.Sleep(10);}check(script.Result=="403","External requests remain blocked: "+Path.GetExtension(file));}
    preview.Dispose();preview=null;window.Close();window=null;
   }File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(preview!=null)preview.Dispose();if(window!=null)window.Close();}}
 }
}
#endif
