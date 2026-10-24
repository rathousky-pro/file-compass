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
 internal static class MediaProcess {
  internal static readonly string Formats="mov,mp3,aac,ac3,eac3,wav,flac,ogg,matroska,webm,avi,asf,aiff,mpegts,mpeg";
  internal static IDisposable Acquire(){return PluginIntegrity.Acquire(PluginHashes.Expected.Keys.Where(x=>x.StartsWith("media\\",StringComparison.OrdinalIgnoreCase)&&!x.EndsWith("Magick.Native-Q8-x64.dll",StringComparison.OrdinalIgnoreCase)).ToArray());}
  internal static string Run(string tool,IEnumerable<string> args,CancellationToken token,int seconds,Action<double> progress=null,double duration=0){
   if(tool!="ffmpeg.exe"&&tool!="ffprobe.exe")throw new IOException(L.Text("media.invalidOptions"));var info=new ProcessStartInfo(Path.Combine(PluginIntegrity.PluginRoot,"media",tool),String.Join(" ",args.Select(SshTerminal.QuoteWindowsArgument))){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,WorkingDirectory=Path.Combine(PluginIntegrity.PluginRoot,"media")};
   using(var job=new ArchiveProcessJob(1024))using(var process=new Process{StartInfo=info}){
    token.ThrowIfCancellationRequested();process.Start();try{job.Assign(process);process.StandardInput.Close();try{process.PriorityClass=ProcessPriorityClass.BelowNormal;}catch(InvalidOperationException){}
     var errors=Task.Run(()=>Drain(process.StandardError,32768,null));var output=Task.Run(()=>Drain(process.StandardOutput,2097152,progress==null?(Action<string>)null:line=>{if(line.StartsWith("out_time_us=",StringComparison.Ordinal)){long micros;if(Int64.TryParse(line.Substring(12),out micros)&&duration>0)progress(Math.Min(.99,micros/1000000.0/duration));}}));
     var clock=Stopwatch.StartNew();while(!process.WaitForExit(60)){token.ThrowIfCancellationRequested();if(clock.Elapsed.TotalSeconds>seconds)throw new IOException(L.Text("media.timeout"));if(output.IsFaulted)output.GetAwaiter().GetResult();}process.WaitForExit();string text=output.GetAwaiter().GetResult(),error=errors.GetAwaiter().GetResult();token.ThrowIfCancellationRequested();if(process.ExitCode!=0)throw new IOException(L.Text("media.toolFailed")+"\n"+error);return text;
    }finally{if(!process.HasExited){try{process.Kill();process.WaitForExit(3000);}catch(InvalidOperationException){}}}
   }
  }
  static string Drain(StreamReader reader,int limit,Action<string> line){var tail=new StringBuilder();var pending=new StringBuilder();char[] buffer=new char[2048];int count;while((count=reader.Read(buffer,0,buffer.Length))>0){tail.Append(buffer,0,count);if(tail.Length>limit){if(line==null&&limit>1000000)throw new IOException(L.Text("media.probeLimit"));tail.Remove(0,tail.Length-limit);}if(line!=null)for(int i=0;i<count;i++){char c=buffer[i];if(c=='\n'){line(pending.ToString().TrimEnd('\r'));pending.Clear();}else if(pending.Length<8192)pending.Append(c);}}return tail.ToString();}
  internal static MediaInfo Probe(string path,CancellationToken token){
   PathSafety.RejectLinks(path);using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
    var args=new[]{"-v","error","-protocol_whitelist","file,pipe","-format_whitelist",Formats,"-show_streams","-show_format","-of","json",Path.GetFullPath(path)};string text=Run("ffprobe.exe",args,token,30);
    var root=new JavaScriptSerializer{MaxJsonLength=2097152}.DeserializeObject(text) as Dictionary<string,object>;if(root==null)throw new IOException(L.Text("media.invalidInput"));var result=new MediaInfo();object raw;if(root.TryGetValue("format",out raw)){var f=raw as Dictionary<string,object>;if(f!=null){result.Format=Value(f,"format_name");Double.TryParse(Value(f,"duration"),NumberStyles.Float,CultureInfo.InvariantCulture,out result.Duration);}}
    if(root.TryGetValue("streams",out raw))foreach(var item in (object[])raw){var stream=item as Dictionary<string,object>;if(stream==null)continue;string kind=Value(stream,"codec_type");int index=Number(stream,"index");if(kind=="audio"){var track=new MediaTrack{Index=index,Codec=Value(stream,"codec_name"),Channels=Number(stream,"channels"),SampleRate=Number(stream,"sample_rate"),Layout=Value(stream,"channel_layout")};object tags;if(stream.TryGetValue("tags",out tags)&&tags is Dictionary<string,object>)track.Language=Value((Dictionary<string,object>)tags,"language");Double.TryParse(Value(stream,"duration"),NumberStyles.Float,CultureInfo.InvariantCulture,out track.Duration);result.Audio.Add(track);if(result.Duration<=0)Double.TryParse(Value(stream,"duration"),NumberStyles.Float,CultureInfo.InvariantCulture,out result.Duration);}
     else if(kind=="video"){object disposition;bool cover=stream.TryGetValue("disposition",out disposition)&&disposition is Dictionary<string,object>&&Number((Dictionary<string,object>)disposition,"attached_pic")==1;if(cover)result.Cover=index;else result.Video.Add(index);}}
    if(Double.IsNaN(result.Duration)||Double.IsInfinity(result.Duration)||result.Duration<0||result.Duration>315360000)result.Duration=0;
    if(result.Audio.Count==0)throw new IOException(L.Text("media.noAudio"));return result;
   }
  }
  static string Value(Dictionary<string,object> values,string key){object value;return values.TryGetValue(key,out value)?Convert.ToString(value,CultureInfo.InvariantCulture):"";}
  static int Number(Dictionary<string,object> values,string key){int value;return Int32.TryParse(Value(values,key),out value)?value:0;}
 }
}
