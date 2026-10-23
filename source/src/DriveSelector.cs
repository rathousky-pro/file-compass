using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace CommanderCZ
{
    public sealed class DriveChoice : INotifyPropertyChanged
    {
        public string Root { get; private set; }
        public string Letter { get { return Root.Substring(0, 1).ToLowerInvariant(); } }
        public string Description { get; private set; }
        public string Tooltip { get; private set; }
        public ImageSource Icon { get; private set; }
        public DriveChoice(string root) { Root = root; }
        public void Update(string name, string address, bool network, bool ready, ImageSource icon)
        {
            Description = !String.IsNullOrEmpty(address) ? address : !String.IsNullOrWhiteSpace(name) ? name : Root;
            if (!ready) Description = L.Text("drive.disconnectedLabelPrefix") + Description;
            Tooltip = FilePanel.DriveTooltip(Root, name, address, network, ready);
            Icon = icon ?? DesktopIcons.WindowsDriveIcon(Root, network, ready);
            if (PropertyChanged != null) foreach (string property in new[] { "Description", "Tooltip", "Icon" }) PropertyChanged(this, new PropertyChangedEventArgs(property));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public override string ToString() { return Root; }
    }

    internal static class DriveSelector
    {
        internal static void Configure(ComboBox combo)
        {
            combo.Width = 72;
            combo.ItemTemplate = (DataTemplate)XamlReader.Parse(@"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                <StackPanel Orientation='Horizontal'><Image Source='{Binding Icon}' Width='16' Height='16' Margin='0,0,4,0'/>
                <TextBlock Text='{Binding Letter}' VerticalAlignment='Center'/></StackPanel></DataTemplate>");
            combo.ItemContainerStyle = (Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBoxItem'>
                <Setter Property='MinHeight' Value='25'/><Setter Property='Padding' Value='5,3'/>
                <Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Background' Value='{StaticResource CompassSurface}'/>
                <Setter Property='Foreground' Value='{StaticResource CompassText}'/><Setter Property='ToolTip' Value='{Binding Tooltip}'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'>
                <Border Background='{TemplateBinding Background}' CornerRadius='2' Padding='{TemplateBinding Padding}'>
                <Grid TextElement.Foreground='{TemplateBinding Foreground}'>
                <Grid.ColumnDefinitions><ColumnDefinition Width='23'/><ColumnDefinition Width='26'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                <Image Source='{Binding Icon}' Width='16' Height='16' HorizontalAlignment='Left'/>
                <TextBlock Grid.Column='1' Text='{Binding Letter}' VerticalAlignment='Center'/>
                <TextBlock Grid.Column='2' Text='{Binding Description}' VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/>
                </Grid></Border>
                </ControlTemplate></Setter.Value></Setter>
                <Style.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='{StaticResource CompassHover}'/></Trigger>
                <Trigger Property='IsHighlighted' Value='True'><Setter Property='Background' Value='{StaticResource CompassAccent}'/><Setter Property='Foreground' Value='{StaticResource CompassSurface}'/></Trigger>
                <Trigger Property='IsSelected' Value='True'><Setter Property='Background' Value='{StaticResource CompassAccent}'/><Setter Property='Foreground' Value='{StaticResource CompassSurface}'/></Trigger>
                </Style.Triggers></Style>");
            combo.Template = (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'>
                <Grid><ToggleButton Style='{x:Null}' Focusable='False' HorizontalAlignment='Stretch' VerticalAlignment='Stretch' MinWidth='0' MinHeight='0' Padding='0' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}' ClickMode='Press'>
                <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border x:Name='Frame' Background='{StaticResource CompassSurface}' BorderBrush='{StaticResource CompassLine}' BorderThickness='1' CornerRadius='4'/>
                <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Frame' Property='Background' Value='{StaticResource CompassHover}'/><Setter TargetName='Frame' Property='BorderBrush' Value='{StaticResource CompassAccent}'/></Trigger></ControlTemplate.Triggers>
                </ControlTemplate></ToggleButton.Template></ToggleButton>
                <Grid IsHitTestVisible='False'><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='21'/></Grid.ColumnDefinitions>
                <ContentPresenter Content='{TemplateBinding SelectedItem}' ContentTemplate='{TemplateBinding ItemTemplate}' Margin='5,0,0,0' VerticalAlignment='Center'/>
                <Border Grid.Column='1' BorderBrush='{StaticResource CompassLine}' BorderThickness='1,0,0,0' Margin='0,3'><Path Data='M 0 0 L 4 4 L 8 0' Stroke='{StaticResource CompassText}' StrokeThickness='1.2' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></Grid>
                <Popup x:Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False'>
                <Border Background='{StaticResource CompassSurface}' BorderBrush='{StaticResource CompassLine}' BorderThickness='1' CornerRadius='4' Padding='2' Width='380' Margin='0,2,0,0'>
                <ScrollViewer MaxHeight='340' CanContentScroll='True' HorizontalScrollBarVisibility='Disabled' VerticalScrollBarVisibility='Auto'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer>
                </Border></Popup></Grid></ControlTemplate>");
        }
    }
}

