using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace FileCompass {
 internal static class ConflictChoiceTests {
  internal static int Run(string root) {
   Directory.CreateDirectory(root);
   string report=Path.Combine(root,"conflict-choice-test.txt");
   Window window=null;
   try {
    L.ChangeLanguage("cs");FluentTheme.Ensure();
    var selector=new ConflictChoiceSelector<CopyRule>(
      new[]{Tuple.Create(CopyRule.OverwriteAll,L.Text("action.overwrite")),Tuple.Create(CopyRule.SkipAll,L.Text("action.skip")),Tuple.Create(CopyRule.Ask,L.Text("conflict.ask"))},
      new[]{Tuple.Create(CopyRule.Older,L.Text("conflict.overwriteOlderTargets")),Tuple.Create(CopyRule.RenameAll,L.Text("conflict.autoRenameCopies"))},CopyRule.Ask);
    window=new Window{Content=selector,Width=700,Height=240,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual,ShowInTaskbar=false};
    window.Show();window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));
    var radios=((WrapPanel)selector.Children[0]).Children.OfType<RadioButton>().ToArray();
    if(radios.Length!=4 || radios.Count(x=>x.IsChecked==true)!=1||selector.Value!=CopyRule.Ask)throw new IOException("Initial policy not exclusively selected");
    radios[0].IsChecked=true;
    if(selector.Value!=CopyRule.OverwriteAll||radios.Count(x=>x.IsChecked==true)!=1)throw new IOException("Overwrite toggle failed");
    radios[1].IsChecked=true;
    if(selector.Value!=CopyRule.SkipAll||radios.Count(x=>x.IsChecked==true)!=1)throw new IOException("Skip toggle failed");
    selector.Select(CopyRule.RenameAll);
    if(selector.Value!=CopyRule.RenameAll||radios[3].IsChecked!=true||selector.Children.OfType<ComboBox>().Single().Visibility!=Visibility.Visible)throw new IOException("More selector failed");
    selector.Select(CopyRule.Ask);
    if(selector.Value!=CopyRule.Ask||radios.Count(x=>x.IsChecked==true)!=1||selector.Children.OfType<ComboBox>().Single().Visibility!=Visibility.Collapsed)throw new IOException("Returning from More failed");
    File.WriteAllText(report,"PASS: mutually exclusive Overwrite / Skip / Ask / More selections");
    return 0;
   }catch(Exception ex){File.WriteAllText(report,"FAIL: "+ex);return 1;}
   finally{if(window!=null)window.Close();}
  }
 }
}
