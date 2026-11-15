using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace FileCompass {
    // Reusable mutually exclusive pill choices, with the less common policies under More.
    internal sealed class ConflictChoiceSelector<T> : StackPanel where T:struct {
        readonly Dictionary<T,RadioButton> radios=new Dictionary<T,RadioButton>();
        readonly Dictionary<T,ComboBoxItem> extras=new Dictionary<T,ComboBoxItem>();
        readonly ComboBox more = new ComboBox{MinWidth=122,MinHeight=30,Margin=new Thickness(3,0,0,0)};
        readonly RadioButton moreRadio;
        bool updating;
        T current;
        public event Action<T> Changed;
        public T Value {get{return current;}set{Select(value);}}
        public ConflictChoiceSelector(IEnumerable<Tuple<T,string>> prominent,IEnumerable<Tuple<T,string>> secondary,T initial){
            Orientation=Orientation.Vertical;
            var bar=new WrapPanel{Margin=new Thickness(0,2,0,4)};
            Children.Add(bar);
            string group="conflict-"+Guid.NewGuid().ToString("N");
            foreach(var pair in prominent){
                var r=Choice(pair.Item2,group);
                var value=pair.Item1;
                radios.Add(value,r);
                r.Checked+=delegate {if(!updating)Set(value);};
                bar.Children.Add(r);
            }
            var others=secondary.ToArray();
            if(others.Length>0) {
                moreRadio=Choice(L.Text("action.more"),group);
                moreRadio.Checked+=delegate {if(updating)return;var selected=more.SelectedItem as ComboBoxItem;if(selected==null&&more.Items.Count>0)more.SelectedIndex=0;selected=more.SelectedItem as ComboBoxItem;if(selected!=null)Set((T)selected.Tag);};
                bar.Children.Add(moreRadio);
                foreach(var pair in others) {var item=new ComboBoxItem{Content=pair.Item2,Tag=pair.Item1};more.Items.Add(item);extras.Add(pair.Item1,item);}
                more.SelectionChanged+=delegate {if(updating)return;var item=more.SelectedItem as ComboBoxItem;if(item!=null){moreRadio.IsChecked=true;Set((T)item.Tag);}};
                Children.Add(more);
                more.Visibility=Visibility.Collapsed;
                moreRadio.Checked+=delegate {more.Visibility=Visibility.Visible;};
                moreRadio.Unchecked+=delegate {more.Visibility=Visibility.Collapsed;};
            }
            Select(initial);
        }
        static RadioButton Choice(string text,string group){
            var r=new RadioButton{Content=text,Foreground=FluentTheme.Text,Background=CompactTheme.WhiteSurface,BorderBrush=CompactTheme.FieldLine,GroupName=group,MinHeight=30,MinWidth=93,Margin=new Thickness(0,0,6,0),Padding=new Thickness(12,5,12,5),Cursor=System.Windows.Input.Cursors.Hand};
            r.Template=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='RadioButton'>
              <Border x:Name='Frame' CornerRadius='6' BorderThickness='1' BorderBrush='{TemplateBinding BorderBrush}' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'>
                <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextElement.Foreground='{TemplateBinding Foreground}'/>
              </Border>
              <ControlTemplate.Triggers>
               <Trigger Property='IsChecked' Value='True'><Setter TargetName='Frame' Property='Background' Value='#176AD3'/><Setter TargetName='Frame' Property='BorderBrush' Value='#176AD3'/><Setter Property='Foreground' Value='White'/></Trigger>
               <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Frame' Property='BorderBrush' Value='#0871DD'/></Trigger>
               <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger>
              </ControlTemplate.Triggers></ControlTemplate>");
            return r;
        }
        void Set(T value){current=value;if(Changed!=null)Changed(value);}
        public void Select(T value){
            updating=true;
            try{
                RadioButton r;
                if(radios.TryGetValue(value,out r)){r.IsChecked=true;if(moreRadio!=null)moreRadio.IsChecked=false;}
                else if(moreRadio!=null&&extras.ContainsKey(value)){moreRadio.IsChecked=true;more.SelectedItem=extras[value];more.Visibility=Visibility.Visible;}
                else throw new ArgumentOutOfRangeException("value");
                current=value;
            }finally{updating=false;}
            if(Changed!=null)Changed(value);
        }
        public bool Has(T value){return radios.ContainsKey(value)||extras.ContainsKey(value);}
    }
}
