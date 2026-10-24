using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace FileCompass {
    public sealed class DetailRow { public string Key {get;set;} public string Label {get;set;} public string Value {get;set;} }
    public sealed class FileDetails {
        public bool IsDocument {get;set;}
        public string Summary {get;set;} public string Excerpt {get;set;} public string ExcerptLabel {get;set;}
        public List<DetailRow> Rows {get;set;}
        public FileDetails(){Rows=new List<DetailRow>();}
        internal void Add(string key,object value) { if(key=="orientation"&&Convert.ToString(value)=="None")return;if(value is double&&(Double.IsNaN((double)value)||Double.IsInfinity((double)value)))return;if(new[]{"iso","aperture","focalLength","orientation"}.Contains(key)&&Convert.ToString(value,CultureInfo.InvariantCulture)=="0")return; string text=Convert.ToString(value,CultureInfo.CurrentCulture);if(String.IsNullOrWhiteSpace(text)||Rows.Count>=70)return;text=new string(text.Where(c=>!Char.IsControl(c)||c=='\n'||c=='\t').Take(1000).ToArray()).Trim();if(text.Length>0&&!Rows.Any(x=>x.Label==L.Text("details."+key)&&x.Value==text))Rows.Add(new DetailRow{Key=key,Label=L.Text("details."+key),Value=text}); }
    }
    internal static partial class FileMetadata {
        internal static FileDetails Basic(string path) {
            var info=new FileInfo(path);var data=new FileDetails{Summary=(String.IsNullOrEmpty(info.Extension)?L.Text("details.file"):info.Extension.TrimStart('.').ToUpperInvariant())+" · "+CopyUi.Bytes(info.Length)};
            data.Add("name",info.Name);data.Add("format",info.Extension.TrimStart('.').ToUpperInvariant());data.Add("size",CopyUi.Bytes(info.Length)+" ("+info.Length.ToString("N0")+" B)");
            data.Add("modified",info.LastWriteTime);data.Add("created",info.CreationTime);data.Add("attributes",info.Attributes);return data;
        }
        internal static object Prop(object obj,string name) {try{var p=obj==null?null:obj.GetType().GetProperty(name);return p==null?null:p.GetValue(obj,null);}catch{return null;}}
        internal static FileDetails Read(string path) {
            var data=Basic(path);string ext=Path.GetExtension(path).ToLowerInvariant();
            try {
                if(new[]{".ico",".cur"}.Contains(ext))Icons(path,data);
                else if(new[]{".jpg",".jpeg",".png",".bmp",".gif",".tif",".tiff",".wdp",".webp",".heic",".avif"}.Contains(ext))Image(path,data);
                else if(ext==".pdf"){data.IsDocument=true;Pdf(path,data);}
                else if(new[]{".docx",".docm",".dotx",".dotm",".xlsx",".xlsm",".xltx",".xltm",".pptx",".pptm",".ppsx",".ppsm",".potx",".potm",".odt",".ods",".odp"}.Contains(ext)){data.IsDocument=true;Office(path,data,ext);}
                else if(FileViewers.Kind(path)=="media")Media(path,data);
                else if(new[]{".exe",".dll",".sys",".ocx",".scr"}.Contains(ext))Executable(path,data);
                else if(new[]{".zip",".7z",".rar",".tar",".gz",".bz2",".xz",".tgz",".tbz2",".txz"}.Contains(ext))Archive(path,data);
                else if(new[]{".ttf",".otf",".ttc"}.Contains(ext))Font(path,data);
                else if(new[]{".doc",".xls",".xlsb",".ppt",".rtf",".msg",".eml",".lnk"}.Contains(ext)){data.IsDocument=ext!=".lnk";Shell(path,data);}
                else Text(path,data,ext);
            }catch(Exception ex){data.Add("notice",(ex.GetType().Name.IndexOf("Password",StringComparison.OrdinalIgnoreCase)>=0||ex.GetType().Name.IndexOf("Encrypted",StringComparison.OrdinalIgnoreCase)>=0)?L.Text("details.passwordProtected"):L.Text("details.partial"));}
            var common=new HashSet<string>(new[]{"name","format","size","modified","created","attributes"}.Select(x=>L.Text("details."+x)));
            var important=new HashSet<string>(new[]{"dimensions","pages","sheets","sheetNames","taken","camera","exposure","aperture","iso","iconSizes","duration","fontFamily","product"}.Select(x=>L.Text("details."+x)));
            data.Rows=data.Rows.OrderBy(x=>important.Contains(x.Label)?0:common.Contains(x.Label)?2:1).ToList();
            return data;
        }
        static void Executable(string path,FileDetails d) {
            var v=FileVersionInfo.GetVersionInfo(path);d.Add("title",v.FileDescription);d.Add("product",v.ProductName);d.Add("company",v.CompanyName);d.Add("fileVersion",v.FileVersion);d.Add("productVersion",v.ProductVersion);d.Add("copyright",v.LegalCopyright);d.Add("originalName",v.OriginalFilename);
            using(var s=Open(path))using(var r=new BinaryReader(s)){if(r.ReadUInt16()!=0x5a4d)return;s.Position=60;uint offset=r.ReadUInt32();if(offset>s.Length-6)return;s.Position=offset;if(r.ReadUInt32()!=0x4550)return;ushort machine=r.ReadUInt16();d.Add("architecture",machine==0x8664?"x64":machine==0x14c?"x86":machine==0xaa64?"ARM64":"0x"+machine.ToString("X4"));}
        }
        static void Zip(string path,FileDetails d) {
            using(var stream=Open(path)){int size=(int)Math.Min(stream.Length,65557);stream.Position=stream.Length-size;var bytes=new byte[size];int read=stream.Read(bytes,0,size);
                for(int i=read-22;i>=0;i--)if(BitConverter.ToUInt32(bytes,i)==0x06054b50 && i+22+BitConverter.ToUInt16(bytes,i+20)==read){
                    ushort count=BitConverter.ToUInt16(bytes,i+10);d.Add("entries",count==65535?"ZIP64":count.ToString());d.Add("archiveComment",BitConverter.ToUInt16(bytes,i+20)>0?Encoding.UTF8.GetString(bytes,i+22,BitConverter.ToUInt16(bytes,i+20)):null);break;
                }
            }
        }
        static Stream Open(string path){return new DetailReadStream(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete));}
        sealed class DetailReadStream:Stream {
            readonly Stream stream;long readBytes;readonly Stopwatch clock=Stopwatch.StartNew();
            internal DetailReadStream(Stream source){stream=source;}
            public override int Read(byte[] b,int o,int c){if(readBytes>64*1048576L||clock.ElapsedMilliseconds>5000)throw new IOException("Metadata read budget");int n=stream.Read(b,o,c);readBytes+=n;return n;}
            public override bool CanRead{get{return true;}}public override bool CanWrite{get{return false;}}public override bool CanSeek{get{return stream.CanSeek;}}
            public override long Length{get{return stream.Length;}}public override long Position{get{return stream.Position;}set{stream.Position=value;}}
            public override long Seek(long o,SeekOrigin s){return stream.Seek(o,s);}public override void Flush(){}public override void SetLength(long v){throw new NotSupportedException();}public override void Write(byte[] b,int o,int c){throw new NotSupportedException();}
            protected override void Dispose(bool disposing){if(disposing)stream.Dispose();base.Dispose(disposing);}
        }
    }
    internal static class FileMetadataService {
        static readonly SemaphoreSlim gate=new SemaphoreSlim(2);
        internal static int Worker(string path,string result) {
            try{var data=FileMetadata.Read(path);File.WriteAllText(result,new JavaScriptSerializer{MaxJsonLength=131072}.Serialize(data),Encoding.UTF8);return 0;}
            catch{return 1;}
        }
        static string Quote(string value){return SshTerminal.QuoteWindowsArgument(value);}
        internal static async Task<FileDetails> Load(string path,CancellationToken token) {
            await gate.WaitAsync(token);
            try {
                using(var temp=new ArchiveTemp())
                using(var job=new ArchiveProcessJob(384))
                using(var process=new Process {StartInfo=new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--metadata-worker "+Quote(path)+" "+Quote(Path.Combine(temp.Path,"details.json"))){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden}}) {
                    process.Start();job.Assign(process);var clock=Stopwatch.StartNew();
                    try {
                        while(!process.HasExited){token.ThrowIfCancellationRequested();if(clock.ElapsedMilliseconds>6500)throw new TimeoutException();await Task.Delay(40,token);}
                        string file=Path.Combine(temp.Path,"details.json");
                        if(process.ExitCode==0&&File.Exists(file)&&new FileInfo(file).Length<=131072){
                            var data=new JavaScriptSerializer{MaxJsonLength=131072}.Deserialize<FileDetails>(File.ReadAllText(file));
                            if(data!=null&&data.Rows!=null&&data.Rows.Count<=70&&data.Rows.All(x=>x!=null&&x.Label!=null&&x.Label.Length<=200&&x.Value!=null&&x.Value.Length<=1000))return data;
                        }
                    }finally{if(!process.HasExited){try{process.Kill();process.WaitForExit(1000);}catch(InvalidOperationException){}}}
                }
            }catch(OperationCanceledException){throw;}catch(Exception){}
            finally{gate.Release();}
            token.ThrowIfCancellationRequested();var fallback=new FileDetails {Summary=Path.GetFileName(path)};fallback.Add("name",Path.GetFileName(path));fallback.Add("notice",L.Text("details.partial"));return fallback;
        }
    }
}





