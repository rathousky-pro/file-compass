using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
namespace FileCompass {
 internal sealed class MediaProfile {
  internal string Id,Extension,Codec,Muxer;internal int MaxChannels;internal bool Lossless,Vbr,Video;
  internal MediaProfile(string id,string ext,string codec,string muxer,int channels,bool lossless=false,bool vbr=false,bool video=false){Id=id;Extension=ext;Codec=codec;Muxer=muxer;MaxChannels=channels;Lossless=lossless;Vbr=vbr;Video=video;}
  public override string ToString(){return L.Text("media.profile."+Id);}
 }
 public sealed class MediaAudioOptions {
  public double Speed=1,TargetDurationSeconds;public bool PreservePitch=true;
  public string RateMode;public double? VbrQuality;public string Profile="aac";public int Bitrate=192,Channels=2,SampleRate=48000,Depth=16,Track;public bool Vbr,Normalize,Upmix,Copy,KeepVideo,KeepMetadata=true;
 }
 public sealed class MediaImageOptions {
  public string Format="PNG";public int Quality=90,Resize,Width=1920,Height=1080,Percent=50,Rotation;public bool FirstFrame,KeepMetadata=true,Lossless;
 }
 public sealed class MediaTrack {public double Duration;public int Index,Channels,SampleRate;public string Codec,Layout,Language;}
 public sealed class MediaInfo {
  public double Duration;public int Width,Height,Frames;public string Format;public List<MediaTrack> Audio=new List<MediaTrack>();public List<int> Video=new List<int>();public int Cover=-1;
  internal string Summary{get{return Width>0?Width+" × "+Height+" px · "+Frames+" "+L.Text("media.frames"):Audio.Count==0?L.Text("media.noAudio"):String.Join(" · ",new[]{Audio[0].Codec,Audio[0].Channels+" ch",Audio[0].SampleRate+" Hz",TimeSpan.FromSeconds(Math.Max(0,Duration)).ToString(@"hh\:mm\:ss")})+(Audio.Count>1?" · "+Audio.Count+" "+L.Text("media.tracks"):"");}}
 }
 internal sealed class MediaRow:INotifyPropertyChanged {
  public string Path{get;set;}public string Name{get{return System.IO.Path.GetFileName(Path);}}public bool Image{get;set;}public MediaInfo Info;
  bool included=true;string summary="",status="",target="";double progress;
  public bool Included{get{return included;}set{included=value;Changed("Included");}}public string Summary{get{return summary;}set{summary=value;Changed("Summary");}}public string Status{get{return status;}set{status=value;Changed("Status");}}public string Target{get{return target;}set{target=value;Changed("Target");}}public double Progress{get{return progress;}set{progress=value;Changed("Progress");}}
  bool hasProgress;public string ProgressText{get{return hasProgress?progress.ToString("F0",CultureInfo.CurrentCulture)+" %":"";}}
  public event PropertyChangedEventHandler PropertyChanged;void Changed(string name){if(name=="Progress"){hasProgress=true;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("ProgressText"));}if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs(name));}
 }
 internal static class MediaCatalog {
  internal static readonly MediaProfile[] Audio={new MediaProfile("mp3",".mp3","libmp3lame","mp3",2,false,true),new MediaProfile("aac",".m4a","aac","ipod",8,false,true,true),new MediaProfile("adts",".aac","aac","adts",8,false,true),new MediaProfile("ac3",".ac3","ac3","ac3",6),new MediaProfile("eac3",".eac3","eac3","eac3",6),new MediaProfile("flac",".flac","flac","flac",8,true),new MediaProfile("wav",".wav","pcm_s16le","wav",8,true),new MediaProfile("opus",".opus","libopus","opus",8,false,true),new MediaProfile("vorbis",".ogg","libvorbis","ogg",8,false,true),new MediaProfile("alac",".m4a","alac","ipod",8,true,false,true)};
  internal static readonly string[] Images={"PNG","JPEG","WebP","AVIF","TIFF","BMP","GIF"};
  internal static bool IsImage(string path){return new[]{".png",".jpg",".jpeg",".bmp",".tif",".tiff",".webp",".avif",".gif",".heic",".heif",".ico",".jxl"}.Contains(Path.GetExtension(path).ToLowerInvariant());}
  internal static bool IsAudio(string path){return new[]{".mp3",".aac",".ac3",".eac3",".wav",".flac",".ogg",".opus",".m4a",".m4b",".mp4",".mkv",".mov",".avi",".webm",".wma",".aiff",".aif",".mka",".oga",".mp2",".m2ts",".ts"}.Contains(Path.GetExtension(path).ToLowerInvariant());}
  internal static MediaProfile Profile(string id){var profile=Audio.FirstOrDefault(x=>x.Id==id);if(profile==null)throw new IOException(L.Text("media.invalidOptions"));return profile;}
  internal static string Extension(bool image,MediaImageOptions picture,MediaAudioOptions sound){return image?(picture.Format=="JPEG"?".jpg":picture.Format=="TIFF"?".tiff":"."+picture.Format.ToLowerInvariant()):sound.KeepVideo&&Profile(sound.Profile).Video?".mp4":Profile(sound.Profile).Extension;}
  internal static string Target(string source,string folder,string extension,ISet<string> reserved){for(int i=0;i<10000;i++){string path=Path.Combine(folder,Path.GetFileNameWithoutExtension(source)+"_converted"+(i==0?"":"_"+(i+1))+extension);if(!File.Exists(path)&&!Directory.Exists(path)&&!reserved.Contains(path)){reserved.Add(path);return path;}}throw new IOException(L.Text("parts.outputExists"));}
 }
}
