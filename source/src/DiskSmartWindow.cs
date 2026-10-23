using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
namespace CommanderCZ {
    internal sealed class DiskSmartWindow : Window {
        internal readonly ComboBox Disks=new ComboBox{MinWidth=200,DisplayMemberPath="Label"};
        internal readonly DataGrid Attributes=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,IsReadOnly=true,Background=CompactTheme.WhiteSurface,RowBackground=CompactTheme.WhiteSurface,AlternatingRowBackground=CompactTheme.ThemeBrush("#F4F7FB"),HeadersVisibility=DataGridHeadersVisibility.Column,EnableRowVirtualization=true,RowHeight=23,ColumnHeaderHeight=28};
        readonly TextBlock model=new TextBlock{FontSize=21,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,8,0,8)},health=new TextBlock{FontSize=23,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap},temperature=new TextBlock{FontSize=26,FontWeight=FontWeights.SemiBold},status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,3)};
        readonly Dictionary<string,TextBlock> values=new Dictionary<string,TextBlock>();
        readonly Button refresh, elevated, copy, save;
        readonly DispatcherTimer timer=new DispatcherTimer();readonly ProgressBar progress=new ProgressBar{Height=3,IsIndeterminate=true,Visibility=Visibility.Collapsed};
        CancellationTokenSource cancellation;bool closed;string collectionStatus;internal Task LoadingTask;internal List<SmartDisk> Results=new List<SmartDisk>();
        internal DiskSmartWindow(Window owner,bool autoLoad=true) {
            CopyUi.Style(this,owner,L.Text("smart.title"),1120);SizeToContent=SizeToContent.Manual;ResizeMode=ResizeMode.CanResize;var work=SystemParameters.WorkArea;Width=Math.Min(1120,work.Width-32);Height=Math.Min(850,work.Height-32);MinWidth=Math.Min(780,Width);MinHeight=Math.Min(600,Height);
            var body=new DockPanel{Margin=new Thickness(12)};Content=body;
            var top=new DockPanel();DockPanel.SetDock(top,Dock.Top);body.Children.Add(top);
            var navigation=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(navigation,Dock.Right);top.Children.Add(navigation);
            navigation.Children.Add(CopyUi.Button("◀",delegate{if(Disks.Items.Count>0)Disks.SelectedIndex=(Disks.SelectedIndex+Disks.Items.Count-1)%Disks.Items.Count;}));navigation.Children.Add(CopyUi.Button("▶",delegate{if(Disks.Items.Count>0)Disks.SelectedIndex=(Disks.SelectedIndex+1)%Disks.Items.Count;}));
            refresh=CopyUi.Button(L.Text("action.refresh"),delegate{if(cancellation==null)LoadingTask=RefreshDisks();});navigation.Children.Add(refresh);top.Children.Add(Disks);Disks.SelectionChanged+=delegate{RenderDisk();};
            var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);body.Children.Add(footer);footer.Children.Add(progress);footer.Children.Add(status);
            var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};footer.Children.Add(actions);
            var interval=new ComboBox{MinWidth=140,Margin=new Thickness(3),ToolTip=L.Text("smart.interval")};foreach(string key in new[]{"smart.manual","smart.everyMinute","smart.everyFiveMinutes"})interval.Items.Add(L.Text(key));interval.SelectedIndex=0;interval.SelectionChanged+=delegate{timer.Stop();if(interval.SelectedIndex>0){timer.Interval=TimeSpan.FromMinutes(interval.SelectedIndex==1?1:5);timer.Start();}};actions.Children.Add(interval);
            elevated=CopyUi.Button(L.Text("smart.asAdmin"),delegate{try{Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--smart-window"){UseShellExecute=true,Verb="runas"});Close();}catch(System.ComponentModel.Win32Exception ex){status.Text=ex.NativeErrorCode==1223?L.Text("smart.elevationCancelled"):ex.Message;}});elevated.Visibility=SmartData.Administrator?Visibility.Collapsed:Visibility.Visible;actions.Children.Add(elevated);
            copy=CopyUi.Button(L.Text("smart.copy"),delegate{var disk=Disks.SelectedItem as SmartDisk;if(disk!=null)try{Clipboard.SetText(Report(disk));}catch(System.Runtime.InteropServices.COMException ex){status.Text=ex.Message;}});actions.Children.Add(copy);
            save=CopyUi.Button(L.Text("smart.save"),delegate{var disk=Disks.SelectedItem as SmartDisk;if(disk==null)return;var dialog=new Microsoft.Win32.SaveFileDialog{Filter="Text (*.txt)|*.txt|JSON (*.json)|*.json",FileName="SMART",AddExtension=true};if(dialog.ShowDialog(this)==true)try{File.WriteAllText(dialog.FileName,dialog.FilterIndex==2?disk.RawJson:Report(disk),new System.Text.UTF8Encoding(true));}catch(Exception ex){status.Text=ex.Message;}});actions.Children.Add(save);actions.Children.Add(CopyUi.Button(L.Text("action.close"),Close));
            var summary=new StackPanel();DockPanel.SetDock(summary,Dock.Top);body.Children.Add(summary);summary.Children.Add(model);
            var overview=new Grid{Margin=new Thickness(0,0,0,10)};overview.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(180)});overview.ColumnDefinitions.Add(new ColumnDefinition());summary.Children.Add(overview);
            var cards=new StackPanel{Margin=new Thickness(0,0,12,0)};overview.Children.Add(cards);cards.Children.Add(Card("smart.health",health));cards.Children.Add(Card("smart.temperature",temperature));
            var info=new Grid();Grid.SetColumn(info,1);overview.Children.Add(info);for(int i=0;i<4;i++)info.ColumnDefinitions.Add(new ColumnDefinition{Width=i%2==0?GridLength.Auto:new GridLength(1,GridUnitType.Star)});
            string[] keys={"firmware","serial","interface","capacity","letters","standard","rotation","hours","cycles","read","written","features"};
            for(int i=0;i<keys.Length;i++){int row=i/2,column=(i%2)*2;if(i%2==0)info.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var label=new TextBlock{Text=L.Text("smart."+keys[i]),Foreground=FluentTheme.Muted,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,4,8,4)};Grid.SetRow(label,row);Grid.SetColumn(label,column);info.Children.Add(label);var value=new TextBlock{Text="—",TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,4,12,4)};values[keys[i]]=value;Grid.SetRow(value,row);Grid.SetColumn(value,column+1);info.Children.Add(value);}
            AddColumn("ID","Id",.25);AddColumn(L.Text("smart.attribute"),"Name",2.2);AddColumn(L.Text("smart.current"),"Current",.6);AddColumn(L.Text("smart.worst"),"Worst",.6);AddColumn(L.Text("smart.threshold"),"Threshold",.6);AddColumn(L.Text("smart.raw"),"Raw",1.5);AddColumn(L.Text("smart.state"),"State",.7);body.Children.Add(Attributes);
            Attributes.SizeChanged+=delegate { LayoutColumns(); };
            timer.Tick+=delegate{if(cancellation==null)LoadingTask=RefreshDisks();};Loaded+=delegate{if(autoLoad)LoadingTask=RefreshDisks();};Closed+=delegate{closed=true;timer.Stop();if(cancellation!=null)cancellation.Cancel();};
            status.Text=L.Text("smart.starting");RenderDisk();
        }
        Border Card(string title,TextBlock value){var panel=new StackPanel();panel.Children.Add(new TextBlock{Text=L.Text(title),Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,6)});panel.Children.Add(value);return new Border{Child=panel,Padding=new Thickness(12),Margin=new Thickness(0,0,0,8),CornerRadius=new CornerRadius(7),Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(1)};}
        void AddColumn(string title,string binding,double width){Attributes.Columns.Add(new DataGridTextColumn{Header=title,Binding=new Binding(binding),Width=binding=="Name"?new DataGridLength(1,DataGridLengthUnitType.Star):new DataGridLength(binding=="Id"?42:binding=="Raw"?180:binding=="State"?90:70),MinWidth=binding=="Id"?42:binding=="Name"?220:binding=="Raw"?160:binding=="State"?90:70});}
        internal async Task RefreshDisks(){
            if(cancellation!=null)return;cancellation=new CancellationTokenSource();var token=cancellation.Token;string selected=(Disks.SelectedItem as SmartDisk)==null?null:((SmartDisk)Disks.SelectedItem).Device;refresh.IsEnabled=false;progress.Visibility=Visibility.Visible;status.Text=L.Text("smart.loading");
            var report=new Progress<string>(x=>{if(!closed&&cancellation!=null)status.Text=L.Text("smart.loading")+" "+x;});
            try{var disks=await Task.Run(()=>SmartData.Collect(token,x=>((IProgress<string>)report).Report(x)));if(closed)return;Results=disks;Disks.ItemsSource=Results;Disks.SelectedItem=Results.FirstOrDefault(x=>x.Device==selected)??Results.FirstOrDefault();collectionStatus=Results.Count==0?L.Text("smart.noDisks"):(SmartData.Administrator?L.Text("smart.updated"):L.Text("smart.adminHint"))+" · "+DateTime.Now.ToString("T");status.Text=collectionStatus;RenderDisk();}
            catch(OperationCanceledException){}catch(Exception ex){if(!closed)status.Text=L.Text("smart.failed")+" "+ex.Message;}
            finally{cancellation.Dispose();cancellation=null;if(!closed){refresh.IsEnabled=true;progress.Visibility=Visibility.Collapsed;}}
        }
        internal void RenderDisk(){
            var disk=Disks.SelectedItem as SmartDisk;copy.IsEnabled=save.IsEnabled=disk!=null;Attributes.ItemsSource=disk==null?null:disk.Attributes;
            if(disk==null){model.Text=L.Text("smart.title");health.Text=L.Text("smart.unknown");temperature.Text="—";foreach(var value in values.Values)value.Text="—";return;}
            var data=disk.Data;model.Text=disk.Model+" · "+SmartData.Capacity(SmartData.Get(data,"user_capacity","bytes"));model.ToolTip=disk.Device;
            health.Text=L.Text("smart."+disk.Health)+(disk.Remaining.HasValue?"\n"+disk.Remaining.Value.ToString("N0")+" %":"");health.ToolTip=L.Text("smart.healthHelp");health.Foreground=CompactTheme.ThemeBrush(disk.Health=="bad"?"#B92331":disk.Health=="caution"?"#B56D38":disk.Health=="good"?"#009B83":"#596572");
            temperature.Text=disk.Temperature.HasValue?disk.Temperature.Value.ToString("N0")+" °C":"—";
            Set("firmware",SmartData.Text(SmartData.Get(data,"firmware_version")));Set("serial",SmartData.Text(SmartData.Get(data,"serial_number")));Set("interface",SmartData.Text(SmartData.Get(data,"device","protocol"),disk.Type));Set("capacity",SmartData.Capacity(SmartData.Get(data,"user_capacity","bytes")));Set("letters",disk.Letters.Length>0?disk.Letters:L.Text("smart.unavailable"));
            Set("standard",SmartData.Text(SmartData.Get(data,"nvme_version","string"),SmartData.Text(SmartData.Get(data,"ata_version","string"))));
            decimal? rpm=SmartData.Number(SmartData.Get(data,"rotation_rate"));Set("rotation",rpm.HasValue?(rpm.Value==0?"SSD":rpm.Value.ToString("N0")+" RPM"):disk.Type=="nvme"?"SSD":L.Text("smart.unavailable"));
            Set("hours",SmartData.Metric(SmartData.Get(data,"power_on_time","hours")));Set("cycles",SmartData.Metric(SmartData.Get(data,"power_cycle_count")));
            var nvme=SmartData.Get(data,"nvme_smart_health_information_log");decimal? read=SmartData.Number(SmartData.Get(nvme,"data_units_read")),written=SmartData.Number(SmartData.Get(nvme,"data_units_written"));
            Set("read",read.HasValue?SmartData.Capacity(read.Value*512000m):L.Text("smart.unavailable"));Set("written",written.HasValue?SmartData.Capacity(written.Value*512000m):L.Text("smart.unavailable"));
            var features=new List<string>();if(SmartData.Get(data,"smart_support","available") as bool?==true)features.Add("SMART");if(SmartData.Get(data,"trim","supported") as bool?==true)features.Add("TRIM");Set("features",features.Count>0?String.Join(", ",features):"—");
            bool isNvme=nvme!=null;for(int i=2;i<=4;i++)Attributes.Columns[i].Visibility=isNvme?Visibility.Collapsed:Visibility.Visible;
            LayoutColumns();
            if(collectionStatus!=null)status.Text=collectionStatus;
            if(!String.IsNullOrEmpty(disk.Error))status.Text=L.Text("smart.readProblem")+" "+disk.Error;
        }
        void LayoutColumns(){if(Attributes.Columns.Count<7||Attributes.ActualWidth<=0)return;Attributes.Columns[1].Width=new DataGridLength(Math.Max(220,Attributes.ActualWidth-24-42-180-90-(Attributes.Columns[2].Visibility==Visibility.Visible?210:0)));}
        void Set(string key,string value){values[key].Text=value;values[key].ToolTip=value;}
        string Report(SmartDisk disk){return disk.Label+Environment.NewLine+L.Text("smart.health")+": "+health.Text+Environment.NewLine+L.Text("smart.temperature")+": "+temperature.Text+Environment.NewLine+String.Join(Environment.NewLine,values.Select(x=>L.Text("smart."+x.Key)+": "+x.Value.Text))+Environment.NewLine+String.Join(Environment.NewLine,disk.Attributes.Select(x=>x.Id+"\t"+x.Name+"\t"+x.Current+"\t"+x.Worst+"\t"+x.Threshold+"\t"+x.Raw+"\t"+x.State))+Environment.NewLine+(disk.Error??"");}
    }
    public partial class MainWindow { internal void ShowDiskSmart(){new DiskSmartWindow(this).Show();} }
}
