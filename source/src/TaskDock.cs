using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace FileCompass {
    internal sealed class DockStatus {
        internal string Title, Description, Icon;
        internal double? Fraction;
        internal bool Busy;
    }
    internal sealed class ToolDockItem {
        internal Window Window;
        internal TransferWindowState Placement;
        internal Func<DockStatus> Read;
        internal bool Background;
    }
    internal static class ToolDock {
        internal static ToolDockItem Attach(Window window, Panel actions, Func<DockStatus> read) {
            WindowEscape.TrackActivity(window,delegate{return read().Busy;});
            var parent = window.Owner;
            while(parent!=null&&!(parent is MainWindow))parent=parent.Owner;
            var main = parent as MainWindow;
            if (main == null) return null;
            var item = new ToolDockItem { Window = window, Read = read };
            item.Placement = new TransferWindowState(window,
                delegate { item.Background = true; main.RefreshTaskDock(); },
                delegate { item.Background = false; main.RefreshTaskDock(); });
            main.RegisterDockTool(item);
            var button = CopyUi.Button(L.Text("copy.backgroundAction"), item.Placement.Background);
            // A Grid footer can reserve a spacer cell for the background action.
            var grid=actions as Grid;
            if(grid!=null){grid.ColumnDefinitions.Insert(3,new ColumnDefinition{Width=GridLength.Auto});foreach(UIElement child in grid.Children)if(Grid.GetColumn(child)>=3)Grid.SetColumn(child,Grid.GetColumn(child)+1);Grid.SetColumn(button,3);}
            button.HorizontalAlignment = HorizontalAlignment.Right;
            actions.Children.Add(button);
            window.Closed += delegate { main.RemoveDockTool(item); };
            return item;
        }
    }
    internal sealed class ActivityTile : Border {
        readonly TextBlock title = new TextBlock { FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        readonly TextBlock description = new TextBlock { FontSize = 11, Foreground = FluentTheme.Muted, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0,6,0,0) };
        readonly TextBlock percent = new TextBlock { Foreground = FluentTheme.Accent, Margin = new Thickness(6,0,0,0) };
        readonly TextBlock icon = new TextBlock { FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 22, Foreground = FluentTheme.Accent, Margin = new Thickness(0,0,10,0), VerticalAlignment = VerticalAlignment.Center };
        readonly ProgressBar progress = new ProgressBar { Minimum = 0, Maximum = 1, Height = 6, BorderThickness = new Thickness(0), Foreground = FluentTheme.Accent, Background = CompactTheme.ThemeBrush("#E7ECF3"), Margin = new Thickness(0,7,0,0) };
        readonly Button pause;
        Action restore, pauseAction;
        internal ActivityTile() {
            Width = 310; Height = 104; Margin = new Thickness(0,0,8,0); Padding = new Thickness(12,9,12,9);
            CornerRadius = new CornerRadius(7); Background = CompactTheme.WhiteSurface;
            BorderBrush = CompactTheme.ThemeBrush("#79A9DF"); BorderThickness = new Thickness(1);
            var dock = new DockPanel(); Child = dock;
            DockPanel.SetDock(icon,Dock.Left); dock.Children.Add(icon);
            var body = new StackPanel(); dock.Children.Add(body);
            var row = new DockPanel(); DockPanel.SetDock(percent,Dock.Right); row.Children.Add(percent); row.Children.Add(title); body.Children.Add(row);
            body.Children.Add(progress); body.Children.Add(description);
            var actions = new DockPanel { Margin = new Thickness(0,5,0,0) }; body.Children.Add(actions);
            var open = CompactTheme.Button("↗ " + L.Text("transfer.compactDetails"), L.Text("transfer.openDetailsTooltip"), delegate { if (restore != null) restore(); });
            open.MinHeight = 0; open.Height = 22; open.MinWidth = 0; open.Padding = new Thickness(5,0,5,0); open.Margin = new Thickness(0); open.Foreground = FluentTheme.Accent;
            DockPanel.SetDock(open,Dock.Right); actions.Children.Add(open);
            pause = CompactTheme.Button("Ⅱ",L.Text("transfer.pauseTooltip"),delegate { if(pauseAction!=null)pauseAction(); });
            pause.MinHeight=0;pause.Height=22;pause.MinWidth=30;pause.Padding=new Thickness(5,0,5,0);pause.Margin=new Thickness(0);pause.HorizontalAlignment=HorizontalAlignment.Left;actions.Children.Add(pause);
            MouseLeftButtonUp += delegate(object sender,System.Windows.Input.MouseButtonEventArgs e) { if (!e.Handled && restore != null) { restore(); e.Handled=true; } };
        }
        internal void Update(DockStatus state, Action open, Action toggle=null, bool paused=false) {
            restore = open; pauseAction = toggle;
            title.Text = state.Title; description.Text = state.Description; icon.Text = state.Icon ?? "⇄";
            ToolTip = state.Title + "\n" + state.Description;
            progress.Visibility = state.Busy || state.Fraction.HasValue ? Visibility.Visible : Visibility.Hidden;
            progress.IsIndeterminate = state.Busy && !state.Fraction.HasValue;
            progress.Value = state.Fraction.HasValue ? Math.Max(0,Math.Min(1,state.Fraction.Value)) : 0;
            percent.Text = state.Fraction.HasValue ? (progress.Value*100).ToString("F0")+" %" : "";
            pause.Visibility = toggle == null ? Visibility.Hidden : Visibility.Visible;
            pause.Content = paused ? "▶ "+L.Text("action.resume") : "Ⅱ "+L.Text("action.pause");
        }
    }
    public partial class MainWindow {
        readonly List<ToolDockItem> dockTools = new List<ToolDockItem>();
        readonly Dictionary<object,ActivityTile> dockTiles = new Dictionary<object,ActivityTile>();
        readonly StackPanel taskTilePanel = new StackPanel { Orientation = Orientation.Horizontal };
        DispatcherTimer taskDockTimer;
        internal void RegisterDockTool(ToolDockItem item) { dockTools.Add(item); }
        internal void RemoveDockTool(ToolDockItem item) { dockTools.Remove(item); RefreshTaskDock(); }
        void InitializeTaskDock() {
            copySummary.Padding = new Thickness(0); copySummary.BorderThickness = new Thickness(0); copySummary.Background = Brushes.Transparent;
            copySummary.Cursor = System.Windows.Input.Cursors.Arrow;
            copySummary.Child = new ScrollViewer { Content = taskTilePanel, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0,0,0,5), MaxHeight = 128 };
            taskDockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            taskDockTimer.Tick += delegate { RefreshTaskDock(); }; taskDockTimer.Start();
            copySummary.SizeChanged += delegate { RefreshTaskDock(); };
            Closed += delegate { taskDockTimer.Stop(); };
        }
        internal void RefreshTaskDock() {
            var present = new HashSet<object>();
            foreach(var item in dockTools.Where(x=>x.Background).ToArray()) {
                var captured=item;var state=item.Read(); if(String.IsNullOrEmpty(state.Title))state.Title=item.Window.Title;
                SetDockTile(item,state,delegate{captured.Placement.Restore();},present);
            }
            foreach(var activity in copyActivities.Children.OfType<CopyActivity>().Where(x=>!x.Finished&&x.Backgrounded).ToArray()) {
                var captured=activity;var job=networkJobs.FirstOrDefault(x=>!x.Finished&&x.Activity==activity);
                var metrics=job!=null?job.Metrics():activity.LocalMetrics!=null?activity.LocalMetrics():null;
                Action pause=null; bool paused=false;
                if(job!=null){var transfer=job;paused=job.Paused;pause=delegate{transfer.Pause(!transfer.Paused);RefreshTaskDock();};}
                else if(activity.LocalMetrics!=null&&copyControl!=null&&copyProgress!=null){paused=copyControl.Paused;pause=delegate{if(copyProgress!=null)copyProgress.TogglePause();RefreshTaskDock();};}
                string text=activity.TransferRoute??Convert.ToString(activity.ToolTip);
                if(metrics!=null)text+=(paused?" · "+L.Text("action.pause"):" · "+(metrics.BytesPerSecond/1048576.0).ToString("F1")+" MiB/s · "+TransferTime(metrics.RemainingSeconds));
                SetDockTile(activity,new DockStatus{Title=activity.TransferTitle??L.Text("transfer.filesTitle"),Description=text,Icon="⇄",Busy=activity.Preparing,Fraction=activity.Preparing?(double?)null:activity.TotalFraction},delegate{if(captured.RestoreDetails!=null)captured.RestoreDetails();else ShowSummaryDetails();},present,pause,paused);
            }
            foreach(var key in dockTiles.Keys.Where(x=>!present.Contains(x)).ToArray()){taskTilePanel.Children.Remove(dockTiles[key]);dockTiles.Remove(key);}
            if(present.Count>0){double width=Math.Max(230,(copySummary.ActualWidth-8*(present.Count-1))/present.Count);int index=0;foreach(var tile in taskTilePanel.Children.OfType<ActivityTile>()){tile.Width=width;tile.Margin=new Thickness(0,0,++index==present.Count?0:8,0);}}
            copySummary.Visibility = present.Count==0?Visibility.Collapsed:Visibility.Visible;
        }
        void SetDockTile(object key,DockStatus state,Action open,HashSet<object> present,Action pause=null,bool paused=false) {
            present.Add(key);ActivityTile tile;if(!dockTiles.TryGetValue(key,out tile)){tile=new ActivityTile();dockTiles.Add(key,tile);taskTilePanel.Children.Add(tile);}tile.Update(state,open,pause,paused);
        }
    }
}
