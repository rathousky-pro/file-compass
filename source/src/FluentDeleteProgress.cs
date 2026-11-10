using System;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
 internal sealed class FluentDeleteProgress : Window {
  readonly CopyControl control;
  readonly Action cancel;
  readonly TransferWindowState placement;
  readonly TransferProgressLayout view;
  readonly Button pause;
  internal Action Backgrounded,Foregrounded;
  bool finished;
  internal FluentDeleteProgress(Window owner,CopyControl control,Action cancel){
   this.control=control;this.cancel=cancel;
   CopyUi.Style(this,owner,L.Text("newui.4"),580);
   placement=new TransferWindowState(this,delegate{if(Backgrounded!=null)Backgrounded();},delegate{if(Foregrounded!=null)Foregrounded();});
   view=new TransferProgressLayout(this,L.Text("newui.4"));
   view.File.Visibility=view.FilePercent.Visibility=System.Windows.Visibility.Collapsed;
   view.DetailBody.Children.Add(new TextBlock{Text=L.Text("newui.5"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted});
   pause=CopyUi.Button(L.Text("action.pause"),delegate{control.Pause(!control.Paused);RefreshPause();});view.Actions.Children.Add(pause);
   view.Actions.Children.Add(CopyUi.Button(L.Text("action.cancel"),RequestCancel));
   view.Actions.Children.Add(CopyUi.Button(L.Text("copy.backgroundAction"),placement.Background));
   Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(!finished){e.Cancel=true;RequestCancel();}};
  }
  internal void RefreshPause(){pause.Content=L.Text(control.Paused?"action.resume":"action.pause");view.Remaining.Text=control.Cancelled?L.Text("newui.6"):control.Paused?L.Text("newui.7"):L.Text("newui.8");}
  void RequestCancel(){cancel();RefreshPause();}
  internal void Update(string path,int completed,int total,bool permanent){view.Current(path,null);view.FilePercent.Visibility=System.Windows.Visibility.Collapsed;view.Percent.Text=(total==0?0:100.0*completed/total).ToString("F0")+" %";view.Overall.Update(total==0?0:(double)completed/total);view.Count.Text=completed+" / "+total;view.Totals.Text=permanent?L.Text("newui.9"):L.Text("newui.10");RefreshPause();}
  internal void Restore(){placement.Restore();}
  internal void Finish(){finished=true;placement.Finish();Close();}
 }
#if APP_TESTS
 public partial class MainWindow {
  internal System.Threading.Tasks.Task VerifyFluentDeletion(string file,string folder){return DeleteBatch(new System.Collections.Generic.List<Entry>{new Entry{FullPath=file,Name=System.IO.Path.GetFileName(file)}},null,false,true,folder);}
 }
#endif
}
