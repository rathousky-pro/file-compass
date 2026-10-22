using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CommanderCZ
{
    internal static class WindowStateTests
    {
        [StructLayout(LayoutKind.Sequential)] struct Bounds { public int Left,Top,Right,Bottom; }
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle,out Bounds bounds);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr handle);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
        static void Pump() { Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate { })); }
        static Bounds Position(Window window) { Bounds result; if (!GetWindowRect(new WindowInteropHelper(window).Handle,out result)) throw new Exception("Missing native window"); return result; }
        internal static int Run(string directory)
        {
            Directory.CreateDirectory(directory); var checks = new List<string>(); Action<bool,string> check = delegate(bool valid,string label) { if (!valid) throw new Exception(label); checks.Add(label); };
            Window owner = null; CopyProgressDialog copy = null; NetworkQueueWindow queue = null;
            try {
                owner = new Window { Title = "Window regression owner",Width = 1000,Height = 700,Left = -18000,Top = -18000,WindowStartupLocation = WindowStartupLocation.Manual,ShowActivated = false,ShowInTaskbar = false,Content = new TextBlock { Text = "Owner" } }; owner.Show(); Pump();
                foreach (var ownerState in new[] { WindowState.Normal,WindowState.Maximized }) {
                    owner.WindowState = ownerState; Pump();
                    foreach (bool titleButton in new[] { false,true }) using (var control = new CopyControl()) {
                        copy = new CopyProgressDialog(owner,control) { ShowActivated = false }; copy.Show(); Pump(); var before = Position(copy);
                        for (int cycle = 0; cycle < 3; cycle++) {
                            if (titleButton) SendMessage(new WindowInteropHelper(copy).Handle,0x0112,(IntPtr)0xf020,IntPtr.Zero); else copy.WindowState = WindowState.Minimized;
                            Pump(); check(!copy.IsVisible && copy.WindowState == WindowState.Normal && !IsIconic(new WindowInteropHelper(copy).Handle),"copy queues without native minimized icon (" + ownerState + ", title=" + titleButton + ", cycle=" + cycle + ", visible=" + copy.IsVisible + ", state=" + copy.WindowState + ", iconic=" + IsIconic(new WindowInteropHelper(copy).Handle) + ", loaded=" + copy.IsLoaded + ")");
                            copy.RestoreProgress(); Pump(); var after = Position(copy);
                            check(copy.IsVisible && !IsIconic(new WindowInteropHelper(copy).Handle) && after.Bottom - after.Top > 120 && Math.Abs(after.Left - before.Left) <= 3 && Math.Abs(after.Top - before.Top) <= 3 && Math.Abs((after.Right - after.Left) - (before.Right - before.Left)) <= 3,"copy restores full native bounds and position (" + ownerState + ", title=" + titleButton + ", cycle=" + cycle + ")");
                        }
                        control.Cancel(); copy.Finish(); copy = null; Pump(); check(owner.IsVisible && owner.WindowState == ownerState && !IsIconic(new WindowInteropHelper(owner).Handle),"stopping foreground copy preserves owner state (" + ownerState + ", title=" + titleButton + ")");
                    }
                    foreach (bool immediate in new[] { false,true }) using (var control = new CopyControl()) {
                        copy = new CopyProgressDialog(owner,control) { ShowActivated = false }; copy.Show(); Pump(); copy.WindowState = WindowState.Minimized;
                        if (!immediate) Pump();
                        control.Cancel(); copy.Finish(); copy = null; Pump();
                        check(owner.IsVisible && owner.WindowState == ownerState && !IsIconic(new WindowInteropHelper(owner).Handle),"stopping queued copy preserves owner, including pending minimize callback (" + ownerState + ", immediate=" + immediate + ")");
                    }
                    foreach (bool background in new[] { false,true }) {
                    var job = new NetworkTransferJob(new NetworkProfile(),false); queue = new NetworkQueueWindow(owner,delegate { return new[] { job }; },job) { ShowActivated = false }; queue.Show(); Pump(); var queueBefore = Position(queue);
                    SendMessage(new WindowInteropHelper(queue).Handle,0x0112,(IntPtr)0xf020,IntPtr.Zero); Pump(); queue.RestoreProgress(); Pump(); var queueAfter = Position(queue);
                    check(queue.IsVisible && queueAfter.Bottom - queueAfter.Top > 300 && Math.Abs(queueBefore.Left - queueAfter.Left) <= 3 && Math.Abs(queueBefore.Top - queueAfter.Top) <= 3 && !IsIconic(new WindowInteropHelper(queue).Handle),"network queue restores full native bounds (" + ownerState + ")");
                    if (background) { queue.WindowState = WindowState.Minimized; Pump(); } job.Cancel(); job.Finish("Zrušeno");
                    var end = DateTime.UtcNow.AddSeconds(3); bool closed = false; queue.Closed += delegate { closed = true; }; while (!closed && DateTime.UtcNow < end) { Pump(); System.Threading.Thread.Sleep(20); }
                    check(closed && owner.IsVisible && owner.WindowState == ownerState && !IsIconic(new WindowInteropHelper(owner).Handle),"stopping network copy preserves owner state (" + ownerState + ", background=" + background + ")"); queue = null;
                    }
                }
                owner.Close(); owner = null; File.WriteAllLines(Path.Combine(directory,"window-state-result.txt"),checks); File.AppendAllText(Path.Combine(directory,"window-state-result.txt"),"\nPASS: " + checks.Count + " checks"); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(directory,"window-state-result.txt"),"FAIL after " + checks.Count + " checks\n" + ex); return 1; }
            finally { if (copy != null) copy.Finish(); if (owner != null) owner.Close(); }
        }
    }
}

