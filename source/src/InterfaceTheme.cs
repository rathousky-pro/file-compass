using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace FileCompass {
    internal sealed class InterfaceTheme : DependencyObject {
        internal static readonly InterfaceTheme State=new InterfaceTheme();
        public static readonly DependencyProperty BalancedProperty=DependencyProperty.Register("Balanced",typeof(bool),typeof(InterfaceTheme),new PropertyMetadata(true));
        public static readonly DependencyProperty CleanProperty=DependencyProperty.Register("Clean",typeof(bool),typeof(InterfaceTheme),new PropertyMetadata(true));
        public bool Balanced { get { return true; } }
        public bool Clean { get { return true; } }
        internal event EventHandler Changed { add {} remove {} }
        static bool installed;
        static readonly DependencyProperty FramedProperty=DependencyProperty.RegisterAttached("Framed",typeof(bool),typeof(InterfaceTheme),new PropertyMetadata(false));
        internal static bool IsFramed(Window window){return (bool)window.GetValue(FramedProperty);}
        internal static void Install(ResourceDictionary resources){
            if(installed)return;installed=true;resources["CompassMotifState"]=State;
            resources["CompassDialogOutline"]=CompactTheme.Line;
            CleanTheme.Install(resources);
            EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,new RoutedEventHandler(delegate(object sender,RoutedEventArgs args){if(Object.ReferenceEquals(sender,args.OriginalSource))Prepare((Window)sender);}));
        }
        internal static void Refresh(){
            var app=Application.Current;if(app==null)return;
            foreach(Window window in app.Windows.Cast<Window>().ToArray())if(window.IsLoaded){Prepare(window);CleanTheme.Refresh(window);CleanOptions.Refresh(window);}
        }
        internal static void Prepare(Window window){
            if(window is MainWindow||IsFramed(window))return;
            var content=window.Content as FrameworkElement;if(content==null)return;
            var grid=content as Grid;var existing=grid!=null&&grid.Children.Count==1?grid.Children[0] as Border:null;
            if(existing!=null){
                existing.Tag="window-shell";
                var panel=existing.Child as Panel;
                bool composed=panel!=null&&panel.Children.OfType<FrameworkElement>().Any(x=>Convert.ToString(x.Tag).StartsWith("dialog-",StringComparison.Ordinal));
                if(!composed){var child=existing.Child;existing.Child=null;existing.Child=new Border{Tag="dialog-content-card",Background=CompactTheme.WhiteSurface,CornerRadius=new CornerRadius(11),Padding=new Thickness(14),Child=child};}
                window.SetValue(FramedProperty,true);return;
            }
            window.Content=null;
            var card=new Border{Tag="dialog-content-card",Background=CompactTheme.WhiteSurface,CornerRadius=new CornerRadius(11),Padding=new Thickness(14),Child=content};
            content.Margin=new Thickness(0);
            window.Content=new Border{Tag="window-shell",Background=CompactTheme.Chrome,Padding=new Thickness(8),CornerRadius=new CornerRadius(16),Child=card};
            window.Background=CompactTheme.Chrome;window.SetValue(FramedProperty,true);
        }
    }
}
