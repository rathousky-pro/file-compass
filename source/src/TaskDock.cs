using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace FileCompass {
    internal enum DockActivityKind { Tool, LocalTransfer, NetworkTransfer }
    internal sealed class DockStatus {
        internal DockActivityKind Kind;
        internal string Title, Description, Icon;
        internal double? Fraction;
        internal bool Busy;
    }
    internal sealed class ToolDockItem {
        internal Window Window;
        internal TransferWindowState Placement;
        internal Func<DockStatus> Read;
        internal bool Background;
        internal Action Pause,Cancel;
        internal Func<bool> Paused;
    }
    internal static class ToolDock {
        internal static ToolDockItem Attach(Window window, Panel actions, Func<DockStatus> read, Action pause=null, Func<bool> paused=null, Action cancel=null) {
            WindowEscape.TrackActivity(window,delegate{return read().Busy;});
            var parent = window.Owner;
            while(parent!=null&&!(parent is MainWindow))parent=parent.Owner;
            var main = parent as MainWindow;
            if (main == null) return null;
            var item = new ToolDockItem { Window = window, Read = read,Pause=pause,Paused=paused,Cancel=cancel };
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
        internal DockActivityKind Kind {get;private set;}
        readonly Border categoryMark=new Border{Width=3,HorizontalAlignment=HorizontalAlignment.Left,CornerRadius=new CornerRadius(2),Visibility=Visibility.Collapsed};
        readonly TextBlock category=new TextBlock{FontSize=13,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center};
        readonly Image categoryIcon=new Image{Width=18,Height=18,Margin=new Thickness(0,0,5,0)};
        readonly StackPanel identity=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,9,0),Visibility=Visibility.Collapsed};
        readonly TextBlock title=new TextBlock{FontWeight=FontWeights.SemiBold,FontSize=14,TextTrimming=TextTrimming.CharacterEllipsis};
        readonly TextBlock description=new TextBlock{FontSize=13,Foreground=FluentTheme.Muted,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,4,0,0)};
        readonly TextBlock percent=new TextBlock{FontSize=14,Foreground=FluentTheme.Accent,Margin=new Thickness(5,0,0,0)};
        readonly ProgressBar progress=new ProgressBar{Minimum=0,Maximum=1,Height=6,BorderThickness=new Thickness(0),Foreground=FluentTheme.Accent,Background=CompactTheme.ThemeBrush("#E7ECF3"),Margin=new Thickness(0,4,0,0)};
        readonly Button pause,cancel,details;Action restore,pauseAction,cancelAction;
        internal ActivityTile(){
            Width=310;Height=80;Margin=new Thickness(0,0,7,0);Padding=new Thickness(9,7,9,7);CornerRadius=new CornerRadius(6);Background=CompactTheme.WhiteSurface;BorderBrush=CompactTheme.ChromeLine;BorderThickness=new Thickness(1);
            var dock=new DockPanel{Margin=new Thickness(8,0,0,0)};var surface=new Grid();surface.Children.Add(categoryMark);surface.Children.Add(dock);Child=surface;
            var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(7,0,0,0)};DockPanel.SetDock(actions,Dock.Right);dock.Children.Add(actions);
            details=Control("↗",L.Text("transfer.openDetailsTooltip"),delegate{if(restore!=null)restore();});actions.Children.Add(details);
            pause=Control("Ⅱ",L.Text("transfer.pauseTooltip"),delegate{if(pauseAction!=null)pauseAction();});actions.Children.Add(pause);
            cancel=Control("×",L.Text("action.cancel"),delegate{if(cancelAction!=null)cancelAction();});actions.Children.Add(cancel);
            System.Windows.Automation.AutomationProperties.SetAutomationId(pause,"ActivityPause");System.Windows.Automation.AutomationProperties.SetAutomationId(cancel,"ActivityCancel");
            var body=new StackPanel{VerticalAlignment=VerticalAlignment.Center};dock.Children.Add(body);var row=new DockPanel();DockPanel.SetDock(percent,Dock.Right);row.Children.Add(percent);identity.Children.Add(categoryIcon);identity.Children.Add(category);DockPanel.SetDock(identity,Dock.Left);row.Children.Add(identity);row.Children.Add(title);body.Children.Add(row);body.Children.Add(progress);body.Children.Add(description);
            MouseLeftButtonUp+=delegate(object sender,System.Windows.Input.MouseButtonEventArgs e){if(!e.Handled&&restore!=null){restore();e.Handled=true;}};
        }
        static Button Control(string caption,string tip,Action run){var b=CompactTheme.Button(caption,tip,delegate(object sender,RoutedEventArgs e){e.Handled=true;run();});b.Height=30;b.MinHeight=0;b.MinWidth=30;b.Width=30;b.FontSize=16;b.Padding=new Thickness(0);b.Margin=new Thickness(2,0,0,0);return b;}
        internal void Update(DockStatus state,Action open,Action toggle=null,bool paused=false,Action stop=null){
            Kind=state.Kind;bool transfer=Kind!=DockActivityKind.Tool;identity.Visibility=categoryMark.Visibility=transfer?Visibility.Visible:Visibility.Collapsed;
            if(transfer){bool network=Kind==DockActivityKind.NetworkTransfer;category.Text=L.Text(network?"taskDock.network":"taskDock.local");categoryIcon.Source=DesktopIcons.ToolbarDrawing(network?"network":"copy",ToolbarThemeSettings.Current);var ink=CompactTheme.ColorBrush(network?"#3158B5":"#17775F");category.Foreground=categoryMark.Background=progress.Foreground=ink;System.Windows.Automation.AutomationProperties.SetItemType(this,category.Text);}else{progress.Foreground=FluentTheme.Accent;System.Windows.Automation.AutomationProperties.SetItemType(this,"");}
            restore=open;pauseAction=toggle;cancelAction=stop;title.Text=state.Title;description.Text=state.Description;ToolTip=state.Title+"\n"+state.Description;
            progress.Visibility=state.Busy||state.Fraction.HasValue?Visibility.Visible:Visibility.Hidden;progress.IsIndeterminate=state.Busy&&!state.Fraction.HasValue&&!paused;progress.Value=state.Fraction.HasValue?Math.Max(0,Math.Min(1,state.Fraction.Value)):0;
            percent.Text=paused?L.Text("state.paused"):state.Fraction.HasValue?(progress.Value*100).ToString("F0")+" %":"";
            pause.Visibility=toggle!=null||state.Busy||(state.Fraction.HasValue&&state.Fraction.Value<1)?Visibility.Visible:Visibility.Collapsed;pause.IsEnabled=toggle!=null;pause.Content=paused?"▶":"Ⅱ";pause.ToolTip=L.Text(toggle==null?"common.optionNotSupported":paused?"transfer.resumeTooltip":"transfer.pauseTooltip");cancel.Visibility=stop==null?Visibility.Collapsed:Visibility.Visible;
        }
    }
    public partial class MainWindow {
        readonly List<ToolDockItem> dockTools = new List<ToolDockItem>();
        readonly Dictionary<object,ActivityTile> dockTiles = new Dictionary<object,ActivityTile>();
        readonly StackPanel taskTilePanel = new StackPanel { Orientation=Orientation.Horizontal };
        ScrollViewer wideTaskScroll;
        DispatcherTimer taskDockTimer;
        internal void RegisterDockTool(ToolDockItem item) { dockTools.Add(item); }
        internal void RemoveDockTool(ToolDockItem item) { dockTools.Remove(item); RefreshTaskDock(); }
        void InitializeTaskDock() {
            copySummary.Padding = new Thickness(0); copySummary.BorderThickness = new Thickness(0); copySummary.Background = Brushes.Transparent;
            copySummary.Cursor = System.Windows.Input.Cursors.Arrow;
            copySummary.Height=100;wideTaskScroll=new ScrollViewer { Content=taskTilePanel,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,0,5) };copySummary.Child=wideTaskScroll;
            taskDockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            taskDockTimer.Tick += delegate { RefreshTaskDock(); }; taskDockTimer.Start();
            copySummary.SizeChanged += delegate { RefreshTaskDock(); };
            Closed += delegate { taskDockTimer.Stop(); };
        }
        internal void RefreshTaskDock() {
            var present = new HashSet<object>();
            foreach(var item in dockTools.Where(x=>x.Background).ToArray()) {
                var captured=item;var state=item.Read(); if(String.IsNullOrEmpty(state.Title))state.Title=item.Window.Title;
                SetDockTile(item,state,delegate{captured.Placement.Restore();},present,state.Busy?item.Pause:null,item.Paused!=null&&item.Paused(),item.Cancel??delegate{captured.Window.Close();});
            }
            foreach(var activity in copyActivities.Children.OfType<CopyActivity>().Where(x=>!x.Finished&&x.Backgrounded).ToArray()) {
                var captured=activity;var job=networkJobs.FirstOrDefault(x=>!x.Finished&&x.Activity==activity);
                var metrics=job!=null?job.Metrics():activity.LocalMetrics!=null?activity.LocalMetrics():null;
                Action pause=null; bool paused=false;
                if(activity.PauseAction!=null){pause=activity.PauseAction;paused=activity.IsPaused!=null&&activity.IsPaused();}
                if(job!=null){var transfer=job;paused=job.Paused;pause=delegate{transfer.Pause(!transfer.Paused);RefreshTaskDock();};}
                else if(activity.PauseAction==null&&activity.LocalMetrics!=null&&copyControl!=null&&copyProgress!=null){paused=copyControl.Paused;pause=delegate{if(copyProgress!=null)copyProgress.TogglePause();RefreshTaskDock();};}
                string text=activity.TransferRoute??Convert.ToString(activity.ToolTip);
                if(metrics!=null)text+=(paused?" · "+L.Text("state.paused"):" · "+(metrics.BytesPerSecond/1048576.0).ToString("F1")+" MiB/s · "+TransferTime(metrics.RemainingSeconds));
                var kind=job!=null?DockActivityKind.NetworkTransfer:activity.Kind;if(kind==DockActivityKind.Tool&&activity.LocalMetrics!=null)kind=DockActivityKind.LocalTransfer;
                SetDockTile(activity,new DockStatus{Kind=kind,Title=activity.TransferTitle??L.Text("transfer.filesTitle"),Description=text,Icon="⇄",Busy=activity.Preparing,Fraction=activity.Preparing?(double?)null:activity.TotalFraction},delegate{if(captured.RestoreDetails!=null)captured.RestoreDetails();else ShowSummaryDetails();},present,pause,paused,delegate{captured.RequestCancel();});
            }
            foreach(var key in dockTiles.Keys.Where(x=>!present.Contains(x)).ToArray()){var parent=dockTiles[key].Parent as Panel;if(parent!=null)parent.Children.Remove(dockTiles[key]);dockTiles.Remove(key);}
            // Reorder existing cards only when membership/order changes; timer updates retain their instances.
            var ordered=dockTools.Where(x=>present.Contains(x)).Cast<object>().Concat(copyActivities.Children.OfType<CopyActivity>().Where(x=>present.Contains(x)).Cast<object>()).Select(x=>dockTiles[x]).OrderBy(x=>x.Kind==DockActivityKind.Tool?0:1).ToArray();
            for(int i=0;i<ordered.Length;i++)if(!Object.ReferenceEquals(taskTilePanel.Children[i],ordered[i])){taskTilePanel.Children.Remove(ordered[i]);taskTilePanel.Children.Insert(i,ordered[i]);}
            double available=Math.Max(0,copySummary.ActualWidth-2),gap=6;int compact=dockTiles.Keys.Count(x=>!FullWidthDockItem(x)),flexible=dockTiles.Count-compact;
            double fixedWidth=Math.Min(245,Math.Max(0,available)),remaining=available-compact*fixedWidth-Math.Max(0,dockTiles.Count-1)*gap;
            double flexibleWidth=flexible==0?0:Math.Max(230,remaining/flexible);
            var lastTile=taskTilePanel.Children.OfType<ActivityTile>().LastOrDefault();foreach(var pair in dockTiles){var tile=pair.Value;double width=FullWidthDockItem(pair.Key)?flexibleWidth:fixedWidth;if(Math.Abs(tile.Width-width)>.25)tile.Width=width;tile.Margin=new Thickness(0,0,Object.ReferenceEquals(tile,lastTile)?0:gap,0);}
            var visibility=present.Count==0?Visibility.Collapsed:Visibility.Visible;if(copySummary.Visibility!=visibility)copySummary.Visibility=visibility;
        }
        static bool FullWidthDockItem(object key){var tool=key as ToolDockItem;if(tool!=null)return tool.Window is MediaConverterWindow;var activity=key as CopyActivity;if(activity==null)return false;return !activity.CompactDock;}
        void SetDockTile(object key,DockStatus state,Action open,HashSet<object> present,Action pause=null,bool paused=false,Action cancel=null) {
            present.Add(key);ActivityTile tile;if(!dockTiles.TryGetValue(key,out tile)){tile=new ActivityTile();dockTiles.Add(key,tile);taskTilePanel.Children.Add(tile);}tile.Update(state,open,pause,paused,cancel);
        }
    }
}
