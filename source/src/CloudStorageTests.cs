using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
namespace FileCompass {
    internal static class CloudStorageTests {
        internal static int Run(string root){
            Directory.CreateDirectory(root);var checks=new List<string>();string previous=CloudStore.TestRoot,previousOAuth=CloudApplicationOAuth.TestPath;
            Action<bool,string> check=(valid,name)=>{if(!valid)throw new Exception(name);checks.Add(name);File.AppendAllText(Path.Combine(root,"progress.txt"),name+Environment.NewLine);};
            MainWindow main=null;
            try{
                CloudApplicationOAuth.TestPath=Path.Combine(root,"publisher-google.json");File.WriteAllText(CloudApplicationOAuth.TestPath,"{\"installed\":{\"client_id\":\"12345-filecompasstest.apps.googleusercontent.com\",\"client_secret\":\"test-publisher-secret\"}}");
                CloudStore.TestRoot=Path.Combine(root,"cloud");var a=new CloudAccount{Name="Test cloud",Provider="local"};
                CloudStore.Initialize(a,CancellationToken.None).GetAwaiter().GetResult();
                check(File.ReadLines(CloudStore.Config(a)).Take(4).Any(x=>x.Trim()=="RCLONE_ENCRYPT_V0:"),"rclone configuration is encrypted before the first account token is stored");
                check(CloudStore.Key==CloudStore.Key&&File.ReadAllBytes(CloudStore.KeyFile).Length>32,"Configuration key survives DPAPI round trip");
                string files=Path.Combine(root,"remote-files");Directory.CreateDirectory(files);File.WriteAllText(Path.Combine(files,"český soubor.txt"),"Cloud round trip ✓",Encoding.UTF8);Directory.CreateDirectory(Path.Combine(files,"empty"));
                string marker="secret-marker-"+Guid.NewGuid().ToString("N");
                CloudStore.Run(a,new[]{"config","create","remote","alias","remote",files,"description",marker,"--non-interactive"},CancellationToken.None).GetAwaiter().GetResult();
                check(!File.ReadAllText(CloudStore.Config(a)).Contains(marker)&&!File.ReadAllText(CloudStore.Config(a)).Contains(files),"Provider config and secrets never appear in plaintext in the persisted config");
                var dump=CloudStore.Run(a,new[]{"config","dump"},CancellationToken.None).GetAwaiter().GetResult();
                check(dump.Contains(marker),"rclone can decrypt and read its config using the protected key");
                a.Port=CloudRuntime.FreePort();a.BridgePassword=NetworkSettingsStore.Protect(Guid.NewGuid().ToString("N"));a.Ready=true;CloudKeys.Create(a);
                var accounts=new CloudAccounts();accounts.Accounts.Add(a);CloudStore.Save(accounts);var loaded=CloudStore.Find(a.Id);
                check(loaded.Port==a.Port&&loaded.Ready&&loaded.Fingerprint.StartsWith("SHA256:"),"Cloud account endpoint and pinned host key survive serialization");
                check(!File.ReadAllText(Path.Combine(CloudStore.Root,"accounts.xml")).Contains(NetworkSettingsStore.Unprotect(a.BridgePassword)),"Internal session password is DPAPI-protected");
                CloudRuntime.Ensure(a.Id);var profile=CloudRuntime.Profile(a);profile.Validate();
                check(profile.Host=="127.0.0.1"&&profile.IsSshTransfer&&!profile.SshAgent,"Bridge binds exclusively to loopback and uses authenticated SFTP");
                using(var connection=new RemoteConnection(profile)){
                    var options=connection.Options(NetworkSettingsStore.Unprotect(profile.EncryptedPassword));connection.Open(options);
                    check(connection.Session.ListDirectory("/").Files.Any(x=>x.Name=="český soubor.txt"),"Real rclone bridge lists Unicode filenames");
                    check(connection.Display("/folder").Contains("Test cloud")&&!connection.Display("/folder").Contains("127.0.0.1"),"Panel location presents the cloud account instead of the internal transport address");
                    string target=Path.Combine(root,"download.txt");connection.Session.GetFiles("/český soubor.txt",target).Check();check(File.ReadAllText(target)=="Cloud round trip ✓","Real bridge download preserves file bytes");
                    string upload=Path.Combine(root,"upload.txt");File.WriteAllText(upload,"Uploaded cloud file ✓");
                    using(var worker=connection.TransferConnection()){
                        worker.OpenTransfer();worker.Session.PutFiles(upload,"/uploaded.txt").Check();check(File.ReadAllText(Path.Combine(files,"uploaded.txt"))=="Uploaded cloud file ✓","Independent transfer connection uploads through the real bridge");
                        worker.Session.CreateDirectory("/created");worker.Session.MoveFile("/uploaded.txt","/created/renamed.txt");check(worker.Session.FileExists("/created/renamed.txt"),"Cloud folders and rename/move operations work"); File.WriteAllText(upload,"Changed local content"); var skipped=new NetworkCopyResult();NetworkTransfers.Copy(new Entry{FullPath=upload},null,worker,"/created",NetworkCopyRule.Resume,skipped);check(skipped.Copied==1&&File.ReadAllText(Path.Combine(files,"created","upload.txt"))=="Changed local content","Cloud continuation restarts an unfinished upload as a complete file");File.WriteAllText(upload,"Must not overwrite");var kept=new NetworkCopyResult();NetworkTransfers.Copy(new Entry{FullPath=upload},null,worker,"/created",NetworkCopyRule.Resume,kept);check(kept.Skipped==1&&File.ReadAllText(Path.Combine(files,"created","upload.txt"))=="Changed local content","Cloud continuation preserves already completed destination files");
                        worker.Session.RemoveFiles("/created/renamed.txt").Check();check(!File.Exists(Path.Combine(files,"created","renamed.txt")),"Cloud deletion goes through the normal transfer transport");
                    }
                    var wrong=options;wrong.SshHostKeyFingerprint="SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
                    using(var invalid=new RemoteConnection(profile)){bool rejected=false;try{invalid.Open(wrong);}catch{rejected=true;}check(rejected,"An unexpected bridge host key is rejected");}
                }
                using(var invalid=new RemoteConnection(profile)){
                    bool rejected=false;try{invalid.Open(invalid.Options("wrong password"));}catch{rejected=true;}check(rejected,"Bridge rejects an invalid session password");
                }
                CloudRuntime.Stop(a.Id);CloudRuntime.Ensure(a.Id);
                using(var resumed=new RemoteConnection(profile)){resumed.Open(resumed.Options(NetworkSettingsStore.Unprotect(profile.EncryptedPassword)));check(resumed.Session.FileExists("/český soubor.txt"),"Bridge restart preserves the account identity and pinned key");}
                using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool stopped=false;try{CloudStore.Run(a,new[]{"lsjson","remote:"},cancel.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){stopped=true;}check(stopped,"Cancelled cloud commands stop promptly");}
                string quoted=CloudStore.Quote("C:\\path with spaces\\");check(quoted.EndsWith("\\\\\""),"Windows arguments preserve trailing backslashes and spaces");
                var bad=new CloudAccount{Id="../outside"};bool escaped=false;try{CloudStore.AccountPath(bad);}catch(IOException){escaped=true;}check(escaped,"Account storage rejects paths escaping the cloud configuration folder");
                foreach(string provider in new[]{"drive","onedrive","dropbox"}){
                    var probe=new CloudAccount{Name="Authorization probe",Provider=provider};string url=null;
                    using(var deadline=new CancellationTokenSource(20000)){
                        bool cancelled=false;
                        try{CloudStore.Authorize(probe,q=>Task.FromResult(Convert.ToString(q.Option.Default)),deadline.Token,link=>{url=link;deadline.Cancel();}).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
                        check(cancelled&&url!=null&&new Uri(url).IsLoopback,CloudStore.ProviderName(provider)+" starts the real browser authorization flow and cancels before user login");
                    }
                }
                var urls=new List<string>();using(var memory=new MemoryStream(Encoding.UTF8.GetBytes("visit https://evil.example/auth?state=x\nvisit http://127.0.0.1:53682/auth?state=test\nvisit http://127.0.0.1:53682/auth?state=test\n"))){using(var reader=new StreamReader(memory))CloudStore.ReadError(reader,urls.Add).GetAwaiter().GetResult();}
                check(urls.Count==1&&urls[0].StartsWith("http://127.0.0.1:"),"Browser launcher accepts only the local authorization endpoint and ignores repeats");
                check(CloudRuntime.SavedProfile(Guid.NewGuid().ToString("N"))==null,"Removed cloud accounts are reported as missing when restoring saved views");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                main=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);
                var manager=new CloudManagerWindow(main){Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};manager.Show();manager.UpdateLayout();NextStageTests.Draw(manager,Path.Combine(root,"cloud-accounts.png"));
                var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
                timer.Tick+=delegate{
                    var editor=manager.OwnedWindows.Cast<Window>().FirstOrDefault();if(editor==null)return;timer.Stop();editor.UpdateLayout();
                    var services=AdvancedToolsTests.Visual<System.Windows.Controls.ComboBox>(editor).Single();
                    check(services.Items.Cast<string>().SequenceEqual(new[]{"Google Drive","OneDrive","Dropbox","iCloud Drive"}),"Account wizard offers all four cloud services");
                    NextStageTests.Draw(editor,Path.Combine(root,"cloud-add-account.png"));editor.DialogResult=false;
                };
                timer.Start();AdvancedToolsTests.Visual<System.Windows.Controls.Button>(manager).First(x=>Convert.ToString(x.Content)==L.Text("cloud.add")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));timer.Stop();manager.Close();
                NavigationTests.Wait(main.CheckCloudPanel(a,root,check));
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),String.Join(Environment.NewLine,checks)+Environment.NewLine+"FAIL: "+ex);return 1;}
            finally{if(main!=null)main.Close();CloudRuntime.StopAll();CloudStore.TestRoot=previous;CloudApplicationOAuth.TestPath=previousOAuth;}
        }
    }
    public partial class MainWindow {
        internal async Task CheckCloudPanel(CloudAccount account,string root,Action<bool,string> check){
            await ConnectCloud(account);check(left.IsRemote&&left.Remote.Profile.CloudAccountId==account.Id,"Cloud account opens in the active file panel");
            check(!AdvancedToolsTests.Visual<System.Windows.Controls.TextBlock>(left).Any(x=>x.IsVisible&&x.Text.Contains("127.0.0.1")),"Cloud panel headings show the account and service rather than an internal endpoint");check(left.List.Items.Cast<Entry>().Any(x=>FilePanel.EntryName(x)=="český soubor.txt"),"Cloud panel renders the remote file listing");
            var originalConnection=left.Remote;await ConnectCloud(account);check(Object.ReferenceEquals(left.Remote,originalConnection),"Reopening an already connected cloud account reuses its existing connection");
            var saved=left.SaveTabs();check(saved.Any(x=>x.ProfileId=="cloud-"+account.Id),"Saved cloud tabs retain the account identity");
            check(CloudInUse(account.Id),"Account removal detects open cloud panels");
            UpdateLayout();NextStageTests.Draw(this,Path.Combine(root,"cloud-panel.png"));
            left.DisconnectRemote();if(left.NavigationTask!=null)await left.NavigationTask;
            check(!left.IsRemote,"Normal disconnect returns from the cloud to a local folder");
        }
    }
}
