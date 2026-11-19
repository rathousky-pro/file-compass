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
        internal static string DefaultRange(IEnumerable<Tuple<IPAddress,IPAddress,bool,int>> candidates) {
            foreach(var candidate in candidates.OrderByDescending(x=>x.Item3).ThenBy(x=>x.Item4)) {
                if(candidate.Item1==null||candidate.Item2==null||candidate.Item1.AddressFamily!=AddressFamily.InterNetwork||candidate.Item2.AddressFamily!=AddressFamily.InterNetwork)continue;
                var bytes=candidate.Item1.GetAddressBytes();if(!PrivateAddress(bytes))continue;uint mask=Number(candidate.Item2.GetAddressBytes());int prefix=0;while(prefix<32&&(mask&(0x80000000u>>prefix))!=0)prefix++;
                uint expected=prefix==0?0u:UInt32.MaxValue<<(32-prefix);if(mask!=expected||prefix==0)continue;
                uint network=Number(bytes)&mask;if(prefix%8!=0)return Address(network)+"/"+prefix;
                if(prefix==32)return candidate.Item1.ToString();var value=new[]{(network>>24)&255,(network>>16)&255,(network>>8)&255,network&255};
                return String.Join(".",value.Select((octet,index)=>index<prefix/8?octet.ToString():"*"));
            }
            return "";
        }
        internal static string DefaultLocalRange() {
            var candidates=new List<Tuple<IPAddress,IPAddress,bool,int>>();
            try { foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces()) {
                if(adapter.OperationalStatus!=OperationalStatus.Up||adapter.NetworkInterfaceType==NetworkInterfaceType.Loopback)continue;
                try {var properties=adapter.GetIPProperties();bool gateway=properties.GatewayAddresses.Any(x=>x.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.Any.Equals(x.Address));
                    int index=properties.GetIPv4Properties()?.Index??Int32.MaxValue;
                    foreach(var address in properties.UnicastAddresses)if(address.Address.AddressFamily==AddressFamily.InterNetwork)candidates.Add(Tuple.Create(address.Address,address.IPv4Mask,gateway,index));
                }catch(NetworkInformationException){}catch(NotSupportedException){}
            }} catch(NetworkInformationException){}
            return DefaultRange(candidates);
        }
        internal sealed class ScanRange { internal uint First,Last; internal long Count {get{return (long)Last-First+1;}} }
        internal static ScanRange ParseRange(string input) {
            string text=(input??"").Trim();string[] parts=text.Split('/');if(parts.Length>2)throw new FormatException(L.Text("networkScan.invalid"));
            string[] octets=parts[0].Split('.');if(octets.Length!=4)throw new FormatException(L.Text("networkScan.invalid"));
            byte[] bytes=new byte[4];bool wildcard=false;int fixedOctets=0;
            for(int i=0;i<4;i++){string item=octets[i].Trim();if(item=="*"||item.Equals("x",StringComparison.OrdinalIgnoreCase)){wildcard=true;continue;}byte value;if(wildcard||!Byte.TryParse(item,out value))throw new FormatException(L.Text("networkScan.invalid"));bytes[i]=value;fixedOctets++;}
            int prefix=wildcard?fixedOctets*8:32;if(parts.Length==2&&(wildcard||!Int32.TryParse(parts[1],out prefix)||prefix<0||prefix>32))throw new FormatException(L.Text("networkScan.invalid"));
            uint mask=prefix==0?0u:UInt32.MaxValue<<(32-prefix);uint first=Number(bytes)&mask,last=first|~mask;
            if(!PrivateAddress(new[]{(byte)(first>>24),(byte)(first>>16),(byte)(first>>8),(byte)first})||!PrivateAddress(new[]{(byte)(last>>24),(byte)(last>>16),(byte)(last>>8),(byte)last}))throw new FormatException(L.Text("networkScan.privateOnly"));
            if(prefix<=30){first++;last--;}
            return new ScanRange{First=first,Last=last};
        }
        internal static string SubnetLabel(uint address) {return (address>>24)+"."+((address>>16)&255)+"."+((address>>8)&255)+".*";}
        internal static async Task ScanRangeAsync(ScanRange range,CancellationToken token,Action<Entry> publish,Action<long,long,string> progress,Func<string,int,int,Task<bool>> probe=null) {
            long done=0,lastUpdate=0;probe=probe??PortOpen;
            for(long first=range.First;first<=range.Last;){
                token.ThrowIfCancellationRequested();long end=Math.Min((long)range.Last,first|255L),next=first-1;string subnet=SubnetLabel((uint)first);
                progress(Interlocked.Read(ref done),range.Count,subnet);
                var workers=Enumerable.Range(0,32).Select(async ignored=>{
                    while(true){token.ThrowIfCancellationRequested();long value=Interlocked.Increment(ref next);if(value>end)break;string host=Address((uint)value);
                        var open=await Task.WhenAll(Ports.Select(async port=>await probe(host,port,450).ConfigureAwait(false)?port:0)).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();foreach(int port in open.Where(p=>p!=0))publish(Service(host,port));
                        long count=Interlocked.Increment(ref done),now=DateTime.UtcNow.Ticks,previous=Interlocked.Read(ref lastUpdate);
                        if(count==range.Count||(now-previous>=TimeSpan.TicksPerMillisecond*250&&Interlocked.CompareExchange(ref lastUpdate,now,previous)==previous))progress(count,range.Count,subnet);
                    }
                }).ToArray();await Task.WhenAll(workers).ConfigureAwait(false);first=end+1;
            }
        }
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
