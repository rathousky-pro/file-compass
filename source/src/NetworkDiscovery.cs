using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace FileCompass {
    // Supplement Shell discovery with file-service endpoints; TCP probes do not authenticate.
    internal static class NetworkDiscovery {
        static readonly int[] Ports={445,21,22,990,2049};
        static int accessDenied;
        internal static int AccessDeniedCount {get{return Volatile.Read(ref accessDenied);}}
        internal static int CandidateCount {get;private set;}
        internal static IEnumerable<string> LocalAddresses(IPAddress address,IPAddress mask) {
            byte[] ip=address.GetAddressBytes(),bits=mask.GetAddressBytes();
            if(ip.Length!=4||bits.Length!=4||!PrivateAddress(ip))yield break;
            // Bound broad VPN/virtual networks to the attached /24, never sweep a /8 or the Internet.
            uint value=Number(ip),network=value&Number(bits),broadcast=network|~Number(bits);
            if(broadcast-network>255){network=value&0xffffff00u;broadcast=network+255;}
            if(broadcast-network<2)yield break;
            for(uint host=network+1;host<broadcast;host++)if(host!=value)yield return Address(host);
        }
        static bool PrivateAddress(byte[] ip){return ip[0]==10||(ip[0]==172&&ip[1]>=16&&ip[1]<=31)||(ip[0]==192&&ip[1]==168)||(ip[0]==169&&ip[1]==254)||(ip[0]==100&&ip[1]>=64&&ip[1]<=127);}
        static uint Number(byte[] bytes){return ((uint)bytes[0]<<24)|((uint)bytes[1]<<16)|((uint)bytes[2]<<8)|bytes[3];}
        static string Address(uint value){return new IPAddress(new[]{(byte)(value>>24),(byte)(value>>16),(byte)(value>>8),(byte)value}).ToString();}
        internal static Entry Service(string host,int port,string name=null) {
            IPAddress address;
            if(!IPAddress.TryParse(host,out address)||address.AddressFamily!=AddressFamily.InterNetwork)return null;
            if(port==445)return new Entry{Name=name??host,FullPath="\\\\"+host,IsDirectory=true,Icon=DesktopIcons.ToolbarDrawing("network",ToolbarTheme.Classic)};
            NetworkProtocol protocol;string label;
            if(port==21){protocol=NetworkProtocol.FTP;label="FTP";}else if(port==990){protocol=NetworkProtocol.FTP;label="FTPS";}else if(port==22){protocol=NetworkProtocol.SFTP;label="SSH / SFTP";}else if(port==2049){protocol=NetworkProtocol.NFS;label="NFS";}else return null;
            return new Entry{Name=(name??host)+" ("+label+")",FullPath="discovered:"+host+":"+port,IsDirectory=true,Icon=DesktopIcons.ToolbarDrawing("network",ToolbarTheme.Classic),NetworkServiceProfile=new NetworkProfile{Name=name??host,Host=host,Port=port,Protocol=protocol,Path=protocol==NetworkProtocol.NFS?"":"/",FtpTls=port==990,FtpImplicitTls=port==990}};
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct ServerInfo { public uint Platform;[MarshalAs(UnmanagedType.LPWStr)]public string Name;public uint Major,Minor,Type;[MarshalAs(UnmanagedType.LPWStr)]public string Comment; }
        [DllImport("netapi32.dll",CharSet=CharSet.Unicode)]static extern int NetServerEnum(string server,int level,out IntPtr data,int preferred,out int read,out int total,uint type,string domain,ref int resume);
        [DllImport("netapi32.dll")]static extern int NetApiBufferFree(IntPtr data);
        static void WindowsServers(Action<Entry> publish) {
            int resume=0,read,total;IntPtr buffer=IntPtr.Zero;
            try{int error=NetServerEnum(null,101,out buffer,65536,out read,out total,0x3,null,ref resume);if(error!=0&&error!=234)return;
                for(int i=0;i<read;i++){var server=(ServerInfo)Marshal.PtrToStructure(IntPtr.Add(buffer,i*Marshal.SizeOf(typeof(ServerInfo))),typeof(ServerInfo));if(!String.IsNullOrWhiteSpace(server.Name)&&server.Name.IndexOfAny(new[]{'\\','/','\r','\n'})<0)publish(new Entry{Name=server.Name,FullPath="\\\\"+server.Name,IsDirectory=true,Icon=DesktopIcons.ToolbarDrawing("network",ToolbarTheme.Classic)});}
            }finally{if(buffer!=IntPtr.Zero)NetApiBufferFree(buffer);}
        }
        static void KnownServers(Action<Entry> publish) {
            foreach(var drive in DriveInfo.GetDrives()) {
                if(drive.DriveType!=DriveType.Network)continue;
                string remote=MappedPath(drive.Name);if(remote==null)continue;
                // Preserve the complete share/WebDAV path rather than assuming an SMB server root.
                publish(new Entry{Name=drive.Name+" — "+remote,FullPath=remote.TrimEnd('\\'),IsDirectory=true,Icon=DesktopIcons.ToolbarDrawing("network",ToolbarTheme.Classic)});
            }
            if(!File.Exists(NetworkSettingsStore.DefaultPath))return;
            foreach(var profile in new NetworkSettingsStore(NetworkSettingsStore.DefaultPath).Settings.Connections) {
                if(!String.IsNullOrEmpty(profile.CloudAccountId)||String.IsNullOrWhiteSpace(profile.Host))continue;
                if(profile.Protocol==NetworkProtocol.SMB)publish(new Entry{Name=profile.Name,FullPath="\\\\"+profile.Host,IsDirectory=true,Icon=DesktopIcons.ToolbarDrawing("network",ToolbarTheme.Classic)});
                else publish(new Entry{Name=profile.Name+" ("+profile.Protocol+")",FullPath="saved-network:"+profile.Id,IsDirectory=true,Icon=DesktopIcons.ToolbarDrawing("network",ToolbarTheme.Classic),NetworkServiceProfile=profile.Clone(),NetworkServiceSaved=true});
            }
        }
        [DllImport("mpr.dll",CharSet=CharSet.Unicode)]static extern int WNetGetConnection(string drive,System.Text.StringBuilder remote,ref int length);
        static string MappedPath(string drive){int length=2048;var value=new System.Text.StringBuilder(length);return WNetGetConnection(drive.TrimEnd('\\'),value,ref length)==0?value.ToString():null;}
        internal static async Task<bool> PortOpen(string host,int port,int milliseconds) {
            using(var client=new TcpClient()){try{var connect=client.ConnectAsync(host,port);if(await Task.WhenAny(connect,Task.Delay(milliseconds)).ConfigureAwait(false)!=connect){var observe=connect.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);return false;}await connect.ConfigureAwait(false);return true;}catch(SocketException ex){if(ex.SocketErrorCode==SocketError.AccessDenied)Interlocked.Increment(ref accessDenied);return false;}catch(IOException){return false;}catch(ObjectDisposedException){return false;}}
        }
        internal static async Task Discover(Action<Entry> publish) {
            Interlocked.Exchange(ref accessDenied,0);CandidateCount=0;
            // Provider errors must not suppress unrelated discovery sources.
            var legacy=Task.Run(()=>{try{WindowsServers(publish);}catch(System.ComponentModel.Win32Exception){}catch(ExternalException){}});
            try{KnownServers(publish);}catch(IOException){}catch(UnauthorizedAccessException){}
            var hosts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces()) {
                if(adapter.OperationalStatus!=OperationalStatus.Up||adapter.NetworkInterfaceType==NetworkInterfaceType.Loopback)continue;
                foreach(var address in adapter.GetIPProperties().UnicastAddresses)if(address.Address.AddressFamily==AddressFamily.InterNetwork)
                    foreach(string host in LocalAddresses(address.Address,address.IPv4Mask))if(hosts.Count<1024)hosts.Add(host);
            }
            CandidateCount=hosts.Count;
            var deadline=DateTime.UtcNow.AddSeconds(22);var queue=new Queue<string>(hosts);var gate=new object();
            var workers=Enumerable.Range(0,48).Select(async worker=>{
                while(DateTime.UtcNow<deadline){string host;lock(gate){if(queue.Count==0)break;host=queue.Dequeue();}
                    var open=await Task.WhenAll(Ports.Select(async port=>await PortOpen(host,port,450).ConfigureAwait(false)?port:0)).ConfigureAwait(false);
                    if(!open.Any(p=>p!=0))continue;
                    string name=host;try{var lookup=Dns.GetHostEntryAsync(host);if(await Task.WhenAny(lookup,Task.Delay(250)).ConfigureAwait(false)==lookup){var entry=await lookup.ConfigureAwait(false);if(!String.IsNullOrWhiteSpace(entry.HostName))name=entry.HostName;}else{var observe=lookup.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}}catch(SocketException){}
                    foreach(int port in open.Where(p=>p!=0))publish(Service(host,port,name));
                }
            }).ToArray();
            await Task.WhenAll(workers).ConfigureAwait(false);
            if(await Task.WhenAny(legacy,Task.Delay(1000)).ConfigureAwait(false)==legacy)await legacy.ConfigureAwait(false);
            else{var observe=legacy.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
        }
    }
}
