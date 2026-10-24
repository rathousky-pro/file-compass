using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
namespace FileCompass {
 internal static class AssociatedEditingTests {
  internal static int Run(string root) {
   Directory.CreateDirectory(root);var checks=new List<string>();
   Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add(label);};
   try {
    var settings=new ProgramSettings{TextEditorPath=Path.Combine(Environment.SystemDirectory,"notepad.exe"),ImageViewerPath=@"C:\not-used-viewer.exe",MediaPlayerPath=@"C:\not-used-player.exe"};
    foreach(string ext in new[]{".doc",".docx",".docm",".xls",".xlsx",".xlsb",".xlsm",".ppt",".pptx",".pptm",".pps",".ppsx",".pdf",".odt",".ods",".odp",".rtf",".jpg",".png",".webp",".heic",".avif",".psd",".dng",".mp3",".flac",".wav",".mp4",".mkv",".aiff",".mid"}) {
     string file=Path.Combine(root,"Český název s mezerami"+ext);
     check(FileViewers.UsesAssociatedEditor(file),"F4 uses an associated application for "+ext);
     var start=FileViewers.AssociatedEditInfo(file,(e,value,verb)=>value==1&&verb=="open"?"Application command":null);
     check(start.FileName==file&&start.UseShellExecute&&start.Arguments==""&&start.Verb=="open","Associated open preserves filename for "+ext);
    }
    string sample=Path.Combine(root,"example.pdf");
    var edit=FileViewers.AssociatedEditInfo(sample,(e,value,verb)=>value==1&&verb=="edit"?"Editor command":null);
    check(edit.Verb=="edit","Registered Edit action is preferred");
    var uwp=FileViewers.AssociatedEditInfo(sample,(e,value,verb)=>value==18&&verb=="open"?"delegate-guid":null);
    check(uwp.Verb=="open","Delegated application handlers are supported");
    var friendly=FileViewers.AssociatedEditInfo(sample,(e,value,verb)=>value==4&&verb=="open"?"Windows app":null);
    check(friendly.Verb=="open","Application registration can identify a default Windows app");
    bool missing=false;try{FileViewers.AssociatedEditInfo(sample,(e,value,verb)=>null);}catch(IOException ex){missing=ex.Message==L.Text("editor.noAssociatedApplication");}
    check(missing,"Missing association produces actionable message");
    missing=false;try{FileViewers.AssociatedEditInfo(Path.Combine(root,Guid.NewGuid().ToString("N")+".compass-no-association-7a9f"));}catch(IOException){missing=true;}
    check(missing,"Actual Windows association query rejects an unregistered type");
    foreach(int code in new[]{1155,31,2,3}) {
     int count=0;bool failed=false;try{FileViewers.StartAssociatedEdit(edit,info=>{count++;throw new Win32Exception(code);});}catch(IOException ex){failed=ex.Message==L.Text("editor.noAssociatedApplication");}
     check(failed&&count==1,"Broken association does not fall back to Notepad: "+code);
    }
    bool denied=false;try{FileViewers.StartAssociatedEdit(edit,info=>{throw new Win32Exception(5);});}catch(IOException ex){denied=ex.Message.StartsWith(L.Text("editor.associatedApplicationFailed"));}
    check(denied,"Other application launch failures remain readable");
    foreach(string ext in new[]{".txt",".csv",".json",".xml",".md",".lng",".cs",".ps1",".cmd",".bat",".js",".html",".exe",".lnk",".unknown",".svg",".svgz"}) {
     string file=Path.Combine(root,"data"+ext);
     check(!FileViewers.UsesAssociatedEditor(file),"Text/safe data editing stays in the text editor: "+ext);
     var start=FileViewers.LaunchInfo(file,settings,true);
     check(start.FileName==settings.TextEditorPath&&start.Arguments=="\""+file+"\"","Text editor receives file as quoted data: "+ext);
    }
    // Exercise public dispatch against the real machine's association table, without launching apps.
    foreach(string ext in new[]{".pdf",".docx",".png",".mp3"}) {
     try {var start=FileViewers.LaunchInfo(Path.Combine(root,"sample"+ext),settings,true);check(start.FileName.EndsWith(ext)&&start.UseShellExecute,"Public F4 dispatch uses the actual Windows association: "+ext);}
     catch(IOException ex){check(ex.Message==L.Text("editor.noAssociatedApplication"),"Public F4 dispatch reports missing Windows association: "+ext);}
    }
    File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count}));return 0;
   }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}
  }
 }
}
