using System;using System.ComponentModel;using System.Runtime.InteropServices;using System.Windows;using System.Windows.Interop;using System.Windows.Threading;
namespace FileCompass {
 internal static class DialogOwnerActivation {
  static readonly DependencyProperty AttachedProperty=DependencyProperty.RegisterAttached("Attached",typeof(bool),typeof(DialogOwnerActivation),new PropertyMetadata(false));
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
  internal static void Attach(Window dialog){if((bool)dialog.GetValue(AttachedProperty))return;dialog.SetValue(AttachedProperty,true);Window owner=null;WindowState state=WindowState.Normal;bool returnToOwner=false;
   dialog.Closing+=delegate(object sender,CancelEventArgs e){owner=dialog.Owner;returnToOwner=false;if(owner==null||!owner.IsVisible||owner.WindowState==WindowState.Minimized)return;IntPtr handle=new WindowInteropHelper(dialog).Handle;IntPtr foreground=GetForegroundWindow();returnToOwner=dialog.IsActive||(handle!=IntPtr.Zero&&(foreground==handle||GetAncestor(foreground,2)==handle));state=owner.WindowState;};
   dialog.Closed+=delegate{if(!returnToOwner||owner==null||owner.Dispatcher.HasShutdownStarted)return;var target=owner;var previousState=state;target.Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(delegate{if(!target.IsLoaded||!target.IsVisible||!target.IsEnabled)return;if(target.WindowState==WindowState.Minimized)target.WindowState=previousState;target.Activate();}));};
  }
 }
}
