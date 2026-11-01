using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace FileCompass {
 internal static class ToolBackdrop {
  sealed class State {internal bool OriginallyEnabled;internal readonly HashSet<Window> Tools=new HashSet<Window>();}
  static readonly Dictionary<Window,State> owners=new Dictionary<Window,State>();
  static readonly DependencyProperty AttachedProperty=DependencyProperty.RegisterAttached("Attached",typeof(bool),typeof(ToolBackdrop),new PropertyMetadata(false));
  [DllImport("user32.dll")]static extern bool EnableWindow(IntPtr handle,bool enabled);
  [DllImport("user32.dll")]static extern bool IsWindowEnabled(IntPtr handle);
  internal static bool NativeEnabled(Window window){return IsWindowEnabled(new WindowInteropHelper(window).Handle);}
  internal static bool OwnerBlocked(Window owner){State state;return owners.TryGetValue(owner,out state)&&state.Tools.Count>0;}
  static void Release(Window owner,Window tool){State state;if(!owners.TryGetValue(owner,out state))return;state.Tools.Remove(tool);if(state.Tools.Count==0){owners.Remove(owner);EnableWindow(new WindowInteropHelper(owner).Handle,state.OriginallyEnabled);var enabled=state.OriginallyEnabled;owner.Dispatcher.BeginInvoke(new Action(delegate{if(!OwnerBlocked(owner))EnableWindow(new WindowInteropHelper(owner).Handle,enabled);}));}else EnableWindow(new WindowInteropHelper(owner).Handle,false);}
  internal static void Attach(Window tool){if(tool is MainWindow||(bool)tool.GetValue(AttachedProperty))return;tool.SetValue(AttachedProperty,true);Window registered=null;
   Action update=delegate{var owner=tool.Owner;bool visible=tool.IsVisible&&owner!=null;if(registered!=null&&(!visible||registered!=owner)){Release(registered,tool);registered=null;}if(!visible||registered!=null)return;State state;var handle=new WindowInteropHelper(owner).Handle;if(handle==IntPtr.Zero)return;if(!owners.TryGetValue(owner,out state)){state=new State{OriginallyEnabled=owner.IsEnabled};owners[owner]=state;}state.Tools.Add(tool);EnableWindow(handle,false);registered=owner;};
   tool.IsVisibleChanged+=delegate{update();};tool.Closed+=delegate{if(registered!=null){var owner=registered;Release(owner,tool);registered=null;owner.Dispatcher.BeginInvoke(new Action(delegate{if(OwnerBlocked(owner))EnableWindow(new WindowInteropHelper(owner).Handle,false);}));}};update();
  }
 }
}
