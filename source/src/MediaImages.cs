using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Web.Script.Serialization;
using ImageMagick;
namespace FileCompass {
 public sealed class MediaImageRequest {public string Source,Target;public bool Probe;public string OptionsJson;}
 public sealed class MediaImageResponse {public MediaInfo Info;public string Error;}
 internal static class MediaImages {
  internal static MediaInfo Call(string source,string target,MediaImageOptions options,bool probe,CancellationToken token){
   using(var temp=new ArchiveTemp(Path.Combine(AppPaths.ConfigDirectory,"media-workers")))
   using(var job=new ArchiveProcessJob(1024)){
    string request=Path.Combine(temp.Path,"request.json"),result=Path.Combine(temp.Path,"result.json");var json=new JavaScriptSerializer{MaxJsonLength=65536};File.WriteAllText(request,json.Serialize(new MediaImageRequest{Source=source,Target=target,Probe=probe,OptionsJson=json.Serialize(options)}));
    using(var process=new Process{StartInfo=new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--media-image-worker "+SshTerminal.QuoteWindowsArgument(request)+" "+SshTerminal.QuoteWindowsArgument(result)){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden}}){
     process.Start();try{job.Assign(process);var clock=Stopwatch.StartNew();while(!process.WaitForExit(50)){token.ThrowIfCancellationRequested();if(clock.Elapsed.TotalSeconds>150)throw new IOException(L.Text("media.timeout"));}token.ThrowIfCancellationRequested();if(!File.Exists(result)||new FileInfo(result).Length>65536)throw new IOException(L.Text("media.imageFailed"));var response=json.Deserialize<MediaImageResponse>(File.ReadAllText(result));if(response==null||response.Error!=null||response.Info==null)throw new IOException(response==null?L.Text("media.imageFailed"):response.Error??L.Text("media.imageFailed"));return response.Info;}
     finally{if(!process.HasExited){try{process.Kill();process.WaitForExit(3000);}catch(InvalidOperationException){}}}
    }
   }
  }
  internal static int Worker(string requestPath,string responsePath){
   var response=new MediaImageResponse();try{
    if(new FileInfo(requestPath).Length>65536)throw new IOException("Request size limit");var json=new JavaScriptSerializer{MaxJsonLength=65536};var request=json.Deserialize<MediaImageRequest>(File.ReadAllText(requestPath));var options=json.Deserialize<MediaImageOptions>(request.OptionsJson);if(!MediaCatalog.IsImage(request.Source))throw new IOException(L.Text("media.invalidInput"));PathSafety.RejectLinks(request.Source);
    using(PluginIntegrity.Acquire("media\\Magick.Native-Q8-x64.dll")){
     MagickNET.SetNativeLibraryDirectory(Path.Combine(PluginIntegrity.PluginRoot,"media"));var config=ImageMagick.Configuration.ConfigurationFiles.Default;config.Policy.Data="<policymap><policy domain='delegate' rights='none' pattern='*'/><policy domain='filter' rights='none' pattern='*'/><policy domain='path' rights='none' pattern='@*'/><policy domain='resource' name='memory' value='256MiB'/><policy domain='resource' name='map' value='256MiB'/><policy domain='resource' name='disk' value='0'/></policymap>";string configPath=Path.Combine(Path.GetDirectoryName(requestPath),"magick");Directory.CreateDirectory(configPath);MagickNET.Initialize(config,configPath);ResourceLimits.Thread=2;ResourceLimits.Width=32768;ResourceLimits.Height=32768;ResourceLimits.ListLength=1024;ResourceLimits.Time=120;ResourceLimits.MaxProfileSize=16*1048576;
     string ext=Path.GetExtension(request.Source).ToLowerInvariant();MagickFormat format=ext==".jpg"||ext==".jpeg"?MagickFormat.Jpeg:ext==".tif"||ext==".tiff"?MagickFormat.Tiff:ext==".heif"||ext==".heic"?MagickFormat.Heic:ext==".ico"?MagickFormat.Ico:ext==".jxl"?MagickFormat.Jxl:ext==".avif"?MagickFormat.Avif:ext==".gif"?MagickFormat.Gif:ext==".webp"?MagickFormat.WebP:ext==".bmp"?MagickFormat.Bmp:MagickFormat.Png;
     var settings=new MagickReadSettings{Format=format};
     using(var input=new FileStream(request.Source,FileMode.Open,FileAccess.Read,FileShare.Read))using(var frames=new MagickImageCollection()){
      frames.Ping(input,settings);if(frames.Count==0||frames.Count>1024||frames.Sum(x=>(double)x.Width*x.Height)>100000000)throw new IOException(L.Text("imageTools.pixelLimit"));response.Info=new MediaInfo{Width=(int)frames[0].Width,Height=(int)frames[0].Height,Frames=frames.Count,Format=frames[0].Format.ToString()};if(!request.Probe){
       if(!MediaCatalog.Images.Contains(options.Format)||options.Quality<1||options.Quality>100||!new[]{0,90,180,270}.Contains(options.Rotation))throw new IOException(L.Text("media.invalidOptions"));
       frames.Clear();input.Position=0;frames.Read(input,settings);if(options.FirstFrame)while(frames.Count>1)frames.RemoveAt(frames.Count-1);if(frames.Count>1&&!new[]{"GIF","WebP","TIFF"}.Contains(options.Format))throw new IOException(L.Text("media.framesUnsupported"));
       if(frames.Count>1)frames.Coalesce();long total=0;foreach(var image in frames){image.AutoOrient();if(options.Rotation!=0)image.Rotate(options.Rotation);var dimensions=ImageTools.Dimensions((int)image.Width,(int)image.Height,new ImageToolOptions{ResizeMode=options.Resize,Width=options.Width,Height=options.Height,MaxEdge=options.Width,Percent=options.Percent});total+=(long)dimensions[0]*dimensions[1];if(total>100000000)throw new IOException(L.Text("imageTools.pixelLimit"));if(image.Width!=dimensions[0]||image.Height!=dimensions[1])image.Resize(new MagickGeometry((uint)dimensions[0],(uint)dimensions[1]){IgnoreAspectRatio=true});image.Quality=(uint)options.Quality;if(options.Format=="JPEG"||options.Format=="BMP"){image.BackgroundColor=MagickColors.White;image.Alpha(AlphaAction.Remove);}if(options.Format=="WebP"&&options.Lossless)image.Settings.SetDefine(MagickFormat.WebP,"lossless",true);if(!options.KeepMetadata)image.Strip();}
       MagickFormat output=options.Format=="JPEG"?MagickFormat.Jpeg:options.Format=="TIFF"?MagickFormat.Tiff:options.Format=="WebP"?MagickFormat.WebP:options.Format=="AVIF"?MagickFormat.Avif:options.Format=="BMP"?MagickFormat.Bmp:options.Format=="GIF"?MagickFormat.Gif:MagickFormat.Png;
       PathSafety.RejectLinks(request.Target);using(var outputStream=new FileStream(request.Target,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)){frames.Write(outputStream,output);outputStream.Flush(true);}int count=frames.Count;frames.Clear();frames.Ping(request.Target);if(frames.Count!=count||frames.Any(x=>x.Width<1||x.Height<1))throw new IOException(L.Text("media.verifyFailed"));response.Info=new MediaInfo{Width=(int)frames[0].Width,Height=(int)frames[0].Height,Frames=count,Format=output.ToString()};
      }
     }
    }
   }catch(Exception ex){response.Error=ex.Message;}File.WriteAllText(responsePath,new JavaScriptSerializer{MaxJsonLength=65536}.Serialize(response));return 0;
  }
 }
}
