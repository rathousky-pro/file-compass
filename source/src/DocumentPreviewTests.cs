using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace CommanderCZ {
 internal static class DocumentPreviewTests {
  static void Package(string path,params string[] entries) {using(var f=File.Create(path))using(var zip=new ZipArchive(f,ZipArchiveMode.Create))for(int i=0;i<entries.Length;i+=2)using(var w=new StreamWriter(zip.CreateEntry(entries[i]).Open()))w.Write(entries[i+1]);}
  internal static int Run(string root) {
   Directory.CreateDirectory(root);var checks=new List<string>();Window window=null;QuickPreviewPane pane=null;
   Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add(label);};
   try {
    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
    string docx=Path.Combine(root,"Preview.docx"),xlsx=Path.Combine(root,"Preview.xlsx"),pdf=Path.Combine(root,"Preview.pdf");
    const string rel="http://schemas.openxmlformats.org/package/2006/relationships";
    Package(docx,"[Content_Types].xml","<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Default Extension='rels' ContentType='application/vnd.openxmlformats-package.relationships+xml'/><Override PartName='/word/document.xml' ContentType='application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml'/></Types>","_rels/.rels","<Relationships xmlns='"+rel+"'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument' Target='word/document.xml'/></Relationships>","word/document.xml","<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:rPr><w:b/><w:sz w:val='36'/></w:rPr><w:t>Compass formatted document</w:t></w:r></w:p><w:p><w:r><w:t>Local document preview.</w:t></w:r></w:p><w:sectPr><w:pgSz w:w='11906' w:h='16838'/></w:sectPr></w:body></w:document>");
    Package(xlsx,"[Content_Types].xml","<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Override PartName='/xl/workbook.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml'/></Types>","xl/workbook.xml","<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='First' sheetId='1' r:id='rId1'/><sheet name='Second' sheetId='2' r:id='rId2'/></sheets></workbook>","xl/_rels/workbook.xml.rels","<Relationships xmlns='"+rel+"'><Relationship Id='rId1' Target='worksheets/sheet1.xml' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet'/><Relationship Id='rId2' Target='worksheets/sheet2.xml' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet'/></Relationships>","xl/worksheets/sheet1.xml","<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><dimension ref='A1:B2'/><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>Compass table</t></is></c><c r='B1'><v>42</v></c></row></sheetData></worksheet>","xl/worksheets/sheet2.xml","<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>Second sheet content</t></is></c></row></sheetData></worksheet>");
    var builder=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();var font=builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4).AddText("Compass PDF preview",22,new UglyToad.PdfPig.Core.PdfPoint(40,700),font);File.WriteAllBytes(pdf,builder.Build());
    pane=new QuickPreviewPane(delegate{});window=new Window{Content=pane,Width=1000,Height=700,ShowActivated=false,ShowInTaskbar=false};window.Show();
    DocumentPreview.ForceUnavailable=true;pane.ShowFile(docx);
    check(pane.WaitingForDocument&&!AdvancedToolsTests.Visual<TextBox>(pane).Any(x=>x.IsVisible&&x.Text.Length>0),"Missing-runtime fallback waits for the completed text excerpt");
    NavigationTests.Wait(pane.LoadingTask);NavigationTests.Wait(pane.MetadataLoadingTask);
    check(AdvancedToolsTests.Visual<TextBlock>(pane).Any(x=>x.Text==L.Text("preview.webviewMissing")),"Missing runtime shows explanatory notice");
    check(AdvancedToolsTests.Visual<Button>(pane).Any(x=>Convert.ToString(x.Content)==L.Text("preview.installWebview")&&x.IsVisible),"Official installation link is visible");
    check(AdvancedToolsTests.Visual<TextBox>(pane).Any(x=>x.Text.Contains("Compass formatted document")),"Existing text excerpt remains available without runtime");
    NextStageTests.Draw(window,Path.Combine(root,"fallback.png"));
    DocumentPreview.ForceUnavailable=false;
    check(DocumentPreview.Available(),"System WebView2 runtime is available for rendering tests");
    foreach(string path in new[]{docx,xlsx,pdf}) {
     bool flashed=false;var observer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1)};
     observer.Tick+=delegate{if(pane.WaitingForDocument&&AdvancedToolsTests.Visual<TextBox>(pane).Any(x=>x.IsVisible&&x.Text.Length>0))flashed=true;};
     observer.Start();
     try{pane.ShowFile(path);check(pane.WaitingForDocument,"Document starts with loading placeholder: "+Path.GetExtension(path));NavigationTests.Wait(pane.LoadingTask);Wait(pane.DocumentLoadingTask);NavigationTests.Wait(pane.MetadataLoadingTask);}
     finally{observer.Stop();}
     check(!flashed,"No intermediate hexadecimal or text preview: "+Path.GetExtension(path));
     var rendered=AdvancedToolsTests.Visual<DocumentPreview>(pane).SingleOrDefault();
     check(rendered!=null&&rendered.Visibility==Visibility.Visible,"Formatted preview visible: "+Path.GetExtension(path));
     check(!AdvancedToolsTests.Visual<TextBlock>(pane).Any(x=>x.IsVisible&&x.Text==L.Text("preview.loadingStatus")),"Ready document has no stale loading status: "+Path.GetExtension(path));
     if(path==docx) {
      var script=rendered.view.ExecuteScriptAsync("document.querySelector('#output').textContent");Wait(script);check(script.Result.Contains("Compass formatted document"),"DOCX content rendered");
      var toggle=AdvancedToolsTests.Visual<CheckBox>(pane).Single(x=>Convert.ToString(x.Content)==L.Text("preview.formatted"));
      toggle.IsChecked=false;check(rendered.Visibility==Visibility.Hidden&&AdvancedToolsTests.Visual<TextBox>(pane).Any(x=>x.IsVisible&&x.Text.Contains("Compass formatted document")),"Formatted toggle restores the document text excerpt");
      toggle.IsChecked=true;check(rendered.Visibility==Visibility.Visible,"Formatted toggle restores visual document");
      script=rendered.view.ExecuteScriptAsync("getComputedStyle(document.querySelector('#output span')).fontWeight");Wait(script);check(script.Result.Contains("700"),"DOCX bold formatting retained");
      script=rendered.view.ExecuteScriptAsync("window.blocked=0;fetch('https://example.com/').then(r=>window.blocked=r.status).catch(()=>window.blocked=403)");Wait(script); for(int attempt=0;attempt<100;attempt++){script=rendered.view.ExecuteScriptAsync("window.blocked");Wait(script);if(script.Result!="0")break;Thread.Sleep(10);} check(script.Result=="403","External network requests are blocked");
     }
     if(path==xlsx) {
      var script=rendered.view.ExecuteScriptAsync("document.querySelectorAll('#tabs button').length");Wait(script);check(script.Result=="2","Excel sheet tabs rendered");
      script=rendered.view.ExecuteScriptAsync("document.querySelectorAll('#tabs button')[1].click();document.querySelector('#output').textContent");Wait(script);check(script.Result.Contains("Second sheet content"),"Excel sheet switching works");
     }
     using(var image=File.Create(Path.Combine(root,Path.GetExtension(path).TrimStart('.')+".png"))) {var capture=rendered.view.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,image);Wait(capture);}
     using(File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}check(true,"Preview releases source file: "+Path.GetExtension(path));
    }
    string huge=Path.Combine(root,"large.pdf");using(var large=File.Create(huge))large.SetLength(64L*1024*1024+1);
    pane.ShowFile(huge);Wait(pane.DocumentLoadingTask);check(AdvancedToolsTests.Visual<TextBlock>(pane).Any(x=>x.Text==L.Text("preview.documentTooLarge")),"Oversized document falls back before loading the browser");
    pane.ShowFile(docx);pane.ShowFile(xlsx);pane.ShowMessage("Cancelled");for(int i=0;i<40;i++){Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));Thread.Sleep(15);}
    check(!AdvancedToolsTests.Visual<DocumentPreview>(pane).Any(),"Rapid changes cancel and dispose the old document preview");
    string broken=Path.Combine(root,"broken.docx");File.WriteAllText(broken,"invalid");
    pane.ShowFile(broken);Wait(pane.DocumentLoadingTask);check(AdvancedToolsTests.Visual<TextBlock>(pane).Any(x=>x.Text==L.Text("preview.documentFailed")),"Damaged document falls back with a readable notice");
    File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count}));return 0;
   }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}
   finally{DocumentPreview.ForceUnavailable=false;if(pane!=null)pane.DisposePreview();if(window!=null)window.Close();}
  }
  static void Wait(Task task){var end=DateTime.UtcNow.AddSeconds(50);while(!task.IsCompleted&&DateTime.UtcNow<end){Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));Thread.Sleep(15);}if(!task.IsCompleted)throw new Exception("Preview timeout");task.GetAwaiter().GetResult();}
 }
}
