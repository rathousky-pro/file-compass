using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
namespace CommanderCZ {
    internal static class PreviewRefreshTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root);var checks=new List<string>();MainWindow main=null;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add(label);};
            try {
                System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher)); FluentTheme.Ensure(); string folder=Directory.CreateDirectory(Path.Combine(root,"source")).FullName;
                string file=Path.Combine(folder,"selected.txt");File.WriteAllText(file,"Selected preview content");
                main=new MainWindow(folder,new ProgramSettings{AutoRefresh=false}){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);
                NavigationTests.Wait(main.VerifyPreviewRefresh(folder,file,root,check));
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),"FAIL: "+ex);return 1;}
            finally{if(main!=null)main.Close();}
        }
    }
    public partial class MainWindow {
        internal async Task VerifyPreviewRefresh(string folder,string file,string root,Action<bool,string> check) {
            left.SetRefreshPolicy(false,false);right.SetRefreshPolicy(false,false);SetPanelLayout("SideBySide");SetActive(left);
            left.Navigate(folder,false);await left.NavigationTask;left.List.SelectedItem=left.List.Items.Cast<Entry>().Single(x=>x.FullPath==file);
            var stableSource=left.List.ItemsSource;var stableSelected=left.List.SelectedItem;int resets=0;
            ((System.Collections.Specialized.INotifyCollectionChanged)stableSource).CollectionChanged+=delegate(object sender,System.Collections.Specialized.NotifyCollectionChangedEventArgs args){if(args.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Reset)resets++;};
            left.Refresh();check(left.List.IsEnabled,"Same-folder refresh does not disable or grey the list");await left.NavigationTask;
            check(Object.ReferenceEquals(stableSource,left.List.ItemsSource)&&Object.ReferenceEquals(stableSelected,left.List.SelectedItem),"Unchanged refresh preserves list and selected row instances");
            File.WriteAllText(Path.Combine(folder,"incremental.txt"),"added");left.Refresh();await left.NavigationTask;
            check(resets==0&&Object.ReferenceEquals(stableSelected,left.List.SelectedItem)&&left.List.Items.Cast<Entry>().Any(x=>x.FullPath.EndsWith("incremental.txt")),"Adding a file updates the list without a reset or replacing unrelated rows");
            File.Delete(Path.Combine(folder,"incremental.txt"));left.Refresh();await left.NavigationTask;
            check(resets==0&&!left.List.Items.Cast<Entry>().Any(x=>x.FullPath.EndsWith("incremental.txt")),"Deleting a file updates the list without a reset");
            OpenQuickPreview();await quickPreview.LoadingTask;int original=quickPreview.LoadVersion;
            quickPreview.ShowFile(file, true);check(quickPreview.LoadVersion==original,"Repeated selection of an unchanged local file preserves the preview");
            for(int i=0;i<5;i++){File.WriteAllText(Path.Combine(folder,"unrelated.txt"),"Change "+i);left.Refresh();await left.NavigationTask;}
            check(quickPreview!=null&&quickPreview.LoadVersion==original,"Unrelated folder changes and repeated list replacement do not reload Ctrl+Q preview");
            right.Refresh();await right.NavigationTask;check(quickPreview!=null&&quickPreview.LoadVersion==original,"Refreshing the covered destination panel does not close the preview"); check(quickPreview.IsCurrentLocalFile(file),"Restored cursor still refers to the original preview");
            File.WriteAllText(file,"Updated selected file with a different size");left.Refresh();await left.NavigationTask;await quickPreview.LoadingTask;
            check(quickPreview.LoadVersion>original,"An actual selected-file modification refreshes the preview");
            original=quickPreview.LoadVersion;File.SetLastWriteTimeUtc(file,File.GetLastWriteTimeUtc(file).AddMinutes(1));left.Refresh();await left.NavigationTask;await quickPreview.LoadingTask;
            check(quickPreview.LoadVersion>original,"Timestamp changes refresh a same-size selected file");
            SetPanelLayout("Single");left.List.SelectedItem=left.List.Items.Cast<Entry>().Single(x=>x.FullPath==file);left.SetDetailExpanded(true);UpdateLayout();if(left.DetailLoadingTask!=null)await left.DetailLoadingTask;
            var detail=AdvancedToolsTests.Visual<QuickPreviewPane>(left).Single();await detail.LoadingTask;original=detail.LoadVersion;
            for(int i=0;i<5;i++){File.WriteAllText(Path.Combine(folder,"unrelated.txt"),"Another change "+i);left.Refresh();await left.NavigationTask;if(left.DetailLoadingTask!=null)await left.DetailLoadingTask;}
            check(detail.LoadVersion==original,"Bottom preview survives folder refreshes without cancellation or repeated loading");
            File.WriteAllText(file,"Changed again");left.Refresh();await left.NavigationTask;await left.DetailLoadingTask;
            check(detail.LoadVersion>original,"Bottom preview reloads when its selected file changes");
            var other=left.List.Items.Cast<Entry>().Single(x=>x.FullPath==Path.Combine(folder,"unrelated.txt"));original=detail.LoadVersion;left.List.SelectedItem=other;await left.DetailLoadingTask;
            check(detail.LoadVersion>original&&detail.IsCurrentLocalFile(other.FullPath),"Changing the cursor still loads a different file");
            left.List.SelectedItem=left.List.Items.Cast<Entry>().Single(x=>x.FullPath==file);await left.DetailLoadingTask;File.Delete(file);left.Refresh();await left.NavigationTask;if(left.DetailLoadingTask!=null)await left.DetailLoadingTask;
            check(!detail.IsCurrentLocalFile(file),"Deleting the selected file clears its stale preview");
            var remaining=left.List.Items.Cast<Entry>().FirstOrDefault(x=>!x.IsDirectory);if(remaining!=null){left.List.SelectedItem=remaining;await left.DetailLoadingTask;original=detail.LoadVersion;left.SetRefreshPolicy(true,false);await Task.Delay(500);var previousNavigation=left.NavigationTask;File.WriteAllText(Path.Combine(folder,"watcher-change.txt"),"Background activity");await Task.Delay(1900);await left.NavigationTask;if(left.DetailLoadingTask!=null)await left.DetailLoadingTask;check(left.WatcherActive&&!Object.ReferenceEquals(previousNavigation,left.NavigationTask),"Filesystem watcher actually refreshed the folder");check(detail.LoadVersion==original,"Real FileSystemWatcher refresh caused by another file preserves the preview");left.SetRefreshPolicy(false,false);}
            NextStageTests.Draw(this,Path.Combine(root,"preview-after-refresh.png"));
        }
    }
}
