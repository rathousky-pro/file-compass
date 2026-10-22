using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace CommanderCZ {
    internal static class VisualFeedbackTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root); var checks = new List<string>(); MainWindow main = null;
            Action<bool,string> check = (valid,label) => { if (!valid) throw new Exception(label); checks.Add(label); };
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                var source = Directory.CreateDirectory(Path.Combine(root,"Žluťoučká složka")).FullName;
                File.WriteAllBytes(Path.Combine(source,"data.bin"),new byte[3*1048576]);
                var updates = new List<string>();
                ArchiveEngine.Create(Path.Combine(root,"test.zip"),new[]{source},ArchiveSettings.Load(),CancellationToken.None,updates.Add,new ArchivePackOptions());
                ArchivePreparationProgress parsed;
                check(updates.Any(x => ArchivePreparationProgress.TryParse(x,out parsed) && parsed.Path.Contains("Žluťoučká")),"Preparation reports original Unicode source path");
                check(updates.Any(x => ArchivePreparationProgress.TryParse(x,out parsed) && parsed.Bytes > 0),"Preparation reports bytes while copying");
                check(ArchiveEngine.Open(Path.Combine(root,"test.zip"),null,CancellationToken.None).Items.Any(x => x.Path == "data.bin" || x.Path.EndsWith("/data.bin")),"Prepared archive is readable");
                using(var cancel = new CancellationTokenSource()) {
                    bool cancelled=false;
                    try { ArchiveEngine.Create(Path.Combine(root,"cancel.zip"),new[]{source},ArchiveSettings.Load(),cancel.Token,x => { if(x.StartsWith("@archive-preparation|"))cancel.Cancel(); },new ArchivePackOptions()); } catch(OperationCanceledException){cancelled=true;}
                    check(cancelled && !File.Exists(Path.Combine(root,"cancel.zip")),"Cancellation during preparation does not publish archive");
                }
                main = new MainWindow(root,new ProgramSettings {AutoRefresh=false}) {Width=1440,Height=850,ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};
                main.Show(); NavigationTests.WaitPanels(main); main.CheckVisualFeedback(root,check);
                using(var cancel = new CancellationTokenSource()) {
                    var dialog = new ArchiveProgressDialog(main,"Vytváření archivu · fotografie.zip",cancel); dialog.Left=-18000;dialog.Top=-18000;dialog.Show();dialog.UpdatePreparation(new ArchivePreparationProgress { Path=@"D:\Fotografie\Dovolená\IMG_2026.jpg",Items=1248,Bytes=780*1048576L });dialog.UpdateLayout();
                    check(AdvancedToolsTests.Visual<LocalCopyProgressBar>(dialog).All(x => x.Visibility == Visibility.Collapsed),"No indeterminate bar during preparation");
                    NextStageTests.Draw(dialog,Path.Combine(root,"priprava-archivu.png"));
                    dialog.Update("Komprimace",.42,true); check(AdvancedToolsTests.Visual<LocalCopyProgressBar>(dialog).All(x => x.Visibility == Visibility.Visible),"Determinate progress appears during compression");dialog.Finish();
                }
                File.WriteAllLines(Path.Combine(root,"visual-feedback-result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            } catch(Exception ex){File.WriteAllText(Path.Combine(root,"visual-feedback-result.txt"),"FAIL after "+checks.Count+"\n"+ex);return 1;}
            finally{if(main!=null)main.Close();}
        }
    }
    public partial class MainWindow {
        internal void CheckVisualFeedback(string root,Action<bool,string> check) {
            SetPanelLayout("SideBySide");SetActive(left);left.SetDisplay("Thumbnails");
            check(System.Windows.Automation.AutomationProperties.GetItemStatus(toolbarThumbnailsButton)==L.Text("toolbar.enabled"),"Thumbnail toggle follows active panel display");
            right.SetDisplay("Details");SetActive(right);
            check(System.Windows.Automation.AutomationProperties.GetItemStatus(toolbarThumbnailsButton)==L.Text("toolbar.disabled"),"Switching panels refreshes thumbnail toggle");
            check(left.BorderBrush != right.BorderBrush && right.IsActivePanel,"Only active panel has accented outline");
            SetActive(left);toolbarAllFiles=true;UpdateViewToolbarControls();UpdateLayout();
            NextStageTests.Draw(this,Path.Combine(root,"aktivni-panel-miniatury.png"));
            toolbarAllFiles=false;left.SetDisplay("Details");UpdateViewToolbarControls();UpdateLayout();
            check(System.Windows.Automation.AutomationProperties.GetItemStatus(toolbarAllFilesButton)==L.Text("toolbar.disabled"),"Hidden-files toggle exposes disabled state");
            NextStageTests.Draw(this,Path.Combine(root,"vypnute-prepinace.png"));
        }
    }
}



