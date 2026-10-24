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
            toolbarLayoutButton = Tool(tools, "details", L.Text("toolbar.panelLayoutHelp"), CycleToolbarLayout, L.Text("toolbar.panelsSideBySide"));
            toolbarAllFilesButton = Tool(tools, "document", L.Text("toolbar.allFilesHelp"), ToggleToolbarAllFiles, L.Text("toolbar.allFilesYes"));
            UpdateViewToolbarControls();
        }

        void CycleToolbarLayout() { SetPanelLayout(panelLayout == "SideBySide" ? "Stacked" : panelLayout == "Stacked" ? "Single" : "SideBySide"); }

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
            SetToolbarLabel(toolbarLayoutButton, L.Text(panelLayout == "Single" ? "toolbar.panelsSingle" : panelLayout == "Stacked" ? "toolbar.panelsStacked" : "toolbar.panelsSideBySide"));
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





