using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
namespace CommanderCZ {
internal static class DialogEnterTests {
internal static int Run(string root) {
Directory.CreateDirectory(root);var checks=new List<string>();Window owner=null;
Action<bool,string> check=delegate(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);};
try {
owner=new Window{Width=800,Height=600,Left=-18000,Top=-18000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};owner.Show();
Func<Window,Key,bool?> press=delegate(Window dialog,Key key){Exception error=null;dialog.Loaded+=delegate{dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{try{var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(dialog),0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};dialog.RaiseEvent(args);if(dialog.IsVisible && key==Key.Escape){var cancel=FluentUiTests.Children<Button>(dialog).First(x=>x.IsCancel);cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}if(dialog.IsVisible)throw new Exception("Key did not complete dialog");}catch(Exception ex){error=ex;dialog.Close();}}));};var result=dialog.ShowDialog();if(error!=null)throw error;return result;};
var entries=new[]{new Entry{FullPath=Path.Combine(root,"source.txt"),Name="source.txt",Bytes=3}};
foreach(bool move in new[]{false,true}){var setup=new CopySetupDialog(owner,entries,root,new string[0],move,new ProgramSettings());check(press(setup,Key.Enter)==true && setup.Options!=null && setup.Options.Move==move,"Enter accepts default "+(move?"move":"copy")+" options");}
foreach(bool permanent in new[]{false,true}){var deletion=new DeleteConfirmationDialog(owner,entries.ToList(),true,permanent,root);check(press(deletion,Key.Enter)==true && deletion.Permanent==permanent,"Enter accepts selected deletion mode permanent="+permanent);}
var remoteDelete=new DeleteConfirmationDialog(owner,entries.ToList(),false,false,root);check(press(remoteDelete,Key.Enter)==true && remoteDelete.Permanent,"Enter confirms permanent deletion when recycle is unavailable");
var network=new NetworkCopyDialog(owner,1,root,root,null,null,false);check(press(network,Key.Enter)==true && network.Rule==NetworkCopyRule.Skip,"Enter accepts network copy default");
var cancelled=new CopySetupDialog(owner,entries,root,new string[0]);check(press(cancelled,Key.Escape)!=true && cancelled.Options==null,"cancel does not create a copy operation");
File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count}));return 0;
}catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),String.Join("\n",checks)+"\nFAIL: "+ex);return 1;}finally{if(owner!=null)owner.Close();}
}
}}
