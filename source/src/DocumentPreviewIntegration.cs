using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal sealed partial class QuickPreviewPane {
        DocumentPreview documentPreview;
        bool documentReady, documentLoading, documentDetailsLoading;
        readonly TextBlock documentLoadingLabel = new TextBlock {Text=L.Text("preview.loadingStatus"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed};
        internal bool WaitingForDocument {get{if(HtmlCodeSelected)return lastData==null;return WantsDocument&&!ShowingDocument&&((documentLoading&&(DocumentPreview.IsHtml(current)||formatted.IsChecked==true))||documentDetailsLoading||lastData==null);}}
        internal Task DocumentLoadingTask {get;private set;}
        readonly StackPanel documentNotice = new StackPanel {Visibility=Visibility.Collapsed,Margin=new Thickness(0,4,0,6)};
        readonly TextBlock documentNoticeText = new TextBlock {TextWrapping=TextWrapping.Wrap};
        readonly Button webviewInstall = CopyUi.Button(L.Text("preview.installWebview"),delegate {try {Process.Start(new ProcessStartInfo(DocumentPreview.InstallUrl){UseShellExecute=true});}catch(Exception){}});
        void InitializeDocumentPreview(DockPanel body) {
            InitializeHtmlControls(body);
            content.Children.Add(documentLoadingLabel);
            documentNotice.Children.Add(documentNoticeText);documentNotice.Children.Add(webviewInstall);
            webviewInstall.HorizontalAlignment=HorizontalAlignment.Left;
            DockPanel.SetDock(documentNotice,Dock.Top);body.Children.Add(documentNotice);
        }
        bool WantsDocument {get{return current!=null&&remoteTemplate==null&&currentMode==PreviewMode.Auto&&currentOffset==0&&DocumentPreview.Supports(current);}}
        bool ShowingDocument {get{return WantsDocument&&documentReady&&(DocumentPreview.IsHtml(current)?!HtmlCodeSelected:formatted.IsChecked==true);}}
        void ClearDocumentPreview() {
            documentReady=false;documentLoading=false;documentDetailsLoading=false;documentLoadingLabel.Visibility=Visibility.Collapsed;documentNotice.Visibility=Visibility.Collapsed;
            if(documentPreview!=null){content.Children.Remove(documentPreview);documentPreview.Dispose();documentPreview=null;}
        }
        async Task LoadDocumentPreview(string path,CancellationToken token,int request) {
            if(!WantsDocument)return;
            bool available=DocumentPreview.Available();
            if(!available) {
                documentLoading=false;if(DocumentPreview.IsHtml(path))htmlCodeMode=true;
                documentNoticeText.Text=L.Text("preview.webviewMissing");
                webviewInstall.Visibility=Visibility.Visible;documentNotice.Visibility=Visibility.Visible;return;
            }
            var preview=new DocumentPreview {Visibility=Visibility.Hidden};
            documentPreview=preview;content.Children.Add(preview);
            bool ready=await preview.ShowAsync(path,token);
            if(request!=version||token.IsCancellationRequested||documentPreview!=preview)return;
            documentLoading=false;documentReady=ready;
            if(!ready) {
                if(DocumentPreview.IsHtml(path))htmlCodeMode=true;
                content.Children.Remove(preview);preview.Dispose();documentPreview=null;
                documentNoticeText.Text=preview.Error??L.Text("preview.documentFailed");
                webviewInstall.Visibility=Visibility.Collapsed;documentNotice.Visibility=Visibility.Visible;
            }
            formatted.IsEnabled=ready||richAvailable;UpdateFormat();if(!ShowingDocument)ApplyDocumentExcerpt();
        }
    }
}
