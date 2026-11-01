#if APP_TESTS
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal sealed partial class DiskAnalyzerWindow {
        internal void VerifyAnalyzerWorkspace(string output,Action<bool,string> check) {
            double oldWidth=Width,oldHeight=Height;int oldGroup=GroupChoice.SelectedIndex;
            var result=Result;var scan=ScanTask;
            try {
                Width=1160;Height=820;UpdateLayout();NavigationTests.Wait(Task.Delay(80));UpdateLayout();
                check(AnalyzerPages.Items.Count==3&&AnalyzerPages.SelectedIndex==0,"Analyzer opens on results with separate scope/filter and diagnostic tabs");
                check(Folders.IsVisible&&Chart.IsVisible&&!ScanRoots.IsVisible&&!filterPanel.IsVisible,"Scan settings do not consume the results viewport");
                check(Folders.ActualHeight>=400&&Chart.ActualHeight>=260,"Normal window reserves at least 400 px for files and 260 px for the chart");
                check(Chart.ActualHeight>legend.ActualHeight*1.5,"Chart receives most of the vertical chart-and-legend area");
                check(AdvancedToolsTests.Visual<GridSplitter>(this).Count()==2,"File/chart width and chart/legend height have independent splitters");
                GroupChoice.SelectedIndex=2;AnalyzerPages.SelectedIndex=1;UpdateLayout();
                check(ScanRoots.IsVisible&&filterPanel.IsVisible&&!Chart.IsVisible,"Scope and filters are directly accessible on their own page");
                NextStageTests.Draw(this,Path.Combine(output,"workspace-options.png"));
                AnalyzerPages.SelectedIndex=2;UpdateLayout();
                check(diagnostics.IsVisible&&diagnostics.ActualHeight>=300,"Diagnostics use the available page height instead of a small expander");
                NextStageTests.Draw(this,Path.Combine(output,"workspace-diagnostics.png"));
                AnalyzerPages.SelectedIndex=0;UpdateLayout();
                check(Object.ReferenceEquals(Result,result)&&Object.ReferenceEquals(ScanTask,scan)&&GroupChoice.SelectedIndex==2,"Switching pages preserves results and chart settings without restarting the scan");
                foreach(double width in new[]{780.0,1160.0}) {
                    Width=width;Height=560;NavigationTests.Wait(Task.Delay(80));UpdateLayout();
                    check(Chart.ActualHeight>=120&&Chart.ActualWidth>=200&&Folders.ActualHeight>=150,"Compact results retain a usable chart and file list at width "+width);
                    var frame=(FrameworkElement)Content;
                    check(AnalyzerPages.TranslatePoint(new Point(AnalyzerPages.ActualWidth,AnalyzerPages.ActualHeight),frame).Y<=frame.ActualHeight&&Chart.TransformToAncestor(frame).Transform(new Point(Chart.ActualWidth,Chart.ActualHeight)).X<=frame.ActualWidth,"Results remain inside the window at width "+width);
                    NextStageTests.Draw(this,Path.Combine(output,"workspace-compact-"+width+".png"));
                }
            } finally {Width=oldWidth;Height=oldHeight;GroupChoice.SelectedIndex=oldGroup;AnalyzerPages.SelectedIndex=0;UpdateLayout();}
        }
        internal void VerifyAnalyzerTranslatedWidths(string output,Action<bool,string> check){
            string language=L.Language;double width=Width,height=Height,font=FontSize;var result=Result;var task=ScanTask;
            bool hadStyle=Resources.Contains(typeof(TextBlock));object oldStyle=hadStyle?Resources[typeof(TextBlock)]:null;
            var sizedControls=tabs.Items.Cast<Control>().Concat(AnalyzerPages.Items.Cast<Control>()).Concat(new Control[]{GroupChoice,MetricChoice,ChartChoice,ShareChoice}).Concat(analyzerActions.Children.OfType<Control>()).ToArray();
            var originalFonts=sizedControls.ToDictionary(x=>x,x=>x.ReadLocalValue(Control.FontSizeProperty));
            try{
                AnalyzerPages.SelectedIndex=0;Height=820;
                foreach(double size in new[]{12.0,16.0})foreach(string locale in L.SupportedLanguages){
                    FontSize=size;var textStyle=new Style(typeof(TextBlock),Application.Current.TryFindResource(typeof(TextBlock)) as Style);textStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty,size));Resources[typeof(TextBlock)]=textStyle;
                    foreach(var control in sizedControls)control.FontSize=size;
                    L.ChangeLanguage(locale);LiveLanguage.Refresh(this);FitAnalyzerWidth();NavigationTests.Wait(Task.Delay(60));UpdateLayout();
                    var headers=tabs.Items.Cast<TabItem>().ToArray();var rows=headers.Select(x=>x.TranslatePoint(new Point(),tabs).Y).ToArray();
                    var pages=AnalyzerPages.Items.Cast<TabItem>().Select(x=>x.TranslatePoint(new Point(),AnalyzerPages).Y).ToArray();
                    bool fits=Width+1>=AnalyzerRequiredWidth,inside=headers.Sum(x=>x.DesiredSize.Width)<=tabs.ActualWidth+1;
                    var scroll=AdvancedToolsTests.Visual<ScrollViewer>(tabs).First();
                    check(rows.Max()-rows.Min()<1&&pages.Max()-pages.Min()<1&&(fits?inside:scroll.ScrollableWidth>0),locale+" / "+size+": translated headers stay in one row; window="+Width.ToString("F0")+", measured="+AnalyzerRequiredWidth.ToString("F0"));
                    var choice=ChartChoice.TranslatePoint(new Point(),this);var chart=Chart.TranslatePoint(new Point(),this);
                    check(choice.X>=chart.X-12&&choice.Y+ChartChoice.ActualHeight<=chart.Y&&analyzerActions.DesiredSize.Width<=((ScrollViewer)analyzerActions.Parent).ViewportWidth+1,locale+" / "+size+": chart type is over the graph and footer actions fit their row");
                    if(size==16&&(locale=="cs"||locale=="de"||locale=="fr"))NextStageTests.Draw(this,Path.Combine(output,"translated-"+locale+"-16.png"));
                }
                check(Object.ReferenceEquals(result,Result)&&Object.ReferenceEquals(task,ScanTask),"Language and font sizing retain the current analysis without restarting it");
            }finally{foreach(var item in originalFonts){if(item.Value==DependencyProperty.UnsetValue)item.Key.ClearValue(Control.FontSizeProperty);else item.Key.SetValue(Control.FontSizeProperty,item.Value);}if(hadStyle)Resources[typeof(TextBlock)]=oldStyle;else Resources.Remove(typeof(TextBlock));L.ChangeLanguage(language);LiveLanguage.Refresh(this);FontSize=font;Width=width;Height=height;FitAnalyzerWidth();UpdateLayout();}
        }
    }
}
#endif
