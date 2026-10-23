using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace CommanderCZ
{
    public partial class MainWindow
    {
        readonly System.Collections.Generic.List<Image> toolbarImages = new System.Collections.Generic.List<Image>();
        void ChooseToolbarTheme()
        {
            var dialog = new ToolbarThemeDialog(this);
            if (dialog.ShowDialog() == true)
                ApplyToolbarTheme(dialog.SelectedTheme);
        }
        internal void ApplyToolbarTheme(ToolbarTheme theme)
        { foreach (var icon in toolbarImages) icon.Source = DesktopIcons.ToolbarDrawing((string)icon.Tag, theme); }
        readonly TextBox commandLine = new TextBox { Height = 24, VerticalContentAlignment = VerticalAlignment.Center, BorderBrush = CompactTheme.Line, BorderThickness = new Thickness(1) };
        readonly TextBlock commandPrompt = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 500 };

        void AddChrome(DockPanel root, StackPanel bottom)
        {
            var top = new StackPanel { Background = CompactTheme.Chrome };
            DockPanel.SetDock(top, Dock.Top); root.Children.Insert(0, top);
            var menuRow = new DockPanel();
            var help = new Menu { Background = CompactTheme.Chrome };
            var helpItem = new MenuItem { Header = L.Text("action.help"), Height = 28 }; help.Items.Add(helpItem);
            AddMenu(helpItem, L.Text("app.aboutMenu"), delegate { MessageBox.Show(this, L.Text("app.aboutText"), ApplicationBrand.Name); });
            DockPanel.SetDock(help, Dock.Right); menuRow.Children.Add(help);
            var menus = new Menu { Background = CompactTheme.Chrome, Height = 30 };
            var file = MenuRoot(menus, L.Text("menu.file"));
            AddMenu(file, L.Text("action.properties"), ShowProperties, "Alt+Enter"); AddMenu(file, L.Text("action.openWith"), OpenWith); AddMenu(file, L.Text("action.changeAttributes"), ChangeAttributes); file.Items.Add(new Separator());
            AddArchiveMenu(file, L.Text("action.compress"), CreateArchive, "Alt+F5", "pack");
            AddArchiveMenu(file, L.Text("action.extract"), ExtractArchives, "Alt+F9", "unpack");
            AddArchiveMenu(file, L.Text("action.testArchive"), TestArchives, "Alt+Shift+F9", "archive-test");
            file.Items.Add(new Separator());
            AddMenu(file, L.Text("action.compareFiles"), CompareFilesByContent, "Ctrl+Shift+D");
            AddMenu(file, L.Text("action.createChecksums"), CreateChecksumsDialog);
            AddMenu(file, L.Text("action.verifyChecksums"), delegate { VerifyChecksumsDialog(); });
            file.Items.Add(new Separator());
            AddMenu(file, L.Text("menu.view"), delegate { active.ViewSelected(); }, "F3"); AddMenu(file, L.Text("action.edit"), EditSelected, "F4");
            AddMenu(file, L.Text("action.copy"), delegate { Transfer(false); }, "F5"); AddMenu(file, L.Text("action.move"), delegate { Transfer(true); }, "F6");
            AddMenu(file, L.Text("action.newFolder"), NewFolder, "F7"); AddMenu(file, L.Text("action.rename"), Rename, "Shift+F6");
            AddMenu(file, L.Text("action.batchRename"), MultiRename, "Ctrl+M");
            AddMenu(file, L.Text("tags.edit"), EditTags);
            AddMenu(file, L.Text("imageTools.title"), QuickImageTools);
            AddMenu(file, L.Text("parts.split"), SplitSelectedFile); AddMenu(file, L.Text("parts.combine"), CombineSelectedFiles);
            AddMenu(file, L.Text("action.delete"), DeleteSelected, "F8 / Delete"); file.Items.Add(new Separator()); AddMenu(file, L.Text("action.exit"), Close, "Alt+F4");
            var select = MenuRoot(menus, L.Text("menu.selection"));
            AddMenu(select, L.Text("selection.markAll"), delegate { active.MarkAll(true); }, "Ctrl+A");
            AddMenu(select, L.Text("selection.clearMarks"), delegate { active.MarkAll(false); });
            AddMenu(select, L.Text("selection.invertMarks"), delegate { active.InvertMarks(); }, "Num *");
            AddMenu(select, L.Text("selection.markByMask"), delegate { active.SelectMask(true); }, "Num +");
            AddMenu(select, L.Text("selection.unmarkByMask"), delegate { active.SelectMask(false); }, "Num −");
            AddMenu(select, L.Text("selection.markSameExtension"), delegate { active.SelectExtension(); });
            AddMenu(select, L.Text("selection.saveMarks"), delegate { active.RememberSelection(); }); AddMenu(select, L.Text("selection.restoreMarks"), delegate { active.RestoreSelection(); });
            var edit = MenuRoot(menus, L.Text("menu.edit"));
            AddMenu(edit, L.Text("action.copyToClipboard"), delegate { CopyClipboard(false); }, "Ctrl+C"); AddMenu(edit, L.Text("action.cut"), delegate { CopyClipboard(true); }, "Ctrl+X"); AddMenu(edit, L.Text("action.paste"), PasteClipboard, "Ctrl+V");
            AddMenu(edit, L.Text("clipboard.copyNames"), delegate { CopyNames(false); }, "Ctrl+Shift+N"); AddMenu(edit, L.Text("clipboard.copyFullPaths"), delegate { CopyNames(true); }, "Ctrl+Shift+C");
            AddMenu(edit, L.Text("clipboardFile.title"), PasteClipboardAsFile, "Ctrl+Shift+V"); var commands = MenuRoot(menus, L.Text("menu.commands"));
            AddMenu(commands, L.Text("action.refresh"), delegate { left.Refresh(); right.Refresh(); }, "Ctrl+R");
            AddMenu(commands, L.Text("action.parentFolder"), delegate { active.Up(); }, "Backspace");
            AddMenu(commands, L.Text("panel.swap"), SwapPanels); AddMenu(commands, L.Text("action.openExplorer"), OpenExplorer);
            AddMenu(commands, L.Text("action.openPowerShellHere"), OpenPowerShell, "Ctrl+Alt+P");
            AddMenu(commands, L.Text("panel.openSameFolderInOtherPanel"), OpenSameFolder);
            AddMenu(commands, L.Text("favorites.openMenu"), ShowFavorites, "Ctrl+D");
            AddMenu(commands, L.Text("workspace.title"), ShowWorkspaces);
            AddMenu(commands, L.Text("tags.results"), ShowTaggedFiles);
            AddMenu(commands, L.Text("projection.addToBasket"), AddToBasket); AddMenu(commands, L.Text("projection.basket"), ShowBasket); AddMenu(commands, L.Text("projection.removeFromBasket"), RemoveFromBasket);
            AddMenu(commands, L.Text("everything.title"), SearchEverything);
            AddMenu(commands, L.Text("analyzer.title"), AnalyzeFolder); AddMenu(commands, L.Text("smart.title"), ShowDiskSmart);
            AddMenu(commands, L.Text("diskUsage.title"), delegate { active.ToggleDiskUsage(); }, "Alt+Shift+Enter");
            AddMenu(commands, L.Text("tabs.new"), delegate { active.NewTab(); }, "Ctrl+T"); AddMenu(commands, L.Text("tabs.close"), delegate { active.CloseTab(active.CurrentTabIndex); }, "Ctrl+W");
            AddMenu(commands, L.Text("tabs.toggleLock"), delegate { active.LockTab(active.CurrentTabIndex); });
            AddMenu(commands, L.Text("queue.copyTitle"), ShowCopyQueue);
            AddMenu(commands,L.Text("queue.restoreSaved"),ShowDurableQueue);
            AddMenu(commands,L.Text("queue.saveAndExit"),SaveQueueAndExit);
            AddMenu(commands, L.Text("queue.networkCopyTitle"), delegate { ShowNetworkQueue(null); });
            AddMenu(commands, L.Text("action.findFiles"), FindFiles, "Alt+F7");
            AddMenu(commands,L.Text("duplicates.title"),FindDuplicates);
            AddMenu(commands, L.Text("action.batchRename"), MultiRename, "Ctrl+M");
            AddMenu(commands, L.Text("action.compareFiles"), CompareFilesByContent, "Ctrl+Shift+D");
            AddMenu(commands, L.Text("action.compareFolders"), delegate { CompareFolders(false); }); AddMenu(commands, L.Text("action.synchronizeFolders"), delegate { CompareFolders(true); });
            var network = MenuRoot(menus, L.Text("connections.title"));
            AddMenu(network, L.Text("network.manageConnectionsMenu"), delegate { ManageConnections(); });
            network.Items.Add(new Separator());
            AddMenu(network, L.Text("network.openActiveSshTerminal"), OpenActiveSshTerminal);
            AddMenu(network, L.Text("network.disconnectActivePanel"), DisconnectNetwork);
            AddMenu(network, L.Text("network.mapDriveMenu"), MapNetworkDrive);
            AddMenu(network, L.Text("network.browseDevices"), delegate { active.Navigate("network:", true); });
            var display = MenuRoot(menus, L.Text("menu.view"));
            AddPanelLayoutMenu(display);
            AddMenu(display, L.Text("panel.quickPreviewOtherPanel"), ToggleQuickPreview, "Ctrl+Q");

            AddMenu(display, L.Text("tags.filter"), FilterTags);
            AddMenu(display, L.Text("projection.flat"), ShowFlatView);
            AddMenu(display, L.Text("projection.normal"), delegate { if (active.CurrentPath != null) active.Navigate(active.CurrentPath, false); });
            var cancelListing = new MenuItem { Header = L.Text("projection.cancelLoading"), InputGestureText = "Esc" }; cancelListing.Click += delegate { active.CancelNavigation(); }; display.Items.Add(cancelListing);
            AddMenu(display, L.Text("panel.quickSearchFilter"), delegate { active.FocusFilter(); }, "Ctrl+F"); AddMenu(display, L.Text("panel.editPath"), delegate { active.FocusAddress(); }, "Ctrl+L");
            AddMenu(display, L.Text("panel.allFiles"), delegate { active.Refresh(); }); AddMenu(display, L.Text("panel.detailsView"), delegate { active.SetDisplay("Details"); });
            AddMenu(display,L.Text("panel.shortView"),delegate { active.SetDisplay("Short"); }); AddMenu(display,L.Text("action.preview"),delegate { active.SetDisplay("Thumbnails"); }); AddMenu(display,L.Text("panel.folderTree"),delegate { active.ToggleTree(); });
            var treeMenu = (MenuItem)display.Items[display.Items.Count - 1]; display.SubmenuOpened += delegate { treeMenu.IsEnabled = panelLayout == "Single"; };
            var config = MenuRoot(menus, L.Text("menu.configuration"));
            AddMenu(config, L.Text("action.settings"), delegate { new ConfigurationDialog(this).ShowDialog(); });
            config.Items.Add(new Separator()); AddMenu(config, L.Text("settings.saveView"), SaveView);
            AddMenu(config, L.Text("history.openMenu"), ShowOperationHistory);
            var start = MenuRoot(menus, L.Text("menu.start"));
            AddMenu(start, L.Text("action.runPowerShellHere"), OpenPowerShell, "Ctrl+Alt+P"); start.Items.Add(new Separator());
            AddMenu(start, L.Text("action.openProjectFolder"), delegate { active.Navigate(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..")), true); });
            OrganizeMainMenus(file, select, edit, commands, network, display);
            menuRow.Children.Add(menus); top.Children.Add(menuRow);
            var tools = new WrapPanel { Orientation = Orientation.Horizontal };
            Tool(tools, "refresh", L.Text("toolbar.refreshShortcut"), delegate { left.Refresh(); right.Refresh(); });
            ToolSeparator(tools);
            AddViewToolbarControls(tools);
            Tool(tools, "invert", L.Text("selection.invertMarks") + " (Num *)", delegate { active.InvertMarks(); });
            var directions = new StackPanel { Orientation = Orientation.Horizontal };
            Tool(directions, "forward", L.Text("toolbar.folderLeftToRight"), delegate { OpenFolderInOtherPanel(left); });
            Tool(directions, "back", L.Text("toolbar.folderRightToLeft"), delegate { OpenFolderInOtherPanel(right); });
            tools.Children.Add(directions);
            updateDirectionTools = delegate {
                double needed = 0;
                foreach (UIElement item in directions.Children) { item.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity)); needed += item.DesiredSize.Width; }
                foreach (UIElement item in tools.Children) if (item != directions) { item.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity)); needed += item.DesiredSize.Width; }
                directions.Visibility = panelLayout != "Single" && tools.ActualWidth >= needed ? Visibility.Visible : Visibility.Collapsed;
            };
            tools.SizeChanged += delegate { updateDirectionTools(); };
            ToolSeparator(tools);
            toolbarPreviewButton = Tool(tools, "view", L.Text("toolbar.filePreviewHelp"), ToggleQuickPreview, L.Text("toolbar.filePreview"));
            Tool(tools, "edit", L.Text("toolbar.editHelp"), EditSelected, L.Text("toolbar.edit"));
            toolbarThumbnailsButton = Tool(tools, "picture", L.Text("media.tilesHelp"), delegate { active.SetDisplay(active.DisplayMode == "Thumbnails" ? "Details" : "Thumbnails"); }, L.Text("action.preview"));
            ToolSeparator(tools);
            Tool(tools, "pack", L.Text("toolbar.compressShortcut"), CreateArchive, L.Text("action.compressArchive"));
            Tool(tools, "unpack", L.Text("toolbar.extractShortcut"), ExtractArchives, L.Text("action.extractArchive"));
            Tool(tools, "ftp", L.Text("connections.title"), delegate { ManageConnections(); }, L.Text("toolbar.connections"));
            ToolSeparator(tools);
            Tool(tools, "search", L.Text("toolbar.findFilesShortcut"), FindFiles, L.Text("action.search"));
            Tool(tools, "favorite", L.Text("toolbar.favoritesShortcut"), ShowFavorites);
            Tool(tools, "settings", L.Text("settings.title"), delegate { new ConfigurationDialog(this).ShowDialog(); });
            top.Children.Add(new Border { Tag = "main-toolbar-frame", Child = tools, Margin = new Thickness(8, 4, 8, 6), CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 4, 6, 4), Background = CompactTheme.PanelSurface, BorderBrush = CompactTheme.ChromeLine, BorderThickness = new Thickness(1) });

            var commandRow = new DockPanel { Margin = new Thickness(8, 2, 8, 6) };
            DockPanel.SetDock(commandPrompt, Dock.Left); commandRow.Children.Add(commandPrompt); commandRow.Children.Add(commandLine);
            commandLine.ToolTip = L.Text("commandLine.inputHelp");
            commandLine.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { commandLine.Clear(); active.List.Focus(); e.Handled = true; }
                if (e.Key == Key.Enter && !String.IsNullOrWhiteSpace(commandLine.Text))
                {
                    if (active.IsRemote || active.IsArchive) { MessageBox.Show(this, L.Text("commandLine.localFolderRequired"), L.Text("commandLine.title")); e.Handled = true; return; }
                    try
                    {
                        Process.Start(new ProcessStartInfo("cmd.exe", "/K " + commandLine.Text) { WorkingDirectory = active.CurrentPath, UseShellExecute = true });
                        commandLine.Clear(); active.List.Focus();
                    }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("commandLine.launchFailed")); }
                    e.Handled = true;
                }
            };
            InitializeCopySummary(); bottom.Children.Add(copySummary);
            bottom.Children.Add(commandRow);
            var keys = new UniformGrid { Rows = 1, Columns = 7, Margin = new Thickness(6, 0, 6, 7) };
            AddAction(keys, L.Text("functionKey.view"), delegate { active.ViewSelected(); }); AddAction(keys, L.Text("functionKey.edit"), EditSelected);
            AddAction(keys, L.Text("functionKey.copy"), delegate { Transfer(false); }); AddAction(keys, L.Text("functionKey.move"), delegate { Transfer(true); });
            AddAction(keys, L.Text("functionKey.newFolder"), NewFolder);
            AddAction(keys, L.Text("functionKey.delete"), DeleteSelected);
            AddAction(keys, L.Text("functionKey.exit"), Close); bottom.Children.Add(keys);
        }

        MenuItem MenuRoot(Menu menu, string name) { var item = new MenuItem { Header = name, Height = 28, Padding = new Thickness(8, 3, 8, 3) }; menu.Items.Add(item); return item; }
        void AddArchiveMenu(MenuItem parent, string title, Action action, string shortcut, string icon)
        {
            AddMenu(parent, title, action, shortcut);
            ((MenuItem)parent.Items[parent.Items.Count - 1]).Icon = DesktopIcons.Toolbar(icon, 16);
            toolbarImages.Add((Image)((MenuItem)parent.Items[parent.Items.Count - 1]).Icon);
        }
        void AddMenu(MenuItem parent, string title, Action action, string shortcut = "")
        {
            var item = new MenuItem { Header = title, InputGestureText = shortcut, IsEnabled = action != null, ToolTip = action == null ? L.Text("common.featureNotImplemented") : null };
            ToolTipService.SetShowOnDisabled(item, true);
            if (action != null) item.Click += delegate { if (!busy && !PanelsLoading) action(); }; parent.Items.Add(item);
        }
        Button Tool(Panel panel, string icon, string tooltip, Action action, string label = null)
        {
            var image = DesktopIcons.Toolbar(icon, 20); toolbarImages.Add(image);
            object content = image;
            if (label != null)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(image); row.Children.Add(new TextBlock { Text = label, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }); content = row;
            }
            var button = CompactTheme.Button(content, tooltip, delegate { if (!busy && !PanelsLoading && action != null) action(); });
            CompactTheme.ToolbarTile(button);
            System.Windows.Automation.AutomationProperties.SetName(button, label ?? tooltip);
            if (action == null) { button.IsEnabled = false; button.ToolTip = tooltip + L.Text("common.notImplementedSuffix"); } else actions.Add(button);
            ToolTipService.SetShowOnDisabled(button, true); panel.Children.Add(button);
            return button;
        }
        void ToolSeparator(Panel panel) { panel.Children.Add(new Border { Width = 1, Height = 22, Background = CompactTheme.Line, Margin = new Thickness(5, 7, 5, 7) }); }
        void OpenExplorer() { if (active.IsArchive) { Process.Start(new ProcessStartInfo(Path.GetDirectoryName(active.Archive.Root.FilePath)) { UseShellExecute = true }); return; } if (active.IsRemote) { MessageBox.Show(this, L.Text("network.directPanelConnectionHelp"), L.Text("network.connectionTitle")); return; } if (active.CurrentPath != null) Process.Start(new ProcessStartInfo(active.CurrentPath) { UseShellExecute = true }); }
        internal void OpenPowerShell()
        {
            if (active.IsRemote || active.IsArchive || active.IsVirtualBrowser || active.CurrentPath == null) { MessageBox.Show(this, L.Text("shell.localFolderRequired"), L.Text("shell.powerShellTitle")); return; }
            try { Process.Start(PowerShellStartInfo(active.CurrentPath)); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("shell.powerShellTitle")); }
        }
        internal static ProcessStartInfo PowerShellStartInfo(string path)
        {
            // The folder is a literal value, never executable PowerShell source.
            string command = "Set-Location -LiteralPath '" + path.Replace("'", "''") + "'";
            return new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"), "-NoLogo -NoExit -EncodedCommand " + Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command))) { UseShellExecute = true, WorkingDirectory = path };
        }
        void SwapPanels()
        {
            if (PanelsLoading) return;
            CloseQuickPreview();
            var split = left.Parent as Grid; if (split == null) return;
            var temporary = left; left = right; right = temporary;
            SetPanelLayout(panelLayout, false, false);
        }
        void EditSelected()
        {
            if (active.IsArchive) { var item = active.List.SelectedItem as Entry; if (item != null && !item.IsDirectory) OpenArchiveFile(active, item, true); return; }
            if (active.IsRemote) { EditRemoteSelected(); return; }
            var entry = active.List.SelectedItem as Entry; if (entry == null || entry.IsDirectory) return;
            try { FileViewers.Open(this, entry.FullPath, true); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("editor.openFailed")); }
        }
        void UpdatePrompt() { if (active != null) commandPrompt.Text = (active.DisplayPath ?? "") + ">"; }
        internal void ShowConnections(Window dialogOwner) { ManageConnections(null, dialogOwner); }
        internal string[] ViewFolders()
        {
            if (left.LocalLoading || right.LocalLoading) throw new IOException(L.Text("settings.waitBeforeSavingView"));
            string fallback = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new[] { left.SavedLocalFolder ?? fallback, right.SavedLocalFolder ?? fallback };
        }
        void SaveView()
        {
            try { var paths = ViewFolders(); var settings = ProgramSettings.Load(); settings.RememberFolders(paths[0], paths[1]); CaptureView(settings); settings.Save(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("settings.saveViewFailed"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }
}




