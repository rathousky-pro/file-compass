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
     AppPaths.TestProfileDirectory=Path.Combine(root,"profile");
     FluentTheme.Ensure();
     var view=new UpdatesView(null);
     window=new Window{Width=1100,Height=780,Content=view,Left=-18000,Top=-18000,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
     window.Show();
     window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));
     if(view.Items.Items.Count!=4 || view.AppItems.Items.Count!=1 || view.Items.Items.OfType<UpdateRow>().Any(x=>!VendorUpdates.Ids.Contains(x.Id)))throw new IOException("Unexpected update entries");
     var vendorRow=view.Items.Items.OfType<UpdateRow>().First();
     view.Items.SelectedItem=vendorRow;
     if(view.AppItems.SelectedItem!=null || !Descendants<Button>(view).Any(x=>Convert.ToString(x.Content)==L.Text("updates.download")))throw new IOException("Selecting a component does not switch the actions");
     var install=Descendants<Button>(view).Single(x=>Convert.ToString(x.Content)==L.Text("updates.appInstall"));
     var rollback=Descendants<Button>(view).Single(x=>Convert.ToString(x.Content)==L.Text("updates.rollback"));
     if(install.Visibility!=Visibility.Collapsed || rollback.Visibility!=Visibility.Visible)throw new IOException("Component actions have incorrect visibility");
     view.Render();
     if(((UpdateRow)view.Items.SelectedItem).Id!=vendorRow.Id || view.AppItems.SelectedItem!=null)throw new IOException("Refresh did not preserve the selected component");
     view.AppItems.SelectedItem=view.AppItems.Items[0];
     if(view.Items.SelectedItem!=null || !Descendants<Button>(view).Any(x=>Convert.ToString(x.Content)==L.Text("updates.appDownload")))throw new IOException("Selecting the application does not switch the actions");
     if(install.Visibility!=Visibility.Collapsed || rollback.Visibility!=Visibility.Collapsed)throw new IOException("Application without a prepared update shows inapplicable actions");
     var package=new VendorUpdate{Id="app",Version="99.0.0",Channel="beta",Build="20991231.100",FileName=ApplicationUpdates.AssetName,Url="https://github.com/"+ApplicationUpdates.Repository+"/releases/download/"+ApplicationUpdates.Tag("99.0.0","beta","20991231.100")+"/"+ApplicationUpdates.AssetName};
     string pendingPath=Path.Combine(AppPaths.ConfigDirectory,"application-update.xml");
     ConfigurationIntegrity.Write(pendingPath,new PendingApplicationUpdate{Folder=@"application\0123456789abcdef0123456789abcdef",Package=package});
     view.Render();
     if(install.Visibility!=Visibility.Visible || install.IsEnabled || rollback.Visibility!=Visibility.Collapsed)throw new IOException("Prepared application update should show install action, disabled without a manager");
     view.Items.SelectedItem=view.Items.Items[0];
     if(install.Visibility!=Visibility.Collapsed || rollback.Visibility!=Visibility.Visible)throw new IOException("Prepared application action leaked into component selection");
     File.Delete(pendingPath);
     view.AppItems.SelectedItem=view.AppItems.Items[0];
     view.Render();
     if(view.AppItems.SelectedItem==null || view.Items.SelectedItem!=null)throw new IOException("Refresh did not preserve the selected application");
     view.AppItems.UpdateLayout();
     var appRow=view.AppItems.Items.OfType<UpdateRow>().FirstOrDefault(x=>x.Id=="app");
     if(appRow==null)throw new IOException("Missing File Compass update row");
     view.AppItems.ScrollIntoView(appRow);
     view.AppItems.UpdateLayout();
     window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));
     var row=view.AppItems.ItemContainerGenerator.ContainerFromItem(appRow) as DataGridRow;
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
