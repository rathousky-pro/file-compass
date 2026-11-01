using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;
namespace FileCompass {
 public sealed class ContentMatch {public long Line;public string Location,Snippet;}
 public sealed class ContentRequest {public string Path;public FileSearchOptions Options;}
 public sealed class ContentResponse {public ContentMatch Match;public string Error;}
 internal static class ContentSearch {
  internal static ContentMatch Find(string path,FileSearchOptions options,CancellationToken token){
   if(String.IsNullOrEmpty(options.Text))return null;if(options.Text.Length>65536)throw new IOException(L.Text("search.textLengthLimit"));
   string ext=Path.GetExtension(path).ToLowerInvariant();if(ext==".pdf"||ext==".docx")return options.DocumentText?DocumentWorker(path,options,token):null;
   using(var reader=new StreamReader(path,Encoding.GetEncoding(options.EncodingCodePage),true,65536))return Scan(reader,options,token,null);
  }
  internal static ContentMatch Scan(TextReader reader,FileSearchOptions options,CancellationToken token,string location){
   char[] buffer=new char[65536];string tail="";long line=1;int read;var regex=options.TextRegex?new Regex(options.Text,options.CaseSensitive?RegexOptions.None:RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1)):null;
   while((read=reader.Read(buffer,0,buffer.Length))>0){OperationPause.Wait(token);string part=tail+new string(buffer,0,read);
    if(regex!=null){int start=0,end;while((end=part.IndexOf('\n',start))>=0){if(end-start>131072)throw new IOException(L.Text("search.regexLineLengthLimit"));string text=part.Substring(start,end-start).TrimEnd('\r');var match=regex.Match(text);if(match.Success)return Result(text,match.Index,line,location);line++;start=end+1;}tail=part.Substring(start);if(tail.Length>131072)throw new IOException(L.Text("search.regexLineLengthLimit"));}
    else{int index=part.IndexOf(options.Text,options.CaseSensitive?StringComparison.Ordinal:StringComparison.OrdinalIgnoreCase);if(index>=0)return Result(part,index,line+part.Take(index).Count(x=>x=='\n'),location);int keep=Math.Min(part.Length,options.Text.Length-1);int consumed=part.Length-keep;line+=part.Take(consumed).Count(x=>x=='\n');tail=part.Substring(consumed);}
   }
   if(regex!=null){var match=regex.Match(tail.TrimEnd('\r'));if(match.Success)return Result(tail,match.Index,line,location);}return null;
  }
  static ContentMatch Result(string text,int index,long line,string location){int start=Math.Max(0,index-50);string snippet=text.Substring(start,Math.Min(220,text.Length-start)).Replace('\r',' ').Replace('\n',' ').Replace('\0',' ');return new ContentMatch{Line=line,Location=location??line.ToString(),Snippet=snippet};}
  static ContentMatch DocumentWorker(string path,FileSearchOptions options,CancellationToken token){
   if(new FileInfo(path).Length>64L*1048576)throw new IOException(L.Text("tools1.documentLimit"));
   using(var temp=new ArchiveTemp(Path.Combine(AppPaths.ConfigDirectory,"content-search")))using(var job=new ArchiveProcessJob(512)){
    string request=Path.Combine(temp.Path,"request.json"),result=Path.Combine(temp.Path,"result.json");var json=new JavaScriptSerializer{MaxJsonLength=262144};File.WriteAllText(request,json.Serialize(new ContentRequest{Path=path,Options=options}),Encoding.UTF8);
    using(var process=new Process{StartInfo=new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--content-search-worker "+SshTerminal.QuoteWindowsArgument(request)+" "+SshTerminal.QuoteWindowsArgument(result)){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden}}){
     process.Start();job.Assign(process);try{var watch=Stopwatch.StartNew();while(!process.WaitForExit(40)){OperationPause.Wait(token);if(watch.ElapsedMilliseconds>15000)throw new IOException(L.Text("tools1.documentTimeout"));}OperationPause.Wait(token);if(!File.Exists(result)||new FileInfo(result).Length>262144)throw new IOException(L.Text("tools1.documentFailed"));var data=json.Deserialize<ContentResponse>(File.ReadAllText(result));if(data==null||!String.IsNullOrEmpty(data.Error))throw new IOException(data==null?L.Text("tools1.documentFailed"):data.Error);return data.Match;}
     finally{if(!process.HasExited){try{process.Kill();process.WaitForExit(2000);}catch{}}}
    }
   }
  }
  internal static int Worker(string request,string result){var response=new ContentResponse();try{
   if(new FileInfo(request).Length>262144)throw new IOException("Request limit");var data=new JavaScriptSerializer{MaxJsonLength=262144}.Deserialize<ContentRequest>(File.ReadAllText(request));if(data==null||data.Options==null||String.IsNullOrEmpty(data.Options.Text)||data.Options.Text.Length>65536||new FileInfo(data.Path).Length>64L*1048576)throw new IOException(L.Text("tools1.documentLimit"));
   string ext=Path.GetExtension(data.Path).ToLowerInvariant();if(ext==".pdf"){
    using(var document=UglyToad.PdfPig.PdfDocument.Open(data.Path)){if(document.NumberOfPages>10000)throw new IOException(L.Text("tools1.documentLimit"));foreach(var page in document.GetPages()){string text=UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(page);using(var reader=new StringReader(text))response.Match=Scan(reader,data.Options,CancellationToken.None,"PDF "+page.Number);if(response.Match!=null)break;}}
   }else if(ext==".docx"){
    using(var input=File.OpenRead(data.Path))using(var zip=new ZipArchive(input,ZipArchiveMode.Read)){long total=0;foreach(var entry in zip.Entries.Where(x=>x.FullName=="word/document.xml"||Regex.IsMatch(x.FullName,@"^word/(header|footer|footnotes|endnotes)[0-9]*\.xml$"))){total+=entry.Length;if(entry.Length>16L*1048576||total>64L*1048576)throw new IOException(L.Text("tools1.documentLimit"));
     using(var stream=entry.Open())using(var xml=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16L*1048576})){
      var paragraph=new StringBuilder();long number=0;while(xml.Read()){if(xml.NodeType==XmlNodeType.Element&&xml.LocalName=="t"){using(var part=xml.ReadSubtree()){part.Read();paragraph.Append(part.ReadElementContentAsString());}if(paragraph.Length>1048576)throw new IOException(L.Text("tools1.documentLimit"));}if(xml.NodeType==XmlNodeType.EndElement&&xml.LocalName=="p"){number++;using(var reader=new StringReader(paragraph.ToString()))response.Match=Scan(reader,data.Options,CancellationToken.None,entry.FullName+" · "+number);paragraph.Clear();if(response.Match!=null)break;}}}
     if(response.Match!=null)break;
    }}
   }else throw new IOException(L.Text("tools1.documentFailed"));
  }catch(Exception ex){response.Error=ex.Message;}File.WriteAllText(result,new JavaScriptSerializer{MaxJsonLength=262144}.Serialize(response),Encoding.UTF8);return 0;}
 }
}
