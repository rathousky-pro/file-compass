using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace FileCompass
{
    internal static class NavigationTests
    {
        internal static void Wait(Task task)
        {
            if (task == null) return; DateTime limit = DateTime.UtcNow.AddSeconds(25);
            while (!task.IsCompleted && DateTime.UtcNow < limit) { Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { })); Thread.Sleep(1); }
            if (!task.IsCompleted) throw new TimeoutException("Navigation test timed out."); task.GetAwaiter().GetResult();
        }
        internal static void WaitPanels(Window window)
        {
            var main = window as MainWindow; if (main != null) Wait(main.StartupRestoration);
            var root = window.Content as DockPanel; if (root == null) return;
            foreach (var panel in FluentUiTests.Children<FilePanel>(root)) Wait(panel.NavigationTask);
        }
    }
}

