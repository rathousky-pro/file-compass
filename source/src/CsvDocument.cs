using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace FileCompass {
 internal sealed class CsvDocument {
  internal readonly List<string[]> Rows=new List<string[]>();internal char Separator;internal int Columns;internal bool Limited,Incomplete;
  internal static bool Supports(string path){return path!=null&&new[]{".csv",".tsv",".tab",".psv"}.Contains(Path.GetExtension(path).ToLowerInvariant());}
  internal static CsvDocument Parse(string text,char? forced=null,bool prefix=false,string path=null){text=text??"";if(text.StartsWith("\uFEFF",StringComparison.Ordinal))text=text.Substring(1);char? declared=null;if(text.Length>=5&&text.StartsWith("sep=",StringComparison.OrdinalIgnoreCase)&&text[4]!='\r'&&text[4]!='\n'&&(text.Length==5||text[5]=='\r'||text[5]=='\n')){declared=text[4];int end=text.IndexOfAny(new[]{'\r','\n'});if(end<0)text="";else{int skip=end+1;if(text[end]=='\r'&&skip<text.Length&&text[skip]=='\n')skip++;text=text.Substring(skip);}}
   char separator=forced??declared??Detect(text,path);return Read(text,separator,prefix,2000);
  }
  static char Detect(string text,string path){char best=path!=null&&(Path.GetExtension(path).Equals(".tsv",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(path).Equals(".tab",StringComparison.OrdinalIgnoreCase))?'\t':',';double score=Double.MinValue;foreach(char candidate in new[]{best,';',',','\t','|'}.Distinct()){var sample=Read(text,candidate,false,32);if(sample.Rows.Count==0)continue;var groups=sample.Rows.Select(x=>x.Length).Where(x=>x>1).GroupBy(x=>x).OrderByDescending(x=>x.Count()).ThenByDescending(x=>x.Key).FirstOrDefault();if(groups==null)continue;double value=1000.0*groups.Count()/sample.Rows.Count+Math.Min(20,groups.Key)*3;if(candidate==','&&System.Text.RegularExpressions.Regex.Matches(text,@"\d,\d").Count>0)value-=50;if(value>score){best=candidate;score=value;}}return best;}
  static CsvDocument Read(string text,char separator,bool prefix,int maxRows){var result=new CsvDocument{Separator=separator};var fields=new List<string>();var cell=new StringBuilder();bool quoted=false,afterQuote=false,started=false,record=false;int cells=0;
   Action field=delegate{if(fields.Count<128)fields.Add(cell.ToString());else result.Limited=true;cell.Clear();started=false;afterQuote=false;};
   Action row=delegate{field();if(record){result.Columns=Math.Max(result.Columns,fields.Count);result.Rows.Add(fields.ToArray());cells+=fields.Count;}fields.Clear();record=false;};
   for(int i=0;i<text.Length;i++){char c=text[i];if(quoted){if(c=='"'){if(i+1<text.Length&&text[i+1]=='"'){if(cell.Length<16384)cell.Append('"');else result.Limited=true;i++;}else{quoted=false;afterQuote=true;}}else{if(cell.Length<16384)cell.Append(c);else result.Limited=true;}continue;}
    if(c==separator){record=true;field();continue;}if(c=='\r'||c=='\n'){if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;row();if(result.Rows.Count>=maxRows||cells>=100000){result.Limited|=i+1<text.Length;return result;}continue;}
    if(c=='"'&&!started){quoted=true;started=true;record=true;continue;}if(afterQuote&&(c==' '||c=='\t'))continue;started=true;record=true;if(cell.Length<16384)cell.Append(c);else result.Limited=true;
   }
   if(record||fields.Count>0){if(prefix||quoted){result.Incomplete=true;}else row();}return result;
  }
 }
}
