using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FileCompass
{
    // Exposes one data track as a seekable 2048-byte-sector stream without copying the image.
    internal static class DiscImageSource
    {
        sealed class Track { internal string File, Mode; internal bool Binary; internal long Index = -1; }
        internal sealed class CueTrack { internal string File; internal int Sector, Offset; internal long Start, Length; internal bool HasAudio; }
        internal static bool IsCue(string path) { return Path.GetExtension(path).Equals(".cue", StringComparison.OrdinalIgnoreCase); }
        internal static string AssociatedCue(string path) { if (!Path.GetExtension(path).Equals(".bin",StringComparison.OrdinalIgnoreCase)) return null; string cue=Path.ChangeExtension(path,".cue");if(!File.Exists(cue))return null;try{return String.Equals(ReadCue(cue).File,Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase)?cue:null;}catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}catch(ArgumentException){return null;} }
        internal static CueTrack ReadCue(string path)
        {
            var info=new FileInfo(path); if(info.Length>1048576)throw new IOException(L.Text("discImage.invalidCue"));
            string baseDir=Path.GetDirectoryName(Path.GetFullPath(path)); var tracks=new List<Track>();string dataFile=null;bool binary=false; Track current=null;
            foreach(string raw in File.ReadAllLines(path,Encoding.UTF8)) {
                string line=raw.Trim();if(line.Length==0||line.StartsWith("REM ",StringComparison.OrdinalIgnoreCase))continue;
                var file=Regex.Match(line,"^FILE\\s+(?:\"([^\"]+)\"|(\\S+))\\s+(\\S+)\\s*$",RegexOptions.IgnoreCase);
                if(file.Success){string relative=file.Groups[1].Success?file.Groups[1].Value:file.Groups[2].Value;if(Path.IsPathRooted(relative)||relative.IndexOf(':')>=0)throw new IOException(L.Text("discImage.unsafeCue"));
                    string full=Path.GetFullPath(Path.Combine(baseDir,relative));if(!full.StartsWith(baseDir.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))throw new IOException(L.Text("discImage.unsafeCue"));dataFile=full;binary=file.Groups[3].Value.Equals("BINARY",StringComparison.OrdinalIgnoreCase);continue;}
                var track=Regex.Match(line,"^TRACK\\s+\\d+\\s+(\\S+)\\s*$",RegexOptions.IgnoreCase);
                if(track.Success){if(dataFile==null)throw new IOException(L.Text("discImage.invalidCue"));current=new Track{File=dataFile,Mode=track.Groups[1].Value.ToUpperInvariant(),Binary=binary};tracks.Add(current);continue;}
                var index=Regex.Match(line,"^INDEX\\s+01\\s+(\\d+):(\\d{2}):(\\d{2})\\s*$",RegexOptions.IgnoreCase);
                if(index.Success){if(current==null||current.Index>=0)throw new IOException(L.Text("discImage.invalidCue"));long minute=Int64.Parse(index.Groups[1].Value,CultureInfo.InvariantCulture),second=Int64.Parse(index.Groups[2].Value,CultureInfo.InvariantCulture),frame=Int64.Parse(index.Groups[3].Value,CultureInfo.InvariantCulture);if(second>=60||frame>=75||minute>100000)throw new IOException(L.Text("discImage.invalidCue"));current.Index=(minute*60+second)*75+frame;}
            }
            var data=tracks.Where(x=>x.Mode!="AUDIO").ToArray();if(data.Length!=1||tracks.Any(x=>x.Index<0))throw new IOException(L.Text("discImage.unsupportedTracks"));
            var selected=data[0];if(!selected.Binary)throw new IOException(L.Text("discImage.unsupportedTracks"));int sector,offset;
            switch(selected.Mode){case "MODE1/2048":sector=2048;offset=0;break;case "MODE1/2352":sector=2352;offset=16;break;case "MODE2/2352":sector=2352;offset=24;break;case "MODE2/2336":sector=2336;offset=8;break;default:throw new IOException(L.Text("discImage.unsupportedTracks"));}
            PathSafety.RejectLinks(selected.File);var source=new FileInfo(selected.File);if(!source.Exists||source.Length%sector!=0)throw new IOException(L.Text("discImage.invalidCue"));
            long sectors=source.Length/sector;int pos=tracks.IndexOf(selected);for(int i=pos+1;i<tracks.Count;i++){if(!String.Equals(tracks[i].File,selected.File,StringComparison.OrdinalIgnoreCase))continue;if(tracks[i].Mode!="AUDIO"||sector!=2352)throw new IOException(L.Text("discImage.unsupportedTracks"));sectors=tracks[i].Index;break;}
            if(selected.Index<0||selected.Index>=sectors||sectors>source.Length/sector)throw new IOException(L.Text("discImage.invalidCue"));
            return new CueTrack{File=selected.File,Sector=sector,Offset=offset,Start=selected.Index,Length=sectors-selected.Index,HasAudio=tracks.Any(x=>x.Mode=="AUDIO")};
        }
        internal static string CueStamp(string path){var track=ReadCue(path);var info=new FileInfo(track.File);return "|cue:"+track.File+":"+info.Length+":"+info.LastWriteTimeUtc.Ticks+":"+info.CreationTimeUtc.Ticks;}
        internal static Stream OpenCue(string path){var track=ReadCue(path);return new SectorStream(track);}
        internal static bool IsRawImage(string path)
        {
            try {using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                if(input.Length>=32774){input.Position=32769;var magic=new byte[5];if(input.Read(magic,0,5)==5&&(Encoding.ASCII.GetString(magic)=="CD001"||Encoding.ASCII.GetString(magic)=="BEA01"))return true;}
                if(input.Length>=1082){input.Position=1080;if(input.ReadByte()==0x53&&input.ReadByte()==0xef)return true;}
                if(input.Length>=37){input.Position=32;var magic=new byte[5];if(input.Read(magic,0,5)==5&&magic.SequenceEqual(new byte[]{(byte)'N',(byte)'X',(byte)'S',(byte)'B',0}))return true;}
                if(input.Length<512)return false;input.Position=0;var boot=new byte[512];if(input.Read(boot,0,boot.Length)!=boot.Length||boot[510]!=0x55||boot[511]!=0xaa)return false;
                if(Encoding.ASCII.GetString(boot,3,8)=="NTFS    ")return true;
                int bytes=boot[11]|boot[12]<<8,cluster=boot[13],reserved=boot[14]|boot[15]<<8;if((boot[0]==0xeb||boot[0]==0xe9)&&new[]{512,1024,2048,4096}.Contains(bytes)&&cluster>0&&(cluster&(cluster-1))==0&&reserved>0&&boot[16]>0)return true;
                for(int i=0;i<4;i++){int p=446+i*16;if((boot[p]==0||boot[p]==0x80)&&boot[p+4]!=0&&BitConverter.ToUInt32(boot,p+8)>0&&BitConverter.ToUInt32(boot,p+12)>0)return true;}return false;
            }}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(ArgumentException){return false;}
        }
        sealed class SectorStream : Stream
        {
            readonly FileStream input;readonly CueTrack track;long position;
            internal SectorStream(CueTrack source){track=source;input=new FileStream(source.File,FileMode.Open,FileAccess.Read,FileShare.Read);}
            public override bool CanRead{get{return true;}}public override bool CanSeek{get{return true;}}public override bool CanWrite{get{return false;}}
            public override long Length{get{return checked(track.Length*2048);}}public override long Position{get{return position;}set{Seek(value,SeekOrigin.Begin);}}
            public override int Read(byte[] buffer,int offset,int count){if(buffer==null)throw new ArgumentNullException("buffer");if(offset<0||count<0||offset>buffer.Length-count)throw new ArgumentOutOfRangeException();int total=0;
                while(count>0&&position<Length){long sector=position/2048;int within=(int)(position%2048),take=(int)Math.Min(Math.Min(count,2048-within),Length-position);input.Position=checked((track.Start+sector)*track.Sector+track.Offset+within);int read=input.Read(buffer,offset,take);if(read<=0)throw new EndOfStreamException();position+=read;offset+=read;count-=read;total+=read;}return total;}
            public override long Seek(long offset,SeekOrigin origin){long next=checked((origin==SeekOrigin.Begin?0:origin==SeekOrigin.Current?position:Length)+offset);if(next<0)throw new IOException();return position=next;}
            protected override void Dispose(bool disposing){if(disposing)input.Dispose();base.Dispose(disposing);}public override void Flush(){}public override void SetLength(long value){throw new NotSupportedException();}public override void Write(byte[] buffer,int offset,int count){throw new NotSupportedException();}
        }
    }
}
