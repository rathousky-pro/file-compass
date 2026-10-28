using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FileCompass
{
    public sealed class ArchivePackOptions
    {
        public string Target, Password;
        public int Level = 6;
        public bool Move;
        public long VolumeBytes;
        public bool? NewestFileDate;
        public bool Paths = true, Recursive = true, Separate, ExcludeBase;
        internal ArchiveInputPolicy Inputs;
    }
    public sealed class ArchivePackDialog : Window
    {
        readonly TextBox target = new TextBox { Margin = new Thickness(2, 3, 5, 5) };
        string format = "zip";
        public ArchivePackOptions Options { get; private set; }
        public ArchivePackDialog(Window owner, int count, string initial)
        {
            CopyUi.Style(this, owner, L.Text("archive.packDialogTitle"), 780);
            var layout=new OperationDialogLayout(this,L.Text("archive.packDialogTitle"),L.Text("archive.totalFilesPrefix")+count+L.Text("archive.targetQuestionSuffix"));
            var body=layout.AddPage(L.Text("operation.basic"));var buttons=layout.Actions;
            var volumesPage=layout.AddPage(L.Text("operation.volumes"));
            var filesPage=layout.AddPage(L.Text("operation.files"));
            var passwordPage=layout.AddPage(L.Text("operation.security"));
            var toolsPage=layout.AddPage(L.Text("operation.tools"));
            var destinationGroup=OperationDialogLayout.Group(body,L.Text("copy.designDestination"));target.Text=initial;
            var targetRow=new DockPanel();destinationGroup.Children.Add(targetRow);targetRow.Children.Add(target);
            var compressionGroup=OperationDialogLayout.Group(body,L.Text("operation.format"));
            var settingsRow=new Grid();settingsRow.ColumnDefinitions.Add(new ColumnDefinition());settingsRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(140)});compressionGroup.Children.Add(settingsRow);
            var formatPanel=new StackPanel{Margin=new Thickness(0,0,12,0)};settingsRow.Children.Add(formatPanel);OperationDialogLayout.Label(formatPanel,L.Text("archive.archiverLabel"));
            var formats=new ComboBox{ItemsSource=new[]{"ZIP","7-ZIP","RAR*","TAR","GZ","ARJ*","LHA*"},SelectedIndex=0};formatPanel.Children.Add(formats);
            var levelPanel=new StackPanel();Grid.SetColumn(levelPanel,1);settingsRow.Children.Add(levelPanel);OperationDialogLayout.Label(levelPanel,L.Text("archive.compressionLevelLabel"));
            var level=new ComboBox();for(int i=0;i<=9;i++)level.Items.Add(i);level.SelectedIndex=6;level.ToolTip=L.Text("archive.memoryPolicy");levelPanel.Children.Add(level);
            var contents=OperationDialogLayout.Group(body,L.Text("operation.contents"));
            Func<Panel,string,bool,CheckBox> add=delegate(Panel panel,string text,bool value){var check=new CheckBox{Content=text,IsChecked=value,Margin=new Thickness(0,4,0,4)};panel.Children.Add(check);return check;};
            var paths=add(contents,L.Text("archive.preservePaths"),true);var recursive=add(contents,L.Text("archive.includeSubfolders"),true);
            var volumesGroup=OperationDialogLayout.Group(volumesPage,L.Text("operation.volumes"));
            var volumes=add(volumesGroup,L.Text("archive.multiVolume"),false);
            var volumeSize=new ComboBox{IsEditable=true,ItemsSource=new[]{"100 MB","700 MB","4 GB"},Text="100 MB",IsEnabled=false,Margin=new Thickness(0,8,0,0)};volumesGroup.Children.Add(volumeSize);
            volumes.Checked+=delegate{volumeSize.IsEnabled=true;};volumes.Unchecked+=delegate{volumeSize.IsEnabled=false;};
            var filesGroup=OperationDialogLayout.Group(filesPage,L.Text("operation.files"));
            var move=add(filesGroup,L.Text("archive.moveInto"),false);move.ToolTip=L.Text("archive.moveVerificationHelp");
            var separate=add(filesGroup,L.Text("archive.separatePerSelection"),false);
            var exclude=add(filesGroup,L.Text("archive.omitBaseFolder"),false);exclude.IsEnabled=false;exclude.Margin=new Thickness(18,4,0,4);
            separate.Checked+=delegate{exclude.IsEnabled=true;};separate.Unchecked+=delegate{exclude.IsEnabled=false;exclude.IsChecked=false;};
            var passwordGroup=OperationDialogLayout.Group(passwordPage,L.Text("operation.security"));
            var password=add(passwordGroup,L.Text("archive.setPassword"),false);
            var secret=new PasswordBox{Margin=new Thickness(0,8,0,0),IsEnabled=false};passwordGroup.Children.Add(secret);
            password.Checked+=delegate{secret.IsEnabled=true;secret.Focus();};password.Unchecked+=delegate{secret.IsEnabled=false;secret.Clear();};
            var toolsGroup=OperationDialogLayout.Group(toolsPage,L.Text("operation.tools"));
            toolsGroup.Children.Add(new TextBlock{Text=L.Text("archive.formatCreationHelp"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,10)});
            toolsGroup.Children.Add(CopyUi.Button(L.Text("action.settings"),delegate{new ConfigurationDialog(this,L.Text("settings.archivesSection")).ShowDialog();}));
            var sfx=add(toolsGroup,L.Text("archive.selfExtracting"),false);sfx.IsEnabled=false;sfx.ToolTip=L.Text("common.optionNotSupported");ToolTipService.SetShowOnDisabled(sfx,true);
            toolsGroup.Children.Add(CopyUi.Button(L.Text("action.help"),delegate{HelpService.Open(this,"archives");}));
            Action applyFormat=delegate{
                string name=(string)formats.SelectedItem;format=name=="7-ZIP"?"7z":name=="LHA*"?"lzh":name.TrimEnd('*').ToLowerInvariant();
                level.IsEnabled=format!="tar"&&format!="lzh";
                level.ToolTip=L.Text(level.IsEnabled?"archive.compressionQualityHelp":"archive.fixedCompressionHelp");ToolTipService.SetShowOnDisabled(level,true);target.Text=ChangeFormat(target.Text,format);
                bool supported=format=="zip"||format=="7z";volumes.IsEnabled=password.IsEnabled=supported;
                if(!supported){volumes.IsChecked=false;password.IsChecked=false;}
            };formats.SelectionChanged+=delegate{applyFormat();};applyFormat();
            var ok = CopyUi.Button(L.Text("action.compressArchive"), delegate
            {
                try
                {
                    string destination = Environment.ExpandEnvironmentVariables(target.Text.Trim());
                    if (count > 1 && separate.IsChecked != true && format == "gz") throw new IOException(L.Text("archive.singleStreamRequired"));
                    if (!Path.IsPathRooted(destination)) throw new IOException(L.Text("archive.absoluteOutputPathRequired"));
                    destination = Path.GetFullPath(ChangeFormat(destination, format));
                    if (!Directory.Exists(Path.GetDirectoryName(destination))) throw new IOException(L.Text("archive.outputFolderMissing"));
                    if (password.IsChecked == true && secret.Password.Length == 0) throw new IOException(L.Text("archive.passwordRequired"));
                    if (password.IsChecked == true && format != "zip" && format != "7z") throw new IOException(L.Text("archive.passwordRequiresZipOr7z"));
                    ArchiveSettings.Load().ValidateCreationTool(destination);
                    if (move.IsChecked == true && recursive.IsChecked != true) throw new IOException(L.Text("archive.moveNeedsRecursive"));
                    if (volumes.IsChecked == true && format != "zip" && format != "7z") throw new IOException(L.Text("archive.volumeFormats"));
                    if ((volumes.IsChecked == true || move.IsChecked == true) && File.Exists(destination)) throw new IOException(L.Text("parts.outputExists"));
                    Options = new ArchivePackOptions { Move = move.IsChecked == true, VolumeBytes = volumes.IsChecked == true ? FileParts.ParseSize(volumeSize.Text) : 0, Target = destination, Level = level.SelectedIndex, Paths = paths.IsChecked == true, Recursive = recursive.IsChecked == true,
                        Separate = separate.IsChecked == true, ExcludeBase = exclude.IsChecked == true, Password = password.IsChecked == true ? secret.Password : null }; DialogResult = true;
                }
                catch (ArchiveToolMissingException ex) { new ConfigurationDialog(this, L.Text("settings.archivesSection"), ex.Message).ShowDialog(); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("action.compressArchive")); }
            }); ok.IsDefault = true; FluentTheme.Primary(ok);
            var browse=CopyUi.Button(L.Text("action.browse"), delegate
            {
                using (var picker = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = Path.GetDirectoryName(target.Text) })
                    if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) target.Text = Path.Combine(picker.SelectedPath, Path.GetFileName(target.Text));
            });
            DockPanel.SetDock(browse,Dock.Right);targetRow.Children.Remove(target);targetRow.Children.Add(browse);targetRow.Children.Add(target);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel); buttons.Children.Add(ok);

            Loaded += delegate { target.Focus(); target.SelectAll(); };
        }
        internal static string ChangeFormat(string path, string format)
        {
            string suffix = ArchiveEngine.OutputSuffix(path);
            return (suffix.Length == 0 ? path : path.Substring(0, path.Length - suffix.Length)) + "." + format;
        }
    }
}
