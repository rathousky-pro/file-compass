using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Diagnostics;
namespace FileCompass {
    internal static partial class ArchiveWorker {
        static DateTimeOffset ZipTime(DateTime value) { return new DateTimeOffset(value.Year<1980?new DateTime(1980,1,1):value.Year>2107?new DateTime(2107,12,31):value); }
        static void WritePlainZip(string archive,string staging,string[] directFiles,string[] args,ArchiveBudget budget,Action<string> progress) {
            string[] files=directFiles??Directory.GetFiles(staging,"*",SearchOption.AllDirectories);
            long total=files.Sum(x=>new FileInfo(x).Length),processed=0;
            string option=args.FirstOrDefault(x=>x.StartsWith("-mx=",StringComparison.Ordinal));int level=option==null?6:Int32.Parse(option.Substring(4));
            var compression=level<=0?CompressionLevel.NoCompression:level<=3?CompressionLevel.Fastest:CompressionLevel.Optimal;
            var clock=Stopwatch.StartNew();bool reported=false;var buffer=new byte[1048576];
            using(var output=new FileStream(archive,FileMode.Create,FileAccess.ReadWrite,FileShare.None))
            using(var bounded=new ArchiveOutputStream(output,budget,archive,false)) {
                using(var zip=new ZipArchive(bounded,ZipArchiveMode.Create,true,System.Text.Encoding.UTF8)) {
                    if(directFiles==null) foreach(var folder in Directory.GetDirectories(staging,"*",SearchOption.AllDirectories)) {
                        budget.Check();string name=folder.Substring(staging.TrimEnd(Path.DirectorySeparatorChar).Length+1).Replace(Path.DirectorySeparatorChar,'/')+"/";
                        var entry=zip.CreateEntry(name);entry.LastWriteTime=ZipTime(Directory.GetLastWriteTime(folder));entry.ExternalAttributes=(int)FileAttributes.Directory;
                    }
                    foreach(var file in files) {
                        budget.Check();RejectLinks(file);
                        string name=directFiles!=null?Path.GetFileName(file):file.Substring(staging.TrimEnd(Path.DirectorySeparatorChar).Length+1).Replace(Path.DirectorySeparatorChar,'/');
                        var entry=zip.CreateEntry(name,compression);entry.LastWriteTime=ZipTime(File.GetLastWriteTime(file));entry.ExternalAttributes=(int)File.GetAttributes(file);
                        if(!reported||clock.ElapsedMilliseconds>=100){reported=true;clock.Restart();progress(new ArchiveCompressionProgress{Original=total,Processed=processed,Packed=output.Length,Percent=total==0?0:Math.Min(99,100.0*processed/total),CurrentFile=name}.Encode());}
                        using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))
                        using(var destination=entry.Open()) {
                            int read;
                            while((read=input.Read(buffer,0,buffer.Length))>0) {
                                budget.Check();destination.Write(buffer,0,read);processed+=read;
                                if(!reported||clock.ElapsedMilliseconds>=100){reported=true;clock.Restart();progress(new ArchiveCompressionProgress{Original=total,Processed=processed,Packed=output.Length,Percent=total==0?0:Math.Min(99,100.0*processed/total),CurrentFile=name}.Encode());}
                            }
                        }
                    }
                }
                progress(new ArchiveCompressionProgress{Original=total,Processed=processed,Packed=output.Length,Percent=99}.Encode());
            }
        }
    }
}

