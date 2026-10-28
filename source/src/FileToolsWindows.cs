using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace FileCompass
{
    public sealed class FileSearchWindow : Window
    {
        readonly ObservableCollection<SearchHit> hits = new ObservableCollection<SearchHit>();
        CancellationTokenSource cancellation; internal MetadataSearchEditor MetadataEditor; internal Func<FileSearchOptions> ReadSearchOptions;
        public FileSearchWindow(Window owner, string root, bool local, bool selectedAvailable, Func<FileSearchOptions, CancellationToken, Action<SearchHit>, Action<string>, int> search, Action<SearchHit> open, Action<string[], string> feedToPanel = null, Action<string, string> compareTwo = null)
        {
            CopyUi.Style(this, owner, L.Text("search.filesTitle"), 880); Height = 720; MinWidth = 760; MinHeight = 660; SizeToContent = SizeToContent.Manual; ResizeMode = ResizeMode.CanResize;
            StackPanel footerActions;var body=SettingsUx.Frame(this,L.Text("search.filesTitle"),out footerActions);
            var top = new DockPanel{Margin=new Thickness(12,6,12,0)}; DockPanel.SetDock(top, Dock.Top); body.Children.Add(top);
            var controls = new StackPanel { Width = 120, Margin = new Thickness(8, 24, 0, 0) }; DockPanel.SetDock(controls, Dock.Right); top.Children.Add(controls);
            var tabs = new TabControl(); top.Children.Add(tabs); var commonPage = new StackPanel { Margin = new Thickness(8) }; var common = FluentTheme.Section(commonPage, L.Text("search.nameAndLocationsGroup")); tabs.Items.Add(new TabItem { Header = L.Text("tab.general"), Content = commonPage });
            common.Children.Add(CopyUi.Label(L.Text("search.fileMaskLabel"))); var mask = new TextBox { Text = "*.*" }; common.Children.Add(mask);
            common.Children.Add(CopyUi.Label(L.Text("search.multipleRootsLabel"))); var path = new TextBox { Text = root, AcceptsReturn = true, Height = 48, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; common.Children.Add(path);
            var regex = new CheckBox { Content = L.Text("search.nameRegex"), Margin = new Thickness(0, 7, 0, 4) }; common.Children.Add(regex);
            var selected = new CheckBox { Content = L.Text("search.selectionOnly"), IsEnabled = selectedAvailable, Margin = new Thickness(0, 4, 0, 4) }; common.Children.Add(selected);
            var depth = new ComboBox { SelectedIndex = 0, Margin = new Thickness(0, 4, 0, 4) }; foreach (string text in new[] { L.Text("search.depthUnlimited"), L.Text("search.depthCurrentFolder"), L.Text("search.depthOneLevel"), L.Text("search.depthTwoLevels") }) depth.Items.Add(text); common.Children.Add(depth);
            var contents = FluentTheme.Section(commonPage, L.Text("search.contentsGroup"));
            var textCheck = new CheckBox { Content = L.Text("search.findText"), IsEnabled = local, Margin = new Thickness(0, 7, 0, 4) }; contents.Children.Add(textCheck);
            var textInput = new TextBox { IsEnabled = false }; contents.Children.Add(textInput); textCheck.Checked += delegate { textInput.IsEnabled = true; }; textCheck.Unchecked += delegate { textInput.IsEnabled = false; };
            var textRegex = new CheckBox { Content = L.Text("search.textRegex"), IsEnabled = local, Margin = new Thickness(0, 4, 0, 4) }; contents.Children.Add(textRegex);
            var sensitive = new CheckBox { Content = L.Text("search.caseSensitive"), Margin = new Thickness(0, 4, 0, 4) }; contents.Children.Add(sensitive);
            var advancedPage = new StackPanel { Margin = new Thickness(8) }; var advanced = FluentTheme.Section(advancedPage, L.Text("search.fileSizesGroup")); tabs.Items.Add(new TabItem { Header = L.Text("tab.advanced"), Content = advancedPage });
            advanced.Children.Add(CopyUi.Label(L.Text("search.minimumSizeBytesLabel"))); var minimum = new TextBox { Text = "0" }; advanced.Children.Add(minimum);
            advanced.Children.Add(CopyUi.Label(L.Text("search.maximumSizeBytesLabel"))); var maximum = new TextBox(); advanced.Children.Add(maximum);
            var dates = FluentTheme.Section(advancedPage,L.Text("search.modifiedDateRangeLabel"));
            dates.Children.Add(CopyUi.Label("Od:")); var dateFrom = new TextBox(); dates.Children.Add(dateFrom); dates.Children.Add(CopyUi.Label("Do:")); var dateTo = new TextBox(); dates.Children.Add(dateTo);
            var attributes = FluentTheme.Section(advancedPage,L.Text("search.attributesAndDuplicatesGroup")); var attributeControls = new System.Collections.Generic.List<CheckBox>();
            foreach (var pair in new[] { new { Name = L.Text("attribute.readOnly"), Value = FileAttributes.ReadOnly }, new { Name = L.Text("attribute.hidden"), Value = FileAttributes.Hidden },new { Name = L.Text("attribute.system"), Value = FileAttributes.System },new { Name = L.Text("attribute.archive"), Value = FileAttributes.Archive } }) {
                var check = new CheckBox { Content = pair.Name + L.Text("search.unchangedAttributeMeansAny"),IsThreeState = true,IsChecked = null,IsEnabled = local,Tag = pair.Value,Margin = new Thickness(0,4,0,4) }; attributes.Children.Add(check); attributeControls.Add(check);
            }
            var duplicates = new CheckBox { Content = L.Text("search.duplicateContentSha256"),IsEnabled = local,Margin = new Thickness(0,5,0,4) }; attributes.Children.Add(duplicates);
            var encoding = new ComboBox { ItemsSource = new[] { "UTF-8 / podle BOM","Windows-1250","UTF-16 LE","UTF-16 BE" },SelectedIndex = 0,IsEnabled = local }; contents.Children.Add(encoding);
            contents.Children.Add(CopyUi.Label(L.Text("search.textLimitMbLabel"))); var textLimit = new TextBox { Text = "0",IsEnabled = local }; contents.Children.Add(textLimit);
            var documentText=new CheckBox{Content=L.Text("tools1.searchDocuments"),IsEnabled=local,Margin=new Thickness(0,5,0,5)};contents.Children.Add(documentText);
            advancedPage.Children.Add(CopyUi.Label(L.Text("search.resourceLimitsHelp")));
            var metadataEditor = MetadataEditor = new MetadataSearchEditor(local,delegate(string pattern){mask.Text=pattern;}); tabs.Items.Add(new TabItem{Header=L.Text("metaSearch.tab"),Content=metadataEditor}); Func<FileSearchOptions> readOptions = ReadSearchOptions = delegate {
                var value = new FileSearchOptions { MetadataConditions=metadataEditor.Read(), Root = path.Text, Mask = mask.Text,Regex = regex.IsChecked == true,SelectedOnly = selected.IsChecked == true,Depth = depth.SelectedIndex == 0 ? Int32.MaxValue : depth.SelectedIndex - 1,
                    Text = textCheck.IsChecked == true ? textInput.Text : null,DocumentText=local&&documentText.IsChecked==true,TextRegex = textRegex.IsChecked == true,CaseSensitive = sensitive.IsChecked == true,Minimum = Int64.Parse(minimum.Text),Maximum = String.IsNullOrWhiteSpace(maximum.Text) ? Int64.MaxValue : Int64.Parse(maximum.Text),
                    ModifiedFrom = String.IsNullOrWhiteSpace(dateFrom.Text) ? (DateTime?)null : DateTime.ParseExact(dateFrom.Text,"dd.MM.yyyy HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture),ModifiedTo = String.IsNullOrWhiteSpace(dateTo.Text) ? (DateTime?)null : DateTime.ParseExact(dateTo.Text,"dd.MM.yyyy HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture),
                    EncodingCodePage = new[] { 65001,1250,1200,1201 }[encoding.SelectedIndex],MaximumTextBytes = checked(Int64.Parse(textLimit.Text) * 1048576),Duplicates = local && duplicates.IsChecked == true };
                if (local) foreach (var input in attributeControls) { if (input.IsChecked == true) value.RequiredAttributes |= (FileAttributes)input.Tag; else if (input.IsChecked == false) value.ExcludedAttributes |= (FileAttributes)input.Tag; }
                if (value.Minimum < 0 || value.Maximum < value.Minimum || value.MaximumTextBytes < 0 || (value.ModifiedFrom.HasValue && value.ModifiedTo.HasValue && value.ModifiedFrom > value.ModifiedTo)) throw new IOException(L.Text("search.invalidDepth"));
                MetadataSearch.Validate(value); if(!local&&MetadataSearch.Enabled(value))throw new IOException(L.Text("metaSearch.localOnly")); return value;
            };
            var profilesPage = new StackPanel { Margin = new Thickness(8) }; var profiles = FluentTheme.Section(profilesPage,L.Text("search.savedProfilesTitle")); tabs.Items.Add(new TabItem { Header = L.Text("search.profilesTab"),Content = profilesPage });
            var profileList = new ComboBox(); profiles.Children.Add(profileList); var profileName = new TextBox { ToolTip = L.Text("search.profileNameLabel") }; profiles.Children.Add(CopyUi.Label(L.Text("common.nameLabel"))); profiles.Children.Add(profileName);
            Action loadProfiles = delegate { try { profileList.ItemsSource = SearchAdvanced.LoadProfiles(); } catch (Exception ex) { profileList.ToolTip = ex.Message; } }; loadProfiles();
            profiles.Children.Add(CopyUi.Button(L.Text("action.saveSettings"),delegate { try { if (String.IsNullOrWhiteSpace(profileName.Text)) throw new IOException(L.Text("search.profileNameRequired")); var values = SearchAdvanced.LoadProfiles(); values.RemoveAll(x => String.Equals(x.Name,profileName.Text,StringComparison.OrdinalIgnoreCase)); values.Add(new SearchProfile { Name = profileName.Text,Options = readOptions() }); SearchAdvanced.SaveProfiles(values); loadProfiles(); } catch (Exception ex) { MessageBox.Show(this,ex.Message,L.Text("search.profileTitle")); } }));
            profiles.Children.Add(CopyUi.Button(L.Text("search.loadProfile"),delegate { var item = profileList.SelectedItem as SearchProfile; if (item == null) return; var value = item.Options; if(!local&&MetadataSearch.Enabled(value)){MessageBox.Show(this,L.Text("metaSearch.localOnly"),L.Text("search.profileTitle"));return;} metadataEditor.Restore(value.MetadataConditions); profileName.Text = item.Name; path.Text = value.Root; mask.Text = value.Mask; regex.IsChecked = value.Regex; selected.IsChecked = selectedAvailable && value.SelectedOnly; depth.SelectedIndex = value.Depth == Int32.MaxValue ? 0 : Math.Min(3,value.Depth + 1); textCheck.IsChecked = local && !String.IsNullOrEmpty(value.Text); textInput.Text = value.Text ?? ""; textRegex.IsChecked = value.TextRegex; documentText.IsChecked=local&&value.DocumentText; sensitive.IsChecked = value.CaseSensitive; minimum.Text = value.Minimum.ToString(); maximum.Text = value.Maximum == Int64.MaxValue ? "" : value.Maximum.ToString(); dateFrom.Text = value.ModifiedFrom.HasValue ? value.ModifiedFrom.Value.ToString("dd.MM.yyyy HH:mm:ss") : ""; dateTo.Text = value.ModifiedTo.HasValue ? value.ModifiedTo.Value.ToString("dd.MM.yyyy HH:mm:ss") : ""; encoding.SelectedIndex = Math.Max(0,Array.IndexOf(new[] { 65001,1250,1200,1201 },value.EncodingCodePage)); textLimit.Text = (value.MaximumTextBytes / 1048576).ToString(); duplicates.IsChecked = local && value.Duplicates; foreach (var input in attributeControls) input.IsChecked = (value.RequiredAttributes & (FileAttributes)input.Tag) != 0 ? (bool?)true : (value.ExcludedAttributes & (FileAttributes)input.Tag) != 0 ? (bool?)false : null; tabs.SelectedIndex = 0; }));
            profiles.Children.Add(CopyUi.Button(L.Text("search.deleteProfile"),delegate { var item = profileList.SelectedItem as SearchProfile; if (item == null) return; try { var values = SearchAdvanced.LoadProfiles(); values.RemoveAll(x => x.Name == item.Name); SearchAdvanced.SaveProfiles(values); loadProfiles(); } catch (Exception ex) { MessageBox.Show(this,ex.Message,L.Text("search.profileTitle")); } }));
            var captionFactory=new FrameworkElementFactory(typeof(TextBlock));captionFactory.SetBinding(TextBlock.TextProperty,new Binding());captionFactory.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);var checkCaption=new DataTemplate{VisualTree=captionFactory};foreach(var option in new[]{regex,selected,textCheck,textRegex,sensitive,duplicates,documentText}.Concat(attributeControls)){option.ContentTemplate=checkCaption;option.HorizontalContentAlignment=HorizontalAlignment.Stretch;}
            // Keep form pages compact in columns; do not hide controls behind a page scrollbar.
            var commonBox=(GroupBox)common.Parent;var contentsBox=(GroupBox)contents.Parent;commonPage.Children.Clear();var commonGrid=new Grid();commonGrid.ColumnDefinitions.Add(new ColumnDefinition());commonGrid.ColumnDefinitions.Add(new ColumnDefinition());commonBox.Margin=new Thickness(0,0,6,0);contentsBox.Margin=new Thickness(6,0,0,0);commonGrid.Children.Add(commonBox);Grid.SetColumn(contentsBox,1);commonGrid.Children.Add(contentsBox);commonPage.Children.Add(commonGrid);
            advancedPage.Children.Clear();var advancedGrid=new Grid();for(int col=0;col<3;col++)advancedGrid.ColumnDefinitions.Add(new ColumnDefinition());int columnIndex=0;foreach(var box in new[]{(GroupBox)advanced.Parent,(GroupBox)dates.Parent,(GroupBox)attributes.Parent}){box.Margin=new Thickness(columnIndex==0?0:5,0,columnIndex==2?0:5,0);Grid.SetColumn(box,columnIndex++);advancedGrid.Children.Add(box);}advancedPage.Children.Add(advancedGrid);advancedPage.Children.Add(CopyUi.Label(L.Text("search.resourceLimitsHelp")));

            var status = CopyUi.Label(L.Text("state.ready"));status.Margin=new Thickness(16,4,16,8); DockPanel.SetDock(status, Dock.Bottom); body.Children.Add(status);
            var list = new ListView { ItemsSource = hits, Margin = new Thickness(16,8,16,10), SelectionMode = SelectionMode.Extended }; var columns = new GridView();
            columns.Columns.Add(new GridViewColumn { Header = L.Text("column.name"), Width = 190, DisplayMemberBinding = new Binding("Name") }); columns.Columns.Add(new GridViewColumn { Header = L.Text("column.path"), Width = 410, DisplayMemberBinding = new Binding("Path") }); columns.Columns.Add(new GridViewColumn { Header = L.Text("column.size"), Width = 95, DisplayMemberBinding = new Binding("Size") }); list.View = columns; body.Children.Add(list); var metadataColumn = new GridViewColumn{Header=L.Text("metaSearch.tab"),Width=0,DisplayMemberBinding=new Binding("Metadata")}; columns.Columns.Insert(1,metadataColumn);
            columns.Columns.Add(new GridViewColumn { Header = L.Text("search.duplicatesTab"),Width = 100,DisplayMemberBinding = new Binding("DuplicateGroup") });
            columns.Columns.Add(new GridViewColumn{Header=L.Text("tools1.matchLocation"),Width=130,DisplayMemberBinding=new Binding("MatchLocation")});
            columns.Columns.Add(new GridViewColumn{Header=L.Text("tools1.snippet"),Width=300,DisplayMemberBinding=new Binding("Snippet")});
            list.MouseDoubleClick += delegate { var hit = list.SelectedItem as SearchHit; if (hit != null) open(hit); };
            Button start = null, feedButton = null, compareButton = null;
            Action updateResultButtons = delegate
            {
                if (feedButton != null) feedButton.IsEnabled = local && hits.Count > 0 && cancellation == null;
                if (compareButton != null)
                {
                    int selFiles = list.SelectedItems.Cast<SearchHit>().Count(x => !x.Directory);
                    int allFiles = hits.Count(x => !x.Directory);
                    compareButton.IsEnabled = local && cancellation == null && (selFiles == 2 || (selFiles == 0 && allFiles == 2));
                }
            };
            list.SelectionChanged += delegate { updateResultButtons(); };
            start = CopyUi.Button(L.Text("action.search"), async delegate
            {
                FileSearchOptions options;
                try
                {
                    options = readOptions();
                    if (options.Minimum < 0 || options.Maximum < options.Minimum) throw new IOException(L.Text("search.invalidSizeRange"));
                    FileSearchEngine.Matches("test", options);
                    if (!options.SelectedOnly && SearchAdvanced.Roots(options.Root).Length == 0) throw new IOException(L.Text("search.rootPathRequired"));
                    if (local && !options.SelectedOnly && SearchAdvanced.Roots(options.Root).Any(x => !Directory.Exists(x) && !File.Exists(x))) throw new IOException(L.Text("search.rootPathMissing"));
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("search.title")); return; }
                metadataColumn.Width=MetadataSearch.Enabled(options)?220:0; columns.Columns[0].Width=MetadataSearch.Enabled(options)?160:190; columns.Columns[2].Width=MetadataSearch.Enabled(options)?260:410; columns.Columns[3].Width=MetadataSearch.Enabled(options)?80:95; columns.Columns[4].Width=MetadataSearch.Enabled(options)?70:100; hits.Clear(); status.Text = options.Duplicates ? L.Text("search.comparingCandidatesStatus") : L.Text("search.runningStatus"); start.IsEnabled = false; tabs.IsEnabled = false; updateResultButtons(); int warnings = 0; string warningText = "";
                cancellation = new CancellationTokenSource(); var token = cancellation.Token;
                var found = new Progress<SearchHit>(delegate(SearchHit hit) { hits.Add(hit); status.Text = L.Text("search.foundCountPrefix") + hits.Count; });
                var inspectionClock=System.Diagnostics.Stopwatch.StartNew(); long lastInspection=-1000; var inspecting=new Progress<string>(delegate(string current){if(cancellation!=null){status.Text=L.Text("metaSearch.inspecting")+current;status.ToolTip=current;}}); options.MetadataInspecting=delegate(string current){if(inspectionClock.ElapsedMilliseconds-lastInspection<150)return;lastInspection=inspectionClock.ElapsedMilliseconds;((IProgress<string>)inspecting).Report(current);}; var warn = new Progress<string>(delegate(string warning) { warnings++; if (warnings < 11) warningText += warning + "\n"; });
                try { int count = await Task.Run(delegate { return search(options, token, ((IProgress<SearchHit>)found).Report, ((IProgress<string>)warn).Report); }); status.Text = L.Text("search.completedCountPrefix") + count + (count >= 20000 ? L.Text("search.resultLimitReachedSuffix") : "") + L.Text("search.warningPrefix") + warnings; status.ToolTip = warningText; }
                catch (OperationCanceledException) { status.Text = L.Text("search.cancelledCountPrefix") + hits.Count; }
                catch (Exception ex) { status.Text = L.Text("search.failedPrefix") + ex.Message; status.ToolTip = ex.ToString(); }
                finally { cancellation.Dispose(); cancellation = null; start.IsEnabled = true; tabs.IsEnabled = true; updateResultButtons(); }
            }); start.IsDefault = true; controls.Children.Add(start);
            if (feedToPanel != null)
            {
                feedButton = CopyUi.Button(L.Text("search.feedToPanel"), delegate
                {
                    var items = list.SelectedItems.Count > 1 ? list.SelectedItems.Cast<SearchHit>().ToArray() : hits.ToArray();
                    if (items.Length == 0) return;
                    string queryLabel = mask.Text;
                    if (textCheck.IsChecked == true && !String.IsNullOrWhiteSpace(textInput.Text)) queryLabel += " (" + textInput.Text.Trim() + ")";
                    feedToPanel(items.Select(x => x.Path).ToArray(), queryLabel);
                    Close();
                });
                feedButton.ToolTip = L.Text("search.feedToPanelHelp");
                feedButton.IsEnabled = false;
                controls.Children.Add(feedButton);
            }
            if (compareTwo != null)
            {
                compareButton = CopyUi.Button(L.Text("search.compareSelectedTwo"), delegate
                {
                    var files = list.SelectedItems.Cast<SearchHit>().Where(x => !x.Directory).ToArray();
                    if (files.Length != 2) files = hits.Where(x => !x.Directory).Take(3).ToArray();
                    if (files.Length == 2) compareTwo(files[0].Path, files[1].Path);
                });
                compareButton.IsEnabled = false;
                controls.Children.Add(compareButton);
            }
            controls.Children.Add(CopyUi.Button(L.Text("action.cancel"), delegate { if (cancellation != null) cancellation.Cancel(); else Close(); }));
            controls.Children.Add(CopyUi.Button(L.Text("action.help"), delegate { HelpService.Open(this,"search"); }));
            ToolDock.Attach(this,controls,delegate{return new DockStatus{Title=Title,Icon="⌕",Description=status.Text,Busy=cancellation!=null};});
            Closing += delegate { if (cancellation != null) cancellation.Cancel(); };
        }
    }
    public sealed class BatchRenameWindow : Window
    {
        internal UndoReceipt[] LastUndoReceipts;
        internal Task PreviewTask { get; private set; }
        internal int PreviewGeneration { get; private set; }
        public BatchRenameWindow(Window owner, string[] paths, Action refresh, Action<bool> activity = null, string initialMask = null, Action<RenameRow[]> committed = null)
        {
            CopyUi.Style(this, owner, L.Text("rename.batchTitle"), 1440); Height = 760; MinWidth = 980; MinHeight = 510; SizeToContent = SizeToContent.Manual; ResizeMode = ResizeMode.CanResize;
            var body = new DockPanel { Margin = new Thickness(12) }; CopyUi.SectionedSurface(this,body);DialogGeometry.Bound(this,1440,760,900,510);
            var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(heading, Dock.Top); body.Children.Add(heading);
            heading.Children.Add(new TextBlock { Text = L.Text("rename.batchDialogTitle"), FontSize = 16, FontWeight = FontWeights.SemiBold });
            heading.Children.Add(new TextBlock { Text = L.Text("rename.previewInstructions"), Foreground = FluentTheme.Muted, Margin = new Thickness(0, 4, 0, 0) });
            var inputs = new Grid(); foreach (double weight in new[] { 2.5, 1.2, 3.0, 1.9 }) inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) }); DockPanel.SetDock(inputs, Dock.Top); body.Children.Add(inputs);
            Func<int, string, StackPanel> group = delegate(int column, string title) { var panel = new StackPanel(); var box = new GroupBox { Header = title, Content = panel, Margin = new Thickness(0, 0, column == 3 ? 0 : 8, 0) }; Grid.SetColumn(box, column); inputs.Children.Add(box); return panel; };
            var names = group(0, L.Text("rename.fileNameLabel")); var name = new TextBox { Text = initialMask ?? "[N]", Margin = new Thickness(0, 0, 0, 6) }; names.Children.Add(name);TokenDropdown.Attach(name,new[]{"[N]","[E]","[C]","[RMD]","[hms]","[N1-5]"}.Concat(FileRenameFields.Tokens).Concat(RenameMetadata.Tokens));
            RenameHistory.Attach(names,name,()=>RenameHistory.Load().Names);
            var insert = new UniformGrid { Columns = 2 }; names.Children.Add(insert);
            foreach (string token in new[] { L.Text("rename.tokenName"), L.Text("rename.tokenDate"), L.Text("rename.tokenSubstring"), L.Text("rename.tokenTime"), L.Text("rename.tokenCounter") }) { string value = token.Split(' ')[0]; var tokenButton = CopyUi.Button(token, delegate { name.SelectedText = value; name.CaretIndex += value.Length; name.Focus(); }); tokenButton.Height = 26; tokenButton.MinWidth = 0; tokenButton.Padding = new Thickness(4, 0, 4, 0); insert.Children.Add(tokenButton); }
            var metadataPreset = new ComboBox { ItemsSource = new[] { L.Text("rename.metadataPresets"), L.Text("rename.photoPreset"), L.Text("rename.musicPreset"), L.Text("fileFields.createdPreset"), L.Text("fileFields.modifiedPreset"), L.Text("fileFields.accessedPreset") }, SelectedIndex = 0, Margin = new Thickness(0, 5, 0, 0) };
            metadataPreset.SelectionChanged += delegate { if (metadataPreset.SelectedIndex == 1) name.Text = "[EXIF.Date]_[EXIF.Time]_[C]"; else if (metadataPreset.SelectedIndex == 2) name.Text = "[MP3.Artist] - [MP3.Track] - [MP3.Title]";else if(metadataPreset.SelectedIndex==3)name.Text="[File.Created:yyyyMMdd_HHmmss]_[N]";else if(metadataPreset.SelectedIndex==4)name.Text="[File.Modified:yyyyMMdd_HHmmss]_[N]";else if(metadataPreset.SelectedIndex==5)name.Text="[File.Accessed:yyyyMMdd_HHmmss]_[N]"; }; names.Children.Add(metadataPreset);
            names.Children.Add(new TextBlock { Text = L.Text("fileFields.help"), TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = FluentTheme.Muted, Margin = new Thickness(0, 5, 0, 0) });
            var extensions = group(1, L.Text("column.extension")); var ext = new TextBox { Text = "[E]" }; extensions.Children.Add(ext); extensions.Children.Add(CopyUi.Button(L.Text("rename.tokenExtension"), delegate { ext.Text = "[E]"; })); extensions.Children.Add(CopyUi.Button(L.Text("rename.tokenCounter"), delegate { ext.Text = "[C]"; }));
            var replacements = group(2, L.Text("rename.findReplaceGroup")); replacements.Children.Add(CopyUi.Label(L.Text("rename.findLabel"))); var find = new TextBox(); replacements.Children.Add(find); replacements.Children.Add(CopyUi.Label(L.Text("rename.replaceWithLabel"))); var replace = new TextBox(); replacements.Children.Add(replace);
            RenameHistory.Attach(replacements,find,()=>RenameHistory.Load().Find); RenameHistory.Attach(replacements,replace,()=>RenameHistory.Load().Replace);
            var switches = new StackPanel { Orientation = Orientation.Horizontal }; replacements.Children.Add(switches);
            var sensitive = new CheckBox { Content = "Aa", Margin = new Thickness(2) }; switches.Children.Add(sensitive); var includeExt = new CheckBox { Content = "[E]", IsChecked = true, Margin = new Thickness(3) }; switches.Children.Add(includeExt); var regex = new CheckBox { Content = L.Text("rename.regex"), Margin = new Thickness(3) }; switches.Children.Add(regex);
            var casing = new ComboBox { SelectedIndex = 0 }; foreach (string caption in new[] { L.Text("attributes.unchanged"), L.Text("rename.upperCase"), L.Text("rename.lowerCase"), L.Text("rename.titleCase") }) casing.Items.Add(caption); replacements.Children.Add(casing);
            var counters = group(3, L.Text("rename.counterGroup")); counters.Children.Add(CopyUi.Label(L.Text("rename.counterStartLabel"))); var first = new TextBox { Text = "1" }; counters.Children.Add(new NumberSpinner(first, 0, Int32.MaxValue)); counters.Children.Add(CopyUi.Label(L.Text("rename.counterStepLabel"))); var step = new TextBox { Text = "1" }; counters.Children.Add(new NumberSpinner(step, 1, Int32.MaxValue)); counters.Children.Add(CopyUi.Label(L.Text("rename.counterDigitsLabel"))); var digits = new TextBox { Text = "1" }; counters.Children.Add(new NumberSpinner(digits, 1, 12));
            var bottom = new DockPanel { Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(bottom, Dock.Bottom); body.Children.Add(bottom);
            var buttons = FluentTheme.Actions(); buttons.Margin = new Thickness(0); DockPanel.SetDock(buttons, Dock.Right); bottom.Children.Add(buttons);
            var secondary = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(secondary, Dock.Left); bottom.Children.Add(secondary);
            var previewHeading = new DockPanel { Margin = new Thickness(0, 14, 0, 8) }; DockPanel.SetDock(previewHeading, Dock.Top); body.Children.Add(previewHeading);
            var status = new TextBlock { Text = L.Text("rename.previewGroup"), Foreground = FluentTheme.Muted, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }; DockPanel.SetDock(status, Dock.Right); previewHeading.Children.Add(status);
            previewHeading.Children.Add(new TextBlock { Text = L.Text("rename.previewTitle"), FontWeight = FontWeights.SemiBold });
            var list = new ListView(); var view = new GridView(); view.Columns.Add(new GridViewColumn { Header = L.Text("rename.originalNameColumn"), Width = 340, DisplayMemberBinding = new Binding("OldName") });
            var newName = new FrameworkElementFactory(typeof(TextBlock)); newName.SetBinding(TextBlock.TextProperty, new Binding("NewName")); newName.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            var changedStyle = new Style(typeof(TextBlock)); var changed = new DataTrigger { Binding = new Binding("Changed"), Value = true }; changed.Setters.Add(new Setter(TextBlock.ForegroundProperty, FluentTheme.Accent)); changed.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold)); changedStyle.Triggers.Add(changed); newName.SetValue(FrameworkElement.StyleProperty, changedStyle);
            view.Columns.Add(new GridViewColumn { Header = L.Text("rename.newNameColumn"), Width = 400, CellTemplate = new DataTemplate { VisualTree = newName } }); view.Columns.Add(new GridViewColumn { Header = L.Text("column.state"), Width = 160, DisplayMemberBinding = L.Binding("Status") }); list.View = view; body.Children.Add(list);
            list.SizeChanged += delegate { if (list.ActualWidth > 300) { view.Columns[0].Width = (list.ActualWidth - 170) * .46; view.Columns[1].Width = (list.ActualWidth - 170) * .54; } };
            RenameRow[] preview = null, undo = null; bool running = false; CancellationTokenSource cancel = null;
            Button start = null, back = null;
            CancellationTokenSource previewCancellation = null; bool closed = false;
            var previewGate = new SemaphoreSlim(1, 1);
            Func<Task> calculate = async delegate
            {
                if (running || closed) return;
                if (previewCancellation != null) previewCancellation.Cancel();
                var request = new CancellationTokenSource(); previewCancellation = request;
                var token = request.Token; int generation = ++PreviewGeneration;
                preview = null; if (start != null) start.IsEnabled = false;
                status.Text = L.Text("rename.preparingPreview");
                // Snapshot controls on UI; metadata and regex work belong to the worker.
                var selectedPaths = (string[])paths.Clone();
                string firstValue = first.Text, stepValue = step.Text, digitsValue = digits.Text;
                var options = new RenameOptions { Name = name.Text, Extension = ext.Text, Find = find.Text, Replace = replace.Text, Regex = regex.IsChecked == true, CaseSensitive = sensitive.IsChecked == true, IncludeExtension = includeExt.IsChecked == true, LetterCase = casing.SelectedIndex };
                try
                {
                    await Task.Delay(250, token);
                    await previewGate.WaitAsync(token);
                    RenameRow[] rows;
                    try { rows = await Task.Run(delegate { options.Start = Int32.Parse(firstValue); options.Step = Int32.Parse(stepValue); options.Digits = Int32.Parse(digitsValue); return BatchRenameEngine.Preview(selectedPaths, options, token); }, token); }
                    finally { previewGate.Release(); }
                    if (closed || generation != PreviewGeneration || token.IsCancellationRequested) return;
                    var shown=list.ItemsSource as RenameRow[];
                    if(shown!=null&&shown.Length==rows.Length&&shown.Select(x=>x.Path).SequenceEqual(rows.Select(x=>x.Path),StringComparer.OrdinalIgnoreCase)){for(int i=0;i<rows.Length;i++)shown[i].Update(rows[i]);preview=shown;}
                    else {preview=rows;list.ItemsSource=rows;} int count = rows.Count(x => x.Changed);
                    status.Text = rows.Length + L.Text("rename.itemCountSeparator") + count + L.Text("rename.changeCountSuffix"); status.Foreground = FluentTheme.Muted; status.ToolTip = null;
                    if (start != null) start.IsEnabled = count > 0;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { if (!closed && generation == PreviewGeneration) { preview = null; status.Text = ex.Message; status.Foreground = System.Windows.Media.Brushes.Firebrick; status.ToolTip = ex.Message; if (start != null) start.IsEnabled = false; } }
                finally { if (previewCancellation == request) previewCancellation = null; request.Dispose(); }
            };
            Action update = delegate
            {
                if (running || closed) return;
                PreviewGeneration++; if (previewCancellation != null) previewCancellation.Cancel();
                preview = null; if (start != null) start.IsEnabled = false;
                PreviewTask = Dispatcher.InvokeAsync(calculate).Task.Unwrap();
            };
            Func<RenameRow[], Task> apply = async delegate(RenameRow[] rows)
            {
                PreviewGeneration++; if (previewCancellation != null) previewCancellation.Cancel(); preview = null;
                RenameHistory.Remember(name.Text,find.Text,replace.Text);
                running = true; if (activity != null) activity(true); inputs.IsEnabled = false; start.IsEnabled = false; back.IsEnabled = false; cancel = new CancellationTokenSource();
                try { LastUndoReceipts=await Task.Run(delegate {var proofRows=rows.Where(x=>x.Changed).ToArray();var proofs=proofRows.Select(x=>UndoReceipt.Capture(x.Path,x.Path,true)).ToArray();BatchRenameEngine.Apply(rows,cancel.Token);return proofs.Select((proof,index)=>UndoReceipt.Committed(proof,proofRows[index].NewPath)).Where(x=>x!=null).ToArray();});if(committed!=null)committed(rows); undo = rows.Select(x => new RenameRow { Path = x.NewPath, OldName = x.NewName, NewName = x.OldName }).ToArray(); paths = rows.Select(x => x.NewPath).ToArray(); refresh(); status.Text = L.Text("rename.completedPrefix") + rows.Length + L.Text("common.itemCountSuffix"); list.ItemsSource = rows; }
                catch (Exception ex) { status.Text = ex is OperationCanceledException ? L.Text("rename.cancelledRolledBack") : L.Code("error.messagePrefix") + ex.Message; MessageBox.Show(this, status.Text, L.Text("rename.operationTitle")); }
                finally { running = false; if (activity != null) activity(false); inputs.IsEnabled = true; cancel.Dispose(); cancel = null; back.IsEnabled = undo != null; update(); }
            };
            start = CopyUi.Button(L.Text("action.start"), async delegate { if (!running && preview != null && preview.Any(x => x.Changed)) await apply(preview); }); start.IsEnabled = false; start.IsDefault = true; start.Width = 108; buttons.Children.Add(start);
            back = CopyUi.Button(L.Text("rename.undoLastChange"), async delegate { if (undo != null) await apply(undo); }); back.IsEnabled = false; secondary.Children.Add(back);
            secondary.Children.Add(CopyUi.Button(L.Text("rename.refreshPreview"), update)); var close = CopyUi.Button(L.Text("action.close"), delegate { if (cancel != null) cancel.Cancel(); else Close(); }); close.IsCancel = true; buttons.Children.Add(close);
            foreach (TextBox input in new[] { name, ext, find, replace, first, step, digits }) input.TextChanged += delegate { update(); };
            foreach (CheckBox check in new[] { regex, sensitive, includeExt }) { check.Checked += delegate { update(); }; check.Unchecked += delegate { update(); }; } casing.SelectionChanged += delegate { update(); };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (running) { cancel.Cancel(); e.Cancel = true; return; } RenameHistory.Remember(name.Text,find.Text,replace.Text); closed = true; PreviewGeneration++; if (previewCancellation != null) previewCancellation.Cancel(); }; update();
            Loaded += delegate { name.Focus(); name.SelectAll(); };
        }
    }
}
