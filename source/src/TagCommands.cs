using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CommanderCZ
{
    public partial class MainWindow
    {
        internal async void EditTags()
        {
            var panel = active; var selected = panel.Selected();
            if (panel.IsRemote || panel.IsArchive || selected.Count == 0 || selected.Any(x => x.IsLink)) { MessageBox.Show(this, L.Text("tags.localSelectionRequired"), ApplicationBrand.Name); return; }
            string[] paths = selected.Select(x => x.FullPath).ToArray();
            var window = new Window(); CopyUi.Style(window, this, L.Text("tags.edit"), 620);
            var body = new StackPanel { Margin = new Thickness(12) }; window.Content = body;
            body.Children.Add(CopyUi.Label(L.Format("tags.selectionCount", paths.Length)));
            var input = new TextBox { Text = selected.Count == 1 ? String.Join("; ", selected[0].TagNames) : "", Margin = new Thickness(0, 8, 0, 8) }; body.Children.Add(input);
            body.Children.Add(CopyUi.Label(L.Text("tags.inputHelp")));
            var mode = new ComboBox { ItemsSource = new[] { L.Text("tags.add"), L.Text("tags.replace"), L.Text("tags.remove") }, SelectedIndex = 0, Margin = new Thickness(0, 8, 0, 8) }; body.Children.Add(mode);
            var choices = new WrapPanel(); body.Children.Add(choices);
            var known = await Task.Run(() => FileTagStore.Current.Snapshot().SelectMany(x => x.Tags ?? new string[0]).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(x => x).Take(20).ToArray());
            if (windowClosed) { window.Close(); return; }
            foreach (string value in new[] { L.Text("tags.important"), L.Text("tags.todo"), L.Text("tags.done") }.Concat(known).Distinct(StringComparer.CurrentCultureIgnoreCase))
            { string tag = value; choices.Children.Add(CopyUi.Button(tag, delegate { try { input.Text = String.Join("; ", FileTagStore.Normalize(input.Text).Concat(new[] { tag }).Distinct(StringComparer.CurrentCultureIgnoreCase)); } catch (Exception ex) { MessageBox.Show(window, ex.Message, L.Text("tags.edit")); } })); }
            body.Children.Add(new TextBlock { Text = L.Text("tags.storageHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(0, 10, 0, 0) });
            if (FileTagStore.LastError != null) body.Children.Add(new TextBlock { Text = L.Text("tags.loadError") + " " + FileTagStore.LastError, TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Firebrick });
            var buttons = FluentTheme.Actions(); body.Children.Add(buttons); string[] tags = null; int action = 0;
            var save = CopyUi.Button(L.Text("action.save"), delegate { try { tags = FileTagStore.Normalize(input.Text); action = mode.SelectedIndex; window.DialogResult = true; } catch (Exception ex) { MessageBox.Show(window, ex.Message, L.Text("tags.edit")); } }); save.IsDefault = true; buttons.Children.Add(save);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { window.DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
            if (window.ShowDialog() != true) return;
            fileToolsRunning++;
            try { await Task.Run(delegate { FileTagStore.Current.Change(paths, tags, action); }); panel.Refresh(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("tags.saveFailed")); }
            finally { fileToolsRunning--; }
        }
        void FilterTags()
        {
            string value = Ask(L.Text("tags.filter"), L.Text("tags.filterPrompt"), "");
            if (value != null) active.SetQuickFilter("#" + value.Trim().TrimStart('#'));
        }
    }
}

