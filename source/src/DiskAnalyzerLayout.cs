using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Interop;

namespace FileCompass {
    internal sealed partial class DiskAnalyzerWindow {
        internal readonly TabControl AnalyzerPages=new TabControl();
        System.Windows.Controls.Grid analyzerSplit;
        StackPanel analyzerActions;
        WrapPanel analyzerChoices;
        string analyzerSizingKey;
        internal double AnalyzerRequiredWidth;

        void InitializeAnalyzerWorkspace(DockPanel body,StackPanel oldHeader,System.Windows.Controls.Grid split,WrapPanel chartControls,System.Windows.Controls.Grid chartBody,StackPanel actions) {
            CleanOptions.Tabs(this,AnalyzerPages,actions);
            body.Children.Remove(oldHeader);body.Children.Remove(split);
            AnalyzerPages.Margin=new Thickness(12,8,12,10);body.Children.Add(AnalyzerPages);

            // Keep scan setup out of the results viewport; existing inputs and commands retain their state.
            var roots=(DockPanel)ScanRoots.Parent;oldHeader.Children.Remove(roots);
            var filters=(Expander)filterPanel.Parent;filters.Content=null;
            var diagnosticsExpander=(Expander)diagnostics.Parent;diagnosticsExpander.Content=null;
            var diagnosticControls=(WrapPanel)ShareChoice.Parent;oldHeader.Children.Remove(diagnosticControls);
            diagnosticControls.Children.Remove(ShareChoice);
            foreach(var label in diagnosticControls.Children.OfType<TextBlock>().Where(x=>x.Text==L.Text("analyzer.share")).ToArray())diagnosticControls.Children.Remove(label);

            var settings=new StackPanel{Margin=new Thickness(2,6,2,2)};
            var rootsBody=new StackPanel();rootsBody.Children.Add(roots);
            ScanRoots.Height=76;ScanRoots.ToolTip=L.Text("analyzer.scanRoots");
            rootsBody.Children.Add(new TextBlock{Text=L.Text("analyzer.help"),Foreground=FluentTheme.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
            settings.Children.Add(new GroupBox{Header=L.Text("analyzer.scanRoots"),Content=rootsBody,Margin=new Thickness(0,0,0,12)});
            settings.Children.Add(new GroupBox{Header=L.Text("analyzer.filters"),Content=filterPanel,Margin=new Thickness(0)});
            var settingsScroll=new ScrollViewer{Content=settings,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,8,0)};

            var diagnosticPage=new DockPanel{Margin=new Thickness(2,8,2,2)};
            DockPanel.SetDock(diagnosticControls,Dock.Top);diagnosticControls.Margin=new Thickness(0,0,0,8);diagnosticPage.Children.Add(diagnosticControls);
            diagnostics.MaxHeight=Double.PositiveInfinity;diagnostics.VerticalAlignment=VerticalAlignment.Stretch;diagnostics.Margin=new Thickness(0);diagnosticPage.Children.Add(diagnostics);

            var results=new DockPanel();
            var chartPane=(DockPanel)chartControls.Parent;chartPane.Children.Remove(chartControls);
            ShareChoice.Margin=new Thickness(0,0,0,4);ShareChoice.ToolTip=L.Text("analyzer.share");chartControls.Children.Add(ShareChoice);
            chartControls.Children.Remove(ChartChoice);
            var filesFrame=(Border)split.Children[0];filesFrame.Child=null;
            var filesPane=new DockPanel();filesFrame.Child=filesPane;
            chartControls.Margin=new Thickness(0,2,0,6);
            var choiceScroll=new ScrollViewer{Content=chartControls,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled};DockPanel.SetDock(choiceScroll,Dock.Top);filesPane.Children.Add(choiceScroll);filesPane.Children.Add(tabs);
            ChartChoice.HorizontalAlignment=HorizontalAlignment.Left;ChartChoice.Margin=new Thickness(0,2,0,6);
            DockPanel.SetDock(ChartChoice,Dock.Top);chartPane.Children.Insert(0,ChartChoice);
            split.Margin=new Thickness(0);results.Children.Add(split);
            split.ColumnDefinitions[0].Width=new GridLength(1.1,GridUnitType.Star);
            split.ColumnDefinitions[2].Width=new GridLength(1,GridUnitType.Star);
            split.ColumnDefinitions[0].MinWidth=270;split.ColumnDefinitions[2].MinWidth=260;
            var horizontalSplit=new GridSplitter{Width=6,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Stretch,Background=CompactTheme.PanelSurface,ResizeDirection=GridResizeDirection.Columns,ResizeBehavior=GridResizeBehavior.PreviousAndNext,ShowsPreview=true,ToolTip=L.Text("analyzer.resizePanes")};
            System.Windows.Controls.Grid.SetColumn(horizontalSplit,1);split.Children.Add(horizontalSplit);

            // The chart gets most of the vertical space; the legend remains resizable and scrollable.
            chartBody.RowDefinitions.Clear();
            chartBody.RowDefinitions.Add(new RowDefinition{Height=new GridLength(2,GridUnitType.Star),MinHeight=120});
            chartBody.RowDefinitions.Add(new RowDefinition{Height=new GridLength(6)});
            chartBody.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star),MinHeight=76});
            System.Windows.Controls.Grid.SetRow(legend,2);
            var verticalSplit=new GridSplitter{Height=6,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Stretch,Background=CompactTheme.PanelSurface,ResizeDirection=GridResizeDirection.Rows,ResizeBehavior=GridResizeBehavior.PreviousAndNext,ShowsPreview=true,ToolTip=L.Text("analyzer.resizeChart")};
            System.Windows.Controls.Grid.SetRow(verticalSplit,1);chartBody.Children.Add(verticalSplit);

            AnalyzerPages.Items.Add(new TabItem{Header=L.Text("analyzer.results"),Content=results});
            AnalyzerPages.Items.Add(new TabItem{Header=L.Text("analyzer.scopeSettings"),Content=settingsScroll});
            AnalyzerPages.Items.Add(new TabItem{Header=L.Text("analyzer.diagnostics"),Content=diagnosticPage});
            AnalyzerPages.SelectedIndex=0;

            var heading=body.Children.OfType<Border>().FirstOrDefault(x=>DockPanel.GetDock(x)==Dock.Top);
            if(heading!=null)heading.Padding=new Thickness(16,8,16,8);
            var footer=body.Children.OfType<Border>().FirstOrDefault(x=>DockPanel.GetDock(x)==Dock.Bottom);
            if(footer!=null)footer.Padding=new Thickness(12,7,12,7);
            // Header rows never wrap. On a screen too narrow for all translated labels they remain scrollable.
            foreach(var control in new[]{tabs,AnalyzerPages})control.Template=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TabControl'><DockPanel KeyboardNavigation.TabNavigation='Local'><ScrollViewer DockPanel.Dock='Top' HorizontalScrollBarVisibility='Auto' VerticalScrollBarVisibility='Disabled' Focusable='False' Padding='1'><StackPanel Orientation='Horizontal' IsItemsHost='True'/></ScrollViewer><ContentPresenter x:Name='PART_SelectedContentHost' ContentSource='SelectedContent' HorizontalAlignment='Stretch' VerticalAlignment='Stretch' Margin='0,3,0,0'/></DockPanel></ControlTemplate>");
            var footerBody=actions.Parent as StackPanel;
            if(footerBody!=null){footerBody.Children.Remove(actions);footerBody.Children.Add(new ScrollViewer{Content=actions,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});}
            analyzerSplit=split;analyzerActions=actions;analyzerChoices=chartControls;
            double originalWidth=Width;
            Loaded+=delegate{analyzerSizingKey=AnalyzerSizingKey();if(Math.Abs(Width-originalWidth)<1)FitAnalyzerWidth();};
            LayoutUpdated+=delegate{
                if(!IsLoaded||!IsVisible)return;string key=AnalyzerSizingKey();if(key==analyzerSizingKey)return;analyzerSizingKey=key;
                Dispatcher.BeginInvoke(new Action(delegate{if(IsVisible)FitAnalyzerWidth();}));
            };
        }
        string AnalyzerSizingKey(){return L.Language+"|"+FontFamily.Source+"|"+FontSize+"|"+tabs.FontSize+"|"+GroupChoice.FontSize;}
        double HeaderWidth(TabControl control){double width=0;foreach(TabItem item in control.Items){item.ApplyTemplate();item.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));width+=item.DesiredSize.Width+4;}return width+12;}
        internal void FitAnalyzerWidth(){
            foreach(var choice in new[]{GroupChoice,MetricChoice,ChartChoice,ShareChoice}){
                double minimum=choice==GroupChoice||choice==ShareChoice?145:choice==ChartChoice?120:115;
                foreach(var item in choice.Items){var text=new TextBlock{Text=L.TranslateExisting(Convert.ToString(item)),Style=choice.TryFindResource(typeof(TextBlock)) as Style,FontFamily=choice.FontFamily,FontStyle=choice.FontStyle,FontWeight=choice.FontWeight};text.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));minimum=Math.Max(minimum,text.DesiredSize.Width+44);}
                choice.MinWidth=Math.Ceiling(minimum);
            }
            analyzerChoices.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));analyzerActions.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));
            ChartChoice.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));
            double left=Math.Max(HeaderWidth(tabs)+20,analyzerChoices.DesiredSize.Width+20),right=Math.Max(450,ChartChoice.DesiredSize.Width+30);
            double chrome=analyzerSplit.ActualWidth>0?Math.Max(64,Width-analyzerSplit.ActualWidth):64;
            AnalyzerRequiredWidth=Math.Ceiling(Math.Max(left+right+8+chrome,Math.Max(HeaderWidth(AnalyzerPages)+chrome,analyzerActions.DesiredSize.Width+60)));
            var screen=System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
            var source=PresentationSource.FromVisual(this);double scale=source==null?1:source.CompositionTarget.TransformFromDevice.M11;
            double areaLeft=screen.Left*scale,areaRight=screen.Right*scale,available=Math.Max(MinWidth,(screen.Width*scale)-32);
            double next=Math.Min(Math.Min(available,MaxWidth),Math.Max(Width,AnalyzerRequiredWidth)),center=Left+Width/2;
            bool onScreen=Left<areaRight&&Left+Width>areaLeft;Width=next;
            analyzerSplit.ColumnDefinitions[0].Width=new GridLength(left,GridUnitType.Star);analyzerSplit.ColumnDefinitions[2].Width=new GridLength(right,GridUnitType.Star);
            if(onScreen)Left=Math.Max(areaLeft+16,Math.Min(center-next/2,areaRight-next-16));
        }
    }
}
