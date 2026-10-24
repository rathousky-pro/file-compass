using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
namespace FileCompass
{
    internal sealed class StartupPreferencesEditor : StackPanel
    {
        readonly List<Action> writes = new List<Action>();
        internal StartupPreferencesEditor(ProgramSettings settings)
        {
            Add("settings.singleInstance", settings.SingleInstance, delegate(bool value) { settings.SingleInstance = value; });
            Children.Add(new TextBlock { Text = L.Text("settings.singleInstanceHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(0, 4, 0, 12) });
            Children.Add(new TextBlock { Text = L.Text("settings.automaticExitState"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 8) });
            Add("settings.saveFoldersOnExit", settings.SaveFoldersOnExit, delegate(bool value) { settings.SaveFoldersOnExit = value; });
            Add("settings.saveTabsOnExit", settings.SaveTabsOnExit, delegate(bool value) { settings.SaveTabsOnExit = value; });
            Add("settings.saveViewOnExit", settings.SaveViewOnExit, delegate(bool value) { settings.SaveViewOnExit = value; });
            Add("settings.saveWindowOnExit", settings.SaveWindowOnExit, delegate(bool value) { settings.SaveWindowOnExit = value; });
            Children.Add(new TextBlock { Text = L.Text("settings.exitStateHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(0, 8, 0, 12) });
        }
        void Add(string key, bool initial, Action<bool> write)
        {
            var check = new CheckBox { Content = new TextBlock { Text = L.Text(key), TextWrapping = TextWrapping.Wrap }, IsChecked = initial, Margin = new Thickness(0, 4, 0, 4) };
            Children.Add(check); writes.Add(delegate { write(check.IsChecked == true); });
        }
        internal void Apply() { foreach (var write in writes) write(); }
    }
}

