using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading;
namespace CommanderCZ {
    internal sealed class ArchiveProgressDialog : Window {
        readonly TransferWindowState placement;
        readonly TextBlock detail = new TextBlock { TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,12,0,10) };
        readonly TextBlock preparationPath = new TextBlock { TextWrapping=TextWrapping.Wrap, MaxHeight=48, TextTrimming=TextTrimming.CharacterEllipsis };
        readonly TextBlock percent = new TextBlock { VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(12,0,0,0), MinWidth=38, TextAlignment=TextAlignment.Right };
        readonly TextBlock compression = new TextBlock { TextWrapping=TextWrapping.Wrap };
        readonly LocalCopyProgressBar track = new LocalCopyProgressBar(true) { Height=6, VerticalAlignment=VerticalAlignment.Center };
        readonly Border fileBox, compressionBox, progressBox;
        bool finished;
        internal Action Backgrounded, Foregrounded;
        internal ArchiveProgressDialog(Window owner,string title,CancellationTokenSource cancellation) {
            CopyUi.Style(this,owner,L.Text("app.dialogTitlePrefix")+title,620);Height=340;SizeToContent=SizeToContent.Manual;ResizeMode=ResizeMode.CanMinimize;
            placement=new TransferWindowState(this,delegate{if(Backgrounded!=null)Backgrounded();},delegate{if(Foregrounded!=null)Foregrounded();});
            var body=new DockPanel {Margin=new Thickness(12)};Content=body;
            var buttons=new Grid {Margin=new Thickness(0,12,0,0)};buttons.ColumnDefinitions.Add(new ColumnDefinition());buttons.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel=CopyUi.Button(L.Text("action.cancel"),cancellation.Cancel);cancel.HorizontalAlignment=HorizontalAlignment.Left;buttons.Children.Add(cancel);
            var minimize=CopyUi.Button(L.Text("action.minimize"),placement.Background);minimize.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(minimize,1);buttons.Children.Add(minimize);
            DockPanel.SetDock(buttons,Dock.Bottom);body.Children.Add(buttons);
            var content=new StackPanel();
            body.Children.Add(new Border { Background=CompactTheme.PanelSurface, BorderBrush=CompactTheme.ChromeLine,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(12),Child=content });
            var heading=CopyUi.Label(title);heading.Margin=new Thickness(0);heading.FontWeight=FontWeights.SemiBold;heading.MaxHeight=36;heading.TextTrimming=TextTrimming.CharacterEllipsis;heading.ToolTip=title;content.Children.Add(heading);
            content.Children.Add(detail);
            var status=new StackPanel(); content.Children.Add(Field(status));
            fileBox=new Border { Child=preparationPath };status.Children.Add(fileBox);
            var row=new DockPanel();DockPanel.SetDock(percent,Dock.Right);row.Children.Add(percent);row.Children.Add(track);
            progressBox=new Border { Child=row, Margin=new Thickness(0,12,0,0) };status.Children.Add(progressBox);
            compressionBox=new Border { Child=compression, Margin=new Thickness(0,12,0,0), Visibility=Visibility.Collapsed };status.Children.Add(compressionBox);
            Update(L.Text("state.preparing"),0,false);
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(!finished){e.Cancel=true;cancellation.Cancel();detail.Text=L.Text("state.cancellingOperation");}};
        }
        static Border Field(UIElement child) { return new Border {Child=child,Background=Brushes.White,BorderBrush=CompactTheme.FieldLine,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Padding=new Thickness(8,6,8,6)}; }
        internal void Update(string text,double fraction,bool known) {
            if(finished)return;
            detail.Text=System.Text.RegularExpressions.Regex.IsMatch(text.Trim(),@"^\d+%$")?"":L.Display(text);
            track.Preparing=false;track.Visibility=known?Visibility.Visible:Visibility.Collapsed;percent.Visibility=known?Visibility.Visible:Visibility.Collapsed;
            progressBox.Visibility=known?Visibility.Visible:Visibility.Collapsed;fileBox.Visibility=known?Visibility.Collapsed:Visibility.Visible;
            track.Update(fraction);percent.Text=known?(fraction*100).ToString("F0")+" %":""; 
        }
        internal void UpdateCurrentFile(string path) { if(finished)return;preparationPath.Text=path??"";preparationPath.ToolTip=path;fileBox.Visibility=String.IsNullOrEmpty(path)?Visibility.Collapsed:Visibility.Visible; }
        internal void UpdatePreparation(ArchivePreparationProgress state) { Update(state.Summary,0,false);UpdateCurrentFile(state.Path);compressionBox.Visibility=Visibility.Collapsed; }
        internal void RestoreProgress(){placement.Restore();}
        internal void UpdateCompression(string text){if(finished)return;compression.Text=text;compressionBox.Visibility=Visibility.Visible;}
        internal void Finish(){if(finished)return;finished=true;placement.Finish();}
    }
}


