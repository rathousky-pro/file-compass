using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
namespace CommanderCZ {
    internal static class PortableTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root);var checks=new List<string>();MainWindow main=null;
            Action<bool,string> check=(valid,text)=>{if(!valid)throw new Exception(text);checks.Add(text);};
            string settings=ProgramSettings.ConfigPath;byte[] previous=File.Exists(settings)?File.ReadAllBytes(settings):null;
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                FileIntegrity.TestDirectory = null; WorkflowStore.TestRoot = null;
                string prefix=Path.GetFullPath(AppPaths.ConfigDirectory)+Path.DirectorySeparatorChar;
                foreach(var path in new[]{ProgramSettings.ConfigPath,ToolbarThemeSettings.ConfigPath,ArchiveSettings.ConfigPath,NetworkSettingsStore.DefaultPath,FileIntegrity.KeyPath,WorkflowStore.Root+Path.DirectorySeparatorChar})check(Path.GetFullPath(path).StartsWith(prefix,StringComparison.OrdinalIgnoreCase),"Portable path: "+path);
                check(new[]{"icons","languages","plugins","ui"}.All(x=>Directory.Exists(Path.Combine(AppPaths.ConfigDirectory,x))),"All supporting folders are inside config");
                check(Directory.GetFiles(AppPaths.ProgramDirectory,"*.exe").Select(Path.GetFileName).SequenceEqual(new[]{"File Compass.exe"}),"Distribution contains one main executable");
                check(ApplicationBrand.Name=="File Compass" && ToolbarThemeSettings.Load().ApplicationIcon==ApplicationIcon.FloppyOrbit,"New name and Floppy Orbit default icon");
                main=new MainWindow(root,new ProgramSettings {AutoRefresh=false}) {Width=1400,Height=850,ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};
                main.Show();NavigationTests.WaitPanels(main);main.CheckPortableView(root,check);
                File.WriteAllLines(Path.Combine(root,"portable-result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"portable-result.txt"),String.Join("\n",checks)+"\nFAIL: "+ex);return 1;}
            finally {if(main!=null)main.Close();if(previous!=null)File.WriteAllBytes(settings,previous);else if(File.Exists(settings))File.Delete(settings);}
        }
    }
    public partial class MainWindow {
        internal void CheckPortableView(string root,Action<bool,string> check) {
            foreach(string mode in new[]{"SideBySide","Stacked","Single"}) {
                SetPanelLayout(mode);UpdateLayout();SaveView();
                var saved=ProgramSettings.Load();
                check(saved.PanelLayout==mode && saved.LeftFolder==left.SavedLocalFolder && saved.RightFolder==right.SavedLocalFolder,"Save View returns without confirmation and stores folders plus "+mode);
                SetPanelLayout(mode=="Single"?"SideBySide":"Single");RestorePanelLayout(saved);UpdateLayout();
                check(panelLayout==mode,"Restoring saved view restores "+mode);
            }
            NextStageTests.Draw(this,Path.Combine(root,"file-compass.png"));
        }
    }
}



