using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
namespace FileCompass {
    internal static class CleanFieldTemplates {
        static readonly Dictionary<Type,ControlTemplate> templates=new Dictionary<Type,ControlTemplate>();
        internal static ControlTemplate For(Type type){ControlTemplate result;if(templates.TryGetValue(type,out result))return result;
            string body=type==typeof(ComboBox)?@"<Grid>
              <Border x:Name='FieldFrame' Margin='1' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='4'/>
              <ToggleButton x:Name='DropDownToggle' HorizontalAlignment='Stretch' VerticalAlignment='Stretch' ClickMode='Press' Focusable='False' IsChecked='{Binding IsDropDownOpen,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}' Background='Transparent' BorderThickness='0' Margin='2'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='Transparent'/></ControlTemplate></ToggleButton.Template></ToggleButton>
              <ContentPresenter x:Name='Selection' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}' Margin='9,3,28,3' VerticalAlignment='Center' IsHitTestVisible='False'/>
              <TextBox x:Name='PART_EditableTextBox' Visibility='Collapsed' Margin='7,3,27,3' Background='Transparent' BorderThickness='0' Padding='0' VerticalContentAlignment='Center' IsReadOnly='{TemplateBinding IsReadOnly}' Text='{Binding Text,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay,UpdateSourceTrigger=PropertyChanged}'><TextBox.Template><ControlTemplate TargetType='TextBox'><ScrollViewer x:Name='PART_ContentHost'/></ControlTemplate></TextBox.Template></TextBox>
              <Path Data='M0,0 L4,4 L8,0' Stroke='{TemplateBinding Foreground}' StrokeThickness='1' Width='8' Height='5' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,11,0' IsHitTestVisible='False'/>
              <Popup x:Name='PART_Popup' Placement='Bottom' PlacementTarget='{Binding RelativeSource={RelativeSource TemplatedParent}}' AllowsTransparency='True' Focusable='False' IsOpen='{TemplateBinding IsDropDownOpen}' PopupAnimation='Fade'><Border Background='{StaticResource CompassSurface}' BorderBrush='{StaticResource CompassLine}' BorderThickness='1' CornerRadius='4' MinWidth='{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='{TemplateBinding MaxDropDownHeight}' Padding='2'><ScrollViewer CanContentScroll='True' VerticalScrollBarVisibility='Auto'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer></Border></Popup>
            </Grid>":@"<Border x:Name='FieldFrame' Margin='1' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='4' Padding='{TemplateBinding Padding}'><ScrollViewer x:Name='PART_ContentHost' SnapsToDevicePixels='True'/></Border>";
            string editable=type==typeof(ComboBox)?@"<Trigger Property='IsEditable' Value='True'><Setter TargetName='Selection' Property='Visibility' Value='Collapsed'/><Setter TargetName='PART_EditableTextBox' Property='Visibility' Value='Visible'/></Trigger>":"";
            string xaml=@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='"+type.Name+@"'>"+body+@"<ControlTemplate.Triggers>"+editable+@"<Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='FieldFrame' Property='BorderBrush' Value='{StaticResource CompassAccent}'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='FieldFrame' Property='BorderBrush' Value='{StaticResource CompassAccent}'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='FieldFrame' Property='Opacity' Value='0.55'/></Trigger></ControlTemplate.Triggers></ControlTemplate>";
            result=(ControlTemplate)XamlReader.Parse(xaml);templates[type]=result;return result;
        }
    }
}