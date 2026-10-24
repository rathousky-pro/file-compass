using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Diagnostics;
using System.Net.Sockets;
namespace FileCompass {
 internal static class PreparedPreviewTests {
  static void Wait(Task task){NavigationTests.Wait(task);}
  static void Until(Func<bool> condition){var end=DateTime.UtcNow.AddSeconds(10);while(!condition()&&DateTime.UtcNow<end){Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background,new Action(delegate{}));Thread.Sleep(20);}if(!condition())throw new Exception("Timeout");}
  static void Loaded(QuickPreviewPane pane){Wait(pane.LoadingTask);Wait(pane.MetadataLoadingTask);Wait(pane.DocumentLoadingTask);}
  internal static int Run(string root){
   Directory.CreateDirectory(root);var passed=new List<string>();QuickPreviewPane pane=null;Window window=null;Process ftp=null;RemoteConnection connection=null;
   Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);passed.Add(label);File.AppendAllText(Path.Combine(root,"progress.txt"),label+Environment.NewLine);};
   try{
    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
    string files=Path.Combine(root,"files");Directory.CreateDirectory(files);File.WriteAllText(Path.Combine(files,"hello.txt"),"Archive and FTP text preview");
    var pdf=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();var font=pdf.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);pdf.AddPage(UglyToad.PdfPig.Content.PageSize.A4).AddText("Temporary PDF preview",16,new UglyToad.PdfPig.Core.PdfPoint(40,700),font);File.WriteAllBytes(Path.Combine(files,"document.pdf"),pdf.Build());
    string zip=Path.Combine(root,"preview.zip");ArchiveEngine.Create(zip,new[]{Path.Combine(files,"hello.txt"),Path.Combine(files,"document.pdf")},ArchiveSettings.Load(),CancellationToken.None);
    var doc=ArchiveEngine.Open(zip);string archiveHash=ArchiveEngineTestHash(zip);
    pane=new QuickPreviewPane(delegate{});window=new Window{Content=pane,Width=950,Height=650,ShowInTaskbar=false,ShowActivated=false};window.Show();
    pane.ShowArchiveFile(doc,"/hello.txt");Loaded(pane);
    check(AdvancedToolsTests.Visual<TextBox>(pane).Any(x=>x.Text.Contains("Archive and FTP text preview")),"Text in an archive uses the shared preview");
    string temp=pane.PreviewTempPath;check(temp!=null&&File.Exists(Path.Combine(temp,"hello.txt")),"Only the selected archive member is extracted to an isolated temporary directory");
    check(!Directory.GetFiles(temp,"*",SearchOption.AllDirectories).Any(x=>x.EndsWith(".pdf")),"Other archive members are not extracted");
    pane.ShowArchiveFile(doc,"/document.pdf");Loaded(pane);Until(()=>!Directory.Exists(temp));
    check(AdvancedToolsTests.Visual<DocumentPreview>(pane).Any(x=>x.Visibility==Visibility.Visible),"Archived PDF uses the actual formatted viewer");
    check(pane.Metadata!=null&&pane.Metadata.Rows.Any(x=>x.Key=="pages"),"Archived PDF includes normal metadata");
    temp=pane.PreviewTempPath;pane.ShowMessage("Closed");Until(()=>!Directory.Exists(temp));check(true,"Closing archive preview removes its temporary files");
    pane.ShowArchiveFile(doc,"/document.pdf");pane.ShowMessage("Cancelled");Wait(pane.LoadingTask);check(pane.PreviewTempPath==null,"A cancelled archive request cannot publish a stale preview");
    check(ArchiveEngineTestHash(zip)==archiveHash,"Preview leaves the source archive unchanged");
    var fake=new ArchiveDocument{FilePath=doc.FilePath,Stamp=doc.Stamp,Type=doc.Type,Items=new List<ArchiveItem>{new ArchiveItem{Path="large.pdf",Bytes=65L*1024*1024}}};
    pane.ShowArchiveFile(fake,"/large.pdf");Wait(pane.LoadingTask);
    check(AdvancedToolsTests.Visual<TextBlock>(pane).Any(x=>x.Text.Contains(L.Text("preview.preparedSizeLimit"))),"Archive size limit is checked before extraction");
    pane.ShowArchiveFile(doc,"/../escape.txt");Wait(pane.LoadingTask);check(pane.PreviewTempPath==null,"Unsafe archive member cannot escape the temporary directory");
    string large=Path.Combine(files,"large.pdf");using(var f=File.Create(large))f.SetLength(65L*1024*1024);
    int port=CloudRuntime.FreePort();string config=Path.Combine(root,"empty-rclone.conf");File.WriteAllText(config,"");
    var info=new ProcessStartInfo(Path.Combine(AppPaths.Resource("plugins"),"cloud","rclone.exe"),"serve ftp "+CloudStore.Quote(files)+" --config "+CloudStore.Quote(config)+" --addr 127.0.0.1:"+port+" --user tester --pass test-password --vfs-cache-mode off"){UseShellExecute=false,CreateNoWindow=true};
    ftp=Process.Start(info);
    Until(delegate{try{using(var client=new TcpClient()){client.Connect("127.0.0.1",port);return true;}}catch{return false;}});
    connection=new RemoteConnection(new NetworkProfile{Name="Preview FTP test",Protocol=NetworkProtocol.FTP,Host="127.0.0.1",Port=port,UserName="tester",Path="/"});
    connection.Open(connection.Options("test-password"));
    pane.ShowRemoteFile("/document.pdf",connection);Loaded(pane);
    check(AdvancedToolsTests.Visual<DocumentPreview>(pane).Any(x=>x.Visibility==Visibility.Visible),"Real FTP download uses the formatted PDF preview");
    check(pane.Metadata!=null&&pane.Metadata.Rows.Any(x=>x.Key=="pages"),"FTP working copy supplies document metadata");
    temp=pane.PreviewTempPath;check(temp!=null&&File.Exists(Path.Combine(temp,"document.pdf")),"FTP preview stores one temporary working copy");
    pane.ShowRemoteFile("/hello.txt",connection);Loaded(pane);Until(()=>!Directory.Exists(temp));
    check(AdvancedToolsTests.Visual<TextBox>(pane).Any(x=>x.Text.Contains("Archive and FTP text preview")),"FTP text keeps the bounded range-based preview");
    check(pane.PreviewTempPath==null,"FTP text preview does not download a full working copy");

    pane.ShowRemoteFile("/large.pdf",connection);Wait(pane.LoadingTask);check(AdvancedToolsTests.Visual<TextBlock>(pane).Any(x=>x.Text.Contains(L.Text("preview.preparedSizeLimit"))),"FTP rejects a large document before downloading");
    pane.ShowRemoteFile("/document.pdf",connection);pane.ShowMessage("Cancelled");Wait(pane.LoadingTask);check(pane.PreviewTempPath==null,"Cancelling FTP preview prevents stale working copies");
    check(File.ReadAllText(Path.Combine(files,"hello.txt"))=="Archive and FTP text preview","Preview does not modify server files");
    File.WriteAllLines(Path.Combine(root,"result.txt"),passed.Concat(new[]{"PASS: "+passed.Count}));return 0;
   }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),passed.Concat(new[]{"FAIL: "+ex}));return 1;}
   finally{if(pane!=null)pane.DisposePreview();if(window!=null)window.Close();if(connection!=null)connection.Dispose();if(ftp!=null){try{if(!ftp.HasExited)ftp.Kill();}catch{}ftp.Dispose();}}
  }
  static string ArchiveEngineTestHash(string path){using(var f=File.OpenRead(path))using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(f));}
 }
}
