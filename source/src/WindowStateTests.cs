using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace FileCompass
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
                owner = new Window { Title = "Window regression owner",Width = 1000,Height = 700,Left = -18000,Top = -18000,WindowStartupLocation = WindowStartupLocation.Manual,ShowActivated = false,ShowInTaskbar = true,Content = new TextBlock { Text = "Owner" } }; owner.Show(); Pump();
                foreach (var ownerState in new[] { WindowState.Normal,WindowState.Maximized }) {
                    owner.WindowState = ownerState; Pump();
                    foreach (int minimizeAction in new[] { 0,1,2 }) using (var control = new CopyControl()) {
                        copy = new CopyProgressDialog(owner,control) { ShowActivated = false }; copy.Show(); Pump(); var before = Position(copy);
                        for (int cycle = 0; cycle < 3; cycle++) {
                            if (minimizeAction == 2) System.Linq.Enumerable.First(FluentUiTests.Children<Button>(copy), x => Convert.ToString(x.Content) == L.Text("copy.backgroundAction")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); else if (minimizeAction == 1) SendMessage(new WindowInteropHelper(copy).Handle,0x0112,(IntPtr)0xf020,IntPtr.Zero); else copy.WindowState = WindowState.Minimized;
                            Pump(); check(owner.WindowState == ownerState && !IsIconic(new WindowInteropHelper(owner).Handle),"backgrounding copy preserves owner immediately"); check(!copy.IsVisible && copy.WindowState == WindowState.Normal && !IsIconic(new WindowInteropHelper(copy).Handle),"copy queues without native minimized icon (" + ownerState + ", title=" + minimizeAction + ", cycle=" + cycle + ", visible=" + copy.IsVisible + ", state=" + copy.WindowState + ", iconic=" + IsIconic(new WindowInteropHelper(copy).Handle) + ", loaded=" + copy.IsLoaded + ")");
                            copy.RestoreProgress(); Pump(); var after = Position(copy);
                            check(copy.IsVisible && !IsIconic(new WindowInteropHelper(copy).Handle) && after.Bottom - after.Top > 120 && Math.Abs(after.Left - before.Left) <= 3 && Math.Abs(after.Top - before.Top) <= 3 && Math.Abs((after.Right - after.Left) - (before.Right - before.Left)) <= 3,"copy restores full native bounds and position (" + ownerState + ", title=" + minimizeAction + ", cycle=" + cycle + ")");
                        }
                        control.Cancel(); copy.Finish(); copy = null; Pump(); check(owner.IsVisible && owner.WindowState == ownerState && !IsIconic(new WindowInteropHelper(owner).Handle),"stopping foreground copy preserves owner state (" + ownerState + ", title=" + minimizeAction + ")");
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
                foreach(var state in new[]{WindowState.Normal,WindowState.Maximized}){
                    owner.WindowState=state;owner.Activate();Pump();
                    var reader=new ExtBrowserWindow(owner){Left=-18000,Top=-18000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};reader.Show();NavigationTests.Wait(reader.Pending);reader.Activate();Pump();
                    check(reader.IsActive,"Linux reader is active before native Close ("+state+")");SendMessage(new WindowInteropHelper(reader).Handle,0x0112,(IntPtr)0xf060,IntPtr.Zero);Pump();
                    check(owner.IsActive&&owner.WindowState==state&&!IsIconic(new WindowInteropHelper(owner).Handle),"Closing Linux reader returns to visible owner and preserves its state ("+state+")");
                    var modal=new Window{Height=160,Content=new TextBlock{Text="Example dialog"},Left=-18000,Top=-18000};CopyUi.Style(modal,owner,"Example",320);modal.WindowStartupLocation=WindowStartupLocation.Manual;modal.Loaded+=delegate{modal.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(modal.Close));};modal.ShowDialog();Pump();
                    check(owner.IsActive&&owner.IsEnabled&&owner.WindowState==state&&!IsIconic(new WindowInteropHelper(owner).Handle),"Closing modal dialog returns to enabled owner without minimizing ("+state+")");
                    var parentDialog=new Window{Height=160,Content=new TextBlock{Text="Parent"},Left=-18000,Top=-18000};CopyUi.Style(parentDialog,owner,"Parent",320);parentDialog.WindowStartupLocation=WindowStartupLocation.Manual;parentDialog.Show();var childDialog=new Window{Height=160,Content=new TextBlock{Text="Child"},Left=-18000,Top=-18000};CopyUi.Style(childDialog,parentDialog,"Child",320);childDialog.WindowStartupLocation=WindowStartupLocation.Manual;childDialog.Show();childDialog.Activate();Pump();childDialog.Close();Pump();check(parentDialog.IsActive&&owner.WindowState==state,"Nested dialog close returns to immediate owner ("+state+")");parentDialog.Close();Pump();check(owner.IsActive&&owner.WindowState==state,"Closing final nested dialog returns to main owner ("+state+")");
                }
                owner.WindowState=WindowState.Normal;var backgroundDialog=new Window{Height=160,Content=new TextBlock{Text="Background"},Left=-18000,Top=-18000};CopyUi.Style(backgroundDialog,owner,"Background",320);backgroundDialog.WindowStartupLocation=WindowStartupLocation.Manual;backgroundDialog.Show();var other=new Window{Height=160,Width=320,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual,Content=new TextBlock{Text="Other window"}};other.Show();other.Activate();Pump();backgroundDialog.Close();Pump();check(other.IsActive,"Closing an inactive dialog does not steal focus from another window");other.Close();owner.Activate();Pump();var minimized=new Window{Height=160,Content=new TextBlock{Text="Minimized"},Left=-18000,Top=-18000};CopyUi.Style(minimized,owner,"Minimized",320);minimized.WindowStartupLocation=WindowStartupLocation.Manual;minimized.Show();owner.WindowState=WindowState.Minimized;Pump();minimized.Close();Pump();check(owner.WindowState==WindowState.Minimized,"Closing a dialog does not restore an intentionally minimized owner");owner.WindowState=WindowState.Normal;
                owner.Close(); owner = null; File.WriteAllLines(Path.Combine(directory,"window-state-result.txt"),checks); File.AppendAllText(Path.Combine(directory,"window-state-result.txt"),"\nPASS: " + checks.Count + " checks"); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(directory,"window-state-result.txt"),"FAIL after " + checks.Count + " checks\n" + ex); return 1; }
            finally { if (copy != null) copy.Finish(); if (owner != null) owner.Close(); }
        }
    }
}
