using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace CommanderCZ
{
    internal sealed class DeleteConfirmationDialog : Window
    {
        internal readonly Button Recycle;
        readonly TextBlock question = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        readonly List<Entry> entries;
        internal bool Permanent { get; private set; }
        internal DeleteConfirmationDialog(Window owner, List<Entry> selected, bool canRecycle, bool permanent, string location, string unavailableReason = null)
        {
            entries = selected; Permanent = permanent || !canRecycle; CopyUi.Style(this, owner, L.Text("delete.chooseActionTitle"), 560);
            var body = new StackPanel { Margin = new Thickness(14) }; Content = body;
            var row = new DockPanel(); var icon = new TextBlock { Text = "?", Foreground = Brushes.White, FontSize = 23, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var circle = new Border { Child = icon, Background = FluentTheme.Accent, Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Margin = new Thickness(0, 0, 14, 0) }; DockPanel.SetDock(circle, Dock.Left); row.Children.Add(circle); row.Children.Add(question); body.Children.Add(row);
            body.Children.Add(new TextBlock { Text = L.Text("delete.permanentButtonHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(46, 10, 0, 8) });
            if (!canRecycle) body.Children.Add(new TextBlock { Text = unavailableReason ?? L.Text("delete.recycleUnavailable"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(46, 0, 0, 8) });
            var details = new TextBlock { Text = location, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = location, Foreground = FluentTheme.Muted, Margin = new Thickness(46, 0, 0, 8) }; body.Children.Add(details);
            var buttons = FluentTheme.Actions(); body.Children.Add(buttons);
            var remove = CopyUi.Button(L.Text("action.delete"), delegate { Permanent = true; DialogResult = true; }); remove.IsDefault = permanent || !canRecycle; remove.ToolTip = L.Text("delete.permanentTitle"); buttons.Children.Add(remove);
            var no = CopyUi.Button(L.Text("action.no"), delegate { DialogResult = false; }); no.IsCancel = true; buttons.Children.Add(no);
            Recycle = CopyUi.Button(L.Text("delete.moveToRecycleBin"), delegate { if (!canRecycle) return; Permanent = false; DialogResult = true; }); Recycle.IsEnabled = canRecycle; Recycle.IsDefault = canRecycle && !permanent; buttons.Children.Add(Recycle);
            UpdateQuestion();
            Loaded += delegate { no.Focus(); };
        }
        void UpdateQuestion()
        {
            if (entries.Count == 1) { var item = entries[0]; string name = Path.GetFileName(item.FullPath.TrimEnd('\\', '/')); question.Text = L.Format(item.IsDirectory ? "delete.chooseFolderAction" : "delete.chooseFileAction", name); }
            else question.Text = L.Format("delete.chooseItemsAction", entries.Count);
        }
    }
}

