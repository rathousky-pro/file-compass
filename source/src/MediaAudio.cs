using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
namespace FileCompass {
 internal static partial class MediaAudio {
  internal static string[] RateModes(string profile){var p=MediaCatalog.Profile(profile);return p.Lossless?new[]{"lossless"}:profile=="mp3"?new[]{"cbr","abr","vbr"}:profile=="opus"?new[]{"cbr","vbr","cvbr"}:profile=="aac"||profile=="adts"||profile=="vorbis"?new[]{"abr","vbr"}:new[]{"cbr"};}
  internal static string Mode(MediaAudioOptions options){return options.RateMode??(options.Vbr?"vbr":RateModes(options.Profile)[0]);}
  internal static double Quality(MediaAudioOptions options){return options.VbrQuality??(options.Profile=="vorbis"?5:2);}
  internal static void Validate(MediaAudioOptions options,MediaInfo info){
   var p=MediaCatalog.Profile(options.Profile);if(options.Track<0||options.Track>=info.Audio.Count)throw new IOException(L.Text("media.trackMissing"));var input=info.Audio[options.Track];int channels=options.Channels==0?input.Channels:options.Channels,rate=options.SampleRate==0?input.SampleRate:options.SampleRate;
   if(channels<1||channels>p.MaxChannels||(options.Channels!=0&&!new[]{1,2,6,8}.Contains(channels)))throw new IOException(L.Text("media.channelsUnsupported"));if(!new[]{8000,11025,12000,16000,22050,24000,32000,44100,48000,88200,96000,176400,192000}.Contains(rate))throw new IOException(L.Text("media.sampleUnsupported"));
   if((p.Id=="ac3"||p.Id=="eac3")&&!new[]{32000,44100,48000}.Contains(rate)||p.Id=="mp3"&&rate>48000||(p.Id=="aac"||p.Id=="adts")&&rate>96000||p.Id=="opus"&&!new[]{8000,12000,16000,24000,48000}.Contains(rate))throw new IOException(L.Text("media.sampleUnsupported"));
   if(!new[]{16,24,32}.Contains(options.Depth)||p.Lossless&&p.Id!="wav"&&options.Depth==32)throw new IOException(L.Text("media.depthUnsupported"));
   if(!p.Lossless&&(options.Bitrate<32||options.Bitrate>(p.Id=="mp3"?320:p.Id=="ac3"?640:1536)))throw new IOException(L.Text("media.bitrateUnsupported"));if(p.Id=="ac3"&&!new[]{32,40,48,56,64,80,96,112,128,160,192,224,256,320,384,448,512,576,640}.Contains(options.Bitrate))throw new IOException(L.Text("media.bitrateUnsupported"));
   string mode=Mode(options);if(!RateModes(p.Id).Contains(mode))throw new IOException(L.Text("media.invalidOptions"));if(mode=="vbr"&&p.Id!="opus"){double quality=Quality(options);if(Double.IsNaN(quality)||Double.IsInfinity(quality)||quality<(p.Id=="vorbis"?-1:p.Id=="aac"||p.Id=="adts"?.1:0)||quality>(p.Id=="mp3"?9:p.Id=="vorbis"?10:5))throw new IOException(L.Text("media.invalidOptions"));}
   if(channels>2&&channels>input.Channels&&(!options.Upmix||input.Channels!=2))throw new IOException(L.Text("media.upmixNeeded"));
   if(options.Copy){string codec=p.Id=="wav"&&new[]{"pcm_s16le","pcm_s24le","pcm_f32le"}.Contains(input.Codec)?input.Codec:p.Id=="wav"?(options.Depth==32?"pcm_f32le":"pcm_s"+options.Depth+"le"):p.Codec=="libmp3lame"?"mp3":p.Codec=="libopus"?"opus":p.Codec=="libvorbis"?"vorbis":p.Codec;if(input.Codec!=codec||options.Normalize||options.Upmix||(options.Channels!=0&&options.Channels!=input.Channels)||(options.SampleRate!=0&&options.SampleRate!=input.SampleRate))throw new IOException(L.Text("media.copyMismatch"));}
   double speed=SpeedFactor(options,info);if(options.Copy&&(Math.Abs(speed-1)>1e-9||options.TargetDurationSeconds>0))throw new IOException(L.Text("media.copyMismatch"));if(options.KeepVideo&&Math.Abs(speed-1)>1e-9)throw new IOException(L.Text("media.speedVideoConflict"));
   if(options.KeepVideo&&!p.Video)throw new IOException(L.Text("media.videoUnsupported"));
  }
  internal static List<string> Arguments(string source,string target,MediaAudioOptions options,MediaInfo info,int threads=4){
   Validate(options,info);var p=MediaCatalog.Profile(options.Profile);var input=info.Audio[options.Track];var args=new List<string>{"-hide_banner","-v","error","-nostdin","-n","-filter_threads",Math.Max(1,threads).ToString(CultureInfo.InvariantCulture),"-protocol_whitelist","file,pipe","-format_whitelist",MediaProcess.Formats,"-i",Path.GetFullPath(source),"-map","0:"+input.Index};
   if(options.KeepVideo){foreach(int video in info.Video){args.AddRange(new[]{"-map","0:"+video});}args.AddRange(new[]{"-c:v","copy"});}
   else if(options.KeepMetadata&&info.Cover>=0&&new[]{"mp3","aac","alac","flac"}.Contains(p.Id))args.AddRange(new[]{"-map","0:"+info.Cover,"-c:v","copy","-disposition:v","attached_pic"});
   args.AddRange(new[]{"-map_metadata",options.KeepMetadata?"0":"-1"});
   if(Math.Abs(SpeedFactor(options,info)-1)>1e-9||options.TargetDurationSeconds>0)args.AddRange(new[]{"-map_chapters","-1"});
   if(options.Copy)args.AddRange(new[]{"-c:a","copy"});else{
    string codec=p.Id=="wav"?(options.Depth==32?"pcm_f32le":options.Depth==24?"pcm_s24le":"pcm_s16le"):p.Codec;args.AddRange(new[]{"-c:a",codec,"-threads",Math.Max(1,threads).ToString(CultureInfo.InvariantCulture)});
    int channels=options.Channels==0?input.Channels:options.Channels,rate=options.SampleRate==0?input.SampleRate:options.SampleRate;
    if(!p.Lossless){string mode=Mode(options);if(mode=="vbr"&&p.Id!="opus")args.AddRange(new[]{"-q:a",Quality(options).ToString(CultureInfo.InvariantCulture)});else args.AddRange(new[]{"-b:a",options.Bitrate+"k"});if(p.Id=="mp3"&&mode=="abr")args.AddRange(new[]{"-abr","1"});if(p.Id=="opus"){args.AddRange(new[]{"-vbr",mode=="cvbr"?"constrained":mode=="vbr"?"on":"off"});if(channels>2)args.AddRange(new[]{"-mapping_family","1"});}}
    else if(p.Id=="flac")args.AddRange(new[]{"-sample_fmt",options.Depth==24?"s32":"s16","-compression_level","5"});else if(p.Id=="alac")args.AddRange(new[]{"-sample_fmt",options.Depth==24?"s32p":"s16p"});
    var filters=SpeedFilters(options,info,rate);if(channels>input.Channels&&channels>2)filters.Add("surround=chl_in=stereo:chl_out="+(channels==6?"5.1":"7.1"));if(options.Normalize)filters.Add("loudnorm=I=-16:TP=-1.5:LRA=11");if(filters.Count>0)args.AddRange(new[]{"-af",String.Join(",",filters)});
    args.AddRange(new[]{"-ac",channels.ToString(CultureInfo.InvariantCulture),"-ar",rate.ToString(CultureInfo.InvariantCulture)});
   }
   args.AddRange(new[]{"-progress","pipe:1","-nostats","-f",options.KeepVideo?"mp4":p.Muxer,target});return args;
  }
  internal static void Convert(string source,string stage,MediaAudioOptions options,CancellationToken token,Action<double> progress,int concurrentFiles=1){
   using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read)){
    var info=MediaProcess.Probe(source,token);var args=Arguments(source,stage,options,info,MediaCpuBudget.AudioThreads(Environment.ProcessorCount,concurrentFiles));double factor=SpeedFactor(options,info);double expected=options.KeepVideo?info.Duration:SourceDuration(options,info)/factor;MediaProcess.Run("ffmpeg.exe",args,token,21600,progress,expected);
    var result=MediaProcess.Probe(stage,token);var track=info.Audio[options.Track];int channels=options.Copy?track.Channels:options.Channels==0?track.Channels:options.Channels;int rate=options.Copy?track.SampleRate:options.SampleRate==0?track.SampleRate:options.SampleRate;
    string expectedCodec=options.Copy?track.Codec:options.Profile=="wav"?(options.Depth==32?"pcm_f32le":"pcm_s"+options.Depth+"le"):options.Profile=="mp3"?"mp3":options.Profile=="opus"?"opus":options.Profile=="vorbis"?"vorbis":options.Profile=="adts"?"aac":options.Profile;
    if(result.Audio.Count!=1||result.Audio[0].Codec!=expectedCodec||result.Audio[0].Channels!=channels||result.Audio[0].SampleRate!=(options.Profile=="opus"?48000:rate)||options.KeepVideo&&result.Video.Count!=info.Video.Count||expected>1&&Math.Abs(result.Duration-expected)>(Math.Abs(factor-1)>1e-9?Math.Max(.25,expected*.00002):Math.Max(1,expected*.03)))throw new IOException(L.Text("media.verifyFailed"));
    MediaProcess.Run("ffmpeg.exe",new[]{"-hide_banner","-v","error","-nostdin","-protocol_whitelist","file,pipe","-format_whitelist",MediaProcess.Formats,"-i",stage,"-map","0:a:0","-f","null","-"},token,21600,delegate(double value){},result.Duration);
   }
  }
 }
}
