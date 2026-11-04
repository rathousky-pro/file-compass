using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FileCompass
{
    public partial class FilePanel
    {
        sealed class PanelTab
        {
            internal SavedTab Saved = new SavedTab();
            internal RemoteConnection Remote;
            internal bool Pending;
            internal string Error;
            internal ArchiveDocument Archive;
            internal string ReturnPath, Cursor;
            internal Tuple<ArchiveDocument, string>[] Parents;
            internal HashSet<string> Marks = new HashSet<string>(StringComparer.Ordinal);
            internal PanelView View = new PanelView();
            internal string[] History = new string[0], Forward = new string[0];
        }
        readonly List<PanelTab> tabs = new List<PanelTab>();
        readonly StackPanel tabStrip = new StackPanel { Orientation = Orientation.Horizontal };
        readonly DockPanel tabRow = new DockPanel { LastChildFill = true, Background = CompactTheme.PanelSurface, Height = 34 };
        DockPanel panelPathRow;
        Border activeTabFace;
        Button newTabButton,cleanRootButton,cleanParentButton;
        internal Action<FilePanel> BookmarksRequested;
        readonly StackPanel crumbs = new StackPanel { Orientation = Orientation.Horizontal };
        readonly StackPanel cleanCrumbs = new StackPanel { Orientation=Orientation.Horizontal };
        readonly ScrollViewer crumbScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, ClipToBounds = true };
        readonly TextBox quickText = new TextBox { Width = 220, MinHeight = 24, VerticalContentAlignment = VerticalAlignment.Center };
        readonly ComboBox quickMode = new ComboBox { Width = 110, MinHeight = 24 };
        readonly DockPanel quickBar = new DockPanel { LastChildFill = false, Margin = new Thickness(4, 2, 4, 2), Visibility = Visibility.Collapsed };
        readonly Popup completions = new Popup { StaysOpen = false, Placement = PlacementMode.Bottom };
        readonly ListBox completionList = new ListBox { Background = CompactTheme.WhiteSurface, MaxHeight = 210, MinWidth = 300 };
        int tabIndex, completing;
        bool switchingTabs, completingAddress;
        bool addressLookupRunning;
        public event EventHandler TabsChanged;
        void NotifyTabsChanged(){if(TabsChanged!=null)TabsChanged(this,EventArgs.Empty);}
        internal int CurrentTabIndex { get { return tabIndex; } }
        internal int TabCount { get { return EmptyPanel?0:tabs.Count; } }
        internal string SavedLocalFolder { get { if(EmptyPanel)return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);if(HasUnavailableTab)return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); if(IsExt)return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); if (!IsRemote && !IsArchive) return CurrentPath; return IsArchive ? archiveReturnPath : localReturnPath; } }
        internal void InitializeNavigation(Grid grid)
        {
            tabs.Add(new PanelTab());
            var wrapper = new DockPanel(); Child = wrapper;
            var row = tabRow;

            newTabButton=CompactTheme.Button("+",L.Text("tabs.newShortcutTooltip"),delegate{NewTab();});
            newTabButton.Tag="panel-tab";newTabButton.Width=36;newTabButton.Height=36;newTabButton.MinWidth=0;newTabButton.Padding=new Thickness(0);newTabButton.Margin=new Thickness(6,0,0,0);newTabButton.Background=CompactTheme.PanelSurface;newTabButton.BorderThickness=new Thickness(0);newTabButton.FontSize=16;
            System.Windows.Automation.AutomationProperties.SetAutomationId(newTabButton,"PanelNewTab");
            var tabScroller=new ScrollViewer { Content = tabStrip, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };tabScroller.PreviewMouseWheel+=delegate(object sender,MouseWheelEventArgs e){tabScroller.ScrollToHorizontalOffset(tabScroller.HorizontalOffset-e.Delta/3.0);e.Handled=true;};row.Children.Add(tabScroller);row.SizeChanged+=delegate(object sender,SizeChangedEventArgs e){if(e.WidthChanged)RenderTabs();};
            DockPanel.SetDock(quickBar, Dock.Bottom); wrapper.Children.Add(quickBar);
            quickMode.Items.Add(L.Text("action.filter")); quickMode.Items.Add(L.Text("action.find")); quickMode.SelectedIndex = 0;
            quickBar.Children.Add(new TextBlock { Text = L.Text("navigation.quickPrefix"), VerticalAlignment = VerticalAlignment.Center });
            quickBar.Children.Add(quickMode); quickBar.Children.Add(quickText);
            quickBar.Children.Add(CompactTheme.Button("×", L.Text("panel.clearFilterTooltip"), delegate { SetQuickFilter(""); List.Focus(); }));
            quickText.TextChanged += delegate { ApplyQuickFilter(); };
            quickMode.SelectionChanged += delegate { ApplyQuickFilter(); };
            quickText.PreviewKeyDown += delegate(object s, KeyEventArgs e) {
                if (e.Key == Key.Escape) { SetQuickFilter(""); List.Focus(); e.Handled = true; }
                if (e.Key == Key.Enter || e.Key == Key.Down) { List.Focus(); e.Handled = true; }
            };
            wrapper.Children.Add(grid);
            PathChanged += delegate { NavigationChanged(); };
            InitializeTabNotice(grid);RenderTabs();
        }
        internal void InitializeAddress(DockPanel row)
        {
            row.Children.Clear();row.Children.Add(tabRow);address.Visibility=Visibility.Collapsed;
            crumbs.Background = CompactTheme.InactivePath;
            crumbScroll.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e) { crumbScroll.ScrollToHorizontalOffset(crumbScroll.HorizontalOffset - e.Delta / 3.0); e.Handled = true; };
            address.GotKeyboardFocus += delegate { RefreshCleanNavigation(); };
            address.LostKeyboardFocus += delegate { address.Visibility=Visibility.Collapsed;tabRow.Visibility=Visibility.Visible;completions.IsOpen = false;RefreshCleanNavigation(); };
            completions.PlacementTarget = address;
            completions.Child = new Border { Background = CompactTheme.WhiteSurface, BorderBrush = CompactTheme.Line, BorderThickness = new Thickness(1), Child = completionList };
            completionList.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { var element = e.OriginalSource as DependencyObject; while (element != null && !(element is ListBoxItem)) element = VisualTreeHelper.GetParent(element); if (element is ListBoxItem) { completionList.SelectedItem = ((ListBoxItem)element).Content; AcceptCompletion(); e.Handled = true; } };
            address.TextChanged += delegate { if (address.IsKeyboardFocusWithin && !completingAddress) CompleteAddress(); };
            address.PreviewKeyDown += delegate(object s, KeyEventArgs e) {
                if (e.Key == Key.Down && completions.IsOpen) { completionList.SelectedIndex = Math.Min(completionList.SelectedIndex + 1, completionList.Items.Count - 1); e.Handled = true; }
                if (e.Key == Key.Up && completions.IsOpen) { completionList.SelectedIndex = Math.Max(completionList.SelectedIndex - 1, 0); e.Handled = true; }
                if (e.Key == Key.Enter && completions.IsOpen && completionList.SelectedIndex >= 0) { AcceptCompletion(); e.Handled = true; }
                if (e.Key == Key.Tab && completions.IsOpen && completionList.Items.Count > 0) { completionList.SelectedIndex = 0; AcceptCompletion(); e.Handled = true; }
            };
            completionList.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter || e.Key == Key.Tab) { AcceptCompletion(); e.Handled = true; } if (e.Key == Key.Escape) { completions.IsOpen = false; address.Focus(); e.Handled = true; } };
        }
        void AcceptCompletion()
        {
            var value = completionList.SelectedItem as string; if (value == null) return;
            completingAddress = true; address.Text = value; completingAddress = false; completions.IsOpen = false; address.Focus(); address.CaretIndex = address.Text.Length;
        }
        async void CompleteAddress()
        {
            int request = ++completing; string input = address.Text;
            if (IsRemote || IsArchive || IsExt || String.IsNullOrWhiteSpace(input)) return;
            await Task.Delay(180); if (request != completing || !address.IsKeyboardFocusWithin || addressLookupRunning) return;
            addressLookupRunning = true;
            string[] matches = null;
            try { matches = await Task.Run(delegate {
                string expanded = Environment.ExpandEnvironmentVariables(input);
                string parent = expanded.EndsWith("\\") ? expanded : Path.GetDirectoryName(expanded);
                string prefix = expanded.EndsWith("\\") ? "" : Path.GetFileName(expanded);
                if (String.IsNullOrEmpty(parent)) return new string[0];
                return Directory.EnumerateDirectories(parent).Where(p => Path.GetFileName(p).StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase)).Take(30).Select(p => p + "\\").ToArray();
            }); } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { } catch (NotSupportedException) { }
            addressLookupRunning = false;
            if (request != completing) { if (address.IsKeyboardFocusWithin) CompleteAddress(); return; }
            if (!address.IsKeyboardFocusWithin) return;
            completionList.ItemsSource = matches; completions.IsOpen = matches != null && matches.Length > 0;
        }
        void CaptureTab()
        {
            if (EmptyPanel || tabs.Count == 0 || CurrentPath == null || switchingTabs) return;
            var tab = tabs[tabIndex];
            if (tab.Pending) return;
            string newProfile = IsRemote ? Remote.Profile.Id : "", newArchive = IsArchive ? Archive.FilePath : "";
            if (tab.Saved.ProfileId != newProfile || tab.Saved.ArchiveFile != newArchive) { tab.Saved.Home = CurrentPath; tab.Saved.Locked = false; }
            tab.Saved.ExtSource=IsExt?ExtLocations.Get(CurrentPath).Source:null;tab.Saved.ExtVolume=IsExt?ExtLocations.Get(CurrentPath).Volume:null;
            tab.Pending = false; tab.Saved.Projection = projection; tab.Saved.Filter = quickText.Text; tab.Saved.Path = CurrentPath; tab.Saved.ProfileId = newProfile;
            tab.Saved.ArchiveFile = IsArchive ? Archive.FilePath : ""; tab.Saved.ArchivePrefix = IsArchive ? ArchivePrefix : "";
            tab.Saved.Caption = IsRemote ? Remote.Profile.DisplayLabel : IsArchive ? Path.GetFileName(Archive.FilePath) : "";
            if (String.IsNullOrEmpty(tab.Saved.Home)) tab.Saved.Home = CurrentPath;
            tab.Remote = Remote; tab.Archive = Archive; tab.ReturnPath = IsArchive ? archiveReturnPath : localReturnPath;
            tab.Parents = archiveParents.ToArray(); SavePanelView(tab.View); tab.Saved.View = tab.View;
            var cursor = List.SelectedItem as Entry; tab.Cursor = cursor == null ? null : cursor.FullPath;
            tab.Marks = new HashSet<string>(List.Items.Cast<Entry>().Where(x => x.IsMarked).Select(x => x.FullPath), StringComparer.Ordinal);
            tab.History = history.ToArray(); tab.Forward = forwardHistory.ToArray();
        }
        static SavedTab CloneSavedTab(SavedTab item)
        {
            var view = item.View ?? new PanelView();
            return new SavedTab { ExtSource=item.ExtSource,ExtVolume=item.ExtVolume,Path = item.Path, Home = item.Home, Label = item.Label, Caption = item.Caption, ProfileId = item.ProfileId, ArchiveFile = item.ArchiveFile, ArchivePrefix = item.ArchivePrefix, Locked = item.Locked, Filter = item.Filter,
                Projection = item.Projection == null ? null : new ProjectionSpec { Kind = item.Projection.Kind, Root = item.Projection.Root, Query = item.Projection.Query, Paths = item.Projection.Paths == null ? null : (string[])item.Projection.Paths.Clone() },
                View = new PanelView { MetadataColumns = view.MetadataColumns == null ? new string[0] : (string[])view.MetadataColumns.Clone(), Sort = view.Sort, Descending = view.Descending, Widths = view.Widths == null ? new double[0] : (double[])view.Widths.Clone(), Display = view.Display, Tree = view.Tree, ThumbnailSize = view.ThumbnailSize, ExtensionColumn=view.ExtensionColumn,AttributesColumn=view.AttributesColumn,SizeColumn=view.SizeColumn,ModifiedColumn=view.ModifiedColumn,ColumnOrder=view.ColumnOrder==null?new string[0]:(string[])view.ColumnOrder.Clone(),DetailExpanded = view.DetailExpanded, DetailConfigured = view.DetailConfigured, DetailRatio = view.DetailRatio, TreeWidth = view.TreeWidth } };
        }
        internal SavedTab[] SaveTabs() { if(EmptyPanel)return new SavedTab[0];CaptureTab(); return tabs.Select(x => CloneSavedTab(x.Saved)).ToArray(); }
        internal void RestoreTabs(SavedTab[] saved, int selected, bool restoreActiveFolder = true)
        {
            if (saved == null || saved.Length == 0) return;
            EmptyPanel=false;tabs.Clear(); foreach (var item in saved.Take(50)) if (item != null && !String.IsNullOrWhiteSpace(item.Path)) { var copy = CloneSavedTab(item);if(copy.ExtSource!=null&&copy.ExtVolume!=null)ExtLocations.Register(copy.ExtSource,copy.ExtVolume); tabs.Add(new PanelTab { Saved = copy, View = copy.View, Pending = true }); }
            if (tabs.Count == 0) tabs.Add(new PanelTab());
            tabIndex = Math.Max(0, Math.Min(selected, tabs.Count - 1)); RenderTabs();
            var tab = tabs[tabIndex];
            if (tab.Saved.View != null) RestorePanelView(tab.View);
            if(!restoreActiveFolder&&!tab.Saved.Locked&&String.IsNullOrEmpty(tab.Saved.ProfileId)&&String.IsNullOrEmpty(tab.Saved.ArchiveFile)){tab.Pending=false;return;}
            // Connections are reopened only on an explicit click; startup never asks for credentials.
            if ((restoreActiveFolder || tab.Saved.Locked) && String.IsNullOrEmpty(tab.Saved.ProfileId) && String.IsNullOrEmpty(tab.Saved.ArchiveFile))
            {
                string filter = tab.Saved.Filter;
                if (tab.Saved.Projection != null) ShowProjection(tab.Saved.Projection, false); else Navigate(tab.Saved.Locked?tab.Saved.Home:tab.Saved.Path, false);
                NavigationTask = RestoreTabFilter(NavigationTask, tab, filter, navigationVersion);
            }else {CancelNavigation();ShowTabUnavailable(tab,L.Text("tabs.disconnected"));}
        }
        async Task RestoreTabFilter(Task loaded, PanelTab tab, string filter, int request)
        {
            if (loaded != null) await loaded.ConfigureAwait(false);
            if (!Dispatcher.HasShutdownStarted) await Dispatcher.InvokeAsync(delegate { if (request == navigationVersion && tabs[tabIndex] == tab) SetQuickFilter(filter); });
        }
        internal void NewTab()
        {
            if (IsLoading || LocalLoading) return;
            if(EmptyPanel){EmptyPanel=false;Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),false);NotifyTabsChanged();return;}
            if(HasUnavailableTab){tabs.Add(new PanelTab());tabIndex=tabs.Count-1;Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),false);RenderTabs();NotifyTabsChanged();return;}
            CaptureTab(); var current = tabs[tabIndex];
            var fresh = new PanelTab { Saved = new SavedTab { ExtSource=current.Saved.ExtSource,ExtVolume=current.Saved.ExtVolume,Projection = projection, Path = CurrentPath, Home = CurrentPath, ProfileId = IsRemote ? Remote.Profile.Id : "", ArchiveFile = IsArchive ? Archive.FilePath : "", ArchivePrefix = IsArchive ? ArchivePrefix : "", Caption = IsRemote ? Remote.Profile.DisplayLabel : IsArchive ? Path.GetFileName(Archive.FilePath) : "" }, Remote = Remote, Archive = Archive, ReturnPath = current.ReturnPath, Parents = current.Parents };
            SavePanelView(fresh.View); fresh.Saved.View = fresh.View;
            tabs.Add(fresh); tabIndex = tabs.Count - 1; RenderTabs();NotifyTabsChanged(); activate(this); List.Focus();
        }
        internal Task TabNavigationTask { get; private set; }
        internal void SwitchTab(int index) { TabNavigationTask = SwitchTabCore(index); }
        async Task SwitchTabCore(int index)
        {
            if (index < 0 || index >= tabs.Count || IsLoading || LocalLoading) return;
            CaptureTab(); tabIndex = index; var tab = tabs[index]; switchingTabs = true;
            try {
                SetQuickFilter(""); CancelNavigation(); RememberRemote(); CloseArchiveContext(); Remote = null;
                RestorePanelView(tab.View);
                if (tab.Saved.Projection != null) { ShowProjection(tab.Saved.Projection, false); await NavigationTask; }
                else if (tab.Remote != null && openConnections.ContainsKey(tab.Remote)) {
                    Remote = tab.Remote; CurrentPath = tab.Saved.Locked ? tab.Saved.Home : tab.Saved.Path;
                    Loading(true);
                    var rows = await Task.Run(delegate { return RemoteRows(tab.Remote.Session.ListDirectory(CurrentPath), CurrentPath); });
                    ApplyRemoteRows(rows, tab.Cursor);
                } else if (!String.IsNullOrEmpty(tab.Saved.ProfileId)) {
                    var main = Window.GetWindow(this) as MainWindow;
                    if (main != null) { await main.ReopenTabTask(this, tab.Saved); if(!IsRemote||Remote.Profile.Id!=tab.Saved.ProfileId)ShowTabUnavailable(tab,L.Text("tabs.disconnected")); }else ShowTabUnavailable(tab,L.Text("tabs.disconnected"));
                } else if (tab.Archive != null) {
                    Archive = tab.Archive; ArchivePrefix = tab.Saved.Locked ? tab.Saved.Home.TrimStart('/') : tab.Saved.ArchivePrefix; archiveReturnPath = tab.ReturnPath;
                    if (tab.Parents != null) foreach (var parent in tab.Parents.Reverse()) archiveParents.Push(parent); ApplyArchiveRows();
                } else if (!String.IsNullOrEmpty(tab.Saved.ArchiveFile)) {
                    await OpenArchive(tab.Saved.ArchiveFile); if (IsArchive) { ArchivePrefix = tab.Saved.ArchivePrefix; ApplyArchiveRows(); }
                } else { Navigate(tab.Saved.Locked ? tab.Saved.Home : tab.Saved.Path, false); await NavigationTask; }
                if (CurrentPath == tab.Saved.Path) {
                    foreach (Entry item in List.Items) item.IsMarked = tab.Marks.Contains(item.FullPath);
                    var cursor = List.Items.Cast<Entry>().FirstOrDefault(x => x.FullPath == tab.Cursor); if (cursor != null) List.SelectedItem = cursor;
                }
                SetQuickFilter(tab.Saved.Filter); history.Clear(); forwardHistory.Clear(); foreach (var path in tab.History.Reverse()) history.Push(path); foreach (var path in tab.Forward.Reverse()) forwardHistory.Push(path);
            } catch (Exception ex) { ShowTabUnavailable(tab,L.Text("tabs.openFailedPrefix")+ex.Message); }
            finally { Loading(false); switchingTabs = false; RenderTabs(); ConfigureWatcher(); activate(this); List.Focus(); }
        }
        internal void CloseTab(int index)
        {
            if (EmptyPanel || IsLoading || LocalLoading || index < 0 || index >= tabs.Count) return;
            CaptureTab();if(!ConfirmRemoteTabClose(tabs[index]))return;
            if(tabs.Count==1){ClearEmptyPanel();return;}
            CaptureTab(); bool selected = index == tabIndex; tabs.RemoveAt(index);
            if (index < tabIndex) tabIndex--; else if (tabIndex >= tabs.Count) tabIndex = tabs.Count - 1;
            if(selected)switchingTabs=true;RenderTabs();NotifyTabsChanged(); if (selected) { SwitchTabWithoutCapture(tabIndex); }
        }
        void SwitchTabWithoutCapture(int index) { SwitchTab(index); }
        internal void MoveTab(int index, int direction)
        {
            int next = index + direction; if (index < 0 || index >= tabs.Count || next < 0 || next >= tabs.Count) return;
            CaptureTab(); var selected = tabs[tabIndex]; var moving = tabs[index]; tabs.RemoveAt(index); tabs.Insert(next, moving); tabIndex = tabs.IndexOf(selected); RenderTabs();NotifyTabsChanged();
        }
        internal void LockTab(int index) { CaptureTab(); var tab = tabs[index]; tab.Saved.Locked = !tab.Saved.Locked; tab.Saved.Home = tab.Saved.Path; RenderTabs();NotifyTabsChanged(); }
        void RenderTabs()
        {
            if(AppearanceTheme.IsClean){RenderCleanTabs();return;}
            tabRow.Background=AppearanceTheme.IsClean?CompactTheme.WhiteSurface:CompactTheme.PanelSurface;
            tabRow.Visibility = address.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;RefreshCleanNavigation();
            if(activeTabFace!=null){activeTabFace.Child=null;activeTabFace=null;}
            var widths=TabWidths();
            tabStrip.Children.Clear(); if(EmptyPanel)return;for (int i = 0; i < tabs.Count; i++) {
                int index = i; var saved = tabs[i].Saved;
                string name = saved.Label;
                if (String.IsNullOrEmpty(name) && saved.Projection != null) name = ProjectionFiles.Title(saved.Projection);
                if (String.IsNullOrEmpty(name)) { name = String.IsNullOrEmpty(saved.Path) ? L.Text("common.folder") : Path.GetFileName((saved.Locked&&!String.IsNullOrEmpty(saved.Home)?saved.Home:saved.Path).TrimEnd('\\', '/')); if (String.IsNullOrEmpty(name)) name = saved.Path; }
                if (tabs[i].Pending&&!String.IsNullOrEmpty(saved.ProfileId)) name += " · "+L.Text("tabs.disconnected");
                if(!String.IsNullOrEmpty(tabs[i].Error))name="⚠ "+name;
                var button = CompactTheme.Button((saved.Locked ? "🔒 " : "") + name, (String.IsNullOrEmpty(saved.Caption) ? "" : saved.Caption + "\n") + saved.Path, delegate { SwitchTab(index); });
                button.Width = widths[i]; button.MaxWidth = widths[i]; button.Height = 30; button.Padding = new Thickness(10, 1, 10, 1);
                button.Margin = new Thickness(3, 4, 0, 0); button.Background = i == tabIndex ? CompactTheme.WhiteSurface : Brushes.Transparent;
                button.Foreground = i == tabIndex ? FluentTheme.Accent : FluentTheme.Text;button.BorderBrush=CompactTheme.ThemeBrush("#A9BCD3");
                var menu = new ContextMenu();
                AddTabAction(menu, L.Text("tabs.new"), NewTab); AddTabAction(menu, L.Text("action.close"), delegate { CloseTab(index); });
                AddTabAction(menu, saved.Locked ? L.Text("action.unlock") : L.Text("tabs.lockHomeFolder"), delegate { LockTab(index); });
                AddTabAction(menu, L.Text("tabs.moveLeft"), delegate { MoveTab(index, -1); }); AddTabAction(menu, L.Text("tabs.moveRight"), delegate { MoveTab(index, 1); });
                AddTabAction(menu, L.Text("tabs.renameMenu"), delegate { string label = NavigationPrompt(L.Text("tabs.title"), L.Text("tabs.nameLabel"), saved.Label); if (label != null) { saved.Label = label; RenderTabs();NotifyTabsChanged(); } });
                var tabPath=new PathTextBlock{FullPath=TabPath(i),Prefix=TabPrefix(i),Width=Math.Max(20,widths[i]-24),Foreground=button.Foreground};button.Content=tabPath;
                button.ContextMenu = menu;
                if(i==tabIndex){
                    var fullCaption=new TextBlock{Text=TabPrefix(i)+TabPath(i),FontFamily=System.Windows.Documents.TextElement.GetFontFamily(this),FontSize=System.Windows.Documents.TextElement.GetFontSize(this)};fullCaption.Measure(new Size(Double.PositiveInfinity,30));
                    if(fullCaption.DesiredSize.Width+TabPath(i).Count(c=>c=='\\'||c=='/')*12+40>widths[i]){var compact=new DockPanel{Width=Math.Max(20,widths[i]-2)};var edit=CompactTheme.Button("✎",L.Text("navigation.editPathShortcutTooltip"),delegate{FocusAddress();});if(AppearanceTheme.IsClean)edit.Height=20;DockPanel.SetDock(edit,Dock.Right);compact.Children.Add(edit);compact.Children.Add(new PathTextBlock{FullPath=TabPath(i),Prefix=TabPrefix(i),Foreground=FluentTheme.Accent,Margin=new Thickness(5,0,2,0)});crumbScroll.Content=compact;}else crumbScroll.Content=crumbs;
                    crumbScroll.ScrollToHorizontalOffset(0);activeTabFace=new Border{Child=crumbScroll,Width=widths[i],Height=30,Margin=new Thickness(2,1,2,1),Background=CompactTheme.ActivePath,BorderBrush=FluentTheme.Accent,BorderThickness=new Thickness(AppearanceTheme.IsClean?1:2),CornerRadius=new CornerRadius(AppearanceTheme.IsClean?6:4),ToolTip=button.ToolTip,ContextMenu=menu};activeTabFace.MouseLeftButtonDown+=delegate{activate(this);};tabStrip.Children.Add(activeTabFace);}else tabStrip.Children.Add(button);
            }
            if(cleanBreadcrumbFrame!=null&&activeTabFace!=null){
                var menu=activeTabFace.ContextMenu;
                if(AppearanceTheme.IsClean){menu.Items.Add(new Separator());AddTabAction(menu,L.Text("navigation.editPathShortcutTooltip"),FocusAddress);AddTabAction(menu,L.Text("favorites.title"),delegate{if(BookmarksRequested!=null)BookmarksRequested(this);});var recent=new MenuItem{Header=L.Text("navigation.folderHistory")};foreach(string path in history.Distinct(StringComparer.OrdinalIgnoreCase).Take(20)){string captured=path;AddTabActionTo(recent,captured,delegate{Navigate(captured,true);});}recent.IsEnabled=recent.Items.Count>0;menu.Items.Add(recent);}
                cleanBreadcrumbFrame.ContextMenu=menu;
            }
        }
        static void AddTabActionTo(MenuItem parent,string title,Action action){var item=new MenuItem{Header=title};item.Click+=delegate{action();};parent.Items.Add(item);}
        string TabPath(int index){return index==tabIndex&&CurrentPath!=null?CurrentPath:tabs[index].Saved.Path??"";}
        string TabPrefix(int index){var tab=tabs[index];return (!String.IsNullOrEmpty(tab.Error)?"⚠ ":tab.Saved.Locked?"🔒 ":"")+(!String.IsNullOrEmpty(tab.Saved.ProfileId)?tab.Saved.Caption+" · ":"");}
        double[] TabWidths(){
            var natural=new double[tabs.Count];
            for(int i=0;i<tabs.Count;i++){var text=new TextBlock{Text=AppearanceTheme.IsClean?CleanTabCaption(i):TabPrefix(i)+TabPath(i),FontFamily=System.Windows.Documents.TextElement.GetFontFamily(this),FontSize=AppearanceTheme.IsClean?Math.Max(14,System.Windows.Documents.TextElement.GetFontSize(this)+1):System.Windows.Documents.TextElement.GetFontSize(this)};text.Measure(new Size(Double.PositiveInfinity,30));natural[i]=Math.Max(70,Math.Min(260,text.DesiredSize.Width+72));}
            double available=Math.Max(100,tabRow.ActualWidth-36-tabs.Count*4);if(natural.Sum()<=available)return natural;
            double lo=70,hi=260;for(int n=0;n<20;n++){double mid=(lo+hi)/2;if(natural.Sum(x=>Math.Min(x,mid))>available)hi=mid;else lo=mid;}return natural.Select(x=>Math.Min(x,lo)).ToArray();
        }
        static void AddTabAction(ContextMenu menu, string title, Action action) { var item = new MenuItem { Header = title }; item.Click += delegate { action(); }; menu.Items.Add(item); }
        void NavigationChanged()
        {
            RefreshCleanDrives();
            if(CurrentPath!=null){EmptyPanel=false;tabs[tabIndex].Pending=false;tabs[tabIndex].Error=null;tabNotice.Visibility=Visibility.Collapsed;}
            if (!switchingTabs) CaptureTab(); RenderCrumbs(); RenderTabs(); ConfigureWatcher();
        }
        void RenderCrumbs()
        {
            crumbs.Children.Clear();cleanCrumbs.Children.Clear(); if (CurrentPath == null) {if(HasUnavailableTab)foreach(var panel in new[]{crumbs,cleanCrumbs})panel.Children.Add(new TextBlock{Text=tabs[tabIndex].Saved.Path,Margin=new Thickness(6),Foreground=FluentTheme.Muted});return;}
            if(IsExt){var location=ExtLocations.Get(CurrentPath);AddCrumb(location.Root,"EXT");string currentExt=location.Root;foreach(string part in ExtLocations.Relative(CurrentPath).Split(new[]{'/'},StringSplitOptions.RemoveEmptyEntries)){currentExt=currentExt.TrimEnd('/')+"/"+part;AddCrumb(currentExt,part);}return;}
            if (IsShellBrowser) { AddCrumb(CurrentPath, ShellLocationName ?? L.Text("tree.systemLocation")); return; }
            if (IsNetworkBrowser) { AddCrumb(NetworkBrowser.Root, L.Text("network.shortTitle")); if (CurrentPath != NetworkBrowser.Root) AddCrumb(CurrentPath, CurrentPath.Trim('\\')); return; }
            string path = CurrentPath; bool virtualPath = IsRemote || IsArchive;
            string root = virtualPath ? "/" : Path.GetPathRoot(path);
            AddCrumb(root, virtualPath ? (IsRemote ? Remote.Profile.DisplayLabel : Path.GetFileName(Archive.FilePath)) : root);
            string current = root;
            foreach (string part in path.Substring(root.Length).Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)) {
                current = virtualPath ? current.TrimEnd('/') + "/" + part : Path.Combine(current, part); AddCrumb(current, part);
            }
            var edit = CompactTheme.Button("✎", L.Text("navigation.editPathShortcutTooltip"), delegate { FocusAddress(); });
            edit.Background = Brushes.Transparent; edit.BorderBrush = Brushes.Transparent; crumbs.Children.Add(edit);

        }
        void AddCrumb(string path,string label){
            foreach(var panel in new[]{crumbs,cleanCrumbs}){
                bool clean=Object.ReferenceEquals(panel,cleanCrumbs);
                if(clean&&panel.Children.Count>0)panel.Children.Add(new TextBlock{Text="›",Foreground=FluentTheme.Accent,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,2,0)});
                string caption=clean?(label.Length==3&&label[1]==':'?label.Substring(0,2):label):label+"  ›";
                var button=CompactTheme.Button(caption,path,delegate{Navigate(path,true);});button.Tag="panel-breadcrumb";button.Height=28;button.MinWidth=0;button.Padding=new Thickness(4,0,4,0);button.Background=Brushes.Transparent;button.BorderBrush=Brushes.Transparent;button.Foreground=FluentTheme.Accent;panel.Children.Add(button);
            }
        }
        internal void FocusAddress() { address.Visibility=Visibility.Visible;RefreshCleanNavigation();address.Focus(); address.SelectAll(); }
        internal static string EntryName(Entry entry) { return entry.IsDirectory ? entry.Name != null && entry.Name.StartsWith("[", StringComparison.Ordinal) && entry.Name.EndsWith("]", StringComparison.Ordinal) ? entry.Name.Substring(1, entry.Name.Length - 2) : entry.Name : entry.Name + (String.IsNullOrEmpty(entry.Extension) ? "" : "." + entry.Extension); }
        internal bool MatchesFilter(object value)
        {
            var item = value as Entry; if (item == null || item.IsParent) return true;
            if ((!showHiddenFiles && item.IsHidden) || (!showSystemFiles && item.IsSystem)) return false;
            if (quickMode.SelectedIndex == 1 || String.IsNullOrEmpty(quickText.Text)) return true;
            string query = quickText.Text; if (query.StartsWith("#")) return item.TagNames.Any(x => x.IndexOf(query.Substring(1), StringComparison.CurrentCultureIgnoreCase) >= 0); return query.IndexOfAny(new[] { '*', '?' }) >= 0 ? MaskMatches(EntryName(item), query) : EntryName(item).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }
        internal static bool MaskMatches(string name, string mask) { return mask.Split(';').Any(x => System.Text.RegularExpressions.Regex.IsMatch(name, "^" + System.Text.RegularExpressions.Regex.Escape(x.Trim() == "*.*" ? "*" : x.Trim()).Replace("\\*", ".*").Replace("\\?", ".") + "$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)); }
        internal void SetQuickFilter(string text) { quickText.Text = text ?? ""; quickBar.Visibility = UnifiedQuickSearch||String.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;if(QuickFilterChanged!=null)QuickFilterChanged(this); }
        void ApplyQuickFilter()
        {
            if (List.ItemsSource == null) return;
            var cursor = List.SelectedItem as Entry; var collection = System.Windows.Data.CollectionViewSource.GetDefaultView(List.ItemsSource); collection.Filter = MatchesFilter;
            if (quickMode.SelectedIndex == 1 && !String.IsNullOrEmpty(quickText.Text)) { var found = List.Items.Cast<Entry>().FirstOrDefault(x => !x.IsParent && EntryName(x).StartsWith(quickText.Text, StringComparison.CurrentCultureIgnoreCase)); if (found != null) List.SelectedItem = found; }
            else if (cursor != null && List.Items.Contains(cursor) && (!cursor.IsParent || String.IsNullOrEmpty(quickText.Text))) List.SelectedItem = cursor;
            else if (List.Items.Count > 0) List.SelectedItem = String.IsNullOrEmpty(quickText.Text)?List.Items[0]:List.Items.Cast<Entry>().FirstOrDefault(x=>!x.IsParent)??List.Items[0];
            if (List.SelectedItem != null) List.ScrollIntoView(List.SelectedItem); UpdateStatus();
        }
        internal void FocusFilter() { if(UnifiedQuickSearch&&QuickFilterFocusRequested!=null){QuickFilterFocusRequested(this);return;}quickBar.Visibility = Visibility.Visible; quickText.Focus(); quickText.SelectAll(); }
        string NavigationPrompt(string title, string label, string initial)
        {
            var window = new Window(); CopyUi.Style(window, Window.GetWindow(this), title, 420); var stack = new StackPanel { Margin = new Thickness(12) }; window.Content = stack;
            stack.Children.Add(CopyUi.Label(label)); var input = new TextBox { Text = initial ?? "", Margin = new Thickness(3, 6, 3, 10) }; stack.Children.Add(input);
            var buttons = FluentTheme.Actions(); stack.Children.Add(buttons); var ok = CopyUi.Button("OK", delegate { window.DialogResult = true; }); ok.IsDefault = true; buttons.Children.Add(ok); var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { window.DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
            window.Loaded += delegate { input.Focus(); input.SelectAll(); }; return window.ShowDialog() == true ? input.Text : null;
        }
    }
}
