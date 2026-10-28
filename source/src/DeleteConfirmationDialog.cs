using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FileCompass
{
    internal sealed class DeleteConfirmationDialog : Window
    {
        internal readonly Button Recycle;
        readonly TextBlock question = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        readonly List<Entry> entries;
        internal bool Permanent { get; private set; }
        internal DeleteConfirmationDialog(Window owner, List<Entry> selected, bool canRecycle, bool permanent, string location, string unavailableReason = null)
        {
            entries = selected; Permanent = permanent || !canRecycle; CopyUi.Style(this, owner, L.Text("delete.chooseActionTitle"), 700);
            var layout=new OperationDialogLayout(this,L.Text("delete.chooseActionTitle"));var body=layout.Body;
            var selection=OperationDialogLayout.Group(body,L.Text("operation.selection"));
            question.FontSize=15;question.FontWeight=FontWeights.SemiBold;question.Margin=new Thickness(0,0,0,10);selection.Children.Add(question);
            var details=new PathTextBlock{Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,8)};details.SetPath("",location);selection.Children.Add(details);
            var items=new StackPanel();
            foreach(var entry in selected){var row=new PathTextBlock{Margin=new Thickness(0,3,0,3)};row.SetPath(entry.IsDirectory?"▸ ":"",entry.FullPath);items.Children.Add(row);}
            selection.Children.Add(new ScrollViewer{Content=items,MaxHeight=100,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var methods=OperationDialogLayout.Group(body,L.Text("delete.methodsTitle"));
            var choices=new Grid();choices.ColumnDefinitions.Add(new ColumnDefinition());choices.ColumnDefinitions.Add(new ColumnDefinition());methods.Children.Add(choices);
            var recycleInfo=new StackPanel{Margin=new Thickness(0,0,16,0)};
            recycleInfo.Children.Add(new TextBlock{Text=L.Text("delete.recycleTitle"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,6)});
            recycleInfo.Children.Add(new TextBlock{Text=canRecycle?L.Text("delete.recycleHelp"):unavailableReason??L.Text("delete.recycleUnavailable"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted});choices.Children.Add(recycleInfo);
            var permanentInfo=new StackPanel();Grid.SetColumn(permanentInfo,1);choices.Children.Add(permanentInfo);
            permanentInfo.Children.Add(new TextBlock{Text=L.Text("delete.permanentTitle"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,6)});
            permanentInfo.Children.Add(new TextBlock{Text=L.Text("delete.permanentButtonHelp"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted});
            var buttons=layout.Actions;
            var no = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); no.IsCancel = true; buttons.Children.Add(no);
            var remove = CopyUi.Button(L.Text("delete.permanentTitle"), delegate { Permanent = true; DialogResult = true; }); remove.IsDefault = permanent || !canRecycle; remove.ToolTip = L.Text("delete.permanentButtonHelp"); buttons.Children.Add(remove);
            Recycle = CopyUi.Button(L.Text("delete.moveToRecycleBin"), delegate { if (!canRecycle) return; Permanent = false; DialogResult = true; }); Recycle.IsEnabled = canRecycle; Recycle.IsDefault = canRecycle && !permanent; buttons.Children.Add(Recycle);
            UpdateQuestion();
            var primary = Recycle.IsDefault ? Recycle : remove;
            FluentTheme.Primary(primary);
            CopyUi.EnterDefault(this, primary);
            Loaded += delegate { primary.Focus(); };
        }
        void UpdateQuestion()
        {
            if (entries.Count == 1) { var item = entries[0]; string name = Path.GetFileName(item.FullPath.TrimEnd('\\', '/')); question.Text = L.Format(item.IsDirectory ? "delete.chooseFolderAction" : "delete.chooseFileAction", name); }
            else question.Text = L.Format("delete.chooseItemsAction", entries.Count);
        }
    }
}
