using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace FileCompass {
 internal static class FluentMaximize {
  internal static void Attach(Window w){w.SourceInitialized+=delegate{HwndSource.FromHwnd(new WindowInteropHelper(w).Handle).AddHook(Hook);};}
  static IntPtr Hook(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled){if(msg==0x24){var info=(MinMax)Marshal.PtrToStructure(lp,typeof(MinMax));var monitor=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(GetMonitorInfo(MonitorFromWindow(hwnd,2),ref monitor)){info.MaxPosition.X=monitor.Work.Left-monitor.Bounds.Left;info.MaxPosition.Y=monitor.Work.Top-monitor.Bounds.Top;info.MaxSize.X=monitor.Work.Right-monitor.Work.Left;info.MaxSize.Y=monitor.Work.Bottom-monitor.Work.Top;Marshal.StructureToPtr(info,lp,false);handled=true;}}return IntPtr.Zero;}
  [StructLayout(LayoutKind.Sequential)]struct Point{public int X,Y;}
  [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)]struct MinMax{public Point Reserved,MaxSize,MaxPosition,MinTrack,MaxTrack;}
  [StructLayout(LayoutKind.Sequential)]struct MonitorInfo{public int Size;public Rect Bounds,Work;public int Flags;}
  [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr hwnd,int flags);
  [DllImport("user32.dll")]static extern bool GetMonitorInfo(IntPtr handle,ref MonitorInfo info);
 }
}
