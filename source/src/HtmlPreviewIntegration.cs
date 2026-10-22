using System;
using System.Windows;
using System.Windows.Controls;
namespace CommanderCZ {
 internal sealed partial class QuickPreviewPane {
  readonly WrapPanel htmlControls=new WrapPanel {Margin=new Thickness(0,0,0,6),Visibility=Visibility.Collapsed};
  readonly RadioButton htmlPage=new RadioButton {Content=L.Text("preview.htmlPage"),IsChecked=true,Margin=new Thickness(0,0,14,0)};
  readonly RadioButton htmlCode=new RadioButton {Content=L.Text("preview.htmlCode")};
  bool updatingHtml,htmlCodeMode;
  bool HtmlCodeSelected {get{return current!=null&&DocumentPreview.IsHtml(current)&&htmlCodeMode;}}
  void InitializeHtmlControls(DockPanel body) {
   string group="HtmlPreview"+Guid.NewGuid().ToString("N");htmlPage.GroupName=htmlCode.GroupName=group;
   htmlControls.Children.Add(htmlPage);htmlControls.Children.Add(htmlCode);
   DockPanel.SetDock(htmlControls,Dock.Top);body.Children.Add(htmlControls);
   htmlPage.Checked+=delegate{if(!updatingHtml){htmlCodeMode=false;UpdateFormat();}};
   htmlCode.Checked+=delegate{if(!updatingHtml){htmlCodeMode=true;UpdateFormat();}};
  }
  void UpdateHtmlControls() {
   bool html=current!=null&&DocumentPreview.IsHtml(current)&&currentMode==PreviewMode.Auto;
   htmlControls.Visibility=html?Visibility.Visible:Visibility.Collapsed;
   formatted.Visibility=html?Visibility.Collapsed:Visibility.Visible;
   if(!html)return;
   updatingHtml=true;htmlPage.IsChecked=!htmlCodeMode;htmlCode.IsChecked=htmlCodeMode;updatingHtml=false;
   htmlPage.IsEnabled=documentReady||documentLoading;
  }
 }
}
