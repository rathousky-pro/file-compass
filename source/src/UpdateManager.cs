using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
namespace FileCompass {
 internal sealed class UpdateManager : IDisposable {
    readonly MainWindow main;
    readonly DispatcherTimer timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
    internal event Action Changed;
    internal bool Checking,Downloading;
    internal string Feedback="";
    CancellationTokenSource checkCancellation;
    bool disposed;
    bool inspecting;
    bool automatic;
    internal UpdateManager(MainWindow owner){main=owner;timer.Tick+=async delegate{ApplyPending();if(automatic&&!Checking&&!Downloading)await Check(true);};timer.Start();}
    internal async Task StartAutomatic(){automatic=true;await Check(true);}
    internal void Notify(){if(Changed!=null)Changed();}
    internal async Task Inventory(){if(inspecting||disposed)return;inspecting=true;try{await Task.Run(()=>VendorUpdates.InspectInstalled(CancellationToken.None));}catch(Exception ex){Feedback=L.Text("updates.failed")+ex.Message;}finally{inspecting=false;if(!disposed)Notify();}}
    internal async Task Check(bool automatic=false){
        if(Checking||disposed)return;
        var state=UpdateRegistry.State();var age=DateTime.UtcNow-state.LastAttemptUtc;if(automatic&&(!state.Daily||(age>=TimeSpan.Zero&&age<TimeSpan.FromDays(1))))return;
        Checking=true;checkCancellation=new CancellationTokenSource();var token=checkCancellation.Token;Feedback=L.Text("updates.checking");Notify();
        try{
            await Inventory();
            UpdateRegistry.Edit(x=>x.LastAttemptUtc=DateTime.UtcNow);
            bool failed=false;var result=await Task.Run(delegate{var items=new System.Collections.Generic.List<VendorUpdate>();foreach(var id in VendorUpdates.Ids){token.ThrowIfCancellationRequested();try{items.Add(VendorUpdates.Check(id,token));}catch(Exception ex){token.ThrowIfCancellationRequested();failed=true;items.Add(new VendorUpdate{Id=id,Name=VendorUpdates.Name(id),Source=VendorUpdates.Home(id),Error=ex.Message});}}return items;});
            token.ThrowIfCancellationRequested();UpdateRegistry.Edit(x=>{x.Available=result;if(!failed)x.LastSuccessUtc=DateTime.UtcNow;});Feedback=L.Text(failed?"updates.checkFailed":"updates.checked");
        }catch(OperationCanceledException){Feedback=L.Text("updates.cancelled");}
        catch(Exception ex){Feedback=L.Text("updates.failed")+ex.Message;}
        finally{Checking=false;checkCancellation.Dispose();checkCancellation=null;Notify();}
    }
    internal void CancelCheck(){if(checkCancellation!=null)checkCancellation.Cancel();}
    internal bool CanDownload(VendorUpdate item){return !Downloading&&String.IsNullOrEmpty(UpdateRegistry.StateError)&&item!=null&&VendorUpdates.ValidOrigin(item)&&String.IsNullOrEmpty(item.Error)&&!String.IsNullOrEmpty(item.Hash);}
    internal async void Download(VendorUpdate item){
        if(!CanDownload(item))return;
        Downloading=true;Feedback=L.Text("updates.downloading");Notify();
        try{
            bool success=await main.RunUpdateDownload(item,delegate(CancellationToken token,Action<string> report){
                CloudStore.SecureDirectory(UpdateRegistry.Root);string folder=UpdateRegistry.SafePath(UpdateRegistry.Root,"downloads\\"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
                string archive=UpdateRegistry.SafePath(folder,item.FileName);
                try{
                    using(var output=new FileStream(archive,FileMode.CreateNew,FileAccess.Write,FileShare.None))UpdateNetwork.Download(item.Url,output,768L*1048576,token,delegate(long received,long total){long expected=total>0?total:item.Size;report(expected>0?Math.Min(89,89.0*received/expected).ToString("F0")+"%":L.Text("updates.downloading")+" "+(received/1048576.0).ToString("F1")+" MiB");});
                    report(L.Text("updates.verifying"));token.ThrowIfCancellationRequested();
                    UpdateRegistry.Stage(item,archive,token,delegate{token.ThrowIfCancellationRequested();});report("100%");
                }finally{PathSafety.DeleteTree(folder);}
            });
            Feedback=success&&UpdateRegistry.State().Pending.Any(x=>x.Id==item.Id)?L.Text("updates.ready"):L.Text("updates.downloadEnded");
        }catch(Exception ex){Feedback=L.Text("updates.failed")+ex.Message;}
        finally{Downloading=false;ApplyPending();Notify();}
    }
    internal void ApplyPending(){
        if(disposed||main.UpdateWorkRunning)return;
        try{bool changed=false;foreach(var package in UpdateRegistry.State().Pending.ToArray())if(UpdateRegistry.Activate(package.Id))changed=true;if(changed){Feedback=L.Text("updates.installed");Notify();}}
        catch(Exception ex){Feedback=L.Text("updates.failed")+ex.Message;timer.Stop();Notify();}
    }
    internal void Rollback(string id){try{if(main.UpdateWorkRunning)throw new IOException(L.Text("updates.waiting"));UpdateRegistry.Rollback(id);Feedback=L.Text("updates.restored");}catch(Exception ex){Feedback=L.Text("updates.failed")+ex.Message;}Notify();}
    public void Dispose(){disposed=true;timer.Stop();CancelCheck();Changed=null;}
 }
 public partial class MainWindow {
    UpdateManager updateManager;
    internal UpdateManager Updates{get{return updateManager??(updateManager=new UpdateManager(this));}}
    internal bool UpdateHostClosed{get{return windowClosed;}}
    internal bool UpdateWorkRunning{get{return fileToolsRunning>0||archiveOperations>0||deletions>0||busy||PanelsLoading||copyRunning||networkCopies>0||networkJobs.Any(x=>!x.Finished)||copyJobs.Count>0;}}
    internal Task<bool> RunUpdateDownload(VendorUpdate item,Action<CancellationToken,Action<string>> work){return ArchiveOperation(L.Text("updates.title")+" · "+item.Name,work);}
 }
}
