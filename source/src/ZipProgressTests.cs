using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
namespace CommanderCZ {
    internal static class ZipProgressTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root);var checks=new List<string>();Window owner=null;
            Action<bool,string> check=(valid,label)=>{if(!valid)throw new Exception(label);checks.Add(label);File.AppendAllText(Path.Combine(root,"zip-progress-log.txt"),label+Environment.NewLine);};
            try {
                string folder=Directory.CreateDirectory(Path.Combine(root,"Fotografie žluťoučké")).FullName;
                Directory.CreateDirectory(Path.Combine(folder,"prázdná"));Directory.CreateDirectory(Path.Combine(folder,"vnořená"));
                var buffer=new byte[1048576];new Random(42).NextBytes(buffer);
                string large=Path.Combine(folder,"vnořená","velký.bin");using(var output=File.Create(large))for(int i=0;i<24;i++)output.Write(buffer,0,buffer.Length);
                File.WriteAllText(Path.Combine(folder,"nulový.txt"),"");
                foreach(string password in new[]{""}) {
                    string archive=Path.Combine(root,password.Length==0?"plain.zip":"encrypted.zip");var updates=new List<ArchiveCompressionProgress>();
                    ArchiveEngine.Create(archive,password.Length==0?new[]{folder}:new[]{large},ArchiveSettings.Load(),CancellationToken.None,text=>{ArchiveCompressionProgress update;if(ArchiveCompressionProgress.TryParse(text,out update))updates.Add(update);},new ArchivePackOptions {Password=password,Level=5});
                    check(updates.Count(x=>x.Processed>0 && x.Percent>0 && x.Percent<99)>=2,"Folder ZIP reports multiple intermediate byte updates "+(password.Length==0?"plain":"encrypted"));
                    check(updates.Any(x=>x.Processed>0 && !String.IsNullOrEmpty(x.CurrentFile) && x.CurrentFile.Contains("velký.bin")),"ZIP reports actual current Unicode member");
                    var doc=ArchiveEngine.Open(archive,password,CancellationToken.None);ArchiveEngine.Test(doc,CancellationToken.None);
                    if(password.Length==0) check(doc.Items.Any(x=>x.Directory && x.Path.TrimEnd('/').EndsWith("prázdná")),"ZIP preserves empty directories");
                    if(password.Length==0) check(doc.Items.Any(x=>!x.Directory && x.Bytes==0),"ZIP preserves empty files");
                    if(password.Length>0)check(doc.Items.Any(x=>x.Encrypted),"ZIP retains encryption");
                    using(var extracted=new ArchiveTemp()){ArchiveEngine.Extract(doc,doc.Items.Select(x=>x.Path),extracted.Path,CancellationToken.None);var file=Directory.GetFiles(extracted.Path,"velký.bin",SearchOption.AllDirectories).Single();check(FileParts.Hash(file,CancellationToken.None)==FileParts.Hash(large,CancellationToken.None),"ZIP extracts exact original bytes");}
                }
                using(var cancel=new CancellationTokenSource()){
                    bool stopped=false;string target=Path.Combine(root,"cancel.zip");
                    try{ArchiveEngine.Create(target,new[]{folder},ArchiveSettings.Load(),cancel.Token,text=>{ArchiveCompressionProgress update;if(ArchiveCompressionProgress.TryParse(text,out update)&&update.Processed>0)cancel.Cancel();});}catch(OperationCanceledException){stopped=true;}
                    check(stopped&&!File.Exists(target),"Cancellation during ZIP byte progress leaves no published archive");
                }
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                FluentTheme.Ensure();owner=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){ShowInTaskbar=false,Width=1000,Height=700,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};owner.Show();
                using(var cancel=new CancellationTokenSource()){
                    var dialog=new ArchiveProgressDialog(owner,"Vytváření archivu · Obrázky.zip",cancel){Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};dialog.Show();
                    dialog.UpdatePreparation(new ArchivePreparationProgress{Path=@"D:\Fotografie\Dovolená\IMG_2026.jpg",Items=1248,Bytes=780*1048576L});dialog.UpdateLayout();NextStageTests.Draw(dialog,Path.Combine(root,"priprava.png"));
                    dialog.Update(L.Text("archive.compressingStatus"),.42,true);dialog.UpdateCurrentFile(@"Fotografie\Dovolená\IMG_2026.jpg");dialog.UpdateCompression(new ArchiveCompressionProgress{Original=100*1048576L,Processed=42*1048576L,Packed=32*1048576L}.Summary());dialog.UpdateLayout();NextStageTests.Draw(dialog,Path.Combine(root,"komprimace.png"));dialog.Finish();
                }
                File.WriteAllLines(Path.Combine(root,"zip-progress-result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"zip-progress-result.txt"),String.Join("\n",checks)+"\nFAIL: "+ex);return 1;}
            finally{if(owner!=null)owner.Close();}
        }
    }
}




