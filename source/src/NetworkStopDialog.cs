using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    internal sealed class NetworkStopDialog : Window
    {
        internal bool KeepPartial;
        internal NetworkStopDialog(Window owner)
        {
            CopyUi.Style(this,owner,L.Text("transfer.interruptTitle"),580);
            var body = new StackPanel { Margin = new Thickness(12) }; Content = body;
            var section = new GroupBox { Header = L.Text("transfer.pendingRelayTitle"),Margin = new Thickness(0,0,0,10),Padding = new Thickness(10) };
            section.Content = new TextBlock { Text = L.Text("transfer.keepPartialHelp"),TextWrapping = TextWrapping.Wrap }; body.Children.Add(section);
            var keep = CopyUi.Button(L.Text("transfer.interruptKeepPartial"),delegate { KeepPartial = true; DialogResult = true; }); keep.IsDefault = true; body.Children.Add(keep);
            body.Children.Add(CopyUi.Button(L.Text("transfer.cancelDeletePartial"),delegate { KeepPartial = false; DialogResult = true; }));
            var back = CopyUi.Button(L.Text("transfer.resumeTooltip"),delegate { DialogResult = false; }); back.IsCancel = true; body.Children.Add(back);
        }
    }
    public partial class MainWindow
    {
        internal bool ConfirmNetworkCancellation(NetworkTransferJob[] jobs)
        {
            var relay = jobs.Where(x => x.RelayTarget != null && !x.Finished && !x.Cancellation.IsCancellationRequested).ToArray();
            if (relay.Length == 0) return true;
            var dialog = new NetworkStopDialog(this); if (dialog.ShowDialog() != true) return false;
            foreach (var job in relay) job.KeepPartialOnCancel = dialog.KeepPartial; return true;
        }
        internal void StopNetworkJobs(NetworkTransferJob[] jobs)
        {
            if (!ConfirmNetworkCancellation(jobs)) return;
            foreach (var job in jobs.Where(x => !x.Finished)) { if (job.Activity != null) job.Activity.RequestCancel(false); else job.Cancel(); }
        }
    }
}

