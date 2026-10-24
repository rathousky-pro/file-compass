using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace FileCompass {
    internal static partial class FileMetadata {
        static void Icons(string path,FileDetails d) {
            using(var stream=Open(path))using(var reader=new BinaryReader(stream)) {
                if(reader.ReadUInt16()!=0)return;ushort kind=reader.ReadUInt16(),count=reader.ReadUInt16();if((kind!=1&&kind!=2)||count>512)return;
                var sizes=new List<string>();
                for(int i=0;i<count;i++){var entry=reader.ReadBytes(16);if(entry.Length!=16)break;int w=entry[0]==0?256:entry[0],h=entry[1]==0?256:entry[1];sizes.Add(w+" × "+h+(kind==1?" · "+BitConverter.ToUInt16(entry,6)+" bpp":""));}
                d.Add("frames",count);d.Add("iconSizes",String.Join("; ",sizes.Distinct()));d.Summary=(kind==1?"ICO":"CUR")+" · "+count+" "+L.Text("details.variants");
            }
        }
        static void Image(string path,FileDetails d) {
            try {using(var stream=Open(path)){
                var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.None);
                var frame=decoder.Frames[0];int w=frame.PixelWidth,h=frame.PixelHeight;
                d.Summary=w+" × "+h+" px · "+(w*(double)h/1000000).ToString("0.##")+" Mpx · "+Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
                d.Add("dimensions",w+" × "+h+" px");d.Add("megapixels",(w*(double)h/1000000).ToString("0.##")+" Mpx");
                d.Add("dpi",frame.DpiX.ToString("0.#")+" × "+frame.DpiY.ToString("0.#"));
                d.Add("pixelFormat",frame.Format.ToString());d.Add("alpha",frame.Format.ToString().IndexOf("bgra",StringComparison.OrdinalIgnoreCase)>=0||frame.Format.ToString().IndexOf("rgba",StringComparison.OrdinalIgnoreCase)>=0||(frame.Palette!=null&&frame.Palette.Colors.Any(x=>x.A<255))?L.Text("details.yes"):L.Text("details.no"));d.Add("depth",frame.Format.BitsPerPixel>0?frame.Format.BitsPerPixel+" bpp":null);
                d.Add("frames",decoder.Frames.Count);d.Add("colorProfile",frame.ColorContexts!=null&&frame.ColorContexts.Count>0?L.Text("details.present"):null);
            }}catch(Exception){}
            try{using(var file=TagLib.File.Create(path,TagLib.ReadStyle.None)){
                var image=file.Tag as TagLib.Image.CombinedImageTag;if(image==null)return;
                foreach(var pair in new[]{new[]{"taken","DateTime"},new[]{"cameraMake","Make"},new[]{"camera","Model"},new[]{"lens","LensModel"},new[]{"iso","ISOSpeedRatings"},new[]{"aperture","FNumber"},new[]{"focalLength","FocalLength"},new[]{"orientation","Orientation"},new[]{"latitude","Latitude"},new[]{"longitude","Longitude"},new[]{"software","Software"},new[]{"copyright","Copyright"},new[]{"comment","Comment"}})d.Add(pair[0],Prop(image,pair[1]));
                object exposure=Prop(image,"ExposureTime");double seconds;if(Double.TryParse(Convert.ToString(exposure,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out seconds)&&seconds>0)d.Add("exposure",seconds<1?"1/"+Math.Round(1/seconds)+" s":seconds.ToString("0.###")+" s");
            }}catch(Exception){}
        }
        static void Media(string path,FileDetails d) {
            using(var file=TagLib.File.Create(path,TagLib.ReadStyle.Average)) {
                var p=file.Properties;d.Summary=Path.GetExtension(path).TrimStart('.').ToUpperInvariant()+" · "+p.Duration.ToString(@"hh\:mm\:ss");
                d.Add("duration",p.Duration>TimeSpan.Zero?(object)p.Duration:null);d.Add("title",file.Tag.Title);d.Add("artist",String.Join(", ",file.Tag.Performers??new string[0]));d.Add("album",file.Tag.Album);d.Add("genre",String.Join(", ",file.Tag.Genres??new string[0]));if(file.Tag.Year>0)d.Add("year",file.Tag.Year);if(file.Tag.Track>0)d.Add("track",file.Tag.Track);
                foreach(var pair in new[]{new[]{"audioBitrate","AudioBitrate"},new[]{"sampleRate","AudioSampleRate"},new[]{"channels","AudioChannels"},new[]{"videoWidth","VideoWidth"},new[]{"videoHeight","VideoHeight"},new[]{"fps","VideoFrameRate"},new[]{"description","Description"}}){object value=Prop(p,pair[1]);if(value!=null&&Convert.ToString(value)!="0")d.Add(pair[0],value);}
            }
        }
    }
}


