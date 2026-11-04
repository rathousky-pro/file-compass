using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace FileCompass.Updating {
 public sealed class ReleaseFile { public string Relative,Hash; public long Size; }
 public sealed class ReleaseManifest { public int Format=1;public string Version,Channel,Build;public List<ReleaseFile> Files=new List<ReleaseFile>(); }
 public sealed class InstallRequest { public int ParentId;public long ParentStarted;public string Target,Package,OriginalHash,ManifestHash;public ReleaseManifest Release; }
 public sealed class RestoreFile {public string Relative,Hash;public bool Existed;}
 public static class UpdateContract {
  public const string ManifestName="file-compass-release.json",Executable="File Compass.exe";
  public static bool ValidIdentity(string version,string channel,string build){Version v;DateTime day;return Regex.IsMatch(version??"",@"^\d{1,4}\.\d{1,4}\.\d{1,4}$")&&System.Version.TryParse(version,out v)&&new[]{"","alpha","beta","rc"}.Contains(channel??"")&&Regex.IsMatch(build??"",@"^\d{8}\.[0-9]{2,9}$")&&DateTime.TryParseExact(build.Substring(0,8),"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day);}
  public static int Compare(string version,string channel,string build,string otherVersion,string otherChannel,string otherBuild){
   if(!ValidIdentity(version,channel,build)||!ValidIdentity(otherVersion,otherChannel,otherBuild))throw new IOException("Invalid release identity.");int result=new Version(version).CompareTo(new Version(otherVersion));if(result!=0)return result;string[] channels={"alpha","beta","rc",""};result=Array.IndexOf(channels,channel??"").CompareTo(Array.IndexOf(channels,otherChannel??""));if(result!=0)return result;result=String.CompareOrdinal(build.Substring(0,8),otherBuild.Substring(0,8));return result!=0?result:Int32.Parse(build.Substring(9),CultureInfo.InvariantCulture).CompareTo(Int32.Parse(otherBuild.Substring(9),CultureInfo.InvariantCulture));
  }
  public static string Information(ReleaseManifest manifest){return manifest.Version+(String.IsNullOrEmpty(manifest.Channel)?"":"-"+manifest.Channel)+"+"+manifest.Build;}
  public static void RejectLinks(string path){for(var current=new DirectoryInfo(Path.GetFullPath(path));current!=null;current=current.Parent)if((Directory.Exists(current.FullName)||File.Exists(current.FullName))&&(File.GetAttributes(current.FullName)&FileAttributes.ReparsePoint)!=0)throw new IOException("Update path contains a link: "+current.FullName);}
  public static string SafePath(string root,string relative){
   if(String.IsNullOrEmpty(relative)||relative.Length>220||relative.Contains("\\")||Path.IsPathRooted(relative)||relative.Split('/').Any(x=>x.Length==0||x=="."||x==".."||x.EndsWith(".")||x.EndsWith(" ")||x.IndexOfAny(Path.GetInvalidFileNameChars())>=0||Regex.IsMatch(x,@"^(?i:CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)")))throw new IOException("Invalid update path: "+relative);
   string full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));if(!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Update path escapes its folder.");RejectLinks(full);return full;
  }
  public static bool AllowedFile(string relative){string first=relative.Split('/')[0];return relative==Executable||relative==Executable+".config"||relative==ManifestName||new[]{"libraries","plugins","preview","icons","languages","help"}.Contains(first)&&relative.Contains('/');}
  public static string Hash(string path){using(var file=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
  public static T Read<T>(string path){if(new FileInfo(path).Length>8L*1048576)throw new IOException("Update metadata exceeds its limit.");return new JavaScriptSerializer{MaxJsonLength=8*1048576}.Deserialize<T>(File.ReadAllText(path));}
  public static void Write<T>(string path,T value){string temp=path+".tmp";File.WriteAllText(temp,new JavaScriptSerializer{MaxJsonLength=8*1048576}.Serialize(value),new System.Text.UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
  public static void Validate(ReleaseManifest manifest){
   if(manifest==null||manifest.Format!=1||!ValidIdentity(manifest.Version,manifest.Channel,manifest.Build)||manifest.Files==null||manifest.Files.Count<1||manifest.Files.Count>20000)throw new IOException("Invalid release manifest.");var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
   foreach(var entry in manifest.Files){if(entry==null||!AllowedFile(entry.Relative??"")||entry.Relative==ManifestName||!seen.Add(entry.Relative)||entry.Size<0||entry.Size>768L*1048576||!Regex.IsMatch(entry.Hash??"",@"^[a-f0-9]{64}$"))throw new IOException("Invalid release file.");SafePath(Path.GetTempPath(),entry.Relative);total=checked(total+entry.Size);}
   if(total>3L*1024*1048576||!seen.Contains(Executable))throw new IOException("Release is incomplete or too large.");
  }
  public static void Verify(string folder,ReleaseManifest manifest){Validate(manifest);foreach(var entry in manifest.Files){string path=SafePath(folder,entry.Relative);if(!File.Exists(path)||new FileInfo(path).Length!=entry.Size||Hash(path)!=entry.Hash)throw new IOException("Release verification failed: "+entry.Relative);}string exe=SafePath(folder,Executable);using(var input=File.OpenRead(exe))using(var reader=new BinaryReader(input)){if(input.Length<128||reader.ReadUInt16()!=0x5a4d)throw new IOException("Invalid release executable.");input.Position=0x3c;int offset=reader.ReadInt32();if(offset<64||offset>input.Length-6)throw new IOException("Invalid release executable.");input.Position=offset;if(reader.ReadUInt32()!=0x4550||reader.ReadUInt16()!=0x8664)throw new IOException("Release executable is not Windows x64.");}if(FileVersionInfo.GetVersionInfo(exe).ProductVersion!=Information(manifest))throw new IOException("Executable version/build does not match the release manifest.");}
 }
}
