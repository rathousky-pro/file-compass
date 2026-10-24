using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace FileCompass {
    internal static partial class FileMetadata {
        static XmlDocument Xml(Stream stream) {
            var doc=new XmlDocument{XmlResolver=null};
            using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2*1048576}))doc.Load(reader);return doc;
        }
        static XmlDocument Part(ZipArchive zip,string name) {
            var entry=zip.GetEntry(name);if(entry==null||entry.Length>2*1048576)return null;
            using(var stream=entry.Open())return Xml(stream);
        }
        static string Element(XmlDocument doc,string name) {var node=doc==null?null:doc.SelectSingleNode("//*[local-name()='"+name+"']");return node==null?null:node.InnerText;}
        static void Office(string path,FileDetails d,string ext) {
            if(new FileInfo(path).Length>128*1048576L){d.Add("notice",L.Text("details.largeDocument"));return;}
            using(var input=Open(path))using(var zip=new ZipArchive(input,ZipArchiveMode.Read)) {
                if(zip.Entries.Count>10000){d.Add("notice",L.Text("details.largeDocument"));return;}
                if(ext==".odt"||ext==".ods"||ext==".odp"){
                    var meta=Part(zip,"meta.xml");foreach(var pair in new[]{new[]{"title","title"},new[]{"author","initial-creator"},new[]{"lastAuthor","creator"},new[]{"subject","subject"},new[]{"keywords","keyword"},new[]{"documentCreated","creation-date"},new[]{"documentModified","date"},new[]{"application","generator"}})d.Add(pair[0],Element(meta,pair[1]));
                    var stats=meta==null?null:meta.SelectSingleNode("//*[local-name()='document-statistic']");if(stats!=null)foreach(XmlAttribute attr in stats.Attributes){if(attr.LocalName=="page-count")d.Add("cachedPages",attr.Value);if(attr.LocalName=="word-count")d.Add("cachedWords",attr.Value);if(attr.LocalName=="table-count")d.Add("sheets",attr.Value);}
                    var content=Part(zip,"content.xml");Excerpt(d,content,"p");return;
                }
                var core=Part(zip,"docProps/core.xml");var app=Part(zip,"docProps/app.xml");
                foreach(var pair in new[]{new[]{"title","title"},new[]{"author","creator"},new[]{"lastAuthor","lastModifiedBy"},new[]{"subject","subject"},new[]{"keywords","keywords"},new[]{"documentCreated","created"},new[]{"documentModified","modified"},new[]{"revision","revision"}})d.Add(pair[0],Element(core,pair[1]));
                foreach(var pair in new[]{new[]{"application","Application"},new[]{"company","Company"},new[]{"cachedPages","Pages"},new[]{"cachedWords","Words"},new[]{"notes","Notes"}})d.Add(pair[0],Element(app,pair[1]));
                d.Add("macros",zip.Entries.Any(x=>x.FullName.EndsWith("vbaProject.bin",StringComparison.OrdinalIgnoreCase))?L.Text("details.present"):null);
                if(zip.GetEntry("word/document.xml")!=null) {
                    Excerpt(d,Part(zip,"word/document.xml"),"t");
                    var comments=Part(zip,"word/comments.xml");if(comments!=null)d.Add("comments",comments.SelectNodes("//*[local-name()='comment']").Count);
                    var document=Part(zip,"word/document.xml");if(document!=null)d.Add("paragraphs",document.SelectNodes("//*[local-name()='p']").Count);
                }else if(zip.GetEntry("xl/workbook.xml")!=null){
                    var workbook=Part(zip,"xl/workbook.xml");var sheets=workbook==null?null:workbook.SelectNodes("//*[local-name()='sheet']");
                    if(sheets!=null){d.Add("sheets",sheets.Count);d.Add("sheetNames",String.Join(", ",sheets.Cast<XmlNode>().Take(20).Select(x=>x.Attributes["name"].Value+(x.Attributes["state"]==null?"":" ("+x.Attributes["state"].Value+")"))));d.Summary=Path.GetExtension(path).TrimStart('.').ToUpperInvariant()+" · "+L.Text("details.sheets")+": "+sheets.Count;}
                    var relationships=Part(zip,"xl/_rels/workbook.xml.rels");int inspected=0;
                    if(sheets!=null&&relationships!=null)foreach(XmlNode sheet in sheets){
                        if(inspected++>=6)break;var id=sheet.Attributes.Cast<XmlAttribute>().FirstOrDefault(x=>x.LocalName=="id");if(id==null)continue;
                        var rel=relationships.SelectNodes("//*[local-name()='Relationship']").Cast<XmlNode>().FirstOrDefault(x=>x.Attributes["Id"]!=null&&x.Attributes["Id"].Value==id.Value);if(rel==null||rel.Attributes["Target"]==null)continue;
                        string target=rel.Attributes["Target"].Value.Replace('\\','/');if(target.Contains("..")||target.Contains(":")||(rel.Attributes["TargetMode"]!=null&&rel.Attributes["TargetMode"].Value=="External"))continue;
                        string part=target.StartsWith("/")?target.TrimStart('/'):"xl/"+target;var xml=Part(zip,part);if(xml==null)continue;
                        var range=xml.SelectSingleNode("//*[local-name()='dimension']");if(range!=null&&range.Attributes["ref"]!=null)d.Add("range",sheet.Attributes["name"].Value+": "+range.Attributes["ref"].Value);
                    }
                    var shared=Part(zip,"xl/sharedStrings.xml");if(shared!=null)Excerpt(d,shared,"t");
                    d.ExcerptLabel=L.Text("details.spreadsheetExcerpt");
                }else if(zip.GetEntry("ppt/presentation.xml")!=null || ext.StartsWith(".ppt") || ext.StartsWith(".pps") || ext.StartsWith(".pot")){
                    var slides=zip.Entries.Where(x=>Regex.IsMatch(x.FullName,@"^ppt/slides/slide\d+\.xml$")).OrderBy(x=>x.FullName).ToArray();d.Add("slides",slides.Length);
                    if(slides.Length>0)Excerpt(d,Part(zip,slides[0].FullName),"t");
                }
            }
        }
        static void Excerpt(FileDetails d,XmlDocument doc,string element) {
            if(doc==null)return;d.Excerpt=String.Join(" ",doc.SelectNodes("//*[local-name()='"+element+"']").Cast<XmlNode>().Take(100).Select(x=>x.InnerText));if(d.Excerpt.Length>2500)d.Excerpt=d.Excerpt.Substring(0,2500)+"…";d.ExcerptLabel=L.Text("details.documentExcerpt");
        }
        static void Pdf(string path,FileDetails d) {
            using(var stream=Open(path))using(var pdf=UglyToad.PdfPig.PdfDocument.Open(stream)) {
                d.Summary="PDF · "+pdf.NumberOfPages+" "+L.Text("details.pageAbbreviation");d.Add("pages",pdf.NumberOfPages);d.Add("pdfVersion",pdf.Version.ToString(CultureInfo.InvariantCulture));
                foreach(var pair in new[]{new[]{"title","Title"},new[]{"author","Author"},new[]{"subject","Subject"},new[]{"keywords","Keywords"},new[]{"application","Creator"},new[]{"producer","Producer"},new[]{"documentCreated","CreationDate"},new[]{"documentModified","ModifiedDate"}})d.Add(pair[0],Prop(pdf.Information,pair[1]));
                if(pdf.NumberOfPages>0) {
                    var page=pdf.GetPage(1);d.Add("pageSize",(Convert.ToDouble(page.Width)*25.4/72).ToString("0.#")+" × "+(Convert.ToDouble(page.Height)*25.4/72).ToString("0.#")+" mm");
                    string text=UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(page);
                    if(!String.IsNullOrWhiteSpace(text)){d.Excerpt=text.Length>2500?text.Substring(0,2500)+"…":text;d.ExcerptLabel=L.Text("details.pdfExcerpt");}else d.Add("notice",L.Text("details.noPdfText"));
                }
            }
        }
        static void Text(string path,FileDetails d,string ext) {
            if(!new[]{".txt",".log",".csv",".tsv",".tab",".psv",".json",".xml",".svg",".html",".htm",".md",".ini",".config",".yaml",".yml",".cs",".js",".ts",".py",".ps1",".bat",".sql",".css",".rtf"}.Contains(ext))return;
            byte[] bytes;using(var stream=Open(path)){bytes=new byte[(int)Math.Min(stream.Length,65536)];int n=stream.Read(bytes,0,bytes.Length);if(n<bytes.Length)Array.Resize(ref bytes,n);}
            string source;string encoding;
            if(bytes.Length>=2&&bytes[0]==255&&bytes[1]==254){source=Encoding.Unicode.GetString(bytes,2,bytes.Length-2);encoding="UTF-16 LE";}
            else if(bytes.Length>=2&&bytes[0]==254&&bytes[1]==255){source=Encoding.BigEndianUnicode.GetString(bytes,2,bytes.Length-2);encoding="UTF-16 BE";}
            else {if(bytes.Any(x=>x==0))return;try{var decoder=new UTF8Encoding(false,true).GetDecoder();var chars=new char[bytes.Length+2];int used,generated;bool done;decoder.Convert(bytes,0,bytes.Length,chars,0,chars.Length,new FileInfo(path).Length<=bytes.Length,out used,out generated,out done);source=new string(chars,0,generated);encoding="UTF-8";}catch{source=Encoding.GetEncoding(1250).GetString(bytes);encoding="Windows-1250 ("+L.Text("details.estimate")+")";}}
            d.Add("encoding",encoding);bool complete=new FileInfo(path).Length<=bytes.Length;d.Add(complete?"lines":"sampleLines",source.Length==0?0:source.Count(x=>x=='\n')+1);
            d.Add("lineEndings",source.Contains("\r\n")?"CRLF":source.Contains("\n")?"LF":source.Contains("\r")?"CR":null);
            if(CsvDocument.Supports(path)){var csv=CsvDocument.Parse(source,null,!complete,path);d.Add("separator",csv.Separator=='\t'?"TAB":csv.Separator.ToString());d.Add("columns",csv.Columns);}

            if((ext==".xml"||ext==".svg")&&complete)using(var memory=new MemoryStream(bytes)){var xml=Xml(memory);d.Add("xmlRoot",xml.DocumentElement.Name);if(ext==".svg"){if(xml.DocumentElement.HasAttribute("width")&&xml.DocumentElement.HasAttribute("height"))d.Add("dimensions",xml.DocumentElement.GetAttribute("width")+" × "+xml.DocumentElement.GetAttribute("height"));d.Add("viewBox",xml.DocumentElement.GetAttribute("viewBox"));}}
            if(ext==".json"&&complete){var obj=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=65536,RecursionLimit=64}.DeserializeObject(source.TrimStart('\ufeff'));var array=obj as object[];var dict=obj as Dictionary<string,object>;d.Add("jsonRoot",array!=null?L.Text("details.array"):dict!=null?L.Text("details.object"):L.Text("details.value"));if(array!=null)d.Add("entries",array.Length);if(dict!=null)d.Add("entries",dict.Count);}
        }
    }
}




