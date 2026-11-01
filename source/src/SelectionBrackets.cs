using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace FileCompass {
 public partial class FilePanel {
  static readonly DependencyProperty BracketOwnerProperty=DependencyProperty.RegisterAttached("BracketOwner",typeof(FilePanel),typeof(FilePanel));
  internal static readonly DependencyProperty BracketCornersProperty=DependencyProperty.RegisterAttached("BracketCorners",typeof(CornerRadius),typeof(FilePanel),new PropertyMetadata(new CornerRadius(0)));
  static readonly DependencyProperty BracketMarkedProperty=DependencyProperty.RegisterAttached("BracketMarked",typeof(bool),typeof(FilePanel),new PropertyMetadata(false,delegate(DependencyObject row,DependencyPropertyChangedEventArgs args){var owner=row.GetValue(BracketOwnerProperty) as FilePanel;if(owner!=null)owner.QueueSelectionBrackets();}));
  bool bracketsQueued;
  void InitializeSelectionBrackets(){
   ((INotifyCollectionChanged)List.Items).CollectionChanged+=delegate{QueueSelectionBrackets();};
   List.ItemContainerGenerator.StatusChanged+=delegate{QueueSelectionBrackets();};
   List.SelectionChanged+=delegate{QueueSelectionBrackets();};
   List.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler(delegate{QueueSelectionBrackets();}));
  }
  void QueueSelectionBrackets(){
   if(bracketsQueued||!List.IsLoaded||Dispatcher.HasShutdownStarted)return;bracketsQueued=true;
   Dispatcher.BeginInvoke(DispatcherPriority.Render,new System.Action(delegate{bracketsQueued=false;if(List.IsLoaded)RefreshSelectionBrackets();}));
  }
  void RefreshSelectionBrackets(){
   // Inspect only realized containers, but use the sorted/filtered view for both neighbours.
   // A selection continuing above/below the viewport must not acquire a false end cap.
   foreach(var row in RealizedRows(List)){
    var entry=row.Content as Entry;bool marked=entry!=null&&entry.IsMarked&&!entry.IsParent;int index=List.ItemContainerGenerator.IndexFromContainer(row);
    bool previous=marked&&index>0&&((Entry)List.Items[index-1]).IsMarked;
    bool next=marked&&index>=0&&index<List.Items.Count-1&&((Entry)List.Items[index+1]).IsMarked;
    double radius=AppearanceTheme.IsClean?6:marked?4:0;
    row.SetValue(BracketCornersProperty,marked?new CornerRadius(previous?0:radius,previous?0:radius,next?0:radius,next?0:radius):new CornerRadius(radius));
   }
  }
 }
}
