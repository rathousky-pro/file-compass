using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
namespace FileCompass {
    public partial class MainWindow {
        bool startupFileFocusPending=true;
        void InitializeStartupKeyboardFocus() {
            Loaded+=delegate{TryFocusStartupFiles();};
            Activated+=delegate{Dispatcher.BeginInvoke(DispatcherPriority.Input,new System.Action(TryFocusStartupFiles));};
            left.LocalNavigationCompleted+=TryFocusStartupFiles;right.LocalNavigationCompleted+=TryFocusStartupFiles;
            left.LoadingChanged+=delegate{TryFocusStartupFiles();};right.LoadingChanged+=delegate{TryFocusStartupFiles();};
        }
        void TryFocusStartupFiles() {
            if(!startupFileFocusPending||windowClosed||!IsLoaded||!IsActive)return;
            var panel=active;if(panel==null)return;
            var focused=Keyboard.FocusedElement;
            // A user who has started editing or chosen another control owns the focus.
            if(focused!=null&&focused!=this&&!panel.List.IsKeyboardFocusWithin){startupFileFocusPending=false;return;}
            // Loaded may run while the initial asynchronous directory scan is still pending.
            if(panel.LocalLoading||panel.IsLoading||!panel.List.IsEnabled||panel.CurrentPath==null)return;
            panel.FocusFiles();
            if(panel.List.IsKeyboardFocusWithin)startupFileFocusPending=false;
        }
    }
}
