using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CommanderCZ {
    internal sealed class DocumentPreview : Grid, IDisposable {
        internal const string InstallUrl = "https://developer.microsoft.com/microsoft-edge/webview2/#download-section";
        internal static bool ForceUnavailable; static bool loaderConfigured;
        internal static bool IsHtml(string path) {return new[]{".html",".htm",".xhtml",".shtml"}.Contains(Path.GetExtension(path).ToLowerInvariant());}
        internal static bool Supports(string path) { return IsHtml(path)||new[]{".pdf",".docx",".xlsx",".xls",".xlsm",".xlsb",".ods"}.Contains(Path.GetExtension(path).ToLowerInvariant()); }
        internal static string Assets { get { return AppPaths.Resource("preview"); } }
        internal static bool Available() {
            if(ForceUnavailable)return false;
            try { if(!loaderConfigured){CoreWebView2Environment.SetLoaderDllFolderPath(Assets);loaderConfigured=true;} return !String.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); } catch { return false; }
        }
        internal WebView2 view;
        bool disposed;
        readonly List<Stream> streams = new List<Stream>();
        internal string Error; internal static string Diagnostic;
        internal async Task<bool> ShowAsync(string path, CancellationToken token) {
            if(!Available()) { Error=L.Text("preview.webviewMissing");return false; }
            try {
                var file=new FileInfo(path);
                if(file.Length>64L*1024*1024) {Error=L.Text("preview.documentTooLarge");return false;}
                byte[] bytes=await Task.Run(()=>{
                    if(new[]{".docx",".xlsx",".xlsm",".xlsb",".ods"}.Contains(Path.GetExtension(path).ToLowerInvariant()))
                    using(var zipInput=File.OpenRead(path)) using(var zip=new System.IO.Compression.ZipArchive(zipInput,System.IO.Compression.ZipArchiveMode.Read)) {
                        long expanded=0;if(zip.Entries.Count>10000)throw new IOException();
                        foreach(var item in zip.Entries){token.ThrowIfCancellationRequested();expanded=checked(expanded+item.Length);if(expanded>128L*1024*1024)throw new IOException();}
                    }
                    return File.ReadAllBytes(path);
                },token);
                token.ThrowIfCancellationRequested(); if(disposed)return false;
                view=new WebView2();Children.Add(view);
                var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(AppPaths.ConfigDirectory,"cache","webview2"),new CoreWebView2EnvironmentOptions("--disable-background-networking"));
                if(disposed||token.IsCancellationRequested)return false;
                await view.EnsureCoreWebView2Async(environment);
                if(disposed||token.IsCancellationRequested)return false;
                var core=view.CoreWebView2;
                bool html=IsHtml(path);if(html)core.Settings.IsScriptEnabled=false;
                core.Settings.AreHostObjectsAllowed=false;core.Settings.AreDevToolsEnabled=false;
                core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
                core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.IsStatusBarEnabled=false;
                core.Settings.HiddenPdfToolbarItems=CoreWebView2PdfToolbarItems.Save|CoreWebView2PdfToolbarItems.SaveAs|CoreWebView2PdfToolbarItems.Print;
                string origin="https://compass-preview.invalid/";
                string id=Guid.NewGuid().ToString("N");
                string input=origin+id+"/document"+Path.GetExtension(path).ToLowerInvariant();
                string entry=origin+id+"/index.html";
                core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
                core.WebResourceRequested+=delegate(object sender,CoreWebView2WebResourceRequestedEventArgs e) {
                    string mime="text/plain"; Stream stream=null;
                    if(e.Request.Uri==input) {stream=new MemoryStream(bytes,false);mime=html?"text/html":Path.GetExtension(path).Equals(".pdf",StringComparison.OrdinalIgnoreCase)?"application/pdf":"application/octet-stream";}
                    else if(html) {stream=HtmlPreviewAssets.Open(path,origin+id+"/",e.Request.Uri,out mime);}
                    else if(e.Request.Uri.StartsWith(origin+id+"/",StringComparison.Ordinal)) {
                        string name=e.Request.Uri.Substring((origin+id+"/").Length);
                        if(new[]{"index.html","viewer.js","docx-preview.min.js","jszip.min.js","xlsx.full.min.js"}.Contains(name)) {
                            stream=new MemoryStream(File.ReadAllBytes(Path.Combine(Assets,name)),false);
                            mime=name.EndsWith(".html")?"text/html; charset=utf-8":"application/javascript";
                        }
                    }
                    if(stream==null){e.Response=environment.CreateWebResourceResponse(null,403,"Blocked","");return;}
                    streams.Add(stream);
                    if(html) {
                        e.Response=environment.CreateWebResourceResponse(stream,200,"OK","Content-Type: "+mime+"\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'none'; script-src 'none'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; frame-src 'none'; connect-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'");return;
                    }
                    e.Response=environment.CreateWebResourceResponse(stream,200,"OK","Content-Type: "+mime+"\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src data: blob:; font-src data: blob:; connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'none'");
                };
                core.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e) {if(e.Uri.Split('#')[0]!=entry&&e.Uri.Split('#')[0]!=input)e.Cancel=true;};
                core.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;};
                core.DownloadStarting+=delegate(object sender,CoreWebView2DownloadStartingEventArgs e){e.Cancel=true;};
                core.PermissionRequested+=delegate(object sender,CoreWebView2PermissionRequestedEventArgs e){e.State=CoreWebView2PermissionState.Deny;};
                var ready=new TaskCompletionSource<bool>();
                bool pdf=Path.GetExtension(path).Equals(".pdf",StringComparison.OrdinalIgnoreCase);
                core.WebMessageReceived+=delegate(object sender,CoreWebView2WebMessageReceivedEventArgs e) {
                    if(e.Source.Split('#')[0]!=entry)return;
                    string message=e.TryGetWebMessageAsString();
                    if(message=="ready")ready.TrySetResult(true);
                    else if(message.StartsWith("error")){Diagnostic=message;ready.TrySetResult(false);}
                };
                core.NavigationCompleted+=delegate(object sender,CoreWebView2NavigationCompletedEventArgs e){if(!e.IsSuccess)ready.TrySetResult(false);else if(pdf||html)ready.TrySetResult(true);};
                core.Navigate(pdf||html?input:entry+"#"+Path.GetFileName(input));
                // NavigationStarting compares the document URL without its fragment.
                using(token.Register(()=>ready.TrySetCanceled())) {
                    var completed=await Task.WhenAny(ready.Task,Task.Delay(30000,token));
                    token.ThrowIfCancellationRequested();
                    bool success=completed==ready.Task&&await ready.Task;
                    if(!success)Error=L.Text("preview.documentFailed");
                    return success&&!disposed;
                }
            } catch(OperationCanceledException){return false;}
              catch(Exception ex){Diagnostic=ex.ToString();Error=L.Text("preview.documentFailed");return false;}
        }
        public void Dispose(){disposed=true;if(view!=null){view.Dispose();view=null;}Children.Clear();foreach(var stream in streams)stream.Dispose();streams.Clear();}
    }
}
