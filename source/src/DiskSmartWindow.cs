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
namespace FileCompass {
    internal sealed class DiskSmartWindow : Window {
        internal readonly ListBox Disks=DiskTiles();
        internal readonly DataGrid Attributes=AttributeGrid(),AttributesRight=AttributeGrid();
        internal readonly TextBlock HealthValue=new TextBlock{FontSize=26,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center};
        internal readonly TextBlock TemperatureValue=new TextBlock{FontSize=32,FontWeight=FontWeights.Bold,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap};
        readonly TextBlock healthRemaining=new TextBlock{FontSize=13,FontWeight=FontWeights.SemiBold,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap};
        readonly TextBlock model=new TextBlock{FontSize=18,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
        readonly TextBlock status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,3)},attributeCount=new TextBlock{Foreground=FluentTheme.Muted,VerticalAlignment=VerticalAlignment.Center};
        readonly Dictionary<string,TextBlock> values=new Dictionary<string,TextBlock>();
        readonly Grid content=new Grid(),summaryLayout=new Grid(),info=new Grid(),tables=new Grid();
        readonly StackPanel headline=new StackPanel();
        readonly ContentControl kindBadge=new ContentControl{ContentTemplate=new DataTemplate{VisualTree=SmartDiskKind.Badge()},Margin=new Thickness(0,0,9,8),VerticalAlignment=VerticalAlignment.Top};
        readonly Border healthCard,temperatureCard,summaryCard,attributesCard;
        readonly Button refresh,elevated,copy,save;
        readonly DispatcherTimer timer=new DispatcherTimer();readonly ProgressBar progress=new ProgressBar{Height=3,IsIndeterminate=true,Visibility=Visibility.Collapsed};
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        CancellationTokenSource cancellation;bool copying,closed,layoutQueued,inLayout,stacked,layoutReady,split;
        internal string Feedback {get{return status.Text;}}
        internal bool CopyEnabled {get{return copy.IsEnabled;}}
        string collectionStatus;SmartSnapshot snapshot;
        internal Task LoadingTask;internal List<SmartDisk> Results=new List<SmartDisk>();
        internal bool SplitAttributes {get{return split;}}
        internal SmartSnapshot Snapshot {get{return snapshot;}}
        internal DiskSmartWindow(Window owner,bool autoLoad=true) {
            CopyUi.Style(this,owner,L.Text("smart.title"),1360);SizeToContent=SizeToContent.Manual;ResizeMode=ResizeMode.CanResize;
            var work=SystemParameters.WorkArea;Width=Math.Min(1360,work.Width-32);Height=Math.Min(900,work.Height-32);MinWidth=Math.Min(800,Width);MinHeight=Math.Min(560,Height);
            StackPanel footerActions;var body=SettingsUx.Frame(this,L.Text("smart.title"),out footerActions);
            var header=(Border)body.Children[0];header.Child=null;var top=new DockPanel();header.Child=top;header.Padding=new Thickness(18,10,18,10);
            refresh=CopyUi.Button(L.Text("action.refresh"),delegate{if(cancellation==null)LoadingTask=RefreshDisks();});refresh.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(refresh,Dock.Right);top.Children.Add(refresh);top.Children.Add(Disks);
            System.Windows.Automation.AutomationProperties.SetName(Disks,L.Text("smart.diskSelection"));
            Disks.SelectionChanged+=delegate{RenderDisk();if(Disks.SelectedItem!=null)Disks.ScrollIntoView(Disks.SelectedItem);};
            var footer=(Border)footerActions.Parent;footer.Child=null;var footerBody=new StackPanel();footerBody.Children.Add(progress);footerBody.Children.Add(status);var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};footerBody.Children.Add(actions);footer.Child=footerBody;
            var interval=new ComboBox{MinWidth=140,Margin=new Thickness(3),ToolTip=L.Text("smart.interval")};foreach(string key in new[]{"smart.manual","smart.everyMinute","smart.everyFiveMinutes"})interval.Items.Add(L.Text(key));interval.SelectedIndex=0;interval.SelectionChanged+=delegate{timer.Stop();if(interval.SelectedIndex>0){timer.Interval=TimeSpan.FromMinutes(interval.SelectedIndex==1?1:5);timer.Start();}};actions.Children.Add(interval);
            elevated=CopyUi.Button(L.Text("smart.asAdmin"),delegate{try{Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--smart-window"){UseShellExecute=true,Verb="runas"});Close();}catch(System.ComponentModel.Win32Exception ex){status.Text=ex.NativeErrorCode==1223?L.Text("smart.elevationCancelled"):ex.Message;}});elevated.Visibility=SmartData.Administrator?Visibility.Collapsed:Visibility.Visible;actions.Children.Add(elevated);
            copy=CopyUi.Button(L.Text("smart.copy"),async delegate{await CopyReportAsync();});actions.Children.Add(copy);
            save=CopyUi.Button(L.Text("smart.save"),SaveReport);save.ToolTip=L.Text("smart.exportHelp");FluentTheme.Primary(save);actions.Children.Add(save);actions.Children.Add(CopyUi.Button(L.Text("action.close"),Close));
            content.Margin=new Thickness(16,12,16,12);body.Children.Add(content);
            var modelRow=new DockPanel();DockPanel.SetDock(kindBadge,Dock.Left);modelRow.Children.Add(kindBadge);modelRow.Children.Add(model);headline.Children.Add(modelRow);var cards=new Grid{Margin=new Thickness(0,0,0,12)};cards.ColumnDefinitions.Add(new ColumnDefinition());cards.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(10)});cards.ColumnDefinitions.Add(new ColumnDefinition());
            healthCard=Card("smart.health",HealthValue,healthRemaining);temperatureCard=Card("smart.temperature",TemperatureValue,null);cards.Children.Add(healthCard);Grid.SetColumn(temperatureCard,2);cards.Children.Add(temperatureCard);headline.Children.Add(cards);
            summaryLayout.Children.Add(headline);summaryLayout.Children.Add(info);
            summaryCard=Section(null,summaryLayout);content.Children.Add(summaryCard);
            for(int i=0;i<SmartSnapshot.InfoKeys.Length;i++){string key=SmartSnapshot.InfoKeys[i];var value=new TextBlock{Text="-",TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,3,3,3)};values.Add(key,value);}
            tables.ColumnDefinitions.Add(new ColumnDefinition());tables.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(0)});tables.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(0)});
            tables.Children.Add(Attributes);Grid.SetColumn(AttributesRight,2);tables.Children.Add(AttributesRight);AttributesRight.Visibility=Visibility.Collapsed;
            var attributePanel=new DockPanel();DockPanel.SetDock(attributeCount,Dock.Top);attributeCount.Margin=new Thickness(0,0,0,8);attributePanel.Children.Add(attributeCount);attributePanel.Children.Add(tables);
            attributesCard=Section(L.Text("smart.attributesTitle"),attributePanel);content.Children.Add(attributesCard);
            foreach(var grid in new[]{Attributes,AttributesRight}){Columns(grid);grid.SizeChanged+=delegate{QueueLayout();};}
            content.SizeChanged+=delegate{QueueLayout();};
            timer.Tick+=delegate{if(cancellation==null)LoadingTask=RefreshDisks();};Loaded+=delegate{QueueLayout();if(autoLoad)LoadingTask=RefreshDisks();};Closed+=delegate{closed=true;timer.Stop();lifetime.Cancel();if(cancellation!=null)cancellation.Cancel();};
            status.Text=L.Text("smart.starting");RenderDisk();
            ToolDock.Attach(this,actions,delegate{return new DockStatus{Title=L.Text("smart.title"),Icon="◉",Busy=cancellation!=null,Description=cancellation!=null?status.Text:model.Text+" · "+TemperatureValue.Text+" · "+HealthValue.Text};});
        }
        sealed class TileScrollPadding : IValueConverter {
            public object Convert(object value,Type type,object parameter,System.Globalization.CultureInfo culture){return new Thickness(0,0,0,value is Visibility&&(Visibility)value==Visibility.Visible?12:0);}
            public object ConvertBack(object value,Type type,object parameter,System.Globalization.CultureInfo culture){throw new NotSupportedException();}
        }
        static ListBox DiskTiles() {
            var list=new ListBox{BorderThickness=new Thickness(0),Padding=new Thickness(0),Background=Brushes.Transparent,ItemTemplate=DiskTileTemplate(),SelectionMode=SelectionMode.Single};
            ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Auto);ScrollViewer.SetVerticalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
            var row=new FrameworkElementFactory(typeof(StackPanel));row.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);row.SetBinding(FrameworkElement.MarginProperty,new Binding("ComputedHorizontalScrollBarVisibility"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(ScrollViewer),1),Converter=new TileScrollPadding()});list.ItemsPanel=new ItemsPanelTemplate(row);
            var style=new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(Control.WidthProperty,156.0));style.Setters.Add(new Setter(Control.HeightProperty,88.0));style.Setters.Add(new Setter(Control.MarginProperty,new Thickness(3,0,3,0)));
            style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(8,2,8,2)));style.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.WhiteSurface));style.Setters.Add(new Setter(Control.BorderBrushProperty,CompactTheme.Line));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(2)));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty,VerticalAlignment.Center));
            style.Setters.Add(new Setter(Control.ToolTipProperty,new Binding("TileHint")));style.Setters.Add(new Setter(System.Windows.Automation.AutomationProperties.NameProperty,new Binding("TileHint")));
            var frame=new FrameworkElementFactory(typeof(Border));frame.Name="TileFrame";frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));
            foreach(var pair in new[]{new[]{Border.BackgroundProperty,Control.BackgroundProperty},new[]{Border.BorderBrushProperty,Control.BorderBrushProperty},new[]{Border.BorderThicknessProperty,Control.BorderThicknessProperty},new[]{Border.PaddingProperty,Control.PaddingProperty}})frame.SetValue(pair[0],new TemplateBindingExtension(pair[1]));
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Stretch);presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);frame.AppendChild(presenter);
            style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(ListBoxItem)){VisualTree=frame}));
            var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.ThemeBrush("#F0F5FA")));style.Triggers.Add(hover);
            var selected=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.ThemeBrush("#E5F1FF")));selected.Setters.Add(new Setter(Control.BorderBrushProperty,FluentTheme.Accent));style.Triggers.Add(selected);
            var focus=new Trigger{Property=UIElement.IsKeyboardFocusWithinProperty,Value=true};focus.Setters.Add(new Setter(Control.BorderBrushProperty,FluentTheme.Accent));style.Triggers.Add(focus);list.ItemContainerStyle=style;
            list.PreviewMouseWheel+=delegate(object sender,System.Windows.Input.MouseWheelEventArgs e){var scroll=TileScroll(list);if(scroll!=null&&scroll.ScrollableWidth>0){scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset-e.Delta);e.Handled=true;}};
            return list;
        }
        static ScrollViewer TileScroll(DependencyObject parent) {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);var scroll=child as ScrollViewer;if(scroll!=null)return scroll;scroll=TileScroll(child);if(scroll!=null)return scroll;}return null;
        }
        static DataTemplate DiskTileTemplate() {
            var panel=new FrameworkElementFactory(typeof(StackPanel));
            string[] paths={"TileHealth","TileTemperature","TileVolumes","TileModel"};double[] sizes={12,22,12,11};
            for(int i=0;i<paths.Length;i++){var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding(paths[i]));text.SetValue(TextBlock.FontSizeProperty,sizes[i]);text.SetValue(TextBlock.TextAlignmentProperty,TextAlignment.Center);text.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);text.SetValue(TextBlock.FontWeightProperty,i==1?FontWeights.Bold:FontWeights.SemiBold);if(i<2)text.SetBinding(TextBlock.ForegroundProperty,new Binding(i==0?"TileHealthBrush":"TileTemperatureBrush"));else text.SetValue(TextBlock.ForegroundProperty,i==2?FluentTheme.Text:FluentTheme.Muted);if(i==1){var temperatureRow=new FrameworkElementFactory(typeof(StackPanel));temperatureRow.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);temperatureRow.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);temperatureRow.AppendChild(text);temperatureRow.AppendChild(SmartDiskKind.Badge(true));panel.AppendChild(temperatureRow);}else panel.AppendChild(text);}
            return new DataTemplate{VisualTree=panel};
        }
        internal void SetResults(List<SmartDisk> disks) {
            var selected=Disks.SelectedItem as SmartDisk;string device=selected==null?null:selected.Device;
            Results=disks;Disks.ItemsSource=Results;Disks.SelectedItem=Results.FirstOrDefault(x=>x.Device==device)??Results.FirstOrDefault();RenderDisk();
        }
        internal async Task CopyReportAsync(Action<string> writer=null,Func<int,Task> pause=null) {
            if(snapshot==null||copying||closed)return;var captured=snapshot;copying=true;copy.IsEnabled=false;status.Text=L.Text("smart.copying");
            try{bool copied=await ClipboardTextWriter.TryWriteAsync(captured.TextReport(),lifetime.Token,writer,pause);if(!closed)status.Text=L.Text(copied?"smart.copied":"smart.clipboardBusy");}
            catch(OperationCanceledException){}catch(Exception){if(!closed)status.Text=L.Text("smart.copyFailed");}
            finally{copying=false;copy.IsEnabled=snapshot!=null&&!closed;}
        }
        static DataGrid AttributeGrid() {
            var grid=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,IsReadOnly=true,Foreground=FluentTheme.Text,Background=CompactTheme.WhiteSurface,RowBackground=CompactTheme.WhiteSurface,AlternatingRowBackground=CompactTheme.ThemeBrush("#F4F7FB"),HeadersVisibility=DataGridHeadersVisibility.Column,EnableRowVirtualization=false,GridLinesVisibility=DataGridGridLinesVisibility.None,BorderThickness=new Thickness(0),SelectionUnit=DataGridSelectionUnit.FullRow,MinRowHeight=25,ColumnHeaderHeight=32,FontSize=12,CanUserSortColumns=false};
            var headers=new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));headers.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(3,0,3,0)));headers.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.PanelSurface));headers.Setters.Add(new Setter(Control.ForegroundProperty,FluentTheme.Text));headers.Setters.Add(new Setter(Control.BorderBrushProperty,CompactTheme.Line));headers.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0,0,0,1)));headers.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Left));grid.ColumnHeaderStyle=headers;
            var cells=new Style(typeof(DataGridCell));cells.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));cells.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));grid.CellStyle=cells;
            ScrollViewer.SetHorizontalScrollBarVisibility(grid,ScrollBarVisibility.Disabled);ScrollViewer.SetVerticalScrollBarVisibility(grid,ScrollBarVisibility.Auto);return grid;
        }
        Border Card(string title,TextBlock value,TextBlock detail) {
            var panel=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var label=new TextBlock{Text=L.Text(title),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,4),FontWeight=FontWeights.SemiBold};
            panel.Children.Add(label);panel.Children.Add(value);if(detail!=null)panel.Children.Add(detail);
            return new Border{Child=panel,Padding=new Thickness(10,9,10,9),MinHeight=100,CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1)};
        }
        Border Section(string title,FrameworkElement element) {
            var panel=new DockPanel();if(title!=null){var heading=new TextBlock{Text=title,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(heading,Dock.Top);panel.Children.Add(heading);}panel.Children.Add(element);
            return new Border{Child=panel,Padding=new Thickness(12),CornerRadius=new CornerRadius(8),Background=CompactTheme.WhiteSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(1)};
        }
        void Columns(DataGrid grid) {
            string[] headers={"ID",L.Text("smart.attribute"),L.Text("smart.current"),L.Text("smart.worst"),L.Text("smart.threshold"),L.Text("smart.raw"),L.Text("smart.state"),L.Text("smart.normalizedCompact")};
            string[] bindings={"Id","Name","Current","Worst","Threshold","Raw","State","Normalized"};
            for(int i=0;i<headers.Length;i++){
                var text=new Style(typeof(TextBlock));text.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));text.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(3,3,3,3)));text.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center));
                if(i>=2&&i<=4||i==7)text.Setters.Add(new Setter(TextBlock.FontSizeProperty,11.0));
                var column=new DataGridTextColumn{Header=new TextBlock{Text=headers[i],FontSize=11,TextWrapping=TextWrapping.Wrap,ToolTip=i==7?L.Text("smart.normalizedHelp"):headers[i]},Binding=new Binding(bindings[i]),Width=new DataGridLength(1,DataGridLengthUnitType.Star),MinWidth=i==1?90:20,ElementStyle=text};
                if(i==7)column.Visibility=Visibility.Collapsed;
                grid.Columns.Add(column);if(i==7)column.DisplayIndex=2;
            }
            RowStyle(grid);
        }
        void RowStyle(DataGrid grid) {
            var rowStyle=new Style(typeof(DataGridRow));var warning=new DataTrigger{Binding=new Binding("State"),Value=L.Text("smart.caution")};warning.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.ThemeBrush("#FFF6D6")));warning.Setters.Add(new Setter(Control.ForegroundProperty,FluentTheme.Text));rowStyle.Triggers.Add(warning);
            var bad=new DataTrigger{Binding=new Binding("State"),Value=L.Text("smart.bad")};bad.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.ThemeBrush("#FFE8E8")));bad.Setters.Add(new Setter(Control.ForegroundProperty,FluentTheme.Text));rowStyle.Triggers.Add(bad);grid.RowStyle=rowStyle;
        }
        void QueueLayout(){if(closed||layoutQueued)return;layoutQueued=true;Dispatcher.BeginInvoke(new Action(()=>{layoutQueued=false;if(!closed)LayoutView();}),DispatcherPriority.Loaded);}
        void LayoutView() {
            if(inLayout||content.ActualWidth<=0||content.ActualHeight<=0)return;inLayout=true;
            try {
                bool nextStacked=content.ActualWidth<980;
                if(!layoutReady||stacked!=nextStacked){
                    stacked=nextStacked;layoutReady=true;content.RowDefinitions.Clear();content.ColumnDefinitions.Clear();summaryLayout.RowDefinitions.Clear();summaryLayout.ColumnDefinitions.Clear();
                    if(stacked){
                        content.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});content.RowDefinitions.Add(new RowDefinition());content.ColumnDefinitions.Add(new ColumnDefinition());
                        Grid.SetColumn(summaryCard,0);Grid.SetRow(summaryCard,0);Grid.SetColumn(attributesCard,0);Grid.SetRow(attributesCard,1);summaryCard.Margin=new Thickness(0,0,0,10);
                        summaryLayout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(Math.Max(235,Math.Min(320,content.ActualWidth*.3)))});summaryLayout.ColumnDefinitions.Add(new ColumnDefinition());
                        Grid.SetRow(headline,0);Grid.SetColumn(headline,0);Grid.SetRow(info,0);Grid.SetColumn(info,1);headline.Margin=new Thickness(0,0,16,0);BuildInformation(2);
                    }else{
                        content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(340)});content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});content.ColumnDefinitions.Add(new ColumnDefinition());content.RowDefinitions.Add(new RowDefinition());
                        Grid.SetColumn(summaryCard,0);Grid.SetRow(summaryCard,0);Grid.SetColumn(attributesCard,2);Grid.SetRow(attributesCard,0);summaryCard.Margin=new Thickness(0);
                        summaryLayout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});summaryLayout.RowDefinitions.Add(new RowDefinition());summaryLayout.ColumnDefinitions.Add(new ColumnDefinition());
                        Grid.SetRow(headline,0);Grid.SetColumn(headline,0);Grid.SetRow(info,1);Grid.SetColumn(info,0);headline.Margin=new Thickness(0);BuildInformation(1);
                    }
                    QueueLayout();
                }
                if(!stacked&&content.ColumnDefinitions.Count==3)content.ColumnDefinitions[0].Width=new GridLength(content.ActualWidth<1160?300:340);
                if(stacked&&summaryLayout.ColumnDefinitions.Count==2)summaryLayout.ColumnDefinitions[0].Width=new GridLength(Math.Max(235,Math.Min(320,content.ActualWidth*.3)));
                if(snapshot==null)return;
                bool useTwo=snapshot.Attributes.Count>0&&tables.ActualHeight>0&&EstimateHeight(snapshot.Attributes,false,tables.ActualWidth)>tables.ActualHeight&&tables.ActualWidth>=640;
                if(useTwo!=split){split=useTwo;BindAttributes();QueueLayout();}
                foreach(var grid in new[]{Attributes,AttributesRight})if(grid.IsVisible)LayoutColumns(grid,split);
            }finally{inLayout=false;}
        }
        void BuildInformation(int pairs) {
            info.Children.Clear();info.RowDefinitions.Clear();info.ColumnDefinitions.Clear();
            for(int i=0;i<pairs;i++){info.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});info.ColumnDefinitions.Add(new ColumnDefinition());}
            for(int i=0;i<SmartSnapshot.InfoKeys.Length;i++){int row=i/pairs,column=(i%pairs)*2;if(i%pairs==0)info.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});string key=SmartSnapshot.InfoKeys[i];var label=new TextBlock{Text=L.Text("smart."+key),Foreground=FluentTheme.Muted,FontSize=11,TextWrapping=TextWrapping.Wrap,MaxWidth=120,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,3,9,3)};Grid.SetRow(label,row);Grid.SetColumn(label,column);info.Children.Add(label);var value=values[key];Grid.SetRow(value,row);Grid.SetColumn(value,column+1);value.Margin=new Thickness(0,3,pairs==2?14:0,3);info.Children.Add(value);}
        }
        void BindAttributes() {
            var rows=snapshot==null?new List<SmartAttribute>():snapshot.Attributes;int middle=(rows.Count+1)/2;
            Attributes.ItemsSource=split?rows.Take(middle).ToList():rows;AttributesRight.ItemsSource=split?rows.Skip(middle).ToList():null;
            AttributesRight.Visibility=split?Visibility.Visible:Visibility.Collapsed;tables.ColumnDefinitions[1].Width=new GridLength(split?12:0);tables.ColumnDefinitions[2].Width=split?new GridLength(1,GridUnitType.Star):new GridLength(0);
        }
        double[] ColumnWidths(double available,bool compact) {
            bool nvme=snapshot!=null&&snapshot.Nvme,states=snapshot!=null&&snapshot.Attributes.Any(a=>!String.IsNullOrEmpty(a.State));
            var widths=compact?new[]{30.0,0,0,0,0,86.0,0,nvme?0:90.0}:new[]{34.0,0,nvme?0:60.0,nvme?0:60.0,nvme?0:60.0,170.0,states?82.0:0,0};
            if(!compact){double fixedTotal=widths.Sum(),scale=Math.Min(1,Math.Max(.45,(available-130)/Math.Max(1,fixedTotal)));for(int i=0;i<widths.Length;i++)if(i!=1&&widths[i]>0)widths[i]=Math.Max(i==0?30:20,Math.Floor(widths[i]*scale));}
            widths[1]=Math.Max(90,available-widths.Sum());return widths;
        }
        void LayoutColumns(DataGrid grid,bool compact) {grid.FontSize=compact?11:12;
            var widths=ColumnWidths(Math.Max(250,grid.ActualWidth-22),compact);((DataGridTextColumn)grid.Columns[1]).Binding=new Binding(compact?"DisplayName":"Name");
            for(int i=0;i<grid.Columns.Count;i++){grid.Columns[i].Visibility=widths[i]>0?Visibility.Visible:Visibility.Collapsed;grid.Columns[i].Width=new DataGridLength(Math.Max(20,widths[i]));var header=grid.Columns[i].Header as TextBlock;if(header!=null)header.MaxWidth=Math.Max(20,widths[i]-6);}
        }
        double EstimateHeight(IList<SmartAttribute> rows,bool compact,double width) {
            var widths=ColumnWidths(Math.Max(250,width-22),compact);double height=34;
            foreach(var row in rows){double item=25;foreach(var value in new[]{Tuple.Create(row.Name,widths[1]),Tuple.Create(row.Raw,widths[5]),Tuple.Create(row.State,widths[6])})if(value.Item2>0){var text=new TextBlock{Text=value.Item1,FontSize=12,FontFamily=FontFamily,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(3)};text.Measure(new Size(Math.Max(30,value.Item2-10),Double.PositiveInfinity));item=Math.Max(item,text.DesiredSize.Height+4);}height+=item;}return height;
        }
        internal void RefreshLanguage(){Title=L.Text("smart.title");Disks.ItemTemplate=DiskTileTemplate();System.Windows.Automation.AutomationProperties.SetName(Disks,L.Text("smart.diskSelection"));foreach(var grid in new[]{Attributes,AttributesRight}){string[] labels={"ID",L.Text("smart.attribute"),L.Text("smart.current"),L.Text("smart.worst"),L.Text("smart.threshold"),L.Text("smart.raw"),L.Text("smart.state"),L.Text("smart.normalizedCompact")};for(int i=0;i<labels.Length;i++){var header=grid.Columns[i].Header as TextBlock;if(header!=null){header.Text=labels[i];header.ToolTip=i==7?L.Text("smart.normalizedHelp"):labels[i];}}RowStyle(grid);}if(layoutReady)BuildInformation(stacked?2:1);RenderDisk();}
        internal void RenderDisk() {
            var disk=Disks.SelectedItem as SmartDisk;snapshot=disk==null?null:SmartSnapshot.Create(disk);copy.IsEnabled=disk!=null&&!copying;save.IsEnabled=disk!=null;
            kindBadge.Content=null;kindBadge.Content=disk;kindBadge.Visibility=disk==null?Visibility.Collapsed:Visibility.Visible;
            if(snapshot==null){model.Text=L.Text("smart.title");HealthValue.Text=L.Text("smart.unknown");TemperatureValue.Text="-";healthRemaining.Text="";foreach(var value in values.Values)value.Text="-";ApplyBadge(healthCard,"unknown");ApplyBadge(temperatureCard,"unknown");attributeCount.Text=L.Format("smart.attributeCount",0);BindAttributes();return;}
            model.Text=snapshot.Model;model.ToolTip=snapshot.Device;HealthValue.Text=snapshot.HealthText;HealthValue.ToolTip=L.Text("smart.healthHelp");healthRemaining.Text=snapshot.RemainingText;healthRemaining.ToolTip=L.Text("smart.healthHelp");healthRemaining.Visibility=snapshot.RemainingText.Length>0?Visibility.Visible:Visibility.Collapsed;TemperatureValue.Text=snapshot.TemperatureText;
            ApplyBadge(healthCard,snapshot.HealthKey);ApplyBadge(temperatureCard,disk.Temperature.HasValue?"temperature":"unknown");foreach(var item in snapshot.Information){values[item.Key].Text=item.Value;values[item.Key].ToolTip=item.Value;}
            attributeCount.Text=L.Format("smart.attributeCount",snapshot.Attributes.Count);BindAttributes();foreach(var grid in new[]{Attributes,AttributesRight})LayoutColumns(grid,split);QueueLayout();
            if(collectionStatus!=null)status.Text=collectionStatus;if(!String.IsNullOrEmpty(snapshot.Error))status.Text=L.Text("smart.readProblem")+" "+snapshot.Error;
        }
        static void ApplyBadge(Border card,string state) {
            string background=state=="good"?"#DCF5E8":state=="caution"?"#FFF0C2":state=="bad"?"#FFE0E0":state=="temperature"?"#DFEEFF":"#E4E7EB";
            string foreground=state=="good"?"#12623C":state=="caution"?"#704600":state=="bad"?"#9E2031":state=="temperature"?"#135C9E":"#596572";
            card.Background=CompactTheme.ThemeBrush(background);card.BorderBrush=CompactTheme.ThemeBrush(foreground);
            foreach(var text in ((StackPanel)card.Child).Children.OfType<TextBlock>())text.Foreground=CompactTheme.ThemeBrush(foreground);
        }
        void SaveReport() {
            if(snapshot==null)return;var captured=snapshot;
            var dialog=new Microsoft.Win32.SaveFileDialog{Filter=L.Text("smart.exportFilter"),FileName="SMART-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),AddExtension=true,DefaultExt=".png",OverwritePrompt=true};
            if(dialog.ShowDialog(this)!=true)return;
            try{string extension=Path.GetExtension(dialog.FileName).ToLowerInvariant();int format=extension==".png"?1:extension==".pdf"?2:extension==".txt"?3:extension==".json"?4:dialog.FilterIndex;if(format==1)SmartReports.Png(dialog.FileName,captured);else if(format==2)SmartReports.Pdf(dialog.FileName,captured);else if(format==3)File.WriteAllText(dialog.FileName,captured.TextReport(),new System.Text.UTF8Encoding(true));else File.WriteAllText(dialog.FileName,captured.RawJson??"{}",new System.Text.UTF8Encoding(true));status.Text=L.Format("smart.exportSaved",dialog.FileName);}catch(Exception ex){status.Text=L.Text("smart.failed")+" "+ex.Message;}
        }
        internal async Task RefreshDisks(){
            if(cancellation!=null)return;cancellation=new CancellationTokenSource();var token=cancellation.Token;refresh.IsEnabled=false;progress.Visibility=Visibility.Visible;status.Text=L.Text("smart.loading");
            var report=new Progress<string>(x=>{if(!closed&&cancellation!=null)status.Text=L.Text("smart.loading")+" "+x;});
            try{var disks=await Task.Run(()=>SmartData.Collect(token,x=>((IProgress<string>)report).Report(x)));if(closed)return;SetResults(disks);collectionStatus=Results.Count==0?L.Text("smart.noDisks"):(SmartData.Administrator?L.Text("smart.updated"):L.Text("smart.adminHint"))+" · "+DateTime.Now.ToString("T");status.Text=collectionStatus;RenderDisk();}
            catch(OperationCanceledException){}catch(Exception ex){if(!closed)status.Text=L.Text("smart.failed")+" "+ex.Message;}
            finally{cancellation.Dispose();cancellation=null;if(!closed){refresh.IsEnabled=true;progress.Visibility=Visibility.Collapsed;}}
        }

    }
    public partial class MainWindow { internal void ShowDiskSmart(){new DiskSmartWindow(this).Show();} }
}
