using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass {
    public partial class MainWindow {
        sealed class UsbDeviceRow {
            internal ExtSource Source;
            internal string[] Roots;
            public override string ToString() {
                return Source.Name + "   ·   " + (Roots.Length==0 ? L.Text("usb.noLetters") : String.Join(", ",Roots));
            }
        }

        void ShowUsbDevices() {
            var window=new Window { MinWidth=480, SizeToContent=SizeToContent.Manual, Height=390 };
            CopyUi.Style(window,this,L.Text("usb.devices"),610);
            window.SizeToContent=SizeToContent.Manual;
            var layout=new DockPanel { Margin=new Thickness(16) };
            window.Content=layout;
            var footer=new StackPanel();
            DockPanel.SetDock(footer,Dock.Bottom);
            layout.Children.Add(footer);
            var status=new TextBlock { TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,10,0,8) };
            footer.Children.Add(status);
            var buttons=new WrapPanel { HorizontalAlignment=HorizontalAlignment.Right };
            footer.Children.Add(buttons);
            var devices=new ListBox { HorizontalContentAlignment=HorizontalAlignment.Stretch };
            layout.Children.Add(devices);
            bool working=false, closed=false;
            window.Closed+=delegate { closed=true; };
            Button eject=null, refresh=null;
            Action update=delegate {
                eject.IsEnabled=!working && devices.SelectedItem!=null;
                refresh.IsEnabled=!working;
                devices.IsEnabled=!working;
            };
            Func<Task> reload=async delegate {
                var previous=(devices.SelectedItem as UsbDeviceRow)?.Source.Token;
                var rows=await Task.Run(()=>UsbStorageDevice.Enumerate().Select(disk=>new { Disk=disk, Source=UsbDriveActions.Ejectable(disk) })
                    .Where(disk=>disk.Source!=null)
                    .Select(disk=>new UsbDeviceRow { Source=disk.Source, Roots=UsbDriveActions.VolumeRoots(disk.Disk.Number) })
                    .OrderBy(row=>row.Source.Name,StringComparer.CurrentCultureIgnoreCase).ToArray());
                if(closed)return;
                devices.ItemsSource=rows;
                devices.SelectedItem=rows.FirstOrDefault(row=>row.Source.Token==previous);
                if(devices.SelectedItem==null && rows.Length>0)devices.SelectedIndex=0;
            };
            Action<bool> run=async ejectSelected=>{
                if(working)return;
                var row=devices.SelectedItem as UsbDeviceRow;
                if(ejectSelected && row==null)return;
                working=true;
                status.Text="";
                update();
                try {
                    if(ejectSelected)await RunUsbDriveAction(row.Source.Device,row.Source,false,window,message=>status.Text=message);
                    await reload();
                    if(!closed && status.Text.Length==0 && devices.Items.Count==0)status.Text=L.Text("usb.noDevices");
                } catch(Exception ex) { if(!closed)status.Text=ex.Message; }
                finally { working=false; if(!closed)update(); }
            };
            refresh=CopyUi.Button(L.Text("action.refresh"),()=>run(false));
            eject=CopyUi.Button(L.Text("usb.eject"),()=>run(true));
            buttons.Children.Add(refresh);
            buttons.Children.Add(eject);
            buttons.Children.Add(CopyUi.Button(L.Text("action.close"),window.Close));
            FluentTheme.Primary(eject);
            devices.SelectionChanged+=delegate { update(); };
            window.Loaded+=delegate { run(false); };
            window.Show();
        }
    }
}
