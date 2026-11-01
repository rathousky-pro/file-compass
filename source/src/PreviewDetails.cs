using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FileCompass {
    internal sealed partial class QuickPreviewPane {
        FileDetails detailsData;
        internal Task MetadataLoadingTask {get;private set;}
        internal FileDetails Metadata {get{return detailsData;}}
        readonly TextBlock basicInfo=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,FontSize=12};
        Button moreDetails;Window detailsPopup;
        void InitializeDetails(DockPanel body,bool compact) {
            System.Windows.Automation.AutomationProperties.SetAutomationId(basicInfo,"PreviewBasicInfo");basicInfo.MaxHeight=compact?20:70;basicInfo.TextWrapping=compact?TextWrapping.NoWrap:TextWrapping.Wrap;basicInfo.TextTrimming=TextTrimming.CharacterEllipsis;
            var info=new DockPanel{Margin=new Thickness(0,6,0,6)};carouselToggle.Margin=new Thickness(0,0,10,0);carouselToggle.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(carouselToggle,Dock.Left);info.Children.Add(carouselToggle);
            moreDetails=CopyUi.Button(L.Text("preview.details"),ShowDetailsPopup);System.Windows.Automation.AutomationProperties.SetAutomationId(moreDetails,"PreviewMore");moreDetails.MinWidth=65;moreDetails.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(moreDetails,Dock.Right);info.Children.Add(moreDetails);info.Children.Add(basicInfo);
            DockPanel.SetDock(info,Dock.Bottom);body.Children.Add(info);
        }
        void ShowBasicInfo(string path){
            try{var file=new System.IO.FileInfo(path);basicInfo.Text=(compactPreview?"":System.IO.Path.GetDirectoryName(path)+"\n")+CopyUi.Bytes(file.Length)+" · "+file.LastWriteTime.ToString("g");basicInfo.ToolTip=path;}catch{basicInfo.Text=path;}
            moreDetails.IsEnabled=true;
        }
        internal void ShowDetailsPopup(){
            if(detailsPopup!=null){detailsPopup.Activate();return;}
            var popup=new Window();CopyUi.Style(popup,Window.GetWindow(this),L.Text("details.heading"),660);popup.Height=520;popup.SizeToContent=SizeToContent.Manual;popup.ResizeMode=ResizeMode.CanResize;
            StackPanel actions;var root=SettingsUx.Frame(popup,L.Text("details.heading"),out actions);var rows=new StackPanel{Margin=new Thickness(20,12,20,12)};
            rows.Children.Add(new TextBlock{Text=Convert.ToString(title.ToolTip),TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
            var data=detailsData;if(data!=null)foreach(var item in data.Rows){var row=new Grid{Margin=new Thickness(0,6,0,6)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(155)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(new TextBlock{Text=item.Label,Foreground=FluentTheme.Muted,TextWrapping=TextWrapping.Wrap});var value=new TextBlock{Text=item.Value,TextWrapping=TextWrapping.Wrap};Grid.SetColumn(value,1);row.Children.Add(value);rows.Children.Add(new Border{Child=row,BorderBrush=CompactTheme.ChromeLine,BorderThickness=new Thickness(0,0,0,1)});}
            else rows.Children.Add(new TextBlock{Text=basicInfo.Text,TextWrapping=TextWrapping.Wrap});
            root.Children.Add(new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});var close=CopyUi.Button(L.Text("action.close"),popup.Close);close.IsCancel=true;actions.Children.Add(close);var copy=CopyUi.Button(L.Text("details.copy"),delegate{try{Clipboard.SetText(data==null?basicInfo.Text:String.Join(Environment.NewLine,data.Rows.Select(x=>x.Label+": "+x.Value)));}catch(System.Runtime.InteropServices.COMException){}});actions.Children.Add(copy);detailsPopup=popup;popup.Closed+=delegate{detailsPopup=null;};popup.Show();
        }
        void ClearDetails(){if(detailsPopup!=null)detailsPopup.Close();basicInfo.Text="";if(moreDetails!=null)moreDetails.IsEnabled=false;detailsData=null;}
        async Task LoadDetails(string path,CancellationToken token,int request) {
            try {
                var data=await Task.Run(delegate { return FileMetadataService.Load(path,token); });
                if(request!=version||token.IsCancellationRequested)return;
                detailsData=data;var dimensions=data.Rows.FirstOrDefault(x=>x.Key=="dimensions"||x.Key=="resolution");basicInfo.Text+=(dimensions==null?"":" · "+dimensions.Value);basicInfo.ToolTip=Convert.ToString(title.ToolTip)+"\n"+data.Summary;
                ApplyDocumentExcerpt();
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
