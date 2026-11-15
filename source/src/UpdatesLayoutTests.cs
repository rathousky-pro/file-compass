using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace FileCompass {
 internal static class UpdatesLayoutTests {
  static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject {
   if(parent==null) yield break;
   for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){
     var child=VisualTreeHelper.GetChild(parent,i);
     if(child is T)yield return (T)child;
     foreach(var x in Descendants<T>(child))yield return x;
   }
  }
  internal static int Run(string root) {
   Directory.CreateDirectory(root);
   string report=Path.Combine(root,"updates-layout-test.txt");
   Window window=null;
   try {
     FluentTheme.Ensure();
     var view=new UpdatesView(null);
     window=new Window{Width=1100,Height=780,Content=view,Left=-18000,Top=-18000,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
     window.Show();
     window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));
     view.Items.UpdateLayout();
     var appRow=view.Items.Items.OfType<UpdateRow>().FirstOrDefault(x=>x.Id=="app");
     if(appRow==null)throw new IOException("Missing File Compass update row");
     view.Items.ScrollIntoView(appRow);
     view.Items.UpdateLayout();
     window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));
     var row=view.Items.ItemContainerGenerator.ContainerFromItem(appRow) as DataGridRow;
     if(row==null)throw new IOException("File Compass DataGridRow not realized");
     var texts=Descendants<TextBlock>(row).ToArray();
     var builds=texts.Where(x=>x.Text!=null&&x.Text.StartsWith("Build ")).ToArray();
     if(builds.Length<1)throw new IOException("Build label not present in visual tree");
     if(builds.Any(x=>x.ActualHeight < 10||!x.IsVisible))throw new IOException("Build label clipped or hidden");
     if(row.ActualHeight<44)throw new IOException("File Compass row too short: "+row.ActualHeight);
     File.WriteAllText(report,"OK row="+row.ActualHeight+" buildlabels="+builds.Length+" heights="+String.Join(",",builds.Select(x=>x.ActualHeight)));
     return 0;
   } catch(Exception ex){File.WriteAllText(report,ex.ToString());return 1;}
   finally {if(window!=null)window.Close();}
  }
 }
}
