using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FileCompass
{
    public sealed class ArchiveCopyDialog : Window
    {
        public CopyOptions Options { get; private set; }
        public ArchiveCopyDialog(Window owner, int count, string source, string target, bool intoArchive)
        {
            CopyUi.Style(this, owner, intoArchive ? L.Text("archive.copyIntoTitle") : L.Text("archive.copyOutTitle"), 590);
            var body = new StackPanel { Margin = new Thickness(10) }; Content = body;
            body.Children.Add(CopyUi.Label(L.Text("copy.selectionQuestionPrefix") + count + L.Text("copy.selectionQuestionSuffix")));
            body.Children.Add(new PathTextBlock{Prefix=L.Text("transfer.sourceLabel"),FullPath=source,Margin=new Thickness(2,4,2,4)});
            body.Children.Add(new PathTextBlock{Prefix=L.Text("transfer.targetLabel"),FullPath=target,Margin=new Thickness(2,4,2,7)});
            body.Children.Add(CopyUi.Label(L.Text("copy.fileMaskLabel")));
            var filter = new ComboBox { IsEditable = true, Text = "*.*", Margin = new Thickness(2, 2, 2, 8) };
            foreach (string value in new[] { "*.*", "*.txt;*.md", "*.jpg;*.png", "*.* | *.tmp;*.bak" }) filter.Items.Add(value);
            filter.ToolTip = L.Text("copy.fileMaskHelp"); body.Children.Add(filter);
            body.Children.Add(CopyUi.Label(L.Text("copy.existingDestinationLabel")));
            var rule = new ComboBox { SelectedIndex = 0, Margin = new Thickness(2, 2, 2, 8) };
            rule.Items.Add(L.Text("conflict.skipExistingFiles")); rule.Items.Add(L.Text("conflict.overwriteExistingFiles")); body.Children.Add(rule);
            var hidden = new CheckBox { Content = L.Text("copy.skipHiddenAndSystem"), Margin = new Thickness(2, 4, 2, 4) }; body.Children.Add(hidden);
            var empty = new CheckBox { Content = L.Text("copy.includeEmptyFolders"), IsChecked = true, Margin = new Thickness(2, 4, 2, 4) }; body.Children.Add(empty);
            var verify = new CheckBox { Content = L.Text("copy.verifySha256"), Margin = new Thickness(2, 4, 2, 4) }; body.Children.Add(verify);
            body.Children.Add(CopyUi.Label(intoArchive ? L.Text("archive.copyIntoHelp") : L.Text("archive.copyOutHelp")));
            var buttons = new UniformGrid { Columns = 2 }; body.Children.Add(buttons);
            var ok = CopyUi.Button(L.Text("action.copy"), delegate
            {
                if (filter.Text.Split('|').Length > 2) { MessageBox.Show(this, L.Text("copy.multipleExclusionSeparators"), L.Text("copy.filterTitle")); return; }
                Options = new CopyOptions { Filter = filter.Text, Rule = rule.SelectedIndex == 1 ? CopyRule.OverwriteAll : CopyRule.SkipAll,
                    SkipHidden = hidden.IsChecked == true, EmptyFolders = empty.IsChecked == true, Verify = verify.IsChecked == true };
                DialogResult = true;
            }); ok.IsDefault = true; buttons.Children.Add(ok);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
        }
    }
}

