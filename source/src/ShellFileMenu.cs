using System;using System.IO;using System.Linq;using System.Runtime.InteropServices;using System.Windows;using System.Windows.Interop;
namespace FileCompass {
 internal static class ShellFileMenu {
  [ComImport,Guid("000214E6-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Folder {
   [PreserveSig]int Parse(IntPtr h,IntPtr bind,[MarshalAs(UnmanagedType.LPWStr)]string name,ref uint eaten,out IntPtr pidl,ref uint attrs);
   [PreserveSig]int Enum(IntPtr h,uint flags,out IntPtr list);
   [PreserveSig]int Bind(IntPtr pidl,IntPtr bind,ref Guid iid,out IntPtr result);
   [PreserveSig]int Storage(IntPtr pidl,IntPtr bind,ref Guid iid,out IntPtr result);
   [PreserveSig]int Compare(IntPtr flags,IntPtr a,IntPtr b);
   [PreserveSig]int View(IntPtr h,ref Guid iid,out IntPtr result);
   [PreserveSig]int Attr(uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)]IntPtr[] pidls,ref uint attrs);
   [PreserveSig]int UI(IntPtr h,uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=1)]IntPtr[] pidls,ref Guid iid,IntPtr reserved,out IntPtr result);
  }
  [ComImport,Guid("000214E4-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Menu {
   [PreserveSig]int Query(IntPtr menu,uint index,uint first,uint last,uint flags);
   [PreserveSig]int Invoke(ref InvokeInfo info);
   [PreserveSig]int Command(UIntPtr id,uint flags,IntPtr reserved,IntPtr name,uint max);
  }
  [ComImport,Guid("000214F4-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Menu2 {
   [PreserveSig]int Query(IntPtr menu,uint index,uint first,uint last,uint flags);[PreserveSig]int Invoke(ref InvokeInfo info);[PreserveSig]int Command(UIntPtr id,uint flags,IntPtr reserved,IntPtr name,uint max);[PreserveSig]int Handle(uint message,IntPtr w,IntPtr l);
  }
  [ComImport,Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Menu3 {
   [PreserveSig]int Query(IntPtr menu,uint index,uint first,uint last,uint flags);[PreserveSig]int Invoke(ref InvokeInfo info);[PreserveSig]int Command(UIntPtr id,uint flags,IntPtr reserved,IntPtr name,uint max);[PreserveSig]int Handle(uint message,IntPtr w,IntPtr l);[PreserveSig]int Handle2(uint message,IntPtr w,IntPtr l,out IntPtr result);
  }
  [StructLayout(LayoutKind.Sequential)]struct Point {internal int X,Y;}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct InvokeInfo {internal uint Size,Mask;internal IntPtr Window,Verb,Parameters,Directory;internal int Show;internal uint HotKey;internal IntPtr Icon,Title,VerbW,ParametersW,DirectoryW,TitleW;internal Point Position;}
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHParseDisplayName(string name,IntPtr bind,out IntPtr pidl,uint requested,out uint attrs);
  [DllImport("shell32.dll")]static extern int SHBindToParent(IntPtr pidl,ref Guid iid,out IntPtr parent,out IntPtr child);
  [DllImport("user32.dll")]static extern IntPtr CreatePopupMenu();[DllImport("user32.dll")]static extern bool DestroyMenu(IntPtr menu);[DllImport("user32.dll")]static extern int GetMenuItemCount(IntPtr menu);
  [DllImport("user32.dll")]static extern uint TrackPopupMenuEx(IntPtr menu,uint flags,int x,int y,IntPtr h,IntPtr parameters);
  [DllImport("user32.dll")]static extern bool GetCursorPos(out Point position);
  internal static bool Compatible(string[] paths){return paths!=null&&paths.Length>0&&paths.Length<=1000&&paths.All(x=>!String.IsNullOrEmpty(x)&&Path.IsPathRooted(x))&&paths.Select(Path.GetDirectoryName).Distinct(StringComparer.Ordinal).Count()==1;}
  internal static int Show(Window owner,string[] paths,bool display=true) {
   if(!Compatible(paths))throw new IOException(L.Text("context.sameFolder"));IntPtr popup=IntPtr.Zero,parentPtr=IntPtr.Zero,menuPtr=IntPtr.Zero;object folderObject=null,menuObject=null;var pidls=new IntPtr[paths.Length];HwndSource source=null;HwndSourceHook hook=null;
   try {
    var relative=new IntPtr[paths.Length];Guid folderId=new Guid("000214E6-0000-0000-C000-000000000046");
    for(int i=0;i<paths.Length;i++){uint attrs;Marshal.ThrowExceptionForHR(SHParseDisplayName(paths[i],IntPtr.Zero,out pidls[i],0,out attrs));IntPtr parent,child;Marshal.ThrowExceptionForHR(SHBindToParent(pidls[i],ref folderId,out parent,out child));if(i==0)parentPtr=parent;else Marshal.Release(parent);relative[i]=child;}
    folderObject=Marshal.GetObjectForIUnknown(parentPtr);Guid menuId=new Guid("000214E4-0000-0000-C000-000000000046");IntPtr hwnd=new WindowInteropHelper(owner).EnsureHandle();Marshal.ThrowExceptionForHR(((Folder)folderObject).UI(hwnd,(uint)relative.Length,relative,ref menuId,IntPtr.Zero,out menuPtr));menuObject=Marshal.GetObjectForIUnknown(menuPtr);var context=(Menu)menuObject;var context3=menuObject as Menu3;var context2=menuObject as Menu2;
    popup=CreatePopupMenu();if(popup==IntPtr.Zero)throw new IOException(L.Text("context.shellFailed"));Marshal.ThrowExceptionForHR(context.Query(popup,0,1,0x7fff,0));int count=GetMenuItemCount(popup);if(!display)return count;
    source=HwndSource.FromHwnd(hwnd);hook=delegate(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x117||message==0x2b||message==0x2c||message==0x120){IntPtr result;if(context3!=null&&context3.Handle2((uint)message,w,l,out result)>=0){handled=true;return result;}if(context2!=null&&message!=0x120&&context2.Handle((uint)message,w,l)>=0)handled=true;}return IntPtr.Zero;};if(source!=null)source.AddHook(hook);
    Point point;GetCursorPos(out point);uint chosen=TrackPopupMenuEx(popup,0x100|0x80,point.X,point.Y,hwnd,IntPtr.Zero);if(chosen!=0){var info=new InvokeInfo{Size=(uint)Marshal.SizeOf(typeof(InvokeInfo)),Mask=0x4000|0x20000000,Window=hwnd,Verb=new IntPtr(chosen-1),VerbW=new IntPtr(chosen-1),Show=1,Position=point};Marshal.ThrowExceptionForHR(context.Invoke(ref info));}return count;
   } finally {if(source!=null&&hook!=null)source.RemoveHook(hook);if(popup!=IntPtr.Zero)DestroyMenu(popup);if(menuObject!=null&&Marshal.IsComObject(menuObject))Marshal.FinalReleaseComObject(menuObject);if(folderObject!=null&&Marshal.IsComObject(folderObject))Marshal.FinalReleaseComObject(folderObject);if(menuPtr!=IntPtr.Zero)Marshal.Release(menuPtr);if(parentPtr!=IntPtr.Zero)Marshal.Release(parentPtr);foreach(var pidl in pidls)if(pidl!=IntPtr.Zero)Marshal.FreeCoTaskMem(pidl);}
  }
 }
}
