using System;using System.IO;using System.Linq;using System.Text;using System.Text.RegularExpressions;using System.Windows;using System.Windows.Controls;using ICSharpCode.AvalonEdit.Highlighting;
namespace FileCompass {
 internal sealed partial class CodeEditorWindow {
  internal readonly ComboBox Syntax=new ComboBox{Width=160,Height=26,Margin=new Thickness(6,0,0,0)};
  bool syncingOptions;
  static readonly string[] SyntaxExtensions={null,"",".sh",".rsc",".json",".yaml",".xml",".ps1",".py",".js",".cs",".ini"};
  void AddSyntaxControl(Panel panel){Syntax.ItemsSource=new[]{L.Text("codeEditor.syntaxAuto"),L.Text("codeEditor.syntaxPlain"),"Shell / Bash","RouterOS","JSON","YAML","XML","PowerShell","Python","JavaScript","C#","INI"};Syntax.SelectedIndex=0;var label=CopyUi.Label(L.Text("codeEditor.syntax"));label.VerticalAlignment=VerticalAlignment.Center;var pair=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,4)};pair.Children.Add(label);pair.Children.Add(Syntax);panel.Children.Add(pair);}
  void InitializeFormatControls(){
   encoding.ItemsSource=new[]{L.Text("codeEditor.utf8NoBom"),"UTF-8 BOM","UTF-16 LE","Windows-1250","Windows-1252",L.Text("codeEditor.original")};encoding.SelectedIndex=5;
   eol.ItemsSource=new[]{L.Text("codeEditor.original"),"CRLF · Windows","LF · Linux / Unix"};eol.SelectedIndex=0;
   eol.ToolTip=L.Text("codeEditor.eolHelp");encoding.ToolTip=L.Text("codeEditor.encodingHelp");
   encoding.SelectionChanged+=delegate{FormatChanged();};eol.SelectionChanged+=delegate{FormatChanged();};
   Syntax.SelectionChanged+=delegate{if(syncingOptions||Current==null)return;Current.SyntaxChoice=Syntax.SelectedIndex;Highlight(Current);UpdateFolding(Current);};
  }
  void SyncOptions(){syncingOptions=true;try{optionsDocument=Current;var doc=Current;
   if(doc!=null){indent.SelectedItem=doc.Indentation.Width.ToString();var encItems=encoding.Items.Cast<string>().ToArray();encItems[5]=L.Text("codeEditor.original")+" · "+doc.File.Encoding.WebName+(doc.File.Preamble.Length>0?" BOM":"");encoding.ItemsSource=encItems;var eolItems=eol.Items.Cast<string>().ToArray();eolItems[0]=L.Text("codeEditor.original")+" · "+EolKind(doc.File.Text);eol.ItemsSource=eolItems;}
   encoding.SelectedIndex=doc==null?5:doc.EncodingChoice;eol.SelectedIndex=doc==null?0:doc.EolChoice;Syntax.SelectedIndex=doc==null?0:doc.SyntaxChoice;
  }finally{syncingOptions=false;}}
  void FormatChanged(){var doc=Current;if(syncingOptions||doc==null||doc.Saving)return;if(doc.EncodingChoice!=encoding.SelectedIndex||doc.EolChoice!=eol.SelectedIndex){doc.EncodingChoice=encoding.SelectedIndex;doc.EolChoice=eol.SelectedIndex;doc.FormatModified=true;doc.Editor.IsModified=true;RefreshStatus();}}
  internal static string EolKind(string text){bool crlf=text.Contains("\r\n");string rest=text.Replace("\r\n","");bool lf=rest.Contains("\n"),cr=rest.Contains("\r");int kinds=(crlf?1:0)+(lf?1:0)+(cr?1:0);return kinds>1?L.Text("codeEditor.mixedEol"):crlf?"CRLF":lf?"LF":cr?"CR":"—";}
  internal static string SyntaxPath(EditorDocument doc){if(doc.SyntaxChoice>0)return "document"+SyntaxExtensions[doc.SyntaxChoice];string path=doc.RemotePath??doc.File.Path;if(!String.IsNullOrEmpty(path))return path;string text=doc.Editor.Text.TrimStart();return text.StartsWith("#!")?"document.sh":text.StartsWith("{")||text.StartsWith("[")?"document.json":text.StartsWith("<")?"document.xml":"";}

 }
}
