using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace FileCompass {
    internal static class UxSurfaceTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root);var passed=new List<string>();MainWindow main=null;ConfigurationDialog config=null;
            Action<bool,string> check=(valid,text)=>{if(!valid)throw new Exception(text);passed.Add(text);};
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                main=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){Width=1440,Height=900,ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};
                main.Show();NavigationTests.WaitPanels(main);main.UpdateLayout();
                var frame=FluentUiTests.Children<Border>(main).Single(x => (string)x.Tag=="main-toolbar-frame");
                check(frame.Margin.Left>=8 && frame.Margin.Right>=8 && frame.CornerRadius.TopLeft>=6,"Toolbar is inset and has rounded corners");
                NextStageTests.Draw(main,Path.Combine(root,"hlavni-okno.png"));
                config=new ConfigurationDialog(main){ShowActivated=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};
                config.Show();config.UpdateLayout();
                for(int i=0;i<config.Sections.Items.Count;i++) {
                    config.Sections.SelectedIndex=i;config.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
                    NextStageTests.Draw(config,Path.Combine(root,"konfigurace-"+i+".png"));
                    var fields=AdvancedToolsTests.Visual<TextBox>(config).Where(x=>x.IsVisible).ToArray();
                    check(fields.All(x=>x.ActualWidth>=25 && x.ActualHeight>=27),"Configuration page "+i+" retains usable input sizes");
                }
                config.Width=740;config.Height=520;config.Sections.SelectedIndex=2;config.UpdateLayout();
                NextStageTests.Draw(config,Path.Combine(root,"konfigurace-uzka.png"));
                check(AdvancedToolsTests.Visual<ScrollViewer>(config).Any(x=>x.ScrollableHeight>0),"Small configuration window retains vertical scrolling");
                config.Close();config=null;
                File.WriteAllLines(Path.Combine(root,"ux-surfaces-result.txt"),passed.Concat(new[]{"PASS: "+passed.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"ux-surfaces-result.txt"),String.Join("\n",passed)+"\nFAIL: "+ex);return 1;}
            finally{if(config!=null)config.Close();if(main!=null)main.Close();}
        }
    }
}
