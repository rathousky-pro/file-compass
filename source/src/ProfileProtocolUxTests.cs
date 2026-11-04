#if APP_TESTS
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Threading;
namespace FileCompass {
 internal static class ProfileProtocolUxTests {
  static void Pump(){NavigationTests.Wait(Task.Delay(150));}
  internal static int Run(string root){Directory.CreateDirectory(root);var checks=new List<string>();NetworkProfileDialog dialog=null;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add(label);};try{
   SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));L.ChangeLanguage("cs");
   dialog=new NetworkProfileDialog(null,new NetworkProfile{Protocol=NetworkProtocol.SMB,Name="Synthetic connection",Host="example.invalid",UserName="synthetic-user"}){Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false};dialog.Show();Pump();dialog.UpdateLayout();
   var protocol=FluentUiTests.Children<ComboBox>(dialog).Single(c=>c.Items.Count==Enum.GetValues(typeof(NetworkProtocol)).Length&&c.Items[0] is NetworkProtocol);
   var name=FluentUiTests.Children<TextBox>(dialog).Single(t=>t.Text=="Synthetic connection");name.Text="Edited connection";
   var user=FluentUiTests.Children<TextBox>(dialog).Single(t=>t.Text=="synthetic-user");user.Text="edited-user";
   var password=FluentUiTests.Children<PasswordBox>(dialog).First(p=>p.IsVisible);password.Password="synthetic-password";
   Func<GroupBox> identity=()=>FluentUiTests.Children<GroupBox>(dialog).Single(g=>Convert.ToString(g.Header)==L.Text("toolbar.connections"));
   var template=identity().Template;var background=identity().Background;var padding=identity().Padding;double font=name.FontSize,height=name.ActualHeight;double initialHeight=dialog.ActualHeight;
   var titleFrame=FluentUiTests.Children<System.Windows.Controls.Border>(dialog).Single(b=>Convert.ToString(b.Tag)=="fluent-title");var title=((DockPanel)titleFrame.Child).Children.OfType<TextBlock>().Single();
   check(title.Text==dialog.Title&&title.Text.EndsWith("SMB"),"initial visible title matches SMB");
   protocol.Focus();Pump();check(protocol.FocusVisualStyle==null&&protocol.Template.FindName("FieldFrame",protocol)!=null,"protocol focus uses the standard field frame without a second focus adorner");NextStageTests.Draw(dialog,Path.Combine(root,"initial-smb.png"));
   foreach(NetworkProtocol type in new[]{NetworkProtocol.FTP,NetworkProtocol.SFTP,NetworkProtocol.SCP,NetworkProtocol.SSH,NetworkProtocol.NFS,NetworkProtocol.SSHFS,NetworkProtocol.SMB}){
    protocol.IsDropDownOpen=true;protocol.SelectedItem=type;protocol.IsDropDownOpen=false;
    check(Object.ReferenceEquals(identity().Template,template)&&Object.ReferenceEquals(identity().Background,background)&&identity().Padding==padding,"rebuilt "+type+" form already has the initial appearance before the next render");
    Pump();dialog.UpdateLayout();check(title.Text==dialog.Title&&title.Text.EndsWith(type.ToString()),"visible title follows selected protocol "+type);
    check(Object.ReferenceEquals(identity().Template,template)&&Object.ReferenceEquals(identity().Background,background)&&identity().Padding==padding,"settled "+type+" form retains the same section appearance");
    check(name.FontSize==font&&Math.Abs(name.ActualHeight-height)<1&&protocol.FocusVisualStyle==null,"field metrics and single focus frame remain stable for "+type);
    check(name.Text=="Edited connection"&&user.Text=="edited-user"&&password.Password=="synthetic-password","protocol switch preserves edited name and credentials for "+type);
    check(FluentUiTests.Children<Grid>(dialog).Count(g=>Convert.ToString(g.Tag)=="fluent-stage")==1,"protocol switch retains one window shell for "+type);
    if(type==NetworkProtocol.FTP||type==NetworkProtocol.SMB)NextStageTests.Draw(dialog,Path.Combine(root,"switched-"+type+".png"));
   }
   check(Math.Abs(dialog.ActualHeight-initialHeight)<2,"returning to SMB restores the original compact height");
   dialog.Close();dialog=null;File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(dialog!=null)dialog.Close();}}
 }
}
#endif
