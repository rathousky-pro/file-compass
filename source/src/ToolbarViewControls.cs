using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FileCompass
{
    public partial class MainWindow
    {
        Button toolbarLayoutButton, toolbarAllFilesButton, toolbarPreviewButton, toolbarThumbnailsButton;
        bool toolbarAllFiles = true;
        Action updateDirectionTools;

        void AddViewToolbarControls(Panel tools)
        {
            toolbarLayoutButton=Tool(tools,"panels-SideBySide",L.Text("toolbar.panelLayoutHelp"),delegate{
                SetPanelLayout(panelLayout=="SideBySide"?"Stacked":panelLayout=="Stacked"?"Single":"SideBySide");
            });
            Tool(tools,"refresh",L.Text("toolbar.refreshShortcut"),RefreshPanels,allowDuringLoading:true);
            toolbarAllFilesButton = Tool(tools, "hidden-files", L.Text("toolbar.allFilesHelp"), ToggleToolbarAllFiles);
            UpdateViewToolbarControls();
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
                if(image!=null){image.Tag="panels-"+panelLayout;image.Source=DesktopIcons.ToolbarDrawing((string)image.Tag,ToolbarThemeSettings.Current);}
                string caption=L.Text(panelLayout=="Single"?"toolbar.panelsSingle":panelLayout=="Stacked"?"toolbar.panelsStacked":"toolbar.panelsSideBySide");
                SetToolbarLabel(toolbarLayoutButton,caption);toolbarLayoutButton.ToolTip=caption+"\n"+L.Text("toolbar.panelLayoutHelp");
            }
            SetToolbarLabel(toolbarAllFilesButton, L.Text(toolbarAllFiles ? "toolbar.allFilesYes" : "toolbar.allFilesNo"));

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
            button.Background = enabled ? CompactTheme.ThemeBrush("#E4F0FF") : CompactTheme.WhiteSurface;
            button.BorderBrush = enabled ? FluentTheme.Accent : CompactTheme.Line;
            button.Foreground = enabled ? FluentTheme.Accent : FluentTheme.Text;
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
