using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace FileCompass {
    internal static class WindowEscape {
        static bool installed;
        static readonly DependencyProperty BusyReaderProperty=DependencyProperty.RegisterAttached("BusyReader",typeof(Func<bool>),typeof(WindowEscape));
        static readonly DependencyProperty CloseTimerProperty=DependencyProperty.RegisterAttached("CloseTimer",typeof(DispatcherTimer),typeof(WindowEscape));
        internal static void TrackActivity(Window window,Func<bool> busy){
            window.SetValue(BusyReaderProperty,busy);
            window.Closed+=delegate{var timer=window.GetValue(CloseTimerProperty) as DispatcherTimer;if(timer!=null)timer.Stop();window.ClearValue(CloseTimerProperty);window.ClearValue(BusyReaderProperty);};
        }
        static void CloseAfterActivity(Window window){
            var busy=window.GetValue(BusyReaderProperty) as Func<bool>;
            if(busy==null||window.GetValue(CloseTimerProperty)!=null)return;
            try{if(!busy())return;}catch{return;}
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};window.SetValue(CloseTimerProperty,timer);
            timer.Tick+=delegate{
                bool active;try{active=busy();}catch{timer.Stop();window.ClearValue(CloseTimerProperty);return;}
                if(active)return;timer.Stop();window.ClearValue(CloseTimerProperty);if(window.IsVisible)window.Close();
            };timer.Start();
        }
        internal static void Install(){
            if(installed)return;installed=true;
            EventManager.RegisterClassHandler(typeof(Window),Keyboard.PreviewKeyDownEvent,new KeyEventHandler(OnKey));
        }
        static void OnKey(object sender,KeyEventArgs args){
            var window=sender as Window;
            if(window==null||window is MainWindow||args.Key!=Key.Escape||Keyboard.Modifiers!=ModifierKeys.None)return;
            // Let an open selector/menu consume its own Escape first.
            for(var node=args.OriginalSource as DependencyObject;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node)){
                var combo=node as ComboBox;if(combo!=null&&combo.IsDropDownOpen)return;
                if(node is MenuItem)return;
            }
            args.Handled=true;if(args.IsRepeat)return;
            CloseAfterActivity(window);
            var cancel=CancelButton(window);
            if(cancel!=null)cancel.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            else window.Close(); // Closing retains save prompts and operation cancellation guards.
        }
        static Button CancelButton(DependencyObject parent){
            var button=parent as Button;if(button!=null&&button.IsCancel&&button.IsVisible&&button.IsEnabled)return button;
            for(int index=0;index<VisualTreeHelper.GetChildrenCount(parent);index++){var found=CancelButton(VisualTreeHelper.GetChild(parent,index));if(found!=null)return found;}
            return null;
        }
    }
}
