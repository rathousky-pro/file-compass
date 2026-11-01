using System;
using System.IO;
using System.Linq;
namespace FileCompass {
 internal static class MediaKeepFormat {
  internal static string Extension(MediaRow row,MediaImageOptions image,MediaAudioOptions audio){return row.Image&&image.KeepFormat||!row.Image&&audio.KeepFormat?Path.GetExtension(row.Path):MediaCatalog.Extension(row.Image,image,audio);}
  internal static MediaImageOptions Image(string source,MediaImageOptions options){if(!options.KeepFormat)return options;var result=options.Clone();string ext=Path.GetExtension(source).ToLowerInvariant();result.Format=ext==".jpg"||ext==".jpeg"?"JPEG":ext==".tif"||ext==".tiff"?"TIFF":ext==".webp"?"WebP":ext==".heif"||ext==".heic"?"HEIC":ext.TrimStart('.').ToUpperInvariant();if(!MediaCatalog.Images.Concat(new[]{"HEIC","ICO","JXL"}).Contains(result.Format))throw new IOException(L.Text("media.keepUnsupported"));return result;}
  internal static MediaAudioOptions Audio(string source,MediaInfo info,MediaAudioOptions options){
   if(!options.KeepFormat)return options;if(info==null||options.Track<0||options.Track>=info.Audio.Count)throw new IOException(L.Text("media.trackMissing"));var track=info.Audio[options.Track];var result=options.Clone();result.Copy=options.Copy||!options.Normalize&&!options.Upmix&&(options.Channels==0||options.Channels==track.Channels)&&(options.SampleRate==0||options.SampleRate==track.SampleRate)&&options.TargetDurationSeconds==0&&Math.Abs(MediaAudio.SpeedFactor(options,info)-1)<1e-9;string codec=track.Codec,ext=Path.GetExtension(source).ToLowerInvariant();
   result.Profile=codec=="aac"?(ext==".aac"?"adts":"aac"):codec=="pcm_s16le"||codec=="pcm_s24le"||codec=="pcm_s32le"||codec=="pcm_f32le"||codec=="pcm_s16be"||codec=="pcm_s24be"||codec=="pcm_s32be"||codec=="pcm_f32be"?"wav":codec;try{MediaCatalog.Profile(result.Profile);}catch(IOException){if(!result.Copy)throw new IOException(L.Text("media.keepUnsupported"));result.Profile="aac";result.CodecOverride=codec;}
   result.MuxerOverride=ext==".wav"?"wav":ext==".aif"||ext==".aiff"?"aiff":ext==".m4a"||ext==".m4b"?"ipod":ext==".mp4"?"mp4":ext==".mov"?"mov":ext==".mkv"||ext==".mka"?"matroska":ext==".webm"?"webm":ext==".avi"?"avi":ext==".wma"?"asf":ext==".ts"||ext==".m2ts"?"mpegts":ext==".aac"?"adts":ext==".ogg"||ext==".oga"||ext==".opus"?"ogg":MediaCatalog.Profile(result.Profile).Muxer;
   if(codec.StartsWith("pcm_",StringComparison.Ordinal))result.CodecOverride=codec;
   if(track.Depth==16||track.Depth==24||track.Depth==32)result.Depth=track.Depth;else if(codec.Contains("24"))result.Depth=24;else if(codec.Contains("32"))result.Depth=32;
   result.RateMode=null;result.Vbr=false;result.VbrQuality=null;result.KeepVideo=options.KeepVideo&&info.Video.Count>0;return result;
  }
 }
}
