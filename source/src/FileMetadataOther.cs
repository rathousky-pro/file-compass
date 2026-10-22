using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Windows.Media;
namespace CommanderCZ {
    internal static partial class FileMetadata {
        static void Archive(string path,FileDetails d) {
            if(Path.GetExtension(path).Equals(".zip",StringComparison.OrdinalIgnoreCase))Zip(path,d);
            using(PluginIntegrity.Acquire(@"7zip\7z.dll")){
                SharpSevenZip.SharpSevenZipBase.SetLibraryPath(ArchiveEngine.SevenZipLibraryPath);
                using(var archive=new SharpSevenZip.SharpSevenZipExtractor(path)){
                    var entries=archive.ArchiveFileData;
                    d.Rows.RemoveAll(x=>x.Label==L.Text("details.entries"));
                    d.Add("archiveFormat",archive.Format);d.Add("entries",entries.Count);d.Add("folders",entries.Count(x=>x.IsDirectory));d.Add("files",entries.Count(x=>!x.IsDirectory));
                    ulong expanded=0;foreach(var item in entries)if(!item.IsDirectory)expanded=checked(expanded+item.Size);
                    if(expanded<=Int64.MaxValue)d.Add("expandedSize",CopyUi.Bytes((long)expanded));
                    if(expanded>0)d.Add("archiveRatio",(100.0*new FileInfo(path).Length/expanded).ToString("0.#")+" %");
                    if(entries.Any(x=>x.Encrypted))d.Add("encryption",L.Text("details.present"));
                    d.Summary=Path.GetExtension(path).TrimStart('.').ToUpperInvariant()+" · "+L.Text("details.entries")+": "+entries.Count;
                }
            }
        }
        static string FontName(IDictionary<CultureInfo,string> values) {if(values==null||values.Count==0)return null;string name;if(values.TryGetValue(CultureInfo.CurrentCulture,out name))return name;return values.Values.FirstOrDefault();}
        static void Font(string path,FileDetails d) {
            var font=new GlyphTypeface(new Uri(path,UriKind.Absolute));
            string family=FontName(font.FamilyNames);d.Summary=family??d.Summary;
            d.Add("fontFamily",family);d.Add("fontFace",FontName(font.FaceNames));d.Add("fontWeight",font.Weight);d.Add("fontStyle",font.Style);d.Add("fontStretch",font.Stretch);d.Add("fontVersion",font.Version);
            d.Add("glyphs",font.CharacterToGlyphMap.Count);d.Add("embedding",font.EmbeddingRights);d.Add("copyright",FontName(font.Copyrights));
        }
    }
}

