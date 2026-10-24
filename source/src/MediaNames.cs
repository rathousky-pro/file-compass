using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
namespace FileCompass {
 internal sealed class MediaMetadataRow {
  public string Path{get;set;} public string Name{get{return System.IO.Path.GetFileName(Path);}} public string OldValue{get;set;}public string NewValue{get;set;}public string Status{get;set;}public string Backup{get;set;}
  internal bool Applied;internal string Hash;internal Dictionary<string,string> Changes;
 }
 internal static class MediaNames {
  internal static readonly string[] ImageExtensions={".jpg",".jpeg",".png",".tif",".tiff"};
  internal static readonly string[] AudioExtensions={".mp3",".flac",".m4a",".m4b",".ogg",".opus",".wav",".aif",".aiff",".wma"};
  internal static readonly string[] WritableTokens=RenameMetadata.Tokens.Where(x=>x!="[EXIF.Camera]").ToArray();
  internal static bool IsImage(string path){return ImageExtensions.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());}
  internal static bool Supported(string path){return IsImage(path)||AudioExtensions.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());}
  internal static Dictionary<string,string> Parse(string name,string mask){
   if(String.IsNullOrWhiteSpace(mask)||mask.Length>2048)throw new IOException(L.Text("mediaNames.badMask"));
   var pattern=new StringBuilder("\\A");int start=0,index=0;var fields=new Dictionary<string,string>();
   foreach(Match m in Regex.Matches(mask,@"\[[^\[\]]+\]")){
    string token=m.Value;if(token!="[Ignore]"&&!WritableTokens.Contains(token))throw new IOException(L.Text("mediaNames.badToken")+" "+token);
    if(token!="[Ignore]"&&fields.Values.Contains(token))throw new IOException(L.Text("mediaNames.duplicateToken"));
    if(m.Index==start&&start>0)throw new IOException(L.Text("mediaNames.separator"));
    string group="f"+index++;if(index>20)throw new IOException(L.Text("mediaNames.badMask"));
    pattern.Append(Regex.Escape(mask.Substring(start,m.Index-start))).Append("(?<"+group+">.+?)");fields.Add(group,token);start=m.Index+m.Length;
   }
   if(fields.Count==0||fields.Values.All(x=>x=="[Ignore]"))throw new IOException(L.Text("mediaNames.badMask"));
   pattern.Append(Regex.Escape(mask.Substring(start))).Append("\\z");var match=Regex.Match(name,pattern.ToString(),RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));if(!match.Success)throw new IOException(L.Text("mediaNames.noMatch"));
   var values=new Dictionary<string,string>();foreach(var pair in fields){if(pair.Value=="[Ignore]")continue;string value=match.Groups[pair.Key].Value.Trim();if(value.Length==0||value.Length>4096)throw new IOException(L.Text("mediaNames.emptyValue"));
    if(new[]{"[MP3.Track]","[MP3.Disc]","[MP3.Year]"}.Contains(pair.Value)){uint number;if(!UInt32.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out number)||number==0||number>(pair.Value=="[MP3.Year]"?9999u:99999u))throw new IOException(L.Text("mediaNames.badNumber")+" "+pair.Value);value=number.ToString(CultureInfo.InvariantCulture);}
    if(pair.Value=="[EXIF.Date]"){DateTime date;if(!DateTime.TryParseExact(value,new[]{"yyyy-MM-dd","yyyyMMdd"},CultureInfo.InvariantCulture,DateTimeStyles.None,out date))throw new IOException(L.Text("mediaNames.badDate"));value=date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);}
    if(pair.Value=="[EXIF.Time]"){DateTime time;if(!DateTime.TryParseExact(value,new[]{"HH-mm-ss","HHmmss"},CultureInfo.InvariantCulture,DateTimeStyles.None,out time))throw new IOException(L.Text("mediaNames.badDate"));value=time.ToString("HH-mm-ss",CultureInfo.InvariantCulture);}
    values.Add(pair.Value,value);
   }return values;
  }
  internal static Dictionary<string,string> Read(TagLib.File file){
   var t=file.Tag;var result=new Dictionary<string,string>{{"[MP3.Title]",t.Title??""},{"[MP3.Artist]",String.Join("; ",t.Performers??new string[0])},{"[MP3.Album]",t.Album??""},{"[MP3.Genre]",String.Join("; ",t.Genres??new string[0])},{"[MP3.Composer]",String.Join("; ",t.Composers??new string[0])},{"[MP3.Track]",t.Track==0?"":t.Track.ToString(CultureInfo.InvariantCulture)},{"[MP3.Disc]",t.Disc==0?"":t.Disc.ToString(CultureInfo.InvariantCulture)},{"[MP3.Year]",t.Year==0?"":t.Year.ToString(CultureInfo.InvariantCulture)}};
   var image=t as TagLib.Image.CombinedImageTag;if(image!=null){result["[Image.Title]"]=image.Title??"";result["[Image.Author]"]=image.Creator??"";result["[Image.Comment]"]=image.Comment??"";result["[Image.Copyright]"]=image.Copyright??"";result["[EXIF.Make]"]=image.Make??"";result["[EXIF.Model]"]=image.Model??"";result["[EXIF.Date]"]=image.DateTime.HasValue?image.DateTime.Value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture):"";result["[EXIF.Time]"]=image.DateTime.HasValue?image.DateTime.Value.ToString("HH-mm-ss",CultureInfo.InvariantCulture):"";}return result;
  }
  internal static string Hash(Stream stream,CancellationToken token){stream.Position=0;using(var sha=SHA256.Create()){byte[] buffer=new byte[1048576];int read;while((read=stream.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();sha.TransformBlock(buffer,0,read,null,0);}sha.TransformFinalBlock(new byte[0],0,0);return Convert.ToBase64String(sha.Hash);}}
  static void ValidatePath(string path){if(!Supported(path)||!File.Exists(path))throw new IOException(L.Text("mediaNames.unsupported"));PathSafety.RejectLinks(path);if((File.GetAttributes(path)&FileAttributes.ReadOnly)!=0)throw new IOException(L.Text("mediaNames.readOnly"));if(new FileInfo(path).Length>1024L*1048576)throw new IOException(L.Text("mediaNames.sizeLimit"));}
  internal static MediaMetadataRow[] Preview(string[] paths,string mask,bool overwrite,CancellationToken token){
   if(paths.Length>500)throw new IOException(L.Text("mediaNames.batchLimit"));return paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(path=>PreviewOne(path,mask,overwrite,token)).ToArray();
  }
  internal static MediaMetadataRow PreviewOne(string path,string mask,bool overwrite,CancellationToken token){
   var row=new MediaMetadataRow{Path=path};try{token.ThrowIfCancellationRequested();ValidatePath(path);var wanted=Parse(System.IO.Path.GetFileNameWithoutExtension(path),mask);
    if(wanted.Keys.Any(x=>IsImage(path)?x.StartsWith("[MP3."):!x.StartsWith("[MP3.")))throw new IOException(L.Text("mediaNames.wrongType"));
    using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
     row.Hash=Hash(input,token);using(var abstraction=new RenameMetadata.MetadataSource(path,token))using(var file=TagLib.File.Create(abstraction,TagLib.ReadStyle.None)){
      var old=Read(file);row.Changes=new Dictionary<string,string>();var previous=new List<string>();var next=new List<string>();
      foreach(var pair in wanted){string value;if(!old.TryGetValue(pair.Key,out value))throw new IOException(L.Text("mediaNames.unsupported"));if(!overwrite&&!String.IsNullOrWhiteSpace(value)||value==pair.Value)continue;row.Changes.Add(pair.Key,pair.Value);previous.Add(pair.Key+" = "+(String.IsNullOrEmpty(value)?"∅":value.Length>1000?value.Substring(0,1000)+"…":value));next.Add(pair.Key+" = "+pair.Value);}
      if(row.Changes.ContainsKey("[EXIF.Time]")&&!row.Changes.ContainsKey("[EXIF.Date]")&&String.IsNullOrEmpty(old["[EXIF.Date]"]))throw new IOException(L.Text("mediaNames.timeNeedsDate"));
      row.OldValue=String.Join("\n",previous);row.NewValue=String.Join("\n",next);row.Status=L.Text(row.Changes.Count==0?"mediaNames.noChange":"mediaNames.ready");
     }
    }
   }catch(OperationCanceledException){throw;}catch(Exception ex){row.Changes=null;row.Status=ex.Message;}return row;
  }
  static void Set(TagLib.File file,Dictionary<string,string> changes){
   var image=file.Tag as TagLib.Image.CombinedImageTag;if(image!=null){file.GetTag(TagLib.TagTypes.XMP,true);image=file.Tag as TagLib.Image.CombinedImageTag;}
   DateTime? taken=image==null?null:image.DateTime;
   foreach(var pair in changes){string v=pair.Value;switch(pair.Key){
    case "[MP3.Title]":file.Tag.Title=v;break;case "[MP3.Artist]":file.Tag.Performers=new[]{v};break;case "[MP3.Album]":file.Tag.Album=v;break;case "[MP3.Genre]":file.Tag.Genres=new[]{v};break;case "[MP3.Composer]":file.Tag.Composers=new[]{v};break;case "[MP3.Track]":file.Tag.Track=UInt32.Parse(v,CultureInfo.InvariantCulture);break;case "[MP3.Disc]":file.Tag.Disc=UInt32.Parse(v,CultureInfo.InvariantCulture);break;case "[MP3.Year]":file.Tag.Year=UInt32.Parse(v,CultureInfo.InvariantCulture);break;
    case "[Image.Title]":image.Title=v;break;case "[Image.Author]":image.Creator=v;break;case "[Image.Comment]":image.Comment=v;break;case "[Image.Copyright]":image.Copyright=v;break;case "[EXIF.Make]":image.Make=v;break;case "[EXIF.Model]":image.Model=v;break;
   }}
   string date,time;if(changes.TryGetValue("[EXIF.Date]",out date))taken=DateTime.ParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture)+(taken.HasValue?taken.Value.TimeOfDay:TimeSpan.Zero);
   if(changes.TryGetValue("[EXIF.Time]",out time)){if(!taken.HasValue)throw new IOException(L.Text("mediaNames.timeNeedsDate"));taken=taken.Value.Date+DateTime.ParseExact(time,"HH-mm-ss",CultureInfo.InvariantCulture).TimeOfDay;}
   if(image!=null&&(changes.ContainsKey("[EXIF.Date]")||changes.ContainsKey("[EXIF.Time]")))image.DateTime=taken;
  }
  internal static void Apply(MediaMetadataRow row,CancellationToken token){
   if(row.Changes==null||row.Changes.Count==0)return;ValidatePath(row.Path);string folder=System.IO.Path.GetDirectoryName(row.Path),temp=System.IO.Path.Combine(folder,".filecompass-metadata-"+Guid.NewGuid().ToString("N")+System.IO.Path.GetExtension(row.Path)),backup=row.Path+".filecompass-metadata-"+Guid.NewGuid().ToString("N")+".bak";
   bool ownTemp=false;using(PathSafety.LockAncestors(row.Path))try{
    using(var input=new FileStream(row.Path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)){
     if(Hash(input,token)!=row.Hash)throw new IOException(L.Text("mediaNames.changed"));var identity=UndoReceipt.Capture(row.Path,null,false);if(!identity.Matches(SafeUndo.Info(input.SafeFileHandle)))throw new IOException(L.Text("mediaNames.changed"));
     input.Position=0;using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){ownTemp=true;byte[] buffer=new byte[1048576];int n;while((n=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();output.Write(buffer,0,n);}output.Flush(true);}
     using(var file=TagLib.File.Create(temp,TagLib.ReadStyle.None)){Set(file,row.Changes);file.Save();}
     using(var file=TagLib.File.Create(temp,TagLib.ReadStyle.None)){var actual=Read(file);foreach(var pair in row.Changes)if(!actual.ContainsKey(pair.Key)||actual[pair.Key]!=pair.Value)throw new IOException(L.Text("mediaNames.notSaved")+" "+pair.Key);}
     AttachmentSecurity.WriteZone(temp,AttachmentSecurity.ReadZone(row.Path));File.SetAttributes(temp,File.GetAttributes(row.Path)&(FileAttributes.Hidden|FileAttributes.System|FileAttributes.Archive));
     token.ThrowIfCancellationRequested();using(var current=SafeUndo.Open(row.Path,false))if(!identity.Matches(SafeUndo.Info(current)))throw new IOException(L.Text("mediaNames.changed"));
     var replacement=UndoReceipt.Capture(temp,null,false);File.Replace(temp,row.Path,backup);ownTemp=false;row.Backup=backup;
     using(var saved=SafeUndo.Open(backup,false))if(!identity.Matches(SafeUndo.Info(saved))){
      var committed=UndoReceipt.Committed(replacement,row.Path);if(committed!=null)File.Replace(backup,row.Path,temp);throw new IOException(L.Text("mediaNames.changed")+" "+backup);
     }
    }
    row.Applied=true;row.Status=L.Text("mediaNames.saved");
   }finally{if(ownTemp&&File.Exists(temp))File.Delete(temp);}
  }
 }
}
