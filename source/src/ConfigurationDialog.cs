using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace FileCompass
{
    public sealed partial class ConfigurationDialog : Window
    {
        internal readonly ListBox Sections = new ListBox();
        readonly ContentControl page = new ContentControl();
        readonly ProgramSettings settings;
        internal readonly UpdatesView UpdatePage;
        readonly ArchiveSettings archives;
        readonly ArchiveLimitEditor archiveLimits;
        readonly FileBehaviorEditor fileBehavior;
        readonly StartupPreferencesEditor startupPreferences;
        readonly TextBox imagePath, mediaPath, editorPath, rarPath, arjPath, lhaPath;
        readonly Dictionary<TextBox,string> savedProgramPaths=new Dictionary<TextBox,string>();
        readonly HashSet<TextBox> explicitProgramSelections=new HashSet<TextBox>();
        readonly TextBlock savedFolders = new TextBlock { TextWrapping = TextWrapping.Wrap };
        ToolbarTheme selectedTheme;
        ApplicationIcon selectedApplicationIcon;
        ToolbarTheme originalTheme;
        ApplicationIcon originalApplicationIcon;
        bool appearanceSaved;
        internal readonly ComboBox LanguageChoice = new ComboBox { MinWidth = 180 };

        readonly MainWindow main;
        internal readonly ConnectionsView Connections;
        readonly FontChoiceControl interfaceFont, fileListFont;
        readonly ComboBox sizeUnits=new ComboBox{MinWidth=200};
        readonly ComboBox buttonIconSize=new ComboBox{MinWidth=100},driveIconSize=new ComboBox{MinWidth=100};
        readonly CheckBox internalEditor=new CheckBox{Content=L.Text("codeEditor.setting"),Margin=new Thickness(0)};
        readonly CheckBox automaticFolderViews = new CheckBox { Content = L.Text("columns.autoSetting"), Margin = new Thickness(0, 6, 0, 6) }; readonly CheckBox internalCodePreview = new CheckBox { Content = L.Text("settings.internalCodePreview"), Margin = new Thickness(0) };
        readonly CheckBox showHidden = new CheckBox { Content = L.Text("settings.showHiddenFiles"), Margin = new Thickness(0, 5, 0, 5) };
        readonly CheckBox showSystem = new CheckBox { Content = L.Text("settings.showSystemFiles"), Margin = new Thickness(0, 5, 0, 5) };
        readonly CheckBox automaticRefresh = new CheckBox { Content = L.Text("settings.autoRefreshLocal"), Margin = new Thickness(0, 14, 0, 6) };
        readonly CheckBox networkRefresh = new CheckBox { Content = L.Text("settings.watchNetworkFolders"), Margin = new Thickness(0, 4, 0, 6) };
        public ConfigurationDialog(Window owner, string section = null, string archiveError = null, NetworkSettingsStore connectionsStore = null)
        {
            section = section ?? L.Text("settings.appearanceSection");
            CopyUi.Style(this, owner, L.Text("menu.configuration"), 1080); InitializeSettingsSize();
            main = owner as MainWindow ?? (owner == null ? null : owner.Owner as MainWindow);
            settings = ProgramSettings.Load(); archives = ArchiveSettings.Load(); selectedTheme = ToolbarThemeSettings.Current;
            AppearanceTheme.RegisterShell(this);
            originalTheme = selectedTheme; originalApplicationIcon = ApplicationBrand.Current;
            StackPanel actions;var body=SettingsUx.Frame(this,L.Text("menu.configuration"),out actions);((Border)body.Parent).Tag="dialog-split-card";
            var cancel=CopyUi.Button(L.Text("action.cancel"),delegate{DialogResult=false;});cancel.IsCancel=true;actions.Children.Add(cancel);
            var apply=CopyUi.Button(L.Text("action.apply"),delegate{Save(false);});System.Windows.Automation.AutomationProperties.SetAutomationId(apply,"ConfigurationApply");actions.Children.Add(apply);
            var ok=CopyUi.Button(L.Text("action.save"),delegate{Save(true);});ok.IsDefault=true;FluentTheme.Primary(ok);actions.Children.Add(ok);
            var navigation=new Border{Tag="dialog-navigation",Width=170,Background=CompactTheme.ToolbarSurface,CornerRadius=new CornerRadius(10),Margin=new Thickness(0,0,8,0),Padding=new Thickness(7,10,7,10),Child=Sections};DockPanel.SetDock(navigation,Dock.Left);body.Children.Add(navigation);SettingsUx.Navigation(Sections);
            body.Children.Add(new Border{Tag="settings-page-card",Child=page,Background=CompactTheme.WhiteSurface,CornerRadius=new CornerRadius(11),Padding=new Thickness(18,14,18,14)});
            var appearance = Page(L.Text("settings.appearanceSection"), L.Text("settings.compactInterfaceDescription"));
            var fonts = FluentTheme.Section(appearance, L.Text("settings.fontTitle"));
            interfaceFont = new FontChoiceControl(L.Text("settings.interfaceFont"), settings.InterfaceFontFamily, settings.InterfaceFontSize, 12);
            fileListFont = new FontChoiceControl(L.Text("settings.fileListFont"), settings.FileListFontFamily, settings.FileListFontSize, 24);
            interfaceFont.Family.MinWidth=fileListFont.Family.MinWidth=100;fonts.Children.Add(interfaceFont); fonts.Children.Add(fileListFont);
            var units=FluentTheme.Section(appearance,L.Text("settings.sizeUnits"));foreach(string key in new[]{"settings.unitsSI","settings.unitsIEC","settings.unitsBytes"})sizeUnits.Items.Add(L.Text(key));sizeUnits.SelectedIndex=ByteSize.Normalize(settings.SizeUnits)=="SI"?0:ByteSize.Normalize(settings.SizeUnits)=="Bytes"?2:1;units.Children.Add(sizeUnits);var sizeUnitsGroup=(GroupBox)units.Parent;appearance.Children.Remove(sizeUnitsGroup);
            showHidden.IsChecked = settings.ShowHiddenFiles; showSystem.IsChecked = settings.ShowSystemFiles;
            var visibility = FluentTheme.Section(appearance, L.Text("settings.fileVisibility")); visibility.Children.Add(showHidden); visibility.Children.Add(showSystem);
            automaticFolderViews.IsChecked = settings.AutomaticFolderViews; var folderViews = FluentTheme.Section(appearance, L.Text("columns.autoTitle")); folderViews.Children.Add(automaticFolderViews); folderViews.Children.Add(new TextBlock { Text = L.Text("columns.autoHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted }); appearance.Children.Add(new TextBlock { Text = L.Text("settings.panelColorsDescription"), Margin = new Thickness(0, 16, 0, 0), Foreground = FluentTheme.Muted });
            automaticRefresh.IsChecked = settings.AutoRefresh; networkRefresh.IsChecked = settings.WatchNetworkFolders;
            var refreshing = FluentTheme.Section(appearance, L.Text("settings.panelRefreshGroup"));
            refreshing.Children.Add(automaticRefresh); refreshing.Children.Add(networkRefresh);
            refreshing.Children.Add(new TextBlock { Text = L.Text("settings.slowNetworkRefreshHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted });
            var icons = Page(L.Text("settings.iconsSection"), L.Text("settings.iconSetsHelp"));
            var applicationIcons = FluentTheme.Section(icons, L.Text("settings.applicationIconsTitle"));
            applicationIcons.Children.Add(new TextBlock { Text = L.Text("settings.applicationIconsHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted });
            selectedApplicationIcon = ApplicationBrand.Current;
            var appIconOptions = new WrapPanel(); applicationIcons.Children.Add(appIconOptions);
            string[] appIconLabels = { L.Text("icons.compass"), L.Text("icons.panels"), L.Text("icons.route"), L.Text("icons.night"), L.Text("icons.floppyRoute"), L.Text("icons.floppyOrbit"), L.Text("icons.floppyClassic") };
            foreach (ApplicationIcon value in Enum.GetValues(typeof(ApplicationIcon)))
            {
                ApplicationIcon capturedIcon = value;
                var preview = new StackPanel { Orientation = Orientation.Horizontal };
                preview.Children.Add(new Image { Source = ApplicationBrand.For(value), Width = 40, Height = 40, Margin = new Thickness(0, 0, 7, 0) });
                preview.Children.Add(new TextBlock { Text = appIconLabels[(int)value], VerticalAlignment = VerticalAlignment.Center });
                var choice = new RadioButton { Content = preview, GroupName = "ApplicationIcon", IsChecked = value == selectedApplicationIcon, Margin = new Thickness(0, 7, 18, 7) };
                choice.Checked += delegate { selectedApplicationIcon = capturedIcon; ApplicationBrand.Apply(capturedIcon); }; appIconOptions.Children.Add(choice);
            }
            applicationIcons.Children.Add(CopyUi.Button(L.Text("icons.createShortcut"), delegate
            {
                var dialog = new SaveFileDialog { Title = L.Text("icons.createShortcut"), Filter = L.Text("icons.shortcutFilter"), DefaultExt = ".lnk", AddExtension = true, FileName = "File Compass", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), OverwritePrompt = true };
                if (dialog.ShowDialog(this) != true) return;
                try { ApplicationShortcut.Create(dialog.FileName, selectedApplicationIcon); MessageBox.Show(this, L.Text("icons.shortcutCreated"), ApplicationBrand.Name, MessageBoxButton.OK, MessageBoxImage.Information); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("icons.createShortcut"), MessageBoxButton.OK, MessageBoxImage.Warning); }
            }));
                        var sizes=FluentTheme.Section(icons,L.Text("settings.iconSizes"));
            foreach(var size in new[]{8.0,10,12,14,16,18,20,22,24,28,32}){buttonIconSize.Items.Add(size);driveIconSize.Items.Add(size);}
            buttonIconSize.SelectedItem=FilePanel.IconSize(settings.ButtonIconSize,20);driveIconSize.SelectedItem=FilePanel.IconSize(settings.DriveIconSize,10);
            if(buttonIconSize.SelectedIndex<0){buttonIconSize.Items.Add(settings.ButtonIconSize);buttonIconSize.SelectedItem=settings.ButtonIconSize;}if(driveIconSize.SelectedIndex<0){driveIconSize.Items.Add(settings.DriveIconSize);driveIconSize.SelectedItem=settings.DriveIconSize;}
            var buttonSizes=new StackPanel();buttonSizes.Children.Add(new TextBlock{Text=L.Text("settings.buttonIconSize"),Margin=new Thickness(0,0,0,5)});buttonSizes.Children.Add(buttonIconSize);
            var driveSizes=new StackPanel();driveSizes.Children.Add(new TextBlock{Text=L.Text("settings.driveIconSize"),Margin=new Thickness(0,0,0,5)});driveSizes.Children.Add(driveIconSize);sizes.Children.Add(Row(buttonSizes,driveSizes));
            string[] titles = { L.Text("settings.fluentIcons"), L.Text("settings.flatIcons"), L.Text("settings.outlineIcons") };
            foreach (ToolbarTheme theme in Enum.GetValues(typeof(ToolbarTheme)))
            {
                ToolbarTheme captured = theme; var card = new StackPanel();
                var radio = new RadioButton { Content = titles[(int)theme], IsChecked = theme == selectedTheme, GroupName = "ConfigurationIcons", Margin = new Thickness(0, 0, 0, 6) };
                radio.Checked += delegate { selectedTheme = captured; PreviewToolbar(captured); }; card.Children.Add(radio);
                var previews = new WrapPanel(); card.Children.Add(previews);
                foreach (string name in ToolbarThemeDialog.PreviewNames)
                    previews.Children.Add(new Border { Width = 30, Height = 30, Background = CompactTheme.WhiteSurface, BorderBrush = CompactTheme.Line, BorderThickness = new Thickness(1), Margin = new Thickness(1), CornerRadius = new CornerRadius(3), Child = new Image { Source = DesktopIcons.ToolbarDrawing(name, theme), Width = 24, Height = 24 }, ToolTip=ToolbarThemeDialog.PreviewLabel(name) });
                icons.Children.Add(new GroupBox { Header = titles[(int)theme], Content = card, Margin = new Thickness(0, 0, 0, 10) });
            }
            var archivePage = Page(L.Text("settings.archivesSection"), L.Text("settings.archiveBrowsingDescription"));
            if (!String.IsNullOrEmpty(archiveError))
                archivePage.Children.Add(new Border { Background = CompactTheme.ThemeBrush("#FFF6D6"), BorderBrush = Brushes.DarkOrange, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 12), Child = new TextBlock { Text = archiveError, Foreground = CompactTheme.ThemeBrush("#B92331"), TextWrapping = TextWrapping.Wrap } });
            archivePage.Children.Add(new TextBlock { Text = L.Text("settings.portableArchiveLibraryPrefix") + ArchiveEngine.SevenZipLibraryPath, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
            var rarSection = FluentTheme.Section(archivePage, L.Text("settings.externalRarArchiver"));
            rarPath = ProgramPath(rarSection, "WinRAR / Rar.exe", archives.WinRarPath);
            rarSection.Children.Add(CopyUi.Button(L.Text("settings.findWinRar"), delegate { Find(rarPath, ArchiveSettings.FindWinRar()); }));
            var arjSection = FluentTheme.Section(archivePage, "ARJ");
            arjPath = ProgramPath(arjSection, "ARJ32.exe / arj.exe (Windows)", archives.ArjPath);
            arjSection.Children.Add(CopyUi.Button(L.Text("action.detectAutomatically"), delegate { Find(arjPath, ArchiveSettings.FindLegacyArchiver(true)); }));
            var lhaSection = FluentTheme.Section(archivePage, "LHA / LZH");
            lhaPath = ProgramPath(lhaSection, "lha.exe (GnuWin32)", archives.LhaPath);
            lhaSection.Children.Add(CopyUi.Button(L.Text("action.detectAutomatically"), delegate { Find(lhaPath, ArchiveSettings.FindLegacyArchiver(false)); }));
            rarSection.Children.Add(CopyUi.Button(L.Text("archivePlugin.getWinRar"), delegate { ArchivePluginWindow.Open(this, ArchivePluginInstaller.Rar, delegate(string path) { rarPath.Text = path; }, rarPath.Text); }));
            arjSection.Children.Add(CopyUi.Button(L.Text("archivePlugin.getPlugin"), delegate { ArchivePluginWindow.Open(this, ArchivePluginInstaller.Arj, delegate(string path) { arjPath.Text = path; }, arjPath.Text); }));
            lhaSection.Children.Add(CopyUi.Button(L.Text("archivePlugin.getPlugin"), delegate { ArchivePluginWindow.Open(this, ArchivePluginInstaller.Lha, delegate(string path) { lhaPath.Text = path; }, lhaPath.Text); }));
            archiveLimits = new ArchiveLimitEditor(archives); archivePage.Children.Add(archiveLimits);
            archivePage.Children.Add(new TextBlock { Text = L.Text("settings.archiveFormatCapabilities"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 0) });
            var viewers = Page(L.Text("settings.viewersTitle"), L.Text("settings.viewerShortcutsHelp"));
            imagePath = ProgramPath(FluentTheme.Section(viewers, L.Text("settings.imagesLabel")), L.Text("settings.builtInImageViewerHint"), settings.ImageViewerPath);
            var mediaSection = FluentTheme.Section(viewers, L.Text("settings.audioAndVideoLabel"));
            mediaPath = ProgramPath(mediaSection, L.Text("settings.mediaPlayerLabel"), settings.MediaPlayerPath);
            mediaSection.Children.Add(CopyUi.Button(L.Text("settings.findMediaPlayerClassic"), delegate { Find(mediaPath, FileViewers.FindMediaPlayer()); }));
            editorPath = ProgramPath(FluentTheme.Section(viewers, L.Text("settings.textEditorLabel")), L.Text("settings.notepadDefaultHint"), settings.TextEditorPath);
            internalEditor.IsChecked=settings.InternalTextEditor;viewers.Children.Add(internalEditor);internalCodePreview.IsChecked = settings.InternalCodePreview; viewers.Children.Add(internalCodePreview);
            viewers.Children.Add(new TextBlock { Text = L.Text("settings.supportedViewerFormatsHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(0, 8, 0, 0) });
            var view = Page(L.Text("settings.startupViewTitle"), L.Text("settings.restoreViewDescription"));
            startupPreferences = new StartupPreferencesEditor(settings); view.Children.Add(startupPreferences);
            UpdateFolders(); view.Children.Add(savedFolders);
            var remember = CopyUi.Button(L.Text("settings.useCurrentPanelFolders"), delegate
            {
                try { var paths = main.ViewFolders(); settings.RememberFolders(paths[0], paths[1]); main.CaptureView(settings); UpdateFolders(); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("settings.saveView")); }
            }); remember.IsEnabled = main != null; view.Children.Add(remember);
            view.Children.Add(new TextBlock { Text = L.Text("settings.saveViewHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(0, 16, 0, 0) });
            var network = Page(L.Text("network.connectionsTitle"), L.Text("connections.settingsDescription"));
            try {
                Connections = new ConnectionsView(this, main, connectionsStore ?? (main == null ? new NetworkSettingsStore(NetworkSettingsStore.DefaultPath) : main.ConnectionSettings), true) { Height = 380 };
                network.Children.Add(Connections);
                Connections.BusyChanged += delegate(bool busy) { Sections.IsEnabled = !busy; ok.IsEnabled = apply.IsEnabled = !busy; QueueSettingsFit(); };
                Connections.Tabs.SelectionChanged += delegate { QueueSettingsFit(); };
            } catch (Exception ex) { network.Children.Add(new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, Foreground = CompactTheme.ThemeBrush("#B92331") }); }
            network.Children.Add(new TextBlock { Text = L.Text("connections.immediateSave"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(0, 10, 0, 0) });
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (Connections != null && !Connections.AllowHostClose()) e.Cancel = true; };
            Closed += delegate { if (!appearanceSaved) { ApplicationBrand.Apply(originalApplicationIcon); PreviewToolbar(originalTheme); } if (Connections != null) Connections.DisposeManagers(); };
            var behavior = Page(L.Text("settings.fileOperations"), L.Text("settings.fileOperationsDescription"));
            fileBehavior = new FileBehaviorEditor(settings); behavior.Children.Add(fileBehavior);
            var updates=Page(L.Text("updates.title"),L.Text("updates.description"));UpdatePage=new UpdatesView(main);updates.Children.Add(UpdatePage);Closed+=delegate{UpdatePage.Dispose();};
            var panelsPage=Page(L.Text("settings.panelsSection"),L.Text("settings.panelsDescription"));
            var names = new[] { L.Text("settings.appearanceSection"), L.Text("settings.iconsSection"), L.Text("settings.archivesSection"), L.Text("settings.viewersTitle"), L.Text("settings.startupViewTitle"), L.Text("network.connectionsTitle"), L.Text("settings.fileOperations"),L.Text("updates.title"),L.Text("settings.panelsSection") };
            var pages = new[] { appearance, icons, archivePage, viewers, view, network, behavior,updates,panelsPage };
            var iconSizesGroup=(GroupBox)sizes.Parent;icons.Children.Remove(iconSizesGroup);ArrangeSettingsPages(pages);Action<GroupBox> detachGroup=delegate(GroupBox group){var parent=group.Parent as Panel;if(parent==null)return;parent.Children.Remove(group);if(parent.Children.Count==0)appearance.Children.Remove(parent);else foreach(UIElement remaining in parent.Children){Grid.SetColumn(remaining,0);Grid.SetColumnSpan(remaining,2);}};
            var visibilityGroup=(GroupBox)visibility.Parent;var folderGroup=(GroupBox)folderViews.Parent;var refreshGroup=(GroupBox)refreshing.Parent;detachGroup(visibilityGroup);detachGroup(folderGroup);detachGroup(refreshGroup);panelsPage.Children.Add(Row(sizeUnitsGroup,visibilityGroup));panelsPage.Children.Add(Row(folderGroup,refreshGroup));icons.Children.Insert(3,iconSizesGroup);
            AddAppearanceChoices(appearance);
            foreach (string name in names) Sections.Items.Add(name);
            Sections.SelectionChanged += delegate { if (Sections.SelectedIndex >= 0) { page.Content = new ScrollViewer { Content = pages[Sections.SelectedIndex], VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; QueueSettingsFit(); } };
            Sections.SelectedIndex = Math.Max(0, Array.IndexOf(names, section));
        }
        StackPanel Page(string title, string description)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; panel.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
            panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(0, 0, 0, 16) }); return panel;
        }
        TextBox ProgramPath(Panel panel, string label, string path)
        {
            panel.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
            var row = new DockPanel(); panel.Children.Add(row); var input = new TextBox { Text = path ?? "", Height = 30, MinHeight = 30, VerticalContentAlignment = VerticalAlignment.Center };
            savedProgramPaths[input]=CleanPath(input.Text);
            var browse = CopyUi.Button(L.Text("common.ellipsis"), delegate { var picker = new OpenFileDialog { Filter = L.Text("settings.executableFileFilter"), CheckFileExists = true }; if (picker.ShowDialog(this) == true) { input.Text = picker.FileName; explicitProgramSelections.Add(input); } }); browse.Width = 36; browse.MinWidth = 36; browse.Height = Double.NaN; browse.MinHeight = 30; browse.Margin = new Thickness(4, 0, 0, 0); DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse); row.Children.Add(input); return input;
        }
        void Find(TextBox input, string found) { if (!String.IsNullOrEmpty(found)) { input.Text = found; explicitProgramSelections.Add(input); } else MessageBox.Show(this, L.Text("settings.programNotFound"), L.Text("settings.programSearchTitle")); }
        static string CleanPath(string value) { return (value??"").Trim().Trim('"'); }
        bool ProgramPathChanged(TextBox input) { string original;return explicitProgramSelections.Contains(input)||!savedProgramPaths.TryGetValue(input,out original)||!String.Equals(CleanPath(input.Text),original,StringComparison.OrdinalIgnoreCase); }
        void PreviewToolbar(ToolbarTheme theme) { ToolbarThemeSettings.Current = theme; if (main != null) main.ApplyToolbarTheme(theme); }
        void UpdateFolders() { savedFolders.Text = L.Text("settings.leftPanelPrefix") + (String.IsNullOrEmpty(settings.LeftFolder) ? L.Text("settings.defaultFolder") : settings.LeftFolder) + L.Text("settings.rightPanelPrefix") + (String.IsNullOrEmpty(settings.RightFolder) ? L.Text("settings.defaultFolder") : settings.RightFolder); savedFolders.Margin = new Thickness(0, 0, 0, 16); }
        void RefreshArchivePaths()
        {
            var current = ArchiveSettings.Load();
            if (!ProgramPathChanged(rarPath)) { rarPath.Text = current.WinRarPath; savedProgramPaths[rarPath]=CleanPath(current.WinRarPath); archives.WinRarPath = current.WinRarPath; }
            if (!ProgramPathChanged(arjPath)) { arjPath.Text = current.ArjPath; savedProgramPaths[arjPath]=CleanPath(current.ArjPath); archives.ArjPath = current.ArjPath; }
            if (!ProgramPathChanged(lhaPath)) { lhaPath.Text = current.LhaPath; savedProgramPaths[lhaPath]=CleanPath(current.LhaPath); archives.LhaPath = current.LhaPath; }
        }
        void Save(bool close)
        {
            try { SaveChanges(); originalTheme=selectedTheme;originalApplicationIcon=selectedApplicationIcon;if(close){appearanceSaved = true; DialogResult = true;} }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("menu.configuration"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        internal void SaveChanges()
        {
                RefreshArchivePaths();
                foreach (var input in new[] { imagePath, mediaPath, editorPath, rarPath, arjPath, lhaPath })
                { string value = input.Text.Trim().Trim('"'); if (ProgramPathChanged(input) && value.Length > 0 && !File.Exists(Environment.ExpandEnvironmentVariables(value))) throw new IOException(L.Text("settings.programNotFound") + "\n" + value); }
                settings.ImageViewerPath = imagePath.Text.Trim().Trim('"'); settings.MediaPlayerPath = mediaPath.Text.Trim().Trim('"'); settings.TextEditorPath = editorPath.Text.Trim().Trim('"');settings.InternalTextEditor=internalEditor.IsChecked==true;
                settings.AutoRefresh = automaticRefresh.IsChecked == true; settings.WatchNetworkFolders = networkRefresh.IsChecked == true;
                settings.InternalCodePreview = internalCodePreview.IsChecked == true; settings.AutomaticFolderViews = automaticFolderViews.IsChecked == true;
                settings.SizeUnits=new[]{"SI","IEC","Bytes"}[Math.Max(0,sizeUnits.SelectedIndex)];
                settings.ShowHiddenFiles = showHidden.IsChecked == true; settings.ShowSystemFiles = showSystem.IsChecked == true;
                fileBehavior.Apply(); startupPreferences.Apply();
                settings.WindowBackdrop = "None"; settings.ColorTheme = "Light";
                settings.InterfaceMotif = "Clean";
                string language = L.SupportedLanguages[LanguageChoice.SelectedIndex];
                settings.ButtonIconSize=(double)buttonIconSize.SelectedItem;settings.DriveIconSize=(double)driveIconSize.SelectedItem;
                interfaceFont.Read(out settings.InterfaceFontFamily, out settings.InterfaceFontSize); fileListFont.Read(out settings.FileListFontFamily, out settings.FileListFontSize);
            archiveLimits.Apply(archives); archives.WinRarPath = rarPath.Text.Trim().Trim('"'); archives.ArjPath = arjPath.Text.Trim().Trim('"'); archives.LhaPath = lhaPath.Text.Trim().Trim('"');
            // Approve only programs explicitly selected or edited in this dialog.
            // Language/appearance saves must neither read an unrelated trust store
            // nor silently authorize a replaced executable at an unchanged path.
            var approvals=new List<string>();
            foreach(var input in new[]{rarPath,arjPath,lhaPath})if(ProgramPathChanged(input)&&!String.IsNullOrWhiteSpace(CleanPath(input.Text))) {
                approvals.Add(Environment.ExpandEnvironmentVariables(CleanPath(input.Text)));
                if(Object.ReferenceEquals(input,rarPath))approvals.Add(archives.RarExecutable());
            }
            foreach(string tool in approvals.Distinct(StringComparer.OrdinalIgnoreCase))ToolTrust.Approve(tool);
            UpdatePage.SavePreferences(); settings.Save(); archives.Save(); new ToolbarThemeSettings { Theme = selectedTheme, ApplicationIcon = selectedApplicationIcon }.Save();
                L.SaveSelection(language);
                ApplicationBrand.Apply(selectedApplicationIcon); ToolbarThemeSettings.Current = selectedTheme; if (main != null) { main.ApplyRefreshSettings(settings.AutoRefresh, settings.WatchNetworkFolders); main.ApplyDisplaySettings(settings); main.ApplyToolbarTheme(selectedTheme); }
                if(L.Language!=language){L.ChangeLanguage(language);LiveLanguage.Refresh(main);}
                foreach(var input in new[]{imagePath,mediaPath,editorPath,rarPath,arjPath,lhaPath})savedProgramPaths[input]=CleanPath(input.Text);
                explicitProgramSelections.Clear();
        }
        void AddAppearanceChoices(StackPanel appearance) {
            foreach (var font in new[] { interfaceFont, fileListFont }) ((FrameworkElement)font.Children[font.Children.Count-1]).Margin = new Thickness(0,4,0,2);
            foreach (var name in L.LanguageNames) LanguageChoice.Items.Add(name); LanguageChoice.SelectedIndex = Array.IndexOf(L.SupportedLanguages,L.PreferredLanguage); if(LanguageChoice.SelectedIndex<0)LanguageChoice.SelectedIndex=Array.IndexOf(L.SupportedLanguages,L.DefaultLanguage);
            var language = new StackPanel(); language.Children.Add(new TextBlock { Text = L.Text("settings.language"), Margin = new Thickness(0,0,0,5) }); language.Children.Add(LanguageChoice);
            LanguageChoice.ToolTip = L.Text("settings.languageRestart");
            var description = (TextBlock)appearance.Children[1]; description.Text = L.Text("settings.panelColorsDescription"); description.Margin = new Thickness(0,0,0,8);
            var redundant = appearance.Children[appearance.Children.Count-1] as TextBlock; if (redundant != null) redundant.Visibility = Visibility.Collapsed;
            LanguageChoice.MinWidth = 0;
            var choices = Row(language); choices.Margin = new Thickness(0,0,0,4); appearance.Children.Insert(2, choices);
        }
    }
}
