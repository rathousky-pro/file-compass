using System;using System.IO;using ICSharpCode.AvalonEdit.Highlighting;
namespace FileCompass {
 internal static class CodeSyntax {
 internal static IHighlightingDefinition ForExtension(string ext){ext=(ext??" ").ToLowerInvariant();if(ext==".lng"||ext==".jsonc")ext=".json";if(ext==".patch")ext=".diff";var script=Script(ext);if(script!=null)return script;if(ext==".yaml"||ext==".yml"){const string yaml="<SyntaxDefinition xmlns='http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008'><Color name='Comment' foreground='#6A9955'/><Color name='Key' foreground='#2879B0' fontWeight='bold'/><Color name='String' foreground='#A45D28'/><RuleSet><Span color='Comment' begin='#'/><Span color='String' begin='&quot;' end='&quot;'/><Span color='String' begin=\"'\" end=\"'\"/><Rule color='Key'>^[ ]*[A-Za-z_][A-Za-z0-9_.-]*(?=\\s*:)</Rule></RuleSet></SyntaxDefinition>";using(var xml=System.Xml.XmlReader.Create(new StringReader(yaml)))return ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(xml,HighlightingManager.Instance);}return HighlightingManager.Instance.GetDefinitionByExtension(ext);}
  internal static IHighlightingDefinition Script(string ext){
   if(ext!=".sh"&&ext!=".bash"&&ext!=".rsc")return null;
   string keywords=ext==".rsc"?@"\b(local|global|if|else|foreach|for|while|do|set|get|put|add|remove|find|return|true|false)\b":@"\b(if|then|else|elif|fi|for|in|do|done|while|case|esac|function|return|export|echo|exit)\b";
   string xml="<SyntaxDefinition xmlns='http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008'><Color name='Comment' foreground='#6A9955'/><Color name='Keyword' foreground='#2879B0' fontWeight='bold'/><Color name='String' foreground='#A45D28'/><Color name='Variable' foreground='#9064AF'/><RuleSet><Span color='Comment' begin='#'/><Span color='String' begin='&quot;' end='&quot;'/><Span color='String' begin=\"'\" end=\"'\"/><Rule color='Variable'>\\$[A-Za-z_][A-Za-z0-9_]*</Rule><Rule color='Keyword'>"+keywords+"</Rule></RuleSet></SyntaxDefinition>";
   using(var reader=System.Xml.XmlReader.Create(new StringReader(xml)))return ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader,HighlightingManager.Instance);
  }
 }
}
