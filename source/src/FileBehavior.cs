using System;
using System.Globalization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
namespace CommanderCZ
{
    internal static class FileBehavior
    {
        internal static int NameSelectionLength(string name)
        { int dot = name.LastIndexOf('.'); return dot > 0 ? dot : name.Length; }
        internal static int CompareNames(string left, string right, bool natural)
        {
            left = left ?? ""; right = right ?? "";
            if (!natural) return StringComparer.CurrentCultureIgnoreCase.Compare(left, right);
            int a = 0, b = 0;
            while (a < left.Length && b < right.Length) {
                bool digitA = left[a] >= '0' && left[a] <= '9', digitB = right[b] >= '0' && right[b] <= '9';
                int endA = a, endB = b;
                while (endA < left.Length && (left[endA] >= '0' && left[endA] <= '9') == digitA) endA++;
                while (endB < right.Length && (right[endB] >= '0' && right[endB] <= '9') == digitB) endB++;
                int result;
                if (digitA && digitB) {
                    int startA = a, startB = b;
                    while (startA < endA && left[startA] == '0') startA++;
                    while (startB < endB && right[startB] == '0') startB++;
                    result = (endA - startA).CompareTo(endB - startB);
                    if (result == 0) result = String.CompareOrdinal(left, startA, right, startB, endA - startA);
                    if (result == 0) result = (endA - a).CompareTo(endB - b);
                } else result = CultureInfo.CurrentCulture.CompareInfo.Compare(left.Substring(a, endA - a), right.Substring(b, endB - b), CompareOptions.IgnoreCase);
                if (result != 0) return result;
                a = endA; b = endB;
            }
            return (left.Length - a).CompareTo(right.Length - b);
        }
    }
    internal sealed class FileBehaviorEditor : StackPanel
    {
        readonly List<Action> changes = new List<Action>();
        internal FileBehaviorEditor(ProgramSettings settings)
        {
            Add("settings.renameWithoutExtension", settings.RenameWithoutExtension, delegate(bool value) { settings.RenameWithoutExtension = value; });
            Add("settings.naturalSorting", settings.NaturalSorting, delegate(bool value) { settings.NaturalSorting = value; });
            Add("settings.foldersAlwaysByName", settings.FoldersAlwaysByName, delegate(bool value) { settings.FoldersAlwaysByName = value; });
            Add("settings.driveAlwaysRoot", settings.DriveAlwaysRoot, delegate(bool value) { settings.DriveAlwaysRoot = value; });
            Add("settings.windowsMouseSelection", settings.WindowsMouseSelection, delegate(bool value) { settings.WindowsMouseSelection = value; });
            Add("settings.maskIncludesFolders", settings.MaskIncludesFolders, delegate(bool value) { settings.MaskIncludesFolders = value; });
            Add("settings.archiveNewestFileDate", settings.ArchiveNewestFileDate, delegate(bool value) { settings.ArchiveNewestFileDate = value; });
            Add("settings.folderBrackets", settings.FolderBrackets, delegate(bool value) { settings.FolderBrackets = value; });
            Add("settings.fullNameTooltips", settings.FullNameTooltips, delegate(bool value) { settings.FullNameTooltips = value; });
            Children.Add(new TextBlock { Text = L.Text("settings.localCopyDefaults"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 7) });
            Add("copy.verifySha256Option", settings.DefaultCopyVerify, delegate(bool value) { settings.DefaultCopyVerify = value; });
            Add("copy.preserveModifiedTime", settings.DefaultCopyTimes, delegate(bool value) { settings.DefaultCopyTimes = value; });
            Add("copy.preserveAttributes", settings.DefaultCopyAttributes, delegate(bool value) { settings.DefaultCopyAttributes = value; });
            Add("copy.copyNtfsPermissions", settings.DefaultCopyPermissions, delegate(bool value) { settings.DefaultCopyPermissions = value; });
            Add("copy.copyEmptyFolders", settings.DefaultCopyEmptyFolders, delegate(bool value) { settings.DefaultCopyEmptyFolders = value; });
            Add("copy.skipHiddenAndSystem", settings.DefaultCopySkipHidden, delegate(bool value) { settings.DefaultCopySkipHidden = value; });
            Children.Add(new TextBlock { Text = L.Text("action.delete"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 7) });
            Add("settings.deleteToRecycleBin", settings.DeleteToRecycleBin, delegate(bool value) { settings.DeleteToRecycleBin = value; });
            Children.Add(new TextBlock { Text = L.Text("settings.fileOperationDefaultsHelp"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(0, 10, 0, 0) });
        }
        void Add(string key, bool initial, Action<bool> write)
        {
            var check = new CheckBox { Content = new TextBlock { Text = L.Text(key), TextWrapping = TextWrapping.Wrap }, IsChecked = initial, Margin = new Thickness(0, 4, 0, 4) };
            Children.Add(check); changes.Add(delegate { write(check.IsChecked == true); });
        }
        internal void Apply() { foreach (var change in changes) change(); }
    }
}

