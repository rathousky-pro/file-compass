using System;
using System.Windows;
using System.Windows.Controls;

namespace CommanderCZ
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
        public CopyErrorDialog(Window owner, CopyFailure failure, bool deleting = false)
        {
            CopyUi.Style(this, owner, deleting ? L.Text("delete.errorWindowTitle") : L.Text("copy.errorWindowTitle"), 440);
            var panel = new StackPanel { Margin = new Thickness(12) }; Content = panel;
            panel.Children.Add(new TextBlock { Text = deleting ? L.Text("delete.cannotDeleteItem") : L.Text("copy.cannotCopyFile"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 2, 8) });
            panel.Children.Add(new TextBlock { Text = deleting ? L.Text("common.itemPrefix") + failure.Source : L.Text("transfer.sourceLabel") + failure.Source + L.Text("transfer.targetSummaryPrefix") + failure.Target, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 2, 8) });
            int code = failure.Error.HResult & 65535;
            string message = code == 32 || code == 33 ? L.Text("error.fileInUse") : failure.Error.Message;
            panel.Children.Add(new TextBlock { Text = "„" + message + "“", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 2, 16) });
            var buttons = new System.Windows.Controls.Primitives.UniformGrid { Rows = 2, Columns = 2, Width = 260, HorizontalAlignment = HorizontalAlignment.Center };
            buttons.Children.Add(CopyUi.Button(L.Text("action.skip"), delegate { Select(CopyErrorChoice.Skip); }));
            buttons.Children.Add(CopyUi.Button(L.Text("action.retry"), delegate { Select(CopyErrorChoice.Retry); }));
            buttons.Children.Add(CopyUi.Button(L.Text("conflict.skipAll"), delegate { Select(CopyErrorChoice.SkipAll); }));
            var cancel = CopyUi.Button(L.Text("action.abort"), delegate { Select(CopyErrorChoice.Cancel); }); cancel.IsCancel = true; buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
        }
        void Select(CopyErrorChoice choice) { Choice = choice; DialogResult = true; }
    }
}

