using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
namespace FileCompass {
 internal static class HtmlPreviewTests {
  internal static int Run(string root) {
   Directory.CreateDirectory(root);var passed=new List<string>();QuickPreviewPane pane=null;Window host=null;
   Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);passed.Add(label);};
   try {
    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
    string page=Path.Combine(root,"page.html");
    File.WriteAllText(Path.Combine(root,"style.css"),"h1{color:rgb(12, 100, 180)}");
    File.WriteAllText(page,"<!doctype html><html><head><meta charset='utf-8'><link rel='stylesheet' href='style.css'></head><body><h1>Compass HTML preview</h1><p>Česká stránka</p><script>document.body.dataset.executed='yes';</script><img src='https://example.com/tracker.png'><a href='https://example.com/'>External link</a></body></html>");
    pane=new QuickPreviewPane(delegate{});host=new Window{Content=pane,Width=1000,Height=700,ShowInTaskbar=false,ShowActivated=false};host.Show();
    pane.ShowFile(page);NavigationTests.Wait(pane.LoadingTask);NavigationTests.Wait(pane.MetadataLoadingTask);NavigationTests.Wait(pane.DocumentLoadingTask);
    var browser=AdvancedToolsTests.Visual<DocumentPreview>(pane).Single();
    check(browser.Visibility==Visibility.Visible,"HTML opens as a rendered page by default");
    var script=browser.view.ExecuteScriptAsync("document.querySelector('h1').textContent");NavigationTests.Wait(script);check(script.Result.Contains("Compass HTML preview"),"Page content renders");
    script=browser.view.ExecuteScriptAsync("getComputedStyle(document.querySelector('h1')).color");NavigationTests.Wait(script);check(script.Result.Contains("12, 100, 180"),"Local relative stylesheet is applied");
    script=browser.view.ExecuteScriptAsync("document.body.dataset.executed||'blocked'");NavigationTests.Wait(script);check(script.Result=="\"blocked\"","Embedded page scripts do not execute");
    script=browser.view.ExecuteScriptAsync("document.querySelector('img').naturalWidth");NavigationTests.Wait(script);check(script.Result=="0","Remote images are blocked");
    using(var image=File.Create(Path.Combine(root,"page.png")))NavigationTests.Wait(browser.view.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,image));
    var code=AdvancedToolsTests.Visual<RadioButton>(pane).Single(x=>Convert.ToString(x.Content)==L.Text("preview.htmlCode"));code.IsChecked=true;
    check(browser.Visibility==Visibility.Hidden,"Source switch hides the page");
    check(AdvancedToolsTests.Visual<CheckBox>(pane).Single(x=>Convert.ToString(x.Content)==L.Text("preview.formatted")).IsChecked==true,"HTML source switch leaves other document formatting preferences unchanged");
    var editor=AdvancedToolsTests.Visual<TextEditor>(pane).Single(x=>x.IsVisible);check(editor.Text.Contains("<h1>Compass")&&editor.SyntaxHighlighting!=null,"Source switch shows highlighted original HTML");
    pane.SetViewMode(PreviewMode.Text);NavigationTests.Wait(pane.LoadingTask);
    var format=AdvancedToolsTests.Visual<CheckBox>(pane).Single(x=>Convert.ToString(x.Content)==L.Text("preview.formatted"));
    check(format.IsEnabled&&AdvancedToolsTests.Visual<TextEditor>(pane).Any(x=>x.IsVisible&&x.SyntaxHighlighting!=null&&x.Text.Contains("<h1>Compass")),"Text mode retains HTML syntax highlighting and enabled formatting");
    pane.SetViewMode(PreviewMode.Hex);NavigationTests.Wait(pane.LoadingTask);
    check(!AdvancedToolsTests.Visual<TextEditor>(pane).Any(x=>x.IsVisible),"Hex mode remains a raw byte view");
    pane.SetViewMode(PreviewMode.Text);NavigationTests.Wait(pane.LoadingTask);
    string cs=Path.Combine(root,"sample.cs");File.WriteAllText(cs,"public class Compass { // comment\n string name = \"File Compass\"; }");pane.ShowFile(cs);NavigationTests.Wait(pane.LoadingTask);NavigationTests.Wait(pane.MetadataLoadingTask);
    check(AdvancedToolsTests.Visual<TextEditor>(pane).Any(x=>x.IsVisible&&x.SyntaxHighlighting!=null),"Text mode highlights C sharp source");
    format.IsChecked=false;check(!AdvancedToolsTests.Visual<TextEditor>(pane).Any(x=>x.IsVisible),"Formatting can be disabled for plain text");
    format.IsChecked=true;check(AdvancedToolsTests.Visual<TextEditor>(pane).Any(x=>x.IsVisible),"Formatting can be enabled again");
    pane.ShowFile(page);NavigationTests.Wait(pane.LoadingTask);NavigationTests.Wait(pane.MetadataLoadingTask);pane.SetViewMode(PreviewMode.Auto);NavigationTests.Wait(pane.LoadingTask);NavigationTests.Wait(pane.DocumentLoadingTask);
    browser=AdvancedToolsTests.Visual<DocumentPreview>(pane).Single();
    var web=AdvancedToolsTests.Visual<RadioButton>(pane).Single(x=>Convert.ToString(x.Content)==L.Text("preview.htmlPage"));web.IsChecked=true;check(browser.Visibility==Visibility.Visible,"Page switch restores the already loaded page");
    string origin="https://compass-preview.invalid/test/";string mime;
    using(var asset=HtmlPreviewAssets.Open(page,origin,origin+"style.css",out mime))check(asset!=null&&mime=="text/css","Local asset resolver allows only supported resources");
    check(HtmlPreviewAssets.Open(page,origin,"https://example.com/style.css",out mime)==null,"Asset resolver rejects remote URLs");
    check(HtmlPreviewAssets.Open(page,origin,origin+"%2e%2e/%2e%2e/private.css",out mime)==null,"Asset resolver rejects path traversal");
    File.WriteAllText(Path.Combine(root,"secret.txt"),"not a website resource");
    check(HtmlPreviewAssets.Open(page,origin,origin+"secret.txt",out mime)==null,"Asset resolver does not expose unrelated files");
    File.WriteAllText(Path.Combine(root,"script.js"),"alert(1)");check(HtmlPreviewAssets.Open(page,origin,origin+"script.js",out mime)==null,"Asset resolver does not serve page scripts");
    foreach(string ext in new[]{".htm",".xhtml",".shtml"})check(DocumentPreview.Supports("page"+ext)&&RichFilePreview.Supports("page"+ext),"HTML family supports both page and code: "+ext);
    DocumentPreview.ForceUnavailable=true;pane.ShowFile(page);NavigationTests.Wait(pane.LoadingTask);NavigationTests.Wait(pane.MetadataLoadingTask);NavigationTests.Wait(pane.DocumentLoadingTask);
    check(AdvancedToolsTests.Visual<TextEditor>(pane).Any(x=>x.IsVisible&&x.Text.Contains("<h1>Compass")),"Missing WebView2 falls back to source code");
    check(AdvancedToolsTests.Visual<RadioButton>(pane).Single(x=>Convert.ToString(x.Content)==L.Text("preview.htmlCode")).IsChecked==true,"Fallback correctly selects Source code");
    check(AdvancedToolsTests.Visual<Button>(pane).Any(x=>x.IsVisible&&Convert.ToString(x.Content)==L.Text("preview.installWebview")),"Fallback retains the official runtime installation link");
    File.WriteAllLines(Path.Combine(root,"result.txt"),passed.Concat(new[]{"PASS: "+passed.Count}));return 0;
   }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),passed.Concat(new[]{"FAIL: "+ex}));return 1;}
   finally{DocumentPreview.ForceUnavailable=false;if(pane!=null)pane.DisposePreview();if(host!=null)host.Close();}
  }
 }
}
