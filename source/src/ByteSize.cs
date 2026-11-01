using System;
namespace FileCompass {
 internal static class ByteSize {
  internal static string Mode="IEC";
  internal static string Normalize(string mode){return mode=="SI"||mode=="Bytes"?mode:"IEC";}
  internal static string Format(decimal bytes){if(Normalize(Mode)=="Bytes")return bytes.ToString("N0")+" B";decimal step=Mode=="SI"?1000m:1024m;var units=Mode=="SI"?new[]{"B","kB","MB","GB","TB","PB"}:new[]{"B","KiB","MiB","GiB","TiB","PiB"};int unit=0;while(Math.Abs(bytes)>=step&&unit<units.Length-1){bytes/=step;unit++;}return bytes.ToString(unit==0?"N0":"N2")+" "+units[unit];}
  internal static string Format(long bytes){return Format(bytes,Mode);}
  internal static string Format(long bytes,string mode){mode=Normalize(mode);if(mode=="Bytes")return bytes.ToString("N0")+" B";double value=bytes,step=mode=="SI"?1000:1024;var units=mode=="SI"?new[]{"B","kB","MB","GB","TB","PB"}:new[]{"B","KiB","MiB","GiB","TiB","PiB"};int unit=0;while(Math.Abs(value)>=step&&unit<units.Length-1){value/=step;unit++;}return value.ToString(unit==0?"N0":"N2")+" "+units[unit];}
 }
}
