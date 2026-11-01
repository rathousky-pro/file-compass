using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;

namespace FileCompass
{
    public enum ToolbarTheme { Classic, Flat, Outline }
    public sealed class ToolbarThemeSettings
    {
        public ToolbarTheme Theme = ToolbarTheme.Classic;
        public ApplicationIcon ApplicationIcon = ApplicationIcon.FloppyOrbit;
        public static string ConfigPath { get { return System.IO.Path.Combine(AppPaths.ConfigDirectory, "appearance.xml"); } }
        public static ToolbarTheme Current = Load().Theme;
        public static ToolbarThemeSettings Load(string path = null)
        {
            try
            {
                path = path ?? ConfigPath; if (!File.Exists(path)) return new ToolbarThemeSettings();
                using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    return (ToolbarThemeSettings)new XmlSerializer(typeof(ToolbarThemeSettings)).Deserialize(reader);
            }
            catch (Exception ex) { if (!(ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is XmlException)) throw; return new ToolbarThemeSettings(); }
        }
        public void Save(string path = null)
        {
            path = path ?? ConfigPath; Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temp)) { new XmlSerializer(typeof(ToolbarThemeSettings)).Serialize(stream, this); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    public sealed class ToolbarThemeDialog : Window
    {
        public ToolbarTheme SelectedTheme { get; private set; }
        internal static readonly string[] PreviewNames = new[] { "refresh", "folder", "details", "picture", "tree", "select", "back", "forward", "pack", "unpack", "ftp", "url", "search", "rename", "edit", "sync", "favorite", "notepad" }.Concat(ToolbarSymbols.Names).Distinct().ToArray();
        static readonly string[] PreviewLabels = { L.Text("action.refresh"), L.Text("action.openFolder"), L.Text("panel.detailsView"), L.Text("toolbar.previewIconsLabel"), L.Text("panel.folderTree"), L.Text("selection.invertMarks"), L.Text("action.back"), L.Text("action.forward"), L.Text("action.compressArchive"), L.Text("action.extractArchive"), L.Text("network.connectionsTitle"), L.Text("toolbar.networkLocations"), L.Text("action.search"), L.Text("rename.batchTitle"), L.Text("action.edit"), L.Text("panel.swap"), L.Text("toolbar.projectFolder"), L.Text("toolbar.notepad") };
        internal static string PreviewLabel(string name){int index=Array.IndexOf(PreviewNames,name);if(index>=0&&index<PreviewLabels.Length)return PreviewLabels[index];return L.Text(name.StartsWith("panels-")?name=="panels-Single"?"toolbar.panelsSingle":name=="panels-Stacked"?"toolbar.panelsStacked":"toolbar.panelsSideBySide":name=="tools"?"operation.tools":name=="undo"?"undoHistory.title":name=="invert"?"selection.invertMarks":name=="settings"?"settings.title":name=="view"?"toolbar.filePreview":name=="new-file"?"codeEditor.new":name=="hidden-files-visible"?"toolbar.allFilesYes":name=="hidden-files-partial"?"toolbar.allFilesPartial":"toolbar.allFilesNo");}
        public ToolbarThemeDialog(Window owner)
        {
            CopyUi.Style(this, owner, L.Text("settings.toolbarIcons"), 740); SelectedTheme = ToolbarThemeSettings.Current;
            var body = new StackPanel { Margin = new Thickness(10) }; Content = body;
            body.Children.Add(CopyUi.Label(L.Text("settings.iconPreviewOrderHelp")));
            string[] titles = { L.Text("settings.fluentIcons"), L.Text("settings.flatIcons"), L.Text("settings.outlineIcons") };
            string[] descriptions = { L.Text("settings.fluentIconDescription"), L.Text("settings.flatIconDescription"), L.Text("settings.outlineIconDescription") };
            foreach (ToolbarTheme theme in Enum.GetValues(typeof(ToolbarTheme)))
            {
                ToolbarTheme selected = theme;
                var card = new StackPanel { Margin = new Thickness(9) };
                var radio = new RadioButton { Content = titles[(int)theme], GroupName = "ToolbarTheme", IsChecked = theme == SelectedTheme, FontWeight = FontWeights.Bold };
                radio.Checked += delegate { SelectedTheme = selected; }; card.Children.Add(radio); card.Children.Add(CopyUi.Label(descriptions[(int)theme]));
                var row = new WrapPanel();
                foreach (string name in PreviewNames)
                    row.Children.Add(new Border { Width = 34, Height = 36, Margin = new Thickness(1, 0, 1, 0), BorderThickness = new Thickness(1), BorderBrush = CompactTheme.Line, Background = CompactTheme.WhiteSurface,
                        Child = new Image { Source = DesktopIcons.ToolbarDrawing(name, theme), Width = 28, Height = 28 }, ToolTip = PreviewLabel(name) });
                card.Children.Add(row); body.Children.Add(new Border { Child = card, BorderThickness = new Thickness(1), BorderBrush = CompactTheme.Line, Margin = new Thickness(0, 3, 0, 5) });
            }
            var buttons = new UniformGrid { Columns = 2, Margin = new Thickness(360, 4, 0, 0) }; body.Children.Add(buttons);
            var save = CopyUi.Button(L.Text("action.applyAndSave"), delegate
            {
                try { new ToolbarThemeSettings { Theme = SelectedTheme, ApplicationIcon = ApplicationBrand.Current }.Save(); ToolbarThemeSettings.Current = SelectedTheme; DialogResult = true; }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("settings.toolbarIcons")); }
            }); save.IsDefault = true; buttons.Children.Add(save);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
        }
    }
}


