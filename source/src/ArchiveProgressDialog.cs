using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
namespace FileCompass {
 internal sealed class ArchiveProgressDialog : Window {
  readonly TransferWindowState placement;
  readonly TransferProgressLayout presentation;
  readonly TextBlock detail=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,8)};
  readonly TextBlock compression=new TextBlock{TextWrapping=TextWrapping.Wrap};
  readonly CompressionRatioTrend ratioTrend=new CompressionRatioTrend{Height=68,Margin=new Thickness(0,8,0,0)};
  bool finished;
  internal Action Backgrounded,Foregrounded;
  internal ArchiveProgressDialog(Window owner,string title,CancellationTokenSource cancellation){
   CopyUi.Style(this,owner,L.Text("app.dialogTitlePrefix")+title,620);SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;
   placement=new TransferWindowState(this,delegate{if(Backgrounded!=null)Backgrounded();},delegate{if(Foregrounded!=null)Foregrounded();});
   presentation=new TransferProgressLayout(this,title);
   presentation.DetailBody.Children.Add(detail);presentation.DetailBody.Children.Add(compression);presentation.DetailBody.Children.Add(ratioTrend);
   presentation.Actions.Children.Add(CopyUi.Button(L.Text("action.cancel"),cancellation.Cancel));
   var background=CopyUi.Button(L.Text("copy.backgroundAction"),placement.Background);FluentTheme.Primary(background);presentation.Actions.Children.Add(background);
   Update(L.Text("state.preparing"),0,false);
   Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(!finished){e.Cancel=true;cancellation.Cancel();presentation.Remaining.Text=L.Text("state.cancellingOperation");}};
  }
  internal void Update(string text,double fraction,bool known){
   if(finished)return;
   string status=System.Text.RegularExpressions.Regex.IsMatch((text??"").Trim(),@"^\d+%$")?"":L.Display(text??"");
   detail.Text=status;presentation.Remaining.Text=status;
   presentation.ShowProgress(known);
   presentation.Overall.Preparing=!known;presentation.Overall.Update(fraction);
   presentation.Percent.Text=known?(Math.Max(0,Math.Min(1,fraction))*100).ToString("F0")+" %":"—";
  }
  internal void UpdateCurrentFile(string path){if(finished)return;if(!String.Equals((string)presentation.CurrentFile.ToolTip,path,StringComparison.Ordinal))presentation.Current(path,null);}
  internal void UpdatePreparation(ArchivePreparationProgress state){Update(state.Summary,0,false);UpdateCurrentFile(state.Path);compression.Text="";}
  internal void RestoreProgress(){placement.Restore();}
  internal void UpdateCompression(ArchiveCompressionProgress state){
   UpdateCompression(state.Summary());
   double processed=state.Complete?state.Original:state.Processed>=0?state.Processed:state.Original*state.Percent/100;
   presentation.Totals.Text=CopyUi.Bytes((long)processed)+" / "+CopyUi.Bytes(state.Original);
   if(state.CurrentFile!=null||state.FileTotal>=0)presentation.Current(state.CurrentFile,state.FileTotal>0?(double?)state.FileDone/state.FileTotal:state.FileTotal==0?(double?)1:null);
   if(processed>0)ratioTrend.Add(100.0*state.Packed/processed,state.Complete?1:state.Percent/100);
  }
  internal void UpdateCompression(string text){if(finished)return;compression.Text=text;}
  internal void Finish(){if(finished)return;finished=true;placement.Finish();}
 }
}
