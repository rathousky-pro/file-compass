using System;using System.Diagnostics;using System.Windows;using System.Windows.Controls;using System.Windows.Controls.Primitives;using System.Windows.Input;using System.Windows.Threading;
namespace FileCompass {
 public partial class FilePanel {
  readonly Stopwatch rightHoldClock=new Stopwatch();readonly DispatcherTimer rightHoldTimer=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(16)};Popup rightHoldPopup;ProgressBar rightHoldBar;TextBlock rightHoldLabel;bool holdMenuShown;
#if APP_TESTS
  internal Action HoldCompletedForTests;
#endif
  internal string RightHoldDescription {get{return rightHoldLabel==null?"":rightHoldLabel.Text;}}
  internal bool RightHoldIndicatorOpen {get{return rightHoldPopup!=null&&rightHoldPopup.IsOpen;}}
  internal double RightHoldProgress {get{return rightHoldBar==null?0:rightHoldBar.Value/100;}}
  internal void StartRightHoldIndicator(Point point){holdMenuShown=false;contextPending=true;rightMoved=false;
   if(rightHoldPopup==null){rightHoldBar=new ProgressBar{Minimum=0,Maximum=100,Width=116,Height=4,Foreground=FluentTheme.Accent,Margin=new Thickness(0,4,0,0)};var body=new StackPanel();rightHoldLabel=new TextBlock{FontSize=10,Foreground=FluentTheme.Text};body.Children.Add(rightHoldLabel);body.Children.Add(rightHoldBar);rightHoldPopup=new Popup{PlacementTarget=List,Placement=PlacementMode.RelativePoint,StaysOpen=true,AllowsTransparency=true,Focusable=false,IsHitTestVisible=false,Child=new Border{Padding=new Thickness(7,5,7,6),Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Child=body}};rightHoldTimer.Tick+=delegate{AdvanceRightHold(rightHoldClock.Elapsed,Mouse.RightButton==MouseButtonState.Pressed);};Unloaded+=delegate{CancelFileContextGesture();};List.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape&&contextPending){CancelFileContextGesture();Mouse.Capture(null);e.Handled=true;}};}
   rightHoldLabel.Text=L.Text("context.programMenu");rightHoldBar.Value=0;rightHoldPopup.HorizontalOffset=point.X+16;rightHoldPopup.VerticalOffset=point.Y+20;rightHoldPopup.IsOpen=false;rightHoldClock.Restart();rightHoldTimer.Start();
  }
  void HideRightHoldIndicator(){rightHoldTimer.Stop();if(rightHoldPopup!=null)rightHoldPopup.IsOpen=false;}
  internal void CancelFileContextGesture(){contextPending=false;HideRightHoldIndicator();EndRightMark();}
  internal void AdvanceRightHold(TimeSpan elapsed,bool pressed){if(!contextPending||rightMoved||!pressed){HideRightHoldIndicator();return;}if(elapsed.TotalMilliseconds<250)return;rightHoldPopup.IsOpen=true;rightHoldBar.Value=Math.Max(0,Math.Min(100,(elapsed.TotalMilliseconds-250)/3.5));if(elapsed.TotalMilliseconds<600)return;CompleteProgramContextHold();}
  void CompleteProgramContextHold(){var entry=contextPressedEntry;holdMenuShown=true;contextPending=false;HideRightHoldIndicator();EndRightMark();Mouse.Capture(null);if(entry!=null)SelectContextEntry(entry);
#if APP_TESTS
   if(HoldCompletedForTests!=null){HoldCompletedForTests();return;}
#endif
   ShowFileContextMenu(false);
  }
 }
}
