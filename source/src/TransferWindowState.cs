using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace FileCompass
{
    // Sending an owned, taskbar-less WPF window through native minimization
    // creates a caption-sized desktop icon. Queueing is hiding, not minimizing.
    internal sealed class TransferWindowState
    {
        readonly Window window; readonly Action background, foreground;
        Rect bounds = Rect.Empty; WindowState restoredState = WindowState.Normal;
        bool changing, queued, finished;
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle,int command);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr handle);
        internal TransferWindowState(Window target,Action toBackground,Action toForeground)
        {
            window = target; background = toBackground; foreground = toForeground;
            window.ShowInTaskbar = false;
            window.SourceInitialized += delegate { var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle); if (source != null) source.AddHook(Message); };
            window.Loaded += delegate { Capture(); };
            window.LocationChanged += delegate { Capture(); };
            window.SizeChanged += delegate { Capture(); };
            window.StateChanged += delegate { if (!changing && window.WindowState == WindowState.Minimized) DeferBackground(); else Capture(); };
            window.IsVisibleChanged += delegate { if (window.IsVisible && !changing && window.WindowState != WindowState.Minimized) { queued = false; Capture(); if (foreground != null) foreground(); } };
        }
        IntPtr Message(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
        {
            if (message == 0x0112 && (wParam.ToInt64() & 0xfff0) == 0xf020) { handled = true; Background(); }
            return IntPtr.Zero;
        }
        void Capture()
        {
            if (changing || queued || !window.IsVisible || window.WindowState == WindowState.Minimized) return;
            var current = window.WindowState == WindowState.Maximized ? window.RestoreBounds : new Rect(window.Left,window.Top,window.ActualWidth,window.ActualHeight);
            if (!current.IsEmpty && !Double.IsNaN(current.X) && !Double.IsNaN(current.Y) && current.Width >= 200 && current.Height >= 120) { bounds = current; restoredState = window.WindowState; }
        }
        internal void DeferBackground() { window.Dispatcher.BeginInvoke(DispatcherPriority.Normal,new Action(Background)); }
        internal void Background()
        {
            if (finished || !window.IsLoaded || queued) return; Capture(); changing = true;
            var owner = window.Owner;
            var ownerState = owner == null ? WindowState.Normal : owner.WindowState;
            try {
                // WPF can report Normal after Hide while the HWND remains iconic.
                // Normalize the native window before hiding it from the desktop.
                window.WindowState = WindowState.Normal;
                var handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero && IsIconic(handle)) ShowWindow(handle,9);
                // Detach the native owner while hiding. This is a transfer to
                // the in-app activity area, never a minimize of a window group.
                window.Owner = null;
                try { window.Hide(); queued = true; }
                finally { window.Owner = owner; }
            }
            finally { changing = false; }
            if (background != null) background();
            Action preserveOwner = delegate {
                if (owner != null && owner.IsVisible && ownerState != WindowState.Minimized && owner.WindowState == WindowState.Minimized)
                    owner.WindowState = ownerState;
                if (owner != null && owner.IsVisible && owner.WindowState != WindowState.Minimized) owner.Activate();
            };
            preserveOwner();
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, preserveOwner);
        }
        internal void Restore()
        {
            if (finished) return;
            changing = true;
            try {
                window.WindowState = WindowState.Normal; window.Show();
                var handle = new WindowInteropHelper(window).Handle; if (handle != IntPtr.Zero && IsIconic(handle)) ShowWindow(handle,9);
                if (!bounds.IsEmpty) { window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = bounds.Left; window.Top = bounds.Top; window.Width = bounds.Width; if (window.SizeToContent == SizeToContent.Manual) window.Height = bounds.Height; }
                window.WindowState = restoredState; queued = false; window.UpdateLayout(); window.Activate();
            } finally { changing = false; }
            if (foreground != null) foreground(); Capture();
        }
        internal void Finish()
        {
            if (finished) return; finished = true;
            var owner = window.Owner; bool activateOwner = window.IsActive && owner != null && owner.IsVisible && owner.WindowState != WindowState.Minimized;
            changing = true; try { window.Hide(); window.WindowState = WindowState.Normal; window.Close(); } finally { changing = false; }
            if (activateOwner) owner.Activate();
        }
    }
}

