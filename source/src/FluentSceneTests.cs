#if APP_TESTS
using System;
using System.IO;
using System.Windows;
namespace FileCompass {
 internal static class FluentSceneTests {
  internal static int Run(string root){Directory.CreateDirectory(root);L.ChangeLanguage("cs");var file=Path.Combine(root,"Návrh rozhraní.pdf");File.WriteAllText(file,"Synthetic visual fixture");var main=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){Left=0,Top=0,Width=1050,Height=690,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();var dialog=new CopySetupDialog(main,new[]{new Entry{FullPath=file,Name=Path.GetFileName(file),Bytes=24}},root,new string[0]);dialog.WindowStartupLocation=WindowStartupLocation.Manual;dialog.Left=170;dialog.Top=20;var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(12)};timer.Tick+=delegate{timer.Stop();dialog.Close();};timer.Start();dialog.ShowDialog();main.Close();return 0;}
 }
}
#endif
