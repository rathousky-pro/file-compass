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
        public bool Move, SelfExtracting; public string Format;
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
            var sfx=add(toolsGroup,L.Text("archive.selfExtracting"),false);sfx.ToolTip=L.Text("archive.sfxHelp");ToolTipService.SetShowOnDisabled(sfx,true);toolsGroup.Children.Add(new TextBlock{Text=L.Text("archive.sfxHelp"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,8,0,8)});
            toolsGroup.Children.Add(CopyUi.Button(L.Text("action.help"),delegate{HelpService.Open(this,"archives");}));
            Action applyFormat=delegate{
                string name=(string)formats.SelectedItem;format=name=="7-ZIP"?"7z":name=="LHA*"?"lzh":name.TrimEnd('*').ToLowerInvariant();
                level.IsEnabled=format!="tar"&&format!="lzh";
                level.ToolTip=L.Text(level.IsEnabled?"archive.compressionQualityHelp":"archive.fixedCompressionHelp");ToolTipService.SetShowOnDisabled(level,true);target.Text=ChangeFormat(target.Text,sfx.IsChecked==true?"exe":format);
                bool supported=format=="zip"||format=="7z";volumes.IsEnabled=supported&&(sfx.IsChecked!=true||format=="7z");password.IsEnabled=supported;if(!volumes.IsEnabled)volumes.IsChecked=false;sfx.IsEnabled=ArchiveSelfExtract.Supports(format);if(!sfx.IsEnabled)sfx.IsChecked=false;
                if(!supported){volumes.IsChecked=false;password.IsChecked=false;}
            };formats.SelectionChanged+=delegate{applyFormat();};sfx.Checked+=delegate{applyFormat();};sfx.Unchecked+=delegate{applyFormat();};applyFormat();
            var ok = CopyUi.Button(L.Text("action.compressArchive"), delegate
            {
                try
                {
                    string destination = Environment.ExpandEnvironmentVariables(target.Text.Trim());
                    if (count > 1 && separate.IsChecked != true && format == "gz") throw new IOException(L.Text("archive.singleStreamRequired"));
                    if (!Path.IsPathRooted(destination)) throw new IOException(L.Text("archive.absoluteOutputPathRequired"));
                    destination = Path.GetFullPath(ChangeFormat(destination, sfx.IsChecked==true?"exe":format));
                    if (!Directory.Exists(Path.GetDirectoryName(destination))) throw new IOException(L.Text("archive.outputFolderMissing"));
                    if (password.IsChecked == true && secret.Password.Length == 0) throw new IOException(L.Text("archive.passwordRequired"));
                    if (password.IsChecked == true && format != "zip" && format != "7z") throw new IOException(L.Text("archive.passwordRequiresZipOr7z"));
                    ArchiveSettings.Load().ValidateCreationTool(ChangeFormat(destination,format));if(sfx.IsChecked==true)ArchiveSelfExtract.Validate(format,ArchiveSettings.Load());
                    if (move.IsChecked == true && recursive.IsChecked != true) throw new IOException(L.Text("archive.moveNeedsRecursive"));
                    if (volumes.IsChecked == true && format != "zip" && format != "7z") throw new IOException(L.Text("archive.volumeFormats"));
                    if ((volumes.IsChecked == true || move.IsChecked == true) && File.Exists(destination)) throw new IOException(L.Text("parts.outputExists"));
                    Options = new ArchivePackOptions { SelfExtracting=sfx.IsChecked==true,Format=format,Move = move.IsChecked == true, VolumeBytes = volumes.IsChecked == true ? FileParts.ParseSize(volumeSize.Text) : 0, Target = destination, Level = level.SelectedIndex, Paths = paths.IsChecked == true, Recursive = recursive.IsChecked == true,
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
            if(AppearanceTheme.IsClean){
                var name=new TextBox{Text=Path.GetFileName(target.Text),Margin=new Thickness(0,3,0,3)};
                var folder=new TextBox{Text=Path.GetDirectoryName(target.Text),Margin=new Thickness(0,3,5,3)};bool syncing=false;
                ((GroupBox)destinationGroup.Parent).Header=L.Text("archive.outputName");targetRow.Children.Clear();targetRow.Children.Add(name);
                var destination=OperationDialogLayout.Group(body,L.Text("archive.outputFolder"));var destinationRow=new DockPanel();destination.Children.Add(destinationRow);DockPanel.SetDock(browse,Dock.Right);destinationRow.Children.Add(browse);destinationRow.Children.Add(folder);
                Action sync=delegate{if(syncing)return;try{syncing=true;target.Text=Path.Combine(folder.Text,name.Text);}catch(ArgumentException){}finally{syncing=false;}};name.TextChanged+=delegate{sync();};folder.TextChanged+=delegate{sync();};
                target.TextChanged+=delegate{if(syncing)return;try{syncing=true;name.Text=Path.GetFileName(target.Text);folder.Text=Path.GetDirectoryName(target.Text);}catch(ArgumentException){}finally{syncing=false;}};
                var originalContents=(GroupBox)contents.Parent;body.Children.Remove(originalContents);originalContents.Content=null;filesPage.Children.Insert(0,new GroupBox{Header=L.Text("operation.contents"),Content=contents,Margin=new Thickness(0,0,0,12)});
                var summaries=new WrapPanel{Margin=new Thickness(0,5,0,10)};body.Children.Add(summaries);
                var volumeSummary=CopyUi.Button(L.Text("archive.noVolumes"),delegate{layout.SelectPage(1);});var passwordSummary=CopyUi.Button(L.Text("archive.noPassword"),delegate{layout.SelectPage(3);});var sfxSummary=CopyUi.Button(L.Text("archive.noSfx"),delegate{layout.SelectPage(4);});
                foreach(var chip in new[]{volumeSummary,passwordSummary,sfxSummary}){chip.MinWidth=0;chip.Tag="archive-summary";chip.Padding=new Thickness(9,4,9,4);summaries.Children.Add(chip);}
                Action summary=delegate{volumeSummary.Content=volumes.IsChecked==true?L.Format("archive.volumeSummary",volumeSize.Text):L.Text("archive.noVolumes");passwordSummary.Content=L.Text(password.IsChecked==true?"archive.withPassword":"archive.noPassword");sfxSummary.Content=L.Text(sfx.IsChecked==true?"archive.withSfx":"archive.noSfx");};volumes.Checked+=delegate{summary();};volumes.Unchecked+=delegate{summary();};volumeSize.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler(delegate{summary();}));password.Checked+=delegate{summary();};password.Unchecked+=delegate{summary();};sfx.Checked+=delegate{summary();};sfx.Unchecked+=delegate{summary();};
                var output=new TextBlock{Text=target.Text,Foreground=FluentTheme.Muted,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,6,0,0)};body.Children.Add(output);target.TextChanged+=delegate{output.Text=target.Text;};
                Loaded+=delegate{name.Focus();name.SelectAll();};
            }else Loaded += delegate { target.Focus(); target.SelectAll(); };
        }
        internal static string ChangeFormat(string path, string format)
        {
            string suffix = ArchiveEngine.OutputSuffix(path);
            return (suffix.Length == 0 ? path : path.Substring(0, path.Length - suffix.Length)) + "." + format;
        }
    }
}
