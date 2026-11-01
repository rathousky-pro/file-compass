using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace FileCompass {
 public partial class MainWindow {
  internal void RefreshCleanPanelAppearance(){if(panelSplit==null)return;panelSplit.Margin=new Thickness(8,0,8,7);panelSplit.Background=Brushes.Transparent;panelSplitter.Background=Brushes.Transparent;panelSplitter.ClearValue(Control.TemplateProperty);}
 }
}
