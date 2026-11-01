using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace FileCompass
{
    public sealed class DriveChoice : INotifyPropertyChanged
    {
        public string Root { get; private set; }
        internal bool Ready { get; private set; }
        public string LocationKey { get; private set; }
        public bool IsLocation { get { return LocationKey!=null; } }
        public bool GroupStart { get; set; }
        public string Letter { get { return IsLocation?"":ExtLocations.IsPath(Root)?"EXT":Root.Substring(0,1).ToLowerInvariant(); } }
        public string SelectionLabel { get { return IsLocation?L.Text(LocationKey):Letter; } }
        public string Description { get; private set; }
        public string Tooltip { get; private set; }
        public ImageSource Icon { get; private set; }
        public DriveChoice(string root) { Root = root; }
        public DriveChoice(string root,string key) {Root=root;LocationKey=key;Description=L.Text(key);Tooltip=Description+"\n"+root;Icon=WindowsShell.Icon(root);}
        public void Update(string name, string address, bool network, bool ready, ImageSource icon)
        {
            Ready=ready;
            Description = !String.IsNullOrEmpty(address) ? address : !String.IsNullOrWhiteSpace(name) ? name : Root;
            if (!ready) Description = L.Text("drive.disconnectedLabelPrefix") + Description;
            Tooltip = ExtLocations.IsPath(Root)?Description+"\n"+L.Text("ext.readOnly"):FilePanel.DriveTooltip(Root, name, address, network, ready);
            Icon = icon ?? DesktopIcons.WindowsDriveIcon(Root, network, ready);
            if (PropertyChanged != null) foreach (string property in new[] { "Description", "Tooltip", "Icon" }) PropertyChanged(this, new PropertyChangedEventArgs(property));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public override string ToString() { return Root; }
    }

    internal static class DriveSelector
    {
        internal static void Resize(ComboBox combo,double iconSize,double height,double padding){
            combo.Resources["DriveSelectorIconSize"]=iconSize;combo.Resources["DriveSelectorIconColumn"]=new GridLength(iconSize+7);combo.Resources["DriveSelectorRowHeight"]=height+6;
            combo.Width=Math.Max(130,iconSize+System.Windows.Documents.TextElement.GetFontSize(combo)*6+padding*2+24);combo.Height=height;combo.MinHeight=0;combo.MinWidth=0;combo.Padding=new Thickness(padding,0,padding,0);combo.VerticalContentAlignment=VerticalAlignment.Center;combo.VerticalAlignment=VerticalAlignment.Center;
        }
        internal static void Configure(ComboBox combo)
        {
            combo.Tag="drive-selector";Resize(combo,16,24,5);combo.SetBinding(FrameworkElement.ToolTipProperty,new System.Windows.Data.Binding("SelectedItem.Tooltip"){Source=combo});
            combo.ItemTemplate = (DataTemplate)XamlReader.Parse(@"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                <StackPanel Orientation='Horizontal'><Image Source='{Binding Icon}' Width='{DynamicResource DriveSelectorIconSize}' Height='{DynamicResource DriveSelectorIconSize}' Margin='0,0,4,0'/>
                <TextBlock Text='{Binding SelectionLabel}' TextTrimming='CharacterEllipsis' VerticalAlignment='Center'/></StackPanel></DataTemplate>");
            combo.ItemContainerStyle = (Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBoxItem'>
                <Setter Property='MinHeight' Value='{DynamicResource DriveSelectorRowHeight}'/><Setter Property='Padding' Value='5,3'/>
                <Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Background' Value='{StaticResource CompassSurface}'/>
                <Setter Property='Foreground' Value='{StaticResource CompassText}'/><Setter Property='ToolTip' Value='{Binding Tooltip}'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'>
                <Border Background='{TemplateBinding Background}' CornerRadius='2' Padding='{TemplateBinding Padding}'>
                <Grid TextElement.Foreground='{TemplateBinding Foreground}'>
                <Grid.ColumnDefinitions><ColumnDefinition Width='{DynamicResource DriveSelectorIconColumn}'/><ColumnDefinition Width='0'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                <Image Source='{Binding Icon}' Width='{DynamicResource DriveSelectorIconSize}' Height='{DynamicResource DriveSelectorIconSize}' HorizontalAlignment='Left'/>
                <TextBlock Grid.Column='1' Text='{Binding Letter}' VerticalAlignment='Center'/>
                <TextBlock Grid.Column='2' Text='{Binding Description}' VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/>
                </Grid></Border>
                </ControlTemplate></Setter.Value></Setter>
                <Style.Triggers><DataTrigger Binding='{Binding GroupStart}' Value='True'><Setter Property='Margin' Value='0,6,0,0'/></DataTrigger><Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='{StaticResource CompassHover}'/></Trigger>
                <Trigger Property='IsHighlighted' Value='True'><Setter Property='Background' Value='{StaticResource CompassAccent}'/><Setter Property='Foreground' Value='{StaticResource CompassSurface}'/></Trigger>
                <Trigger Property='IsSelected' Value='True'><Setter Property='Background' Value='{StaticResource CompassAccent}'/><Setter Property='Foreground' Value='{StaticResource CompassSurface}'/></Trigger>
                </Style.Triggers></Style>");
            combo.Template = (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'>
                <Grid><ToggleButton Style='{x:Null}' Focusable='False' HorizontalAlignment='Stretch' VerticalAlignment='Stretch' MinWidth='0' MinHeight='0' Padding='0' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}' ClickMode='Press'>
                <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border x:Name='Frame' Margin='1' Background='{StaticResource CompassSurface}' BorderBrush='{StaticResource CompassLine}' BorderThickness='1' CornerRadius='4'/>
                <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Frame' Property='Background' Value='{StaticResource CompassHover}'/><Setter TargetName='Frame' Property='BorderBrush' Value='{StaticResource CompassAccent}'/></Trigger></ControlTemplate.Triggers>
                </ControlTemplate></ToggleButton.Template></ToggleButton>
                <Grid IsHitTestVisible='False'><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='21'/></Grid.ColumnDefinitions>
                <TextBlock Text='FOLDERS_LABEL' Margin='8,0,0,0' VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/>
                <Border Grid.Column='1' BorderBrush='{StaticResource CompassLine}' BorderThickness='1,0,0,0' Margin='0,3'><Path Data='M 0 0 L 4 4 L 8 0' Stroke='{StaticResource CompassText}' StrokeThickness='1.2' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></Grid>
                <Popup x:Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False'>
                <Border Background='{StaticResource CompassSurface}' BorderBrush='{StaticResource CompassLine}' BorderThickness='1' CornerRadius='4' Padding='2' Width='380' Margin='0,2,0,0'>
                <ScrollViewer MaxHeight='340' CanContentScroll='True' HorizontalScrollBarVisibility='Disabled' VerticalScrollBarVisibility='Auto'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer>
                </Border></Popup></Grid></ControlTemplate>".Replace("FOLDERS_LABEL",System.Security.SecurityElement.Escape(L.Text("navigation.locations"))));
        }
    }
}
