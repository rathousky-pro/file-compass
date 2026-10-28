using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace FileCompass {
    internal static class UsbDriveActions {
        [DllImport("shell32.dll")] static extern uint SHFormatDrive(IntPtr window, uint drive, uint format, uint options);
        [DllImport("cfgmgr32.dll", CharSet=CharSet.Unicode)] static extern uint CM_Locate_DevNodeW(out uint instance, string id, uint flags);
        [DllImport("cfgmgr32.dll")] static extern uint CM_Get_Parent(out uint parent, uint instance, uint flags);
        [DllImport("cfgmgr32.dll", CharSet=CharSet.Unicode)] static extern uint CM_Get_Device_IDW(uint instance, StringBuilder id, int length, uint flags);
        [DllImport("cfgmgr32.dll", CharSet=CharSet.Unicode)] static extern uint CM_Get_DevNode_Registry_PropertyW(uint instance, uint property, out uint type, [Out] byte[] buffer, ref uint length, uint flags);
        [DllImport("cfgmgr32.dll", CharSet=CharSet.Unicode)] static extern uint CM_Request_Device_EjectW(uint instance, out int veto, StringBuilder name, int length, uint flags);

        internal static bool Eligible(ExtSource source, uint disk, uint system, uint application) {
            return source!=null && source.Physical && source.Removable && disk!=UInt32.MaxValue && disk!=system && disk!=application;
        }
        internal static ExtSource Resolve(string root) {
            try {
                uint disk=ExtDiskStream.DiskNumber(root), system=ExtDiskStream.DiskNumber(Path.GetPathRoot(Environment.SystemDirectory)), app=ExtDiskStream.DiskNumber(Path.GetPathRoot(Process.GetCurrentProcess().MainModule.FileName));
                var source=UsbStorageDevice.Find(disk).Source;
                return Eligible(source,disk,system,app)?source:null;
            } catch { return null; }
        }
        internal static ExtSource ResolvePhysical(ExtSource expected){if(expected==null||!expected.Physical)return null;uint number;if(!expected.Device.StartsWith(@"\\.\PHYSICALDRIVE",StringComparison.OrdinalIgnoreCase)||!UInt32.TryParse(expected.Device.Substring(17),out number))return null;try{var now=UsbStorageDevice.Find(number).Source;uint system=ExtDiskStream.DiskNumber(Path.GetPathRoot(Environment.SystemDirectory)),app=ExtDiskStream.DiskNumber(Path.GetPathRoot(Process.GetCurrentProcess().MainModule.FileName));if(!Eligible(now,number,system,app)||now.Token!=expected.Token||now.Length!=expected.Length)return null;return now;}catch{return null;}}
        internal static void Verify(string root, ExtSource expected) {
            var now=ExtLocations.IsPath(root)?ResolvePhysical(expected):Resolve(root);
            if(expected==null || now==null || !String.Equals(now.Device,expected.Device,StringComparison.OrdinalIgnoreCase) || now.Token!=expected.Token || now.Length!=expected.Length) throw new IOException(L.Text("ext.changed"));
        }
        internal static int DriveIndex(string root) {
            if(!System.Text.RegularExpressions.Regex.IsMatch(root??"",@"^[A-Za-z]:\\$")) throw new IOException(L.Text("ext.invalidPath"));
            return Char.ToUpperInvariant(root[0])-'A';
        }
        internal static bool OnRoots(string path, IEnumerable<string> roots) {
            if(String.IsNullOrEmpty(path)) return false;
            return roots.Any(root=>String.Equals(path,root.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase) || path.StartsWith(root,StringComparison.OrdinalIgnoreCase));
        }
        internal static string[] VolumeRoots(string root, ExtSource source) {
            Verify(root,source); uint disk=ExtLocations.IsPath(root)?UInt32.Parse(source.Device.Substring(17)):ExtDiskStream.DiskNumber(root); var roots=new List<string>{root};
            foreach(var drive in DriveInfo.GetDrives()) {
                try { if((drive.DriveType==DriveType.Removable || drive.DriveType==DriveType.Fixed) && ExtDiskStream.DiskNumber(drive.Name)==disk && !roots.Contains(drive.Name,StringComparer.OrdinalIgnoreCase)) roots.Add(drive.Name); }
                catch(IOException) {} catch(System.ComponentModel.Win32Exception) {} catch(UnauthorizedAccessException) {}
            }
            return roots.ToArray();
        }
        internal static void Format(Window owner, string root, ExtSource source) {
            int index=DriveIndex(root); Verify(root,source);
            uint result=SHFormatDrive(new WindowInteropHelper(owner).Handle,(uint)index,0xffff,0);
            if(result==0xffffffff || result==0xfffffffd) throw new IOException(L.Text("usb.formatFailed"));
        }
        static uint? Parent(uint node) { uint parent; return CM_Get_Parent(out parent,node,0)==0 ? (uint?)parent : null; }
        static string DeviceId(uint node) { var id=new StringBuilder(512); if(CM_Get_Device_IDW(node,id,id.Capacity,0)!=0) throw new IOException(L.Text("ext.changed")); return id.ToString(); }
        static bool Removable(uint node) { uint type,length=4;var data=new byte[4];return CM_Get_DevNode_Registry_PropertyW(node,0x10,out type,data,ref length,0)==0 && type==4 && length==4 && (BitConverter.ToUInt32(data,0)&4)!=0; }
        // Stop at the first removable device, never a USB hub or a host controller.
        internal static uint FindEjectNode(uint node, Func<uint,uint?> parent, Func<uint,string> id, Func<uint,bool> removable) {
            var visited=new HashSet<uint>();
            for(int depth=0;depth<12 && visited.Add(node);depth++) {
                string name=id(node)??"";
                if(!(name.StartsWith("USBSTOR\\",StringComparison.OrdinalIgnoreCase) || name.StartsWith("SCSI\\",StringComparison.OrdinalIgnoreCase) || name.StartsWith("USB\\VID_",StringComparison.OrdinalIgnoreCase))) break;
                if(removable(node)) return node;
                if(name.StartsWith("USB\\VID_",StringComparison.OrdinalIgnoreCase) && name.IndexOf("&MI_",StringComparison.OrdinalIgnoreCase)<0)break;
                uint? next=parent(node);if(!next.HasValue) break;node=next.Value;
            }
            throw new IOException(L.Text("usb.noEjectNode"));
        }
        internal static bool AncestorContains(uint node, uint ancestor, Func<uint,uint?> parent) {
            var seen=new HashSet<uint>();for(int depth=0;depth<32 && seen.Add(node);depth++){if(node==ancestor)return true;uint? next=parent(node);if(!next.HasValue)return false;node=next.Value;}throw new IOException(L.Text("ext.changed"));
        }
        static void ProtectEjectNode(uint ejectNode) {
            foreach(string root in new[]{Path.GetPathRoot(Environment.SystemDirectory),Path.GetPathRoot(Process.GetCurrentProcess().MainModule.FileName)}.Distinct(StringComparer.OrdinalIgnoreCase)) {
                uint disk=ExtDiskStream.DiskNumber(root);uint node=UsbStorageDevice.Find(disk).Node;
                if(AncestorContains(node,ejectNode,Parent))throw new IOException(L.Text("usb.protected"));
            }
        }
        internal static void Eject(string root, ExtSource source) {
            Verify(root,source);string pnp=source.Token.Substring(0,source.Token.LastIndexOf('|'));uint node;
            if(CM_Locate_DevNodeW(out node,pnp,0)!=0)throw new IOException(L.Text("ext.changed"));
            uint target=FindEjectNode(node,Parent,DeviceId,Removable);ProtectEjectNode(target);Verify(root,source);
            int veto;var name=new StringBuilder(260);uint status=CM_Request_Device_EjectW(target,out veto,name,name.Capacity,0);
            if(status!=0)throw new IOException(L.Format("usb.ejectFailed",status,veto,name.ToString()));
        }
    }

    public partial class FilePanel {
        internal async Task ReleaseUsbRoots(string[] roots, string fallback,ExtSource device=null) {
            bool ext=IsExt&&device!=null&&String.Equals(ExtLocations.Get(CurrentPath).Source.Device,device.Device,StringComparison.OrdinalIgnoreCase);
            if(IsRemote || (!ext&&!UsbDriveActions.OnRoots(CurrentPath,roots) && !(IsArchive && UsbDriveActions.OnRoots(Archive.FilePath,roots))))return;
            CancelNavigation();DisposePanelTools();detailRequest++;if(fileDetail!=null)fileDetail.ShowMessage("");
            Navigate(fallback,false);if(NavigationTask!=null)await NavigationTask;
            if(UsbDriveActions.OnRoots(CurrentPath,roots) || (IsArchive && UsbDriveActions.OnRoots(Archive.FilePath,roots)))throw new IOException(L.Text("usb.busy"));
        }
    }

    public partial class MainWindow {
        bool usbActionRunning;
        internal bool UsbActionsBlocked { get { return usbActionRunning || fileToolsRunning>0 || archiveOperations>0 || deletions>0 || busy || PanelsLoading || copyRunning || networkCopies>0 || networkJobs.Any(x=>x.Planning>0) || copyJobs.Count>0; } }
        internal ContextMenu CreateUsbDriveMenu(string root, ExtSource source, Action<bool> invoke) {
            var menu=new ContextMenu();
            var heading=new MenuItem{Header=(ExtLocations.IsPath(root)?"EXT":root)+"  "+source.Name,IsEnabled=false};menu.Items.Add(heading);menu.Items.Add(new Separator());
            var eject=new MenuItem{Header=L.Text("usb.eject"),Icon=new Image{Source=DesktopIcons.FlashDriveIcon,Width=16,Height=16},IsEnabled=!UsbActionsBlocked};eject.Click+=delegate{invoke(false);};menu.Items.Add(eject);
            var format=new MenuItem{Header=L.Text("usb.format"),IsEnabled=!UsbActionsBlocked};format.Click+=delegate{invoke(true);};if(!ExtLocations.IsPath(root))menu.Items.Add(format);
            menu.Items.Add(new Separator());var linux=new MenuItem{Header=L.Text("ext.title")};linux.Click+=delegate{new ExtBrowserWindow(this).Show();};menu.Items.Add(linux);
            return menu;
        }
        internal async void OpenUsbDriveMenu(string root, FrameworkElement target) {
            try {
                var source=await Task.Run(()=>ExtLocations.IsPath(root)?UsbDriveActions.ResolvePhysical(ExtLocations.Get(root).Source):UsbDriveActions.Resolve(root));if(source==null || !target.IsLoaded)return;
                var menu=CreateUsbDriveMenu(root,source,format=>RunUsbDriveAction(root,source,format));menu.PlacementTarget=target;menu.IsOpen=true;
            } catch(Exception ex) { MessageBox.Show(this,ex.Message,ApplicationBrand.Name); }
        }
        internal async void RunUsbDriveAction(string root, ExtSource source, bool format) {
            if(UsbActionsBlocked){MessageBox.Show(this,L.Text("app.fileOperationBusy"),ApplicationBrand.Name);return;}
            usbActionRunning=true;fileToolsRunning++;bool enabled=IsEnabled;IsEnabled=false;
            try {
                using(await ExtIoGate.PauseAsync(source))using(await ExtDiscovery.SuspendAsync()){
                // Eject affects all partitions; Windows Format affects only the chosen letter.
                var roots=await Task.Run(()=>format?new[]{root}:UsbDriveActions.VolumeRoots(root,source));
                string fallback=Path.GetPathRoot(Environment.SystemDirectory);
                if(quickPreview!=null)CloseQuickPreview();
                await Task.WhenAll(left.ReleaseUsbRoots(roots,fallback,source),right.ReleaseUsbRoots(roots,fallback,source));
                if(UsbDriveActions.OnRoots(Environment.CurrentDirectory,roots))Environment.CurrentDirectory=fallback;
                if(format)UsbDriveActions.Format(this,root,source);
                else {await Task.Run(()=>UsbDriveActions.Eject(root,source));MessageBox.Show(this,L.Format("usb.removed",root),L.Text("usb.eject"),MessageBoxButton.OK,MessageBoxImage.Information);}
                }
            } catch(Exception ex) { MessageBox.Show(this,ex.Message,L.Text(format?"usb.format":"usb.eject"),MessageBoxButton.OK,MessageBoxImage.Warning); }
            finally {IsEnabled=enabled;fileToolsRunning--;usbActionRunning=false;left.RefreshDriveBar();right.RefreshDriveBar();}
        }
    }
}
