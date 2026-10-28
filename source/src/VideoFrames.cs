using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;
namespace FileCompass {
 internal sealed class VideoFrame {
    public int Index{get;set;}public double Seconds{get;set;}public BitmapSource Image{get;set;}
    public string Caption{get{return VideoFrames.Time(Seconds);}}
 }
 internal sealed class VideoFramesManifest {public int Version=1;public string Stamp;public double Duration;public double[] Seconds;}
 internal static class VideoFrames {
    static readonly SemaphoreSlim queue=new SemaphoreSlim(1,1);
    static readonly double[] positions={.10,.25,.40,.55,.70,.85};
    internal static string Root{get{return Path.Combine(AppPaths.DataDirectory,"video-previews");}}
    internal static string Time(double value){var time=TimeSpan.FromSeconds(Math.Max(0,value));return time.TotalHours>=1?((long)time.TotalHours).ToString()+time.ToString(@"\:mm\:ss"):((int)time.TotalMinutes).ToString()+time.ToString(@"\:ss");}
    static string Stamp(string path){var file=new FileInfo(path);return Path.GetFullPath(path)+"|"+file.Length+"|"+file.LastWriteTimeUtc.Ticks;}
    static string Key(string stamp){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(stamp))).Replace("-","").ToLowerInvariant();}
    static BitmapSource ReadImage(string path){
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>4*1048576)throw new IOException(L.Text("video.invalidCachedFrame"));PathSafety.RejectLinks(path);
        using(var stream=File.OpenRead(path)){var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);var frame=decoder.Frames[0];if(frame.PixelWidth<1||frame.PixelHeight<1||frame.PixelWidth>1280||frame.PixelHeight>720)throw new IOException(L.Text("video.invalidDimensions"));stream.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;}
    }
    static List<VideoFrame> Cached(string folder,string stamp,CancellationToken token){
        string manifest=Path.Combine(folder,"frames.json");if(!File.Exists(manifest)||new FileInfo(manifest).Length>8192)return null;
        try{PathSafety.RejectLinks(folder);var data=new JavaScriptSerializer().Deserialize<VideoFramesManifest>(File.ReadAllText(manifest));if(data.Version!=1||data.Stamp!=stamp||!ValidDuration(data.Duration)||data.Seconds==null||data.Seconds.Length!=6||data.Seconds.Any(x=>Double.IsNaN(x)||Double.IsInfinity(x)||x<0||x>data.Duration))return null;
            var frames=new List<VideoFrame>();for(int i=0;i<6;i++){token.ThrowIfCancellationRequested();frames.Add(new VideoFrame{Index=i,Seconds=data.Seconds[i],Image=ReadImage(Path.Combine(folder,i+".jpg"))});}Directory.SetLastWriteTimeUtc(folder,DateTime.UtcNow);return frames;
        }catch(OperationCanceledException){throw;}catch{return null;}
    }
    static bool ValidDuration(double value){return !Double.IsNaN(value)&&!Double.IsInfinity(value)&&value>0&&value<=315360000;}
    static Tuple<double,int> Probe(string path,CancellationToken token){
        string output=MediaProcess.Run("ffprobe.exe",new[]{"-v","error","-protocol_whitelist","file,pipe","-format_whitelist",MediaProcess.Formats,"-probesize","5242880","-analyzeduration","5000000","-show_entries","format=duration:stream=index,codec_type,duration:stream_disposition=attached_pic","-of","json",path},token,20);
        var root=new JavaScriptSerializer{MaxJsonLength=2097152}.DeserializeObject(output) as Dictionary<string,object>;object raw;if(root==null||!root.TryGetValue("streams",out raw))throw new IOException(L.Text("video.noStream"));
        var stream=((object[])raw).Cast<Dictionary<string,object>>().FirstOrDefault(x=>Convert.ToString(x["codec_type"])=="video"&&(!x.ContainsKey("disposition")||!((Dictionary<string,object>)x["disposition"]).ContainsKey("attached_pic")||Convert.ToInt32(((Dictionary<string,object>)x["disposition"])["attached_pic"])==0));if(stream==null)throw new IOException(L.Text("video.noStream"));
        double duration=0;object format;if(root.TryGetValue("format",out format)&&((Dictionary<string,object>)format).TryGetValue("duration",out raw))Double.TryParse(Convert.ToString(raw),NumberStyles.Float,CultureInfo.InvariantCulture,out duration);
        if(!ValidDuration(duration)&&stream.TryGetValue("duration",out raw))Double.TryParse(Convert.ToString(raw),NumberStyles.Float,CultureInfo.InvariantCulture,out duration);if(!ValidDuration(duration))throw new IOException(L.Text("video.noDuration"));
        return Tuple.Create(duration,Convert.ToInt32(stream["index"]));
    }
    static void Extract(string path,string output,double seconds,int stream,CancellationToken token){
        MediaProcess.Run("ffmpeg.exe",new[]{"-hide_banner","-v","error","-nostdin","-protocol_whitelist","file,pipe","-format_whitelist",MediaProcess.Formats,"-ss",seconds.ToString("0.000",CultureInfo.InvariantCulture),"-i",path,"-map","0:"+stream,"-an","-sn","-dn","-threads","1","-filter_threads","1","-frames:v","1","-vf","scale=1280:720:force_original_aspect_ratio=decrease,setsar=1","-q:v","3","-f","image2","-update","1","-y",output},token,20);
    }
    internal static List<VideoFrame> Load(string path,CancellationToken token,Action<VideoFrame> ready){
        queue.Wait(token);string work=null;
        try{
            token.ThrowIfCancellationRequested();PathSafety.RejectLinks(path);
            // Hold a stable read snapshot. Never decode a source that is being overwritten.
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
                string sourceStamp=Stamp(path),key=Key("v1|"+sourceStamp+"|"+Stamp(UpdateRegistry.Resolve("media\\ffmpeg.exe"))+"|"+Stamp(UpdateRegistry.Resolve("media\\ffprobe.exe")));
                CloudStore.SecureDirectory(Root);string folder=Path.Combine(Root,key);var cached=Cached(folder,key,token);if(cached!=null){Prune(folder);foreach(var frame in cached){token.ThrowIfCancellationRequested();ready(frame);}return cached;}
                work=Path.Combine(Root,key+"-"+Guid.NewGuid().ToString("N")+".tmp");Directory.CreateDirectory(work);
                using(MediaProcess.Acquire()){
                    var info=Probe(path,token);var frames=new List<VideoFrame>();
                    for(int i=0;i<6;i++){token.ThrowIfCancellationRequested();double seconds=info.Item1*positions[i];string image=Path.Combine(work,i+".jpg");Extract(path,image,seconds,info.Item2,token);if(!File.Exists(image)){seconds=0;Extract(path,image,0,info.Item2,token);}var frame=new VideoFrame{Index=i,Seconds=seconds,Image=ReadImage(image)};frames.Add(frame);ready(frame);}
                    if(Stamp(path)!=sourceStamp)throw new IOException(L.Text("video.changed"));token.ThrowIfCancellationRequested();
                    File.WriteAllText(Path.Combine(work,"frames.json"),new JavaScriptSerializer().Serialize(new VideoFramesManifest{Stamp=key,Duration=info.Item1,Seconds=frames.Select(x=>x.Seconds).ToArray()}));
                    if(!Directory.Exists(folder)){try{Directory.Move(work,folder);work=null;}catch(IOException){if(!Directory.Exists(folder))throw;}}
                    Prune(folder);return frames;
                }
            }
        }finally{if(work!=null&&Directory.Exists(work))PathSafety.DeleteTree(work);queue.Release();}
    }
    static void Prune(string keep){
        try{long total=0;int count=0;foreach(var abandoned in new DirectoryInfo(Root).EnumerateDirectories("*.tmp").Where(x=>x.LastWriteTimeUtc<DateTime.UtcNow.AddDays(-1))){PathSafety.RejectLinks(abandoned.FullName);PathSafety.DeleteTree(abandoned.FullName);}foreach(var directory in new DirectoryInfo(Root).EnumerateDirectories().Where(x=>x.Name.Length==64).OrderByDescending(x=>x.LastWriteTimeUtc)){PathSafety.RejectLinks(directory.FullName);long bytes=directory.EnumerateFiles().Sum(x=>x.Length);total+=bytes;count++;if(directory.FullName!=keep&&(count>128||total>256L*1048576||directory.LastWriteTimeUtc<DateTime.UtcNow.AddDays(-30)))PathSafety.DeleteTree(directory.FullName);}}catch(IOException){}catch(UnauthorizedAccessException){}
    }
 }
}
