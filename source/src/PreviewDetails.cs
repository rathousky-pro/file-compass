using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace CommanderCZ {
    internal sealed partial class QuickPreviewPane {
        readonly Expander detailsExpander=new Expander {IsExpanded=false,Margin=new Thickness(0,4,0,2),Visibility=Visibility.Collapsed};
        readonly TextBlock detailsHeader=new TextBlock {TextTrimming=TextTrimming.CharacterEllipsis};
        readonly StackPanel detailsRows=new StackPanel();
        FileDetails detailsData;
        internal Task MetadataLoadingTask {get;private set;}
        internal FileDetails Metadata {get{return detailsData;}}
        void InitializeDetails(DockPanel body,bool compact) {
            detailsExpander.Header=detailsHeader;
            var rows=new DockPanel();
            var copy=CopyUi.Button(L.Text("details.copy"),delegate {if(detailsData!=null)try{Clipboard.SetText(String.Join(Environment.NewLine,detailsData.Rows.Select(x=>x.Label+": "+x.Value)));}catch(System.Runtime.InteropServices.COMException){}});
            copy.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(copy,Dock.Top);rows.Children.Add(copy);
            rows.Children.Add(new ScrollViewer {Content=detailsRows,MaxHeight=compact?115:190,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            if(compact) {
                rows.Children.Remove(copy); detailsExpander.Header=null;
                var heading=new DockPanel(); copy.Content="⧉";copy.ToolTip=L.Text("details.copy");copy.MinWidth=26;copy.Height=24;copy.Padding=new Thickness(4,0,4,0);copy.Margin=new Thickness(4,0,0,0);copy.Visibility=Visibility.Collapsed;System.Windows.Automation.AutomationProperties.SetName(copy,L.Text("details.copy"));detailsExpander.Width=160;detailsExpander.Margin=new Thickness(8,0,0,0);
                DockPanel.SetDock(copy,Dock.Right);heading.Children.Add(copy);heading.Children.Add(detailsHeader);detailsExpander.Header=heading;
                detailsExpander.Expanded+=delegate {copy.Visibility=Visibility.Visible;DockPanel.SetDock(detailsExpander,Dock.Right);detailsExpander.Width=330;detailsExpander.Margin=new Thickness(8,0,0,0);};
                detailsExpander.Collapsed+=delegate {copy.Visibility=Visibility.Collapsed;DockPanel.SetDock(detailsExpander,Dock.Right);detailsExpander.Width=160;detailsExpander.Margin=new Thickness(8,0,0,0);};
            }
            detailsExpander.Content=new Border {Child=rows,Background=Brushes.White,BorderBrush=CompactTheme.ChromeLine,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Padding=new Thickness(8)};
            DockPanel.SetDock(detailsExpander,compact?Dock.Right:Dock.Bottom);body.Children.Add(detailsExpander);
        }
        void ClearDetails(){detailsData=null;detailsRows.Children.Clear();detailsHeader.Text="";detailsExpander.Visibility=Visibility.Collapsed;}
        async Task LoadDetails(string path,CancellationToken token,int request) {
            try {
                var data=await Task.Run(delegate { return FileMetadataService.Load(path,token); });
                if(request!=version||token.IsCancellationRequested)return;
                detailsData=data;detailsHeader.Text=L.Text("details.heading")+" · "+data.Summary;detailsHeader.ToolTip=detailsHeader.Text;
                detailsRows.Children.Clear();
                foreach(var item in data.Rows) {
                    var row=new Grid {Margin=new Thickness(0,3,0,3)};row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(125)});row.ColumnDefinitions.Add(new ColumnDefinition());
                    row.Children.Add(new TextBlock {Text=item.Label,TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,9,0)});
                    var value=new TextBlock {Text=item.Value,TextWrapping=TextWrapping.Wrap,ToolTip=item.Value};Grid.SetColumn(value,1);row.Children.Add(value);detailsRows.Children.Add(row);
                }
                detailsExpander.Visibility=Visibility.Visible;ApplyDocumentExcerpt();
            }catch(OperationCanceledException){}catch(Exception){}
            finally {if(request==version){documentDetailsLoading=false;if(WantsDocument)UpdateFormat();}}
        }
        void ApplyDocumentExcerpt() {
            if(detailsData==null||!detailsData.IsDocument||lastData==null||currentMode!=PreviewMode.Auto||currentOffset!=0)return;
            text.Text=String.IsNullOrWhiteSpace(detailsData.Excerpt)?L.Text("details.noTextPreview"):detailsData.Excerpt;
            if(ShowingDocument||WaitingForDocument)return;
            picture.Visibility=Visibility.Collapsed;rich.Visibility=Visibility.Collapsed;text.Visibility=Visibility.Visible;text.Text=String.IsNullOrWhiteSpace(detailsData.Excerpt)?L.Text("details.noTextPreview"):detailsData.Excerpt;text.TextWrapping=TextWrapping.Wrap;
            formatted.IsEnabled=documentReady;navigationBar.Visibility=Visibility.Collapsed;status.Text=detailsData.ExcerptLabel??L.Text("details.documentExcerpt");
        }
    }
}





