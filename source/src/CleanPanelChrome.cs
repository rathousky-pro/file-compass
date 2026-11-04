using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FileCompass {
    internal static class CleanDriveIcons {
        internal static ImageSource Drawing(bool network,bool selected){
            Brush ink=selected?Brushes.White:FluentTheme.Accent;var pen=new Pen(ink,1.2){LineJoin=PenLineJoin.Round};var image=new DrawingGroup();
            image.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,20,18))));
            string path=network?"M7,1 L13,1 L13,6 L7,6 Z M10,6 L10,10 M3,10 L17,10 M3,10 L3,13 M17,10 L17,13 M0,13 L6,13 L6,18 L0,18 Z M14,13 L20,13 L20,18 L14,18 Z":"M4,2 L16,2 L19,8 L19,16 L1,16 L1,8 Z M1,8 L19,8 M4,12 L5,12 M8,12 L9,12";
            image.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse(path)));return new DrawingImage(image);
        }
    }
    public partial class FilePanel {
        Grid cleanPaneGrid,cleanDriveGrid;
        bool? extensionColumn,attributesColumn;
        internal static readonly DependencyProperty SeparateExtensionProperty=DependencyProperty.Register("SeparateExtension",typeof(bool),typeof(FilePanel),new PropertyMetadata(false));
        bool BaseColumnVisible(int index){return index==0?true:index==2?sizeColumn??true:index==3?modifiedColumn??true:(index==1?extensionColumn:attributesColumn)??!AppearanceTheme.IsClean;}
        void ApplyBaseColumns(){
            SetValue(SeparateExtensionProperty,BaseColumnVisible(1));
            if(view.Columns.Count<5)return;
            resizingColumns=true;try{foreach(int index in new[]{1,2,3,4}){var column=BaseColumn(index);if(column==null)continue;if(column.Width>=25)RememberWidth(index,column.Width);column.Width=BaseColumnVisible(index)?RememberedWidth(index):0;}}finally{resizingColumns=false;}
        }
        void SetBaseColumnVisible(int index,bool visible){if(index==1)extensionColumn=visible;else if(index==2)sizeColumn=visible;else if(index==3)modifiedColumn=visible;else attributesColumn=visible;if(!visible&&sortProperty==BaseColumnKeys[index])sortProperty="Name";ApplyBaseColumns();preserveColumnWidths=false;ResizeColumns();Sort();}
        void RefreshCleanNavigation(){
            if(panelPathRow==null||cleanBreadcrumbFrame==null)return;
            bool editing=address.Visibility==Visibility.Visible;
            panelPathRow.Visibility=Visibility.Visible;panelPathRow.Margin=new Thickness(0);panelPathRow.Background=Brushes.Transparent;tabRow.Background=Brushes.Transparent;tabRow.Visibility=Visibility.Visible;
            cleanBreadcrumbFrame.Background=IsActivePanel?CompactTheme.ThemeBrush("#D3E2EF"):CompactTheme.PanelSurface;cleanBreadcrumbFrame.ToolTip=CurrentPath;cleanBreadcrumbFrame.Visibility=Visibility.Visible;cleanBreadcrumbDock.Visibility=editing?Visibility.Collapsed:Visibility.Visible;
            if(cleanRootButton!=null)cleanRootButton.IsEnabled=cleanParentButton.IsEnabled=CurrentPath!=null;
        }
        Button networkDriveButton;bool? cleanChrome;double driveIconSize=10,cleanExtensionWidth=80,cleanAttributesWidth=70;
        internal void RefreshCleanChrome(){
            bool clean=AppearanceTheme.IsClean;if(cleanChrome==clean)return;cleanChrome=clean;
            if(cleanPaneGrid!=null){cleanPaneGrid.RowDefinitions[4].Height=new GridLength(clean?30:24);}
            BorderThickness=new Thickness(2);CornerRadius=new CornerRadius(7);Padding=new Thickness(5);Background=clean?CompactTheme.WhiteSurface:Brushes.Transparent;
            if(cleanFileFrame!=null){cleanFileFrame.Background=clean?CompactTheme.WhiteSurface:CompactTheme.PanelSurface;cleanFileFrame.BorderThickness=clean?new Thickness(0):new Thickness(1);cleanFileFrame.CornerRadius=new CornerRadius(clean?0:6);}
            if(cleanDriveFrame!=null){cleanDriveFrame.Background=clean?CompactTheme.WhiteSurface:CompactTheme.PanelSurface;cleanDriveFrame.BorderThickness=clean?new Thickness(0):new Thickness(1);cleanDriveFrame.CornerRadius=new CornerRadius(clean?0:6);cleanDriveFrame.Padding=clean?new Thickness(0):new Thickness(2);cleanDriveFrame.Margin=clean?new Thickness(0):new Thickness(0,0,0,6);}
            if(cleanBreadcrumbFrame!=null)cleanBreadcrumbFrame.Visibility=clean?Visibility.Visible:Visibility.Collapsed;
            List.Margin=clean?new Thickness(8,0,8,0):new Thickness(1,0,1,0);List.BorderThickness=clean?new Thickness(0):new Thickness(0,1,0,1);status.Margin=clean?new Thickness(12,5,8,0):new Thickness(9,3,5,0);panelRole.Visibility=Visibility.Collapsed;
            ApplyBaseColumns();RefreshCleanNavigation();
            tabRow.Background=clean?CompactTheme.WhiteSurface:CompactTheme.PanelSurface;RefreshCleanDrives();ResizeColumns();RenderTabs();Highlight(IsActivePanel);if(view.Columns.Count>0)Sort();UpdateStatus();
        }
        void RefreshCleanDrives(){
            bool clean=AppearanceTheme.IsClean;double height=Math.Max(System.Windows.Documents.TextElement.GetFontSize(this)+6,driveIconSize+8), pad=Math.Max(3,driveIconSize*.4);DriveSelector.Resize(driveSelector,driveIconSize,height,pad);if(cleanPaneGrid!=null){cleanPaneGrid.RowDefinitions[0].Height=new GridLength(height+(clean?12:14));cleanPaneGrid.RowDefinitions[1].Height=new GridLength(0);cleanDriveGrid.RowDefinitions[0].Height=new GridLength(height+8);cleanDriveGrid.RowDefinitions[1].Height=new GridLength(0);}string current="";try{if(CurrentPath!=null)current=Path.GetPathRoot(CurrentPath);}catch(ArgumentException){}
            foreach(var pair in driveButtons){var b=pair.Value;bool network;driveNetworks.TryGetValue(pair.Key,out network);bool selected=clean&&String.Equals(current,pair.Key,StringComparison.OrdinalIgnoreCase);
                b.Tag="drive:"+pair.Key;b.Background=selected?FluentTheme.Accent:CompactTheme.WhiteSurface;b.Foreground=selected?Brushes.White:FluentTheme.Text;b.BorderBrush=selected?FluentTheme.Accent:CompactTheme.FieldLine;b.BorderThickness=new Thickness(1);b.Height=height;b.MinHeight=0;b.MinWidth=0;b.Padding=new Thickness(pad,0,pad,0);b.Margin=new Thickness(1,1,1,1);
                if(clean){var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new Image{Source=DesktopIcons.WindowsDriveIcon(pair.Key,network,!driveChoices.ContainsKey(pair.Key)||driveChoices[pair.Key].Ready),Width=driveIconSize,Height=driveIconSize,Margin=new Thickness(0,0,Math.Max(3,driveIconSize*.3),0)});row.Children.Add(new TextBlock{Text=pair.Key.Substring(0,2).ToUpperInvariant(),VerticalAlignment=VerticalAlignment.Center,Foreground=b.Foreground});b.Content=row;}
                else {DriveChoice choice; var label=DriveLabel(pair.Key,network,!driveChoices.TryGetValue(pair.Key,out choice)||choice.Ready);foreach(var child in label.Children){var image=child as Image;if(image!=null)image.Width=image.Height=driveIconSize;}b.Content=label;}
            }
            foreach(var button in extDriveButtons.Values){button.Margin=new Thickness(1);button.Height=height;button.Padding=new Thickness(pad,0,pad,0);var label=button.Content as StackPanel;if(label!=null)foreach(var child in label.Children){var icon=child as Image;if(icon!=null)icon.Width=icon.Height=driveIconSize;}}
            if(networkDriveButton!=null){networkDriveButton.Margin=new Thickness(1);networkDriveButton.Tag="drive:network";networkDriveButton.Height=height;networkDriveButton.MinHeight=0;networkDriveButton.MinWidth=0;networkDriveButton.Padding=new Thickness(pad,0,pad,0);networkDriveButton.BorderBrush=CompactTheme.FieldLine;networkDriveButton.BorderThickness=new Thickness(1);if(clean){var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new Image{Source=CleanDriveIcons.Drawing(true,false),Width=driveIconSize,Height=driveIconSize,Margin=new Thickness(0,0,Math.Max(3,driveIconSize*.3),0)});row.Children.Add(new TextBlock{Text=L.Text("network.shortTitle"),VerticalAlignment=VerticalAlignment.Center});networkDriveButton.Content=row;}}
        }
    }
}
