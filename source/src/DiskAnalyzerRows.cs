using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Markup;
namespace FileCompass {
    internal sealed partial class DiskAnalyzerWindow {
        Style folderRowStyle;
        void ConfigureFolderRows() {
            System.Windows.Controls.Grid.SetIsSharedSizeScope(Folders,true);
            ScrollViewer.SetHorizontalScrollBarVisibility(Folders,ScrollBarVisibility.Disabled);
            folderRowStyle=new Style(typeof(TreeViewItem));
            folderRowStyle.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));
            folderRowStyle.Setters.Add(new Setter(Control.ForegroundProperty,FluentTheme.Text));
            folderRowStyle.Setters.Add(new Setter(Control.BorderBrushProperty,Brushes.Transparent));
            folderRowStyle.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));
            folderRowStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));
            // Child presenters span the entire row. Only the name/expander is indented;
            // numeric columns consequently share the same right edge at every depth.
            folderRowStyle.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TreeViewItem'>
 <Grid><Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
  <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='3'>
   <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
    <ToggleButton x:Name='Expander' Width='18' Height='24' Margin='{TemplateBinding Padding}' Focusable='False' Foreground='{TemplateBinding Foreground}' IsChecked='{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
     <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Grid Background='Transparent'><Path x:Name='Arrow' Data='M 0,0 L 4,4 L 0,8' Stroke='{TemplateBinding Foreground}' StrokeThickness='1.2' HorizontalAlignment='Center' VerticalAlignment='Center' RenderTransformOrigin='0.5,0.5'/></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Arrow' Property='RenderTransform'><Setter.Value><RotateTransform Angle='90'/></Setter.Value></Setter></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
    </ToggleButton>
    <ContentPresenter Grid.Column='1' ContentSource='Header' HorizontalAlignment='Stretch' VerticalAlignment='Center' Margin='0,2,24,2'/>
   </Grid>
  </Border>
  <ItemsPresenter x:Name='Children' Grid.Row='1'/>
 </Grid>
 <ControlTemplate.Triggers><Trigger Property='IsExpanded' Value='False'><Setter TargetName='Children' Property='Visibility' Value='Collapsed'/></Trigger><Trigger Property='HasItems' Value='False'><Setter TargetName='Expander' Property='Visibility' Value='Hidden'/></Trigger></ControlTemplate.Triggers>
</ControlTemplate>")));
            var selectedRow=new Trigger{Property=TreeViewItem.IsSelectedProperty,Value=true};selectedRow.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.ThemeBrush("#DDEDFC")));folderRowStyle.Triggers.Add(selectedRow);
            var focusedRow=new Trigger{Property=UIElement.IsKeyboardFocusedProperty,Value=true};focusedRow.Setters.Add(new Setter(Control.BorderBrushProperty,FluentTheme.Accent));folderRowStyle.Triggers.Add(focusedRow);
        }
        FrameworkElement FolderHeader(DiskFolder folder) {
            var row=new System.Windows.Controls.Grid{Tag="analyzer.folderRow"};
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star),MinWidth=24});
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto,SharedSizeGroup="AnalyzerFolderSize"});
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto,SharedSizeGroup="AnalyzerFolderCount"});
            var name=new TextBlock{Text=String.IsNullOrEmpty(folder.Name)?folder.Path:folder.Name,TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,VerticalAlignment=VerticalAlignment.Center,ToolTip=folder.Path};
            var size=new TextBlock{Text=folder.Size,TextWrapping=TextWrapping.NoWrap,TextAlignment=TextAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,10,0)};
            var files=new TextBlock{Text=folder.Files.ToString("N0"),TextWrapping=TextWrapping.NoWrap,TextAlignment=TextAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Foreground=FluentTheme.Muted};
            size.ToolTip=L.Text("analyzer.bytes")+": "+folder.Size;files.ToolTip=L.Text("analyzer.files")+": "+files.Text;
            row.Children.Add(name);System.Windows.Controls.Grid.SetColumn(size,1);row.Children.Add(size);System.Windows.Controls.Grid.SetColumn(files,2);row.Children.Add(files);
            return row;
        }
    }
}
