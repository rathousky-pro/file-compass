using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
namespace FileCompass {
    internal static class NavigationFocusTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root);var checks=new List<string>();MainWindow window=null;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add(label);};
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                string folder=Directory.CreateDirectory(Path.Combine(root,"folder")).FullName;
                string child=Directory.CreateDirectory(Path.Combine(folder,"child")).FullName;
                Directory.CreateDirectory(Path.Combine(child,"empty"));
                File.WriteAllText(Path.Combine(child,"first.txt"),"one");File.WriteAllText(Path.Combine(child,"second.txt"),"two");
                window=new MainWindow(folder){Left=-18000,Top=-18000,ShowActivated=true,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();window.Activate();NavigationTests.WaitPanels(window);window.UpdateLayout();
                window.VerifyStartupKeyboardFocus(check);window.VerifyNavigationFocus(folder,child,check);
                window.Close();window=null;
                var saved=new ProgramSettings{LeftFolder=folder,RightFolder=folder,SaveFoldersOnExit=true,SaveTabsOnExit=true,LeftTabs=new[]{new SavedTab{Path=folder},new SavedTab{Path=child}},LeftTabIndex=1};
                window=new MainWindow(null,saved){Left=-18000,Top=-18000,ShowActivated=true,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();window.Activate();NavigationTests.WaitPanels(window);window.UpdateLayout();
                check(window.StartupActivePath==child,"Startup restores the last selected folder tab");window.VerifyStartupKeyboardFocus(check);window.Close();window=null;
                window=new MainWindow(folder){Left=-18000,Top=-18000,ShowActivated=true,WindowStartupLocation=WindowStartupLocation.Manual};
                TextBox editor=null;window.Loaded+=delegate{editor=window.StartupEditAddress();};window.Show();window.Activate();NavigationTests.WaitPanels(window);window.Dispatcher.Invoke(DispatcherPriority.ContextIdle,new Action(()=>{}));
                check(editor!=null&&Object.ReferenceEquals(Keyboard.FocusedElement,editor),"Startup does not take focus away from address editing started during loading");
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),"FAIL after "+checks.Count+" checks\n"+String.Join("\n",checks)+"\n"+ex);return 1;}
            finally{if(window!=null)window.Close();}
        }
    }
    public partial class MainWindow {
        internal string StartupActivePath{get{return active.CurrentPath;}}
        internal TextBox StartupEditAddress(){active.FocusAddress();return Keyboard.FocusedElement as TextBox;}
        internal void VerifyStartupKeyboardFocus(Action<bool,string> check) {
            Dispatcher.Invoke(DispatcherPriority.ContextIdle,new Action(()=>{}));
            check(IsActive&&active.List.IsKeyboardFocusWithin,"Initial active file list receives focus after loading without a click; active="+IsActive+"; focus="+(Keyboard.FocusedElement==null?"null":Keyboard.FocusedElement.GetType().Name));
            int cursor=active.List.SelectedIndex;NavigationKey(active,Key.Down);check(active.List.SelectedIndex==cursor+1,"Down works immediately after startup without explicitly focusing the panel");
            NavigationKey(active,Key.Up);check(active.List.SelectedIndex==cursor,"Up works immediately after startup");
        }
        static void NavigationKey(FilePanel panel,Key key,bool preview=false) {
            var target=Keyboard.FocusedElement as UIElement;if(target==null)throw new Exception("No focused element before "+key);
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(panel.List),Environment.TickCount,key){RoutedEvent=preview?Keyboard.PreviewKeyDownEvent:Keyboard.KeyDownEvent});
        }
        internal void VerifyNavigationFocus(string folder,string child,Action<bool,string> check) {
            ApplyRefreshSettings(false,false);Activate();SetActive(left);
            left.List.SelectedItem=left.List.Items.Cast<Entry>().Single(x=>x.FullPath==child);left.FocusFiles();
            check(left.List.IsKeyboardFocusWithin,"Selected folder row has keyboard focus before Enter");
            NavigationKey(left,Key.Enter,true);NavigationTests.Wait(left.NavigationTask);
            check(left.CurrentPath==child,"Enter opens the selected directory");
            check(left.List.IsKeyboardFocusWithin,"Directory retains keyboard focus after Enter; focus="+(Keyboard.FocusedElement==null?"null":Keyboard.FocusedElement.GetType().Name)+"; ownerActive="+IsActive+"; panelActive="+left.IsActivePanel);
            int selected=left.List.SelectedIndex;NavigationKey(left,Key.Down);check(left.List.SelectedIndex==selected+1,"Down immediately selects the next row without a mouse click");
            NavigationKey(left,Key.Up);check(left.List.SelectedIndex==selected,"Up immediately returns to the previous row");
            left.List.SelectedItem=left.List.Items.Cast<Entry>().Single(x=>x.IsParent);left.FocusFiles();NavigationKey(left,Key.Enter,true);NavigationTests.Wait(left.NavigationTask);
            check(left.CurrentPath==folder&&left.List.IsKeyboardFocusWithin,"Enter on parent directory retains keyboard navigation");
            left.List.SelectedItem=left.List.Items.Cast<Entry>().Single(x=>x.FullPath==child);left.FocusFiles();NavigationKey(left,Key.Enter,true);NavigationTests.Wait(left.NavigationTask);
            left.List.SelectedItem=left.List.Items.Cast<Entry>().Single(x=>x.IsDirectory&&!x.IsParent);left.FocusFiles();NavigationKey(left,Key.Enter,true);NavigationTests.Wait(left.NavigationTask);
            check(left.CurrentPath==Path.Combine(child,"empty")&&left.List.IsKeyboardFocusWithin,"Entering an empty directory retains focus on the parent row");
            left.Up();NavigationTests.Wait(left.NavigationTask);check(left.CurrentPath==child&&left.List.IsKeyboardFocusWithin,"Backspace-style parent navigation restores keyboard focus");
            left.Navigate(Path.Combine(child,"missing"),true);NavigationTests.Wait(left.NavigationTask);check(left.CurrentPath==child&&left.List.IsEnabled&&left.List.IsKeyboardFocusWithin,"Failed directory load leaves the previous file list focused and usable");
            left.Navigate(folder,true);NavigationTests.Wait(left.NavigationTask);left.List.SelectedItem=left.List.Items.Cast<Entry>().Single(x=>x.FullPath==child);left.FocusFiles();NavigationKey(left,Key.Enter,true);
            right.FocusFiles();NavigationTests.Wait(left.NavigationTask);check(right.List.IsKeyboardFocusWithin&&active==right,"Completing a directory load does not steal focus from the other panel");
            SetActive(left);left.FocusFiles();left.Navigate(folder,true);left.FocusAddress();var addressFocus=Keyboard.FocusedElement;NavigationTests.Wait(left.NavigationTask);
            check(addressFocus is TextBox&&Object.ReferenceEquals(Keyboard.FocusedElement,addressFocus),"Completing a directory load does not interrupt address editing");
        }
    }
}
