using System;using System.Collections;using System.Linq;using System.Windows;using System.Windows.Controls;
namespace FileCompass {
 internal sealed class PagedSettingsGrid : DockPanel {
  readonly DataGrid grid;readonly TextBlock label=new TextBlock{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,12,0)};object[] rows=new object[0],slice;int page;bool updating;
  internal PagedSettingsGrid(DataGrid source){grid=source;Height=200;grid.Height=150;grid.RowHeight=28;grid.MinHeight=0;grid.Margin=new Thickness(0,4,0,4);ScrollViewer.SetVerticalScrollBarVisibility(grid,ScrollBarVisibility.Disabled);ScrollViewer.SetHorizontalScrollBarVisibility(grid,ScrollBarVisibility.Disabled);var controls=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};controls.Children.Add(CopyUi.Button("‹",delegate{if(page>0){page--;Publish();}}));controls.Children.Add(label);controls.Children.Add(CopyUi.Button("›",delegate{if((page+1)*3<rows.Length){page++;Publish();}}));DockPanel.SetDock(controls,Dock.Bottom);Children.Add(controls);Children.Add(grid);grid.ItemContainerGenerator.ItemsChanged+=delegate{Read();};Read();}
  void Read(){if(updating||Object.ReferenceEquals(grid.ItemsSource,slice))return;var source=grid.ItemsSource as IEnumerable;if(source==null)return;rows=source.Cast<object>().ToArray();page=0;Publish();}
  void Publish(){updating=true;try{slice=rows.Skip(page*3).Take(3).ToArray();grid.ItemsSource=slice;label.Text=rows.Length==0?"0 / 0":(page*3+1)+"–"+Math.Min(rows.Length,page*3+3)+" / "+rows.Length;}finally{updating=false;}}
 }
}
