using System;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    public enum CopyErrorChoice { Skip, Retry, SkipAll, Cancel }
    public sealed class CopyFailure
    {
        public string Source, Target;
        public Exception Error;
    }
    public sealed class CopyErrorDialog : Window
    {
        public CopyErrorChoice Choice = CopyErrorChoice.Cancel;
        public CopyErrorDialog(Window owner, CopyFailure failure, bool deleting = false, bool moving = false)
        {
            string title=deleting?L.Text("delete.cannotDeleteItem"):L.Text(moving?"action.move":"action.copy");
            CopyUi.Style(this, owner, title, 670);
            StackPanel buttons;var root=SettingsUx.Frame(this,title,out buttons);
            var panel = new StackPanel { Margin = new Thickness(20,16,20,16) };root.Children.Add(panel);
            PathCard(panel,deleting?L.Text("common.itemPrefix"):L.Text("transfer.sourceLabel"),failure.Source);
            if(!deleting)PathCard(panel,L.Text("transfer.targetLabel"),failure.Target);
            int code = failure.Error.HResult & 65535;
            string message = code == 32 || code == 33 ? L.Text("error.fileInUse") : failure.Error.Message;
            panel.Children.Add(new TextBlock { Text = message, Foreground=FluentTheme.Text,TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2,8,2,12) });
            if(code==32||code==33)panel.Children.Add(CopyUi.Button(L.Text("tools1.locks"),delegate{FileLocks.Show(this,new[]{failure.Source,failure.Target});}));
            buttons.Children.Add(CopyUi.Button(L.Text("action.skip"), delegate { Select(CopyErrorChoice.Skip); }));
            buttons.Children.Add(CopyUi.Button(L.Text("conflict.skipAll"), delegate { Select(CopyErrorChoice.SkipAll); }));
            var cancel = CopyUi.Button(L.Text("action.cancelOperation"), delegate { Select(CopyErrorChoice.Cancel); }); cancel.IsCancel = true; buttons.Children.Add(cancel);
            var retry=CopyUi.Button(L.Text("copy.retryAction"), delegate { Select(CopyErrorChoice.Retry); });retry.IsDefault=true;FluentTheme.Primary(retry);buttons.Children.Add(retry);
            buttons.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));Width=Math.Min(SystemParameters.WorkArea.Width-32,Math.Max(Width,buttons.DesiredSize.Width+48));
        }
        static void PathCard(Panel panel,string label,string path){var text=new StackPanel();text.Children.Add(new TextBlock{Text=label,Foreground=FluentTheme.Muted,FontSize=12,Margin=new Thickness(0,0,0,4)});text.Children.Add(new TextBlock{Text=path,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold});panel.Children.Add(new Border{Child=text,Padding=new Thickness(12),Margin=new Thickness(0,0,0,8),Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6)});}
        void Select(CopyErrorChoice choice) { Choice = choice; DialogResult = true; }
    }
}
