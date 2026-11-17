using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FileCompass
{
    public partial class MainWindow
    {
        Button toolbarLayoutButton, toolbarAllFilesButton, toolbarFolderViewButton, toolbarPreviewButton, toolbarThumbnailsButton=null;
        bool toolbarAllFiles = true;
        string toolbarHiddenSymbol="hidden-files-visible";
        Action updateDirectionTools=null;

        void AddViewToolbarControls(Panel tools)
        {
            toolbarLayoutButton=Tool(tools,"panels-SideBySide",L.Text("toolbar.panelLayoutHelp"),delegate{
                SetPanelLayout(panelLayout=="SideBySide"?"Stacked":panelLayout=="Stacked"?"Single":"SideBySide");
            });
            Tool(tools,"refresh",L.Text("toolbar.refreshShortcut"),RefreshPanels,allowDuringLoading:true);
            toolbarAllFilesButton = Tool(tools, "hidden-files", L.Text("toolbar.allFilesHelp"), ToggleToolbarAllFiles);
            toolbarFolderViewButton = Tool(tools, "tree", L.Text("menu.view"), delegate { ShowPanelViewMenu(toolbarFolderViewButton); });
            System.Windows.Automation.AutomationProperties.SetAutomationId(toolbarFolderViewButton, "PanelViewMenu");
            Button favoritesButton = null;
            favoritesButton = Tool(tools, "favorite", L.Text("favorites.openMenu"), delegate { ShowFavorites(true, favoritesButton); });
            System.Windows.Automation.AutomationProperties.SetAutomationId(favoritesButton, "PanelFavorites");
            var labelsButton = Tool(tools, "label", L.Text("tags.workspace"), ShowTaggedFiles);
            System.Windows.Automation.AutomationProperties.SetAutomationId(labelsButton, "PanelLabels");
            UpdateViewToolbarControls();
        }



        internal void ShowPanelViewMenu(FrameworkElement anchor)
        {
            var panel = active;
            if (panel == null || anchor == null) return;

            var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
            var defaults = new MenuItem { Header = L.Text("columns.useDefault"),
                IsEnabled = !panel.EmptyPanel && panel.CurrentPath != null };
            defaults.Click += async delegate
            {
                if (panel.IsFlatView)
                {
                    panel.ToggleFlatView();
                    if (panel.NavigationTask != null) await panel.NavigationTask;
                }
                panel.UseDefaultFolderView();
                if (panel.FolderTreeVisible) panel.SetTree(false);
            };
            menu.Items.Add(defaults);
            var flat = new MenuItem
            {
                Header = L.Text("projection.includeChildren"), IsCheckable = true, IsChecked = panel.IsFlatView,
                IsEnabled = panel.IsFlatView || panel.CanOpenFlatView
            };
            flat.Click += delegate { panel.ToggleFlatView(); };
            menu.Items.Add(flat);
            var tree = new MenuItem
            {
                Header = L.Text("panel.folderTree"), Icon = DesktopIcons.Toolbar("tree", 16),
                IsCheckable = true, IsChecked = panel.FolderTreeVisible
            };
            tree.Click += delegate { panel.ToggleTree(); };
            menu.Items.Add(tree);
            menu.Items.Add(new Separator());
            var view = new MenuItem { Header = panel.FolderViewCaption };
            panel.AddFolderViewOptions(view, false);
            view.IsEnabled = view.Items.OfType<MenuItem>().Any(item => item.IsEnabled);
            menu.Items.Add(view);
            if (anchor == toolbarFolderViewButton) anchor.ContextMenu = menu;
            menu.IsOpen = true;
        }

        void ToggleToolbarAllFiles()
        {
            try {
                var settings = ProgramSettings.Load();
                settings.ShowHiddenFiles = settings.ShowSystemFiles = !toolbarAllFiles;
                settings.Save(); ApplyDisplaySettings(settings);
            } catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("settings.title"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        void UpdateViewToolbarControls()
        {
            if (updateDirectionTools != null) updateDirectionTools();
            if(toolbarLayoutButton!=null){
                var image=toolbarLayoutButton.Content as Image;
                if(image!=null){image.Tag="panels-"+panelLayout;image.Source=AppearanceTheme.IsClean?CleanToolbarIcons.Drawing((string)image.Tag):DesktopIcons.ToolbarDrawing((string)image.Tag,ToolbarThemeSettings.Current);}
                string caption=L.Text(panelLayout=="Single"?"toolbar.panelsSingle":panelLayout=="Stacked"?"toolbar.panelsStacked":"toolbar.panelsSideBySide");
                SetToolbarLabel(toolbarLayoutButton,caption);toolbarLayoutButton.ToolTip=caption+"\n"+L.Text("toolbar.panelLayoutHelp");
            }
            string visibility=L.Text(toolbarHiddenSymbol=="hidden-files-partial"?"toolbar.allFilesPartial":toolbarAllFiles?"toolbar.allFilesYes":"toolbar.allFilesNo");
            SetToolbarLabel(toolbarAllFilesButton,visibility);
            if(toolbarAllFilesButton!=null){var image=toolbarAllFilesButton.Content as Image??(toolbarAllFilesButton.Content as StackPanel)?.Children.OfType<Image>().FirstOrDefault();if(image!=null){image.Tag=toolbarHiddenSymbol;image.Source=DesktopIcons.ToolbarDrawing(toolbarHiddenSymbol,ToolbarThemeSettings.Current);}toolbarAllFilesButton.ToolTip=visibility+"\n"+L.Text("toolbar.allFilesHelp");System.Windows.Automation.AutomationProperties.SetHelpText(toolbarAllFilesButton,visibility);}

            SetToolbarState(toolbarAllFilesButton, toolbarAllFiles);
            SetToolbarState(toolbarThumbnailsButton, active != null && active.DisplayMode == "Thumbnails");
            SetToolbarState(toolbarPreviewButton, FilePreviewOpen);
        }

        static void SetToolbarState(Button button, bool enabled)
        {
            if (button == null) return;
            var row = button.Content as StackPanel;
            if (row != null) {
                foreach (var mark in row.Children.OfType<Border>().Where(x => (string)x.Tag == "toggle-state").ToArray()) row.Children.Remove(mark);
            }
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, L.Text(enabled ? "toolbar.enabled" : "toolbar.disabled"));
            button.SetValue(FluentToolbarUx.ActiveProperty,enabled);FluentToolbarUx.Refresh(button);
        }

        static void SetToolbarLabel(Button button, string label)
        {
            if (button == null) return;
            var row = button.Content as StackPanel;
            if (row != null) { var text = row.Children.OfType<TextBlock>().FirstOrDefault(); if (text != null) text.Text = label; }
            System.Windows.Automation.AutomationProperties.SetName(button, label);
        }
    }
}
