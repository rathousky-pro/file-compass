using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    public partial class MainWindow
    {
        static string[] DefaultLabels()
        {
            return new[] { L.Text("tags.important"), L.Text("tags.todo"), L.Text("tags.done"), "Old",
                L.Text("tags.inProgress"), L.Text("tags.review"), L.Text("tags.waiting"), L.Text("tags.project"),
                L.Text("tags.archive"), L.Text("tags.personal") };
        }

        void ShowTaggedFiles()
        {
            var panel = active;
            var title = L.Text("tags.results");
            var window = new Window { MaxHeight = SystemParameters.WorkArea.Height };
            CopyUi.Style(window, this, title, 540);
            var root = SettingsUx.Frame(window, title, out var actions);
            var body = new DockPanel { Margin = new Thickness(20, 12, 20, 16) };
            root.Children.Add(body);
            var prompt = CopyUi.Label(L.Text("tags.globalPrompt"));
            DockPanel.SetDock(prompt, Dock.Top);
            body.Children.Add(prompt);
            var input = new TextBox { MinHeight = 30, Margin = new Thickness(0, 0, 0, 10) };
            System.Windows.Automation.AutomationProperties.SetLabeledBy(input, prompt);
            DockPanel.SetDock(input, Dock.Top);
            body.Children.Add(input);
            var choices = new WrapPanel();
            body.Children.Add(new ScrollViewer { Content = choices, MinHeight = 100, MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

            string query = null;
            bool exact = false;
            var cancel = CopyUi.Button(L.Text("action.cancel"), () => window.DialogResult = false);
            cancel.IsCancel = true;
            actions.Children.Add(cancel);
            var search = CopyUi.Button(L.Text("action.search"), delegate
            {
                query = input.Text.Trim().TrimStart('#');
                window.DialogResult = true;
            });
            search.IsDefault = true;
            FluentTheme.Primary(search);
            actions.Children.Add(search);
            bool closed = false;
            window.Closed += delegate { closed = true; };
            window.Loaded += async delegate
            {
                input.Focus();
                var loading = CopyUi.Label(L.Text("state.loading"));
                choices.Children.Add(loading);
                var snapshot = await Task.Run(() =>
                {
                    var records = FileTagStore.Current.Snapshot(out var colors, out var known);
                    var error = FileTagStore.LastError;
                    var assigned = new HashSet<string>(records.Where(FileTagStore.MatchesCurrentItem)
                        .SelectMany(record => record.Tags ?? new string[0]).Select(FileTagStore.DisplayName),
                        StringComparer.OrdinalIgnoreCase);
                    var names = known.Concat(colors.Keys).Concat(records.SelectMany(record => record.Tags ?? new string[0]));
                    return (Assigned: assigned, Names: names.Where(name => !String.IsNullOrWhiteSpace(name))
                        .Select(FileTagStore.DisplayName).ToArray(), Colors: colors, Error: error);
                });
                if (closed) return;
                choices.Children.Clear();
                if (snapshot.Error != null)
                {
                    choices.Children.Add(CopyUi.Label(L.Text("tags.loadError") + " " + snapshot.Error));
                    search.IsEnabled = false;
                    return;
                }
                foreach (string tag in DefaultLabels().Concat(snapshot.Names).Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(snapshot.Assigned.Contains).ThenBy(name => name, StringComparer.CurrentCultureIgnoreCase))
                {
                    bool enabled = snapshot.Assigned.Contains(tag);
                    snapshot.Colors.TryGetValue(tag, out var color);
                    var button = CopyUi.Button(tag, delegate
                    {
                        query = tag;
                        exact = true;
                        window.DialogResult = true;
                    });
                    button.IsEnabled = enabled;
                    System.Windows.Automation.AutomationProperties.SetName(button, tag);
                    var caption = new StackPanel { Orientation = Orientation.Horizontal, Opacity = enabled ? 1 : 0.55 };
                    caption.Children.Add(TagSwatch(enabled ? new FileTagBadge(tag, color).Color : FluentTheme.Muted));
                    caption.Children.Add(new TextBlock { Text = tag, VerticalAlignment = VerticalAlignment.Center,
                        FontWeight = enabled ? FontWeights.SemiBold : FontWeights.Normal });
                    button.Content = caption;
                    choices.Children.Add(button);
                }
            };
            if (window.ShowDialog() == true)
                panel.ShowProjection(new ProjectionSpec { Kind = "Tags", Query = query, ExactLabel = exact,
                    Root = panel.SavedLocalFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) });
        }

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
            var choices = new WrapPanel();
            body.Children.Add(choices);
            var snapshot = await Task.Run(() =>
            {
                var records = FileTagStore.Current.Snapshot(out var savedColors, out var known);
                var names = records.SelectMany(record => record.Tags ?? new string[0]).Concat(known).Concat(savedColors.Keys).Select(FileTagStore.DisplayName)
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).Take(20).ToArray();
                return Tuple.Create(names, savedColors);
            });
            if (windowClosed) { window.Close(); return; }
            var changedColors = new System.Collections.Generic.Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            string colorFor(string name)
            {
                return changedColors.TryGetValue(name, out var changed) ? changed :
                    snapshot.Item2.TryGetValue(name, out var saved) ? saved : null;
            }

            string[] labels = DefaultLabels().Concat(snapshot.Item1).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            body.Children.Add(new TextBlock { Text = L.Text("tags.color"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 3) });
            var selectors = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            selectors.ColumnDefinitions.Add(new ColumnDefinition());
            selectors.ColumnDefinitions.Add(new ColumnDefinition());
            var labelChoice = new ComboBox { Margin = new Thickness(0, 0, 8, 0) };
            var colorChoice = new ComboBox();
            System.Windows.Automation.AutomationProperties.SetName(labelChoice,L.Text("tags.colorLabel"));
            System.Windows.Automation.AutomationProperties.SetName(colorChoice,L.Text("tags.color"));
            selectors.Children.Add(labelChoice);
            var colorRow = new DockPanel();
            var chosenSwatch = TagSwatch(System.Windows.Media.Brushes.Transparent);
            DockPanel.SetDock(chosenSwatch,Dock.Left);
            colorRow.Children.Add(chosenSwatch);
            colorRow.Children.Add(colorChoice);
            Grid.SetColumn(colorRow,1);
            selectors.Children.Add(colorRow);
            body.Children.Add(selectors);

            foreach (var option in new[]
            {
                Tuple.Create("tags.colorAuto", (string)null), Tuple.Create("tags.colorBlue", "#326BB1"),
                Tuple.Create("tags.colorRed", "#A83252"), Tuple.Create("tags.colorOrange", "#9F560E"),
                Tuple.Create("tags.colorGreen", "#23744D"), Tuple.Create("tags.colorViolet", "#754CAB"),
                Tuple.Create("tags.colorTeal", "#287581"), Tuple.Create("tags.colorGray", "#57677E"),
                Tuple.Create("tags.colorBrown", "#86602F")
            })
            {
                colorChoice.Items.Add(new ComboBoxItem { Content = L.Text(option.Item1), Tag = option.Item2 });
            }

            var swatches = new System.Collections.Generic.Dictionary<string,Border>(StringComparer.OrdinalIgnoreCase);
            foreach (string tag in labels)
            {
                var swatch = TagSwatch(new FileTagBadge(tag, colorFor(tag)).Color);
                swatches[tag] = swatch;
                var caption = new StackPanel { Orientation = Orientation.Horizontal };
                caption.Children.Add(swatch);
                caption.Children.Add(new TextBlock { Text = tag, VerticalAlignment = VerticalAlignment.Center });
                var button = CopyUi.Button(tag, delegate
                {
                    try
                    {
                        input.Text = String.Join("; ", FileTagStore.Normalize(input.Text).Concat(new[] { tag }).Distinct(StringComparer.OrdinalIgnoreCase));
                        labelChoice.SelectedItem = labelChoice.Items.Cast<string>().FirstOrDefault(name => String.Equals(name, tag, StringComparison.OrdinalIgnoreCase));
                    }
                    catch (IOException ex) { MessageBox.Show(window,ex.Message,L.Text("tags.edit")); }
                });
                button.Content = caption;
                button.Margin = new Thickness(0, 0, 6, 6);
                choices.Children.Add(button);
            }

            bool selectingColor = false;
            void UpdateColor()
            {
                selectingColor = true;
                try
                {
                    string name = labelChoice.SelectedItem as string;
                    colorChoice.IsEnabled = labelChoice.IsEnabled = name != null;
                    string color = name == null ? null : colorFor(name);
                    chosenSwatch.Background = name == null ? System.Windows.Media.Brushes.Transparent : new FileTagBadge(name,color).Color;
                    colorChoice.SelectedItem = colorChoice.Items.OfType<ComboBoxItem>()
                        .FirstOrDefault(item => String.Equals(item.Tag as string,color,StringComparison.OrdinalIgnoreCase)) ?? colorChoice.Items[0];
                }
                finally { selectingColor = false; }
            }

            void UpdateLabels()
            {
                var previous = labelChoice.SelectedItem as string;
                string[] names;
                try { names = FileTagStore.Normalize(input.Text); }
                catch (IOException) { names = new string[0]; }
                labelChoice.ItemsSource = names;
                labelChoice.SelectedItem = names.FirstOrDefault(name => String.Equals(name,previous,StringComparison.OrdinalIgnoreCase)) ?? names.FirstOrDefault();
                UpdateColor();
            }

            labelChoice.SelectionChanged += delegate { UpdateColor(); };
            colorChoice.SelectionChanged += delegate
            {
                if (selectingColor || !(labelChoice.SelectedItem is string name) || !(colorChoice.SelectedItem is ComboBoxItem option)) return;
                changedColors[name] = option.Tag as string;
                var brush = new FileTagBadge(name,colorFor(name)).Color;
                chosenSwatch.Background = brush;
                if (swatches.TryGetValue(name,out var target)) target.Background = brush;
            };
            input.TextChanged += delegate { UpdateLabels(); };
            UpdateLabels();
            body.Children.Add(new TextBlock { Text = L.Text("tags.colorHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted });
            body.Children.Add(new TextBlock { Text = L.Text("tags.storageHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(0, 10, 0, 0) });
            if (FileTagStore.LastError != null) body.Children.Add(new TextBlock { Text = L.Text("tags.loadError") + " " + FileTagStore.LastError, TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Firebrick });
            var buttons = FluentTheme.Actions(); body.Children.Add(buttons); string[] tags = null; int action = 0;
            var save = CopyUi.Button(L.Text("action.save"), delegate { try { tags = FileTagStore.Normalize(input.Text); action = mode.SelectedIndex; window.DialogResult = true; } catch (Exception ex) { MessageBox.Show(window, ex.Message, L.Text("tags.edit")); } }); save.IsDefault = true; buttons.Children.Add(save);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { window.DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
            if (window.ShowDialog() != true) return;
            fileToolsRunning++;
            try { await Task.Run(delegate { FileTagStore.Current.Change(paths, tags, action, changedColors.Where(color => tags.Contains(color.Key,StringComparer.OrdinalIgnoreCase))
                .ToDictionary(color => color.Key,color => color.Value,StringComparer.OrdinalIgnoreCase)); });
                panel.Refresh();
                if (changedColors.Count != 0)
                {
                    var other = panel == left ? right : left;
                    if (!other.IsRemote && !other.IsArchive && !other.IsVirtualBrowser && !other.LocalLoading) other.Refresh();
                } }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("tags.saveFailed")); }
            finally { fileToolsRunning--; }
        }
        static Border TagSwatch(System.Windows.Media.Brush color)
        {
            return new Border { Background = color, BorderBrush = CompactTheme.FieldLine, BorderThickness = new Thickness(1),
                Width = 13, Height = 13, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center };
        }

        void FilterTags()
        {
            string value = Ask(L.Text("tags.filter"), L.Text("tags.filterPrompt"), "");
            if (value != null) active.SetQuickFilter("#" + value.Trim().TrimStart('#'));
        }
    }
}

