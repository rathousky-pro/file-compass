using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading;using System.Runtime.InteropServices;using System.Text;
namespace FileCompass {
    internal static class ArchiveSelfExtract {
        internal static bool Supports(string format){return new[]{"7z","zip","rar","arj"}.Contains(format);}
        internal static string Module(string format,ArchiveSettings settings){
            if(format=="7z")return UpdateRegistry.Resolve(@"7zip\7z.sfx");
            if(format=="arj")return settings.LegacyExecutable(true);
            if(format=="zip"||format=="rar"){string exe=settings.RarExecutable();string file=Path.Combine(Path.GetDirectoryName(exe),format=="zip"?"Zip.SFX":"Default.SFX");if(!File.Exists(file))throw new ArchiveToolMissingException(L.Text("archive.sfxMissingModule")+" "+file);return file;}
            throw new IOException(L.Text("archive.sfxUnsupported"));
        }
        internal static void Validate(string format,ArchiveSettings settings){if(!Supports(format))throw new IOException(L.Text("archive.sfxUnsupported"));string module=Module(format,settings);if(format=="zip"&&!File.Exists(Path.Combine(Path.GetDirectoryName(module),"WinRAR.exe")))throw new ArchiveToolMissingException(L.Text("archive.sfxMissingModule")+" WinRAR.exe");if(!File.Exists(module))throw new ArchiveToolMissingException(L.Text("archive.sfxMissingModule")+" "+module);PathSafety.RejectLinks(module);}
        internal static void Create(string target,IEnumerable<string> input,ArchiveSettings settings,CancellationToken token,Action<string> progress,ArchivePackOptions options){
            Validate(options.Format,settings);if(options.VolumeBytes>0)throw new IOException(L.Text("archive.sfxVolumeFormat"));
            target=Path.GetFullPath(target);if(Path.GetExtension(target).ToLowerInvariant()!=".exe")throw new IOException(L.Text("archive.sfxExeRequired"));var sources=input.Select(Path.GetFullPath).ToArray();foreach(var source in sources)if(WindowsShell.SamePath(source,target)||(Directory.Exists(source)&&WindowsShell.IsWithin(target,source)))throw new IOException(L.Text("archive.outputInsideInputRejected"));
            string before=File.Exists(target)?FileParts.Hash(target,token):null,stage=Path.Combine(Path.GetDirectoryName(target),".FileCompass-"+Guid.NewGuid().ToString("N")+".exe");
            using(var temp=new ArchiveTemp())try{
                var plain=ArchiveExtended.PlainOptions(options);plain.SelfExtracting=false;plain.Move=false;plain.VolumeBytes=0;
                string archive=Path.Combine(temp.Path,"payload."+options.Format);ArchiveEngine.Create(archive,sources,settings,token,progress,plain);
                if(progress!=null)progress(L.Text("archive.sfxBuilding"));
                if(options.Format=="zip"){string tool=Path.Combine(Path.GetDirectoryName(settings.RarExecutable()),"WinRAR.exe");ArchiveEngine.Run(tool,new[]{"s","-cfg-","-sfx","-ibck","-y","payload.zip"},temp.Path,token,progress);File.Copy(Path.ChangeExtension(archive,"exe"),stage);}
                else if(options.Format=="arj"){ArchiveEngine.Run(settings.LegacyExecutable(true),new[]{"y","-+","-y","-je","payload.arj"},temp.Path,token,progress);using(var reader=new BinaryReader(File.OpenRead(Path.ChangeExtension(archive,"exe")))){reader.BaseStream.Position=0x3c;int pe=reader.ReadInt32();if(pe<64||pe>reader.BaseStream.Length-4)throw new IOException(L.Text("archive.sfxUnsupported"));reader.BaseStream.Position=pe;if(reader.ReadUInt32()!=0x4550)throw new IOException(L.Text("archive.sfxUnsupported"));}File.Copy(Path.ChangeExtension(archive,"exe"),stage);}
                else {
                    using(var output=new FileStream(stage,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                        if(options.Format=="7z"){using(var module=PluginIntegrity.OpenVerified(@"7zip\7z.sfx"))Copy(module,output,token);}
                        else using(var module=File.OpenRead(Module(options.Format,settings)))Copy(module,output,token);
                        using(var payload=File.OpenRead(archive))Copy(payload,output,token);
                    }
                }
                if(progress!=null)progress(L.Text("archive.verifying"));var doc=ArchiveEngine.Open(stage,options.Password,token);ArchiveEngine.Test(doc,token,progress);token.ThrowIfCancellationRequested();File.SetLastWriteTimeUtc(stage,File.GetLastWriteTimeUtc(archive));
                if(before!=null){if(!File.Exists(target)||FileParts.Hash(target,token)!=before)throw new IOException(L.Text("archive.destinationChanged"));File.Replace(stage,target,null);}else File.Move(stage,target);
            }finally{if(File.Exists(stage))File.Delete(stage);}
        }
        static void Copy(Stream input,Stream output,CancellationToken token){var buffer=new byte[1048576];int read;while((read=input.Read(buffer,0,buffer.Length))>0){OperationPause.Wait(token);output.Write(buffer,0,read);}}
        internal static IEnumerable<string> Split(string complete,string target,long volume,CancellationToken token,Action<string> progress){
            using(var temp=new ArchiveTemp()){string payload=Path.Combine(temp.Path,"payload.7z");using(var module=PluginIntegrity.OpenVerified(@"7zip\7z.sfx"))using(var input=File.OpenRead(complete))using(var output=File.Create(payload)){input.Position=module.Length;Copy(input,output,token);}return FileParts.Split(payload,Path.ChangeExtension(target,"7z"),volume,token,progress).ToArray();}
        }
        internal static string ExecutablePath(string original){if(!Path.GetFileName(original).Equals("arj32.exe",StringComparison.OrdinalIgnoreCase))return original;var shortName=new StringBuilder(32768);return GetShortPathName(original,shortName,shortName.Capacity)>0?shortName.ToString():original;}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetShortPathName(string input,StringBuilder output,int length);
    }
}