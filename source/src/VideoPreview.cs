using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
namespace FileCompass {
 internal sealed partial class QuickPreviewPane {
    const long VideoRemoteLimit=512L*1024*1024;
    readonly ObservableCollection<VideoFrame> videoFrames=new ObservableCollection<VideoFrame>();
    readonly ListBox videoStrip=new ListBox{BorderThickness=new Thickness(0),Background=Brushes.Transparent,Padding=new Thickness(0,0,0,10),SelectionMode=SelectionMode.Single};
    readonly Border videoFrame=new Border{Visibility=Visibility.Collapsed,BorderThickness=new Thickness(1),BorderBrush=CompactTheme.Line,Background=CompactTheme.PanelSurface,CornerRadius=new CornerRadius(6),Padding=new Thickness(6),Margin=new Thickness(0,6,0,0)};
    readonly DockPanel videoControls=new DockPanel();readonly TextBlock videoPosition=new TextBlock{Foreground=FluentTheme.Muted,VerticalAlignment=VerticalAlignment.Center,FontSize=11};
    readonly Button videoLoad=CopyUi.Button(L.Text("video.load"),delegate{});
    string videoPath,videoRemoteKey;DataTemplate videoSelectionTemplate,normalSelectionTemplate;
    RemoteConnection videoConsentConnection;
    bool videoActive;
    void InitializeVideoPreview(DockPanel body,bool compact){
        if(compact){videoStrip.Padding=new Thickness(0);videoFrame.Padding=new Thickness(4);normalSelectionTemplate=selectionStrip.ItemTemplate;var source=new FrameworkElementFactory(typeof(StackPanel));source.SetBinding(FrameworkElement.ToolTipProperty,new Binding("PathLabel"));source.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);source.SetValue(FrameworkElement.WidthProperty,100.0);var sourceIcon=new FrameworkElementFactory(typeof(Image));sourceIcon.SetValue(FrameworkElement.WidthProperty,16.0);sourceIcon.SetValue(FrameworkElement.HeightProperty,16.0);sourceIcon.SetBinding(Image.SourceProperty,new Binding("Image"));source.AppendChild(sourceIcon);var sourceName=new FrameworkElementFactory(typeof(TextBlock));sourceName.SetValue(FrameworkElement.WidthProperty,80.0);sourceName.SetValue(FrameworkElement.MarginProperty,new Thickness(4,0,0,0));sourceName.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);sourceName.SetBinding(TextBlock.TextProperty,new Binding("Caption"));source.AppendChild(sourceName);videoSelectionTemplate=new DataTemplate{VisualTree=source};}DockPanel.SetDock(videoFrame,Dock.Bottom);body.Children.Add(videoFrame);var panel=new StackPanel();videoFrame.Child=panel;panel.Children.Add(videoControls);panel.Children.Add(videoLoad);panel.Children.Add(videoStrip);
        var play=CopyUi.Button("▶ "+L.Text("video.play"),PlayPreviewVideo);play.MinWidth=0;play.Height=26;play.Padding=new Thickness(7,0,7,0);play.Margin=new Thickness(4,0,0,3);DockPanel.SetDock(play,Dock.Right);videoControls.Children.Add(play);videoControls.Children.Add(videoPosition);
        videoLoad.Visibility=Visibility.Collapsed;videoStrip.ItemsSource=videoFrames;ScrollViewer.SetHorizontalScrollBarVisibility(videoStrip,ScrollBarVisibility.Auto);ScrollViewer.SetVerticalScrollBarVisibility(videoStrip,ScrollBarVisibility.Disabled);
        var host=new FrameworkElementFactory(typeof(StackPanel));host.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);videoStrip.ItemsPanel=new ItemsPanelTemplate(host);
        var row=new FrameworkElementFactory(typeof(StackPanel));row.SetValue(FrameworkElement.WidthProperty,compact?62.0:84.0);var image=new FrameworkElementFactory(typeof(Image));image.SetBinding(Image.SourceProperty,new Binding("Image"));image.SetValue(FrameworkElement.HeightProperty,compact?26.0:48.0);image.SetValue(Image.StretchProperty,Stretch.Uniform);row.AppendChild(image);var caption=new FrameworkElementFactory(typeof(TextBlock));caption.SetBinding(TextBlock.TextProperty,new Binding("Caption"));caption.SetValue(TextBlock.FontSizeProperty,10.0);caption.SetValue(TextBlock.TextAlignmentProperty,TextAlignment.Center);row.AppendChild(caption);videoStrip.ItemTemplate=new DataTemplate{VisualTree=row};
        var itemStyle=new Style(typeof(ListBoxItem));itemStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(4)));itemStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,3,4,0)));itemStyle.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.WhiteSurface));itemStyle.Setters.Add(new Setter(Control.BorderBrushProperty,CompactTheme.Line));itemStyle.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ListBoxItem'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='4' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border></ControlTemplate>")));var selected=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(Control.BorderBrushProperty,FluentTheme.Accent));selected.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.ActivePath));itemStyle.Triggers.Add(selected);videoStrip.ItemContainerStyle=itemStyle;
        videoStrip.SelectionChanged+=delegate{ShowVideoFrame();};
        videoStrip.PreviewKeyDown+=VideoKeys;
        PreviewKeyDown+=delegate(object sender,KeyEventArgs args){var origin=args.OriginalSource as DependencyObject;bool selecting=Object.ReferenceEquals(origin,selectionStrip)||(origin!=null&&selectionStrip.IsAncestorOf(origin));if(videoActive&&!selecting&&!selectionStrip.IsKeyboardFocusWithin&&args.OriginalSource!=find&&args.OriginalSource!=offsetInput&&!(args.OriginalSource is TextBox)&&!(args.OriginalSource is ComboBox))VideoKeys(sender,args);};
    }
    void VideoKeys(object sender,KeyEventArgs args){if(!videoActive||Keyboard.Modifiers!=ModifierKeys.None)return;if(args.Key==Key.Left||args.Key==Key.Right){SelectVideoFrame(videoStrip.SelectedIndex+(args.Key==Key.Right?1:-1));args.Handled=true;}}
    internal void SelectVideoFrame(int index){if(videoFrames.Count==0)return;videoStrip.SelectedIndex=Math.Max(0,Math.Min(videoFrames.Count-1,index));videoStrip.ScrollIntoView(videoStrip.SelectedItem);}
    void ShowVideoFrame(){if(compactPreview)status.Visibility=Visibility.Collapsed;var frame=videoStrip.SelectedItem as VideoFrame;if(frame==null)return;if(lastData==null)lastData=new PreviewData();lastData.Image=frame.Image;lastData.Status=L.Text("video.ready")+" · "+frame.Caption;picture.Source=frame.Image;UpdateFormat();videoPosition.Text=L.Text("video.frames")+" · "+(frame.Index+1)+" / 6 · "+frame.Caption;status.Text=lastData.Status;}
    void ClearVideoPreview(){status.Visibility=Visibility.Visible;videoActive=false;UpdateVideoVisibility();videoPath=null;videoFrames.Clear();videoFrame.Visibility=Visibility.Collapsed;videoLoad.Visibility=Visibility.Collapsed;videoControls.Visibility=videoStrip.Visibility=Visibility.Visible;videoPosition.Text="";consentedVideo=null;if(videoConsentConnection!=null)videoConsentConnection.Dispose();videoConsentConnection=null;}
    void ResetVideoOrigin(){videoRemoteKey=null;}
    void UpdateVideoVisibility(){if(compactPreview){bool slim=videoActive&&currentMode==PreviewMode.Auto;selectionStrip.ItemTemplate=slim?videoSelectionTemplate:normalSelectionTemplate;selectionCount.Visibility=slim?Visibility.Collapsed:Visibility.Visible;selectionStrip.Padding=new Thickness(0,0,0,slim?0:10);selectionFrame.Padding=new Thickness(slim?4:6);selectionFrame.Margin=slim?new Thickness(0,0,0,4):new Thickness(0,5,0,7);}bool visual=currentMode==PreviewMode.Auto&&(videoActive||current!=null&&(FileViewers.Kind(current)=="image"||WantsDocument));if(compactPreview)status.Visibility=visual?Visibility.Collapsed:Visibility.Visible;if(previewModeRow!=null)previewModeRow.Visibility=visual?Visibility.Collapsed:Visibility.Visible;if(previewTools!=null)previewTools.Visibility=compactPreview||visual?Visibility.Collapsed:Visibility.Visible;if(videoActive)videoFrame.Visibility=currentMode==PreviewMode.Auto?Visibility.Visible:Visibility.Collapsed;}
    async Task LoadVideoPreview(string path,CancellationToken token,int request){
        videoActive=true;videoPath=path;videoFrame.Visibility=Visibility.Visible;videoPosition.Text=L.Text("video.generating");
        try{
            await Task.Delay(180,token);
            var updates=new Progress<VideoFrame>(delegate(VideoFrame frame){if(request!=version||token.IsCancellationRequested)return;if(!videoFrames.Any(x=>x.Index==frame.Index))videoFrames.Add(frame);if(videoStrip.SelectedIndex<0)SelectVideoFrame(0);status.Text=L.Text("video.generating")+" · "+videoFrames.Count+" / 6";});
            var frames=await Task.Run(()=>VideoFrames.Load(path,token,((IProgress<VideoFrame>)updates).Report));if(request!=version||token.IsCancellationRequested)return;
            foreach(var frame in frames)if(!videoFrames.Any(x=>x.Index==frame.Index))videoFrames.Add(frame);if(videoStrip.SelectedIndex<0)SelectVideoFrame(0);ShowVideoFrame();formatted.IsEnabled=false;navigationBar.Visibility=Visibility.Collapsed;
        }catch(OperationCanceledException){}catch(Exception ex){if(request==version){status.Visibility=Visibility.Visible;status.Text=L.Text("preview.loadFailedPrefix")+ex.Message;videoPosition.Text=L.Text("video.failed");}}
    }
    static bool NetworkVideoPath(string path){try{return path.StartsWith(@"\\",StringComparison.Ordinal)||new DriveInfo(Path.GetPathRoot(path)).DriveType==DriveType.Network;}catch{return false;}}
    void VideoConsent(string path,string key,string message,Action load){
        if(videoRemoteKey==key)return;ShowMessage(message);videoRemoteKey=key;title.Text=Path.GetFileName(path);title.ToolTip=path;videoFrame.Visibility=Visibility.Visible;videoControls.Visibility=videoStrip.Visibility=Visibility.Collapsed;videoLoad.Visibility=Visibility.Visible;videoLoad.ToolTip=message;videoLoad.Click-=LoadConsentedVideo;consentedVideo=load;videoLoad.Click+=LoadConsentedVideo;
    }
    Action consentedVideo;
    void LoadConsentedVideo(object sender,RoutedEventArgs args){var action=consentedVideo;if(action!=null){consentedVideo=null;action();}}
    void ShowRemoteVideo(string path,RemoteConnection remote,string revision){string key=remote.Profile.Id+"|"+path+"|"+revision;if(videoRemoteKey==key)return;var connection=remote.TransferConnection();VideoConsent(path,key,L.Text("video.remoteConsent"),delegate{ShowDownloadedPreview(path,connection,VideoRemoteLimit,key);});videoConsentConnection=connection;}
    async void PlayPreviewVideo(){
        string path=videoPath;if(String.IsNullOrEmpty(path))return;
        if(previewTemp==null){try{FileViewers.Open(Window.GetWindow(this),path);}catch(Exception ex){status.Text=ex.Message;}return;}
        ArchiveTemp playback=null;var sourceTemp=previewTemp;
        try{using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){string parent=Path.Combine(AppPaths.DataDirectory,"video-playback");Directory.CreateDirectory(parent);playback=new ArchiveTemp(parent);string target=Path.Combine(playback.Path,Path.GetFileName(path));await Task.Run(delegate{using(var output=File.Create(target))input.CopyTo(output);});var window=new MediaViewerWindow(Window.GetWindow(this),target);var retained=playback;window.Closed+=delegate{ReleasePreviewTemp(retained);};window.Show();playback=null;}}
        catch(Exception ex){status.Text=ex.Message;}
        finally{ReleasePreviewTemp(playback);if(!Object.ReferenceEquals(previewTemp,sourceTemp))ReleasePreviewTemp(sourceTemp);}
    }
 }
}
