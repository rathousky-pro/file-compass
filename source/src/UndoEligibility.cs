using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace FileCompass {
 internal sealed class UndoInspection {
  internal readonly List<UndoReceipt> Available=new List<UndoReceipt>();internal readonly List<string> Problems=new List<string>();internal int Total;
 }
 internal static class UndoEligibility {
  internal static UndoInspection Inspect(UndoGroup group,bool verifyHash,CancellationToken token){
   var result=new UndoInspection();var receipts=SafeUndo.Remaining(group);result.Total=receipts.Length;
   var removals=new HashSet<string>(receipts.Where(x=>!x.Move).Select(x=>x.Target),StringComparer.OrdinalIgnoreCase);
   foreach(var r in receipts.OrderBy(x=>x.Directory?1:0).ThenByDescending(x=>x.Target.Length)){
    token.ThrowIfCancellationRequested();try{
     if(!File.Exists(r.Target)&&!Directory.Exists(r.Target))throw new IOException(L.Text("undoHistory.missing"));
     PathSafety.RejectLinks(r.Target);if(r.Move){PathSafety.RejectLinks(r.Source);if(File.Exists(r.Source)||Directory.Exists(r.Source))throw new IOException(L.Text("tools1.undoCollision"));}
     if(r.RestoreFolder){PathSafety.RejectLinks(r.Source);if(File.Exists(r.Source))throw new IOException(L.Text("tools1.undoCollision"));}
     using(var handle=SafeUndo.Open(r.Target,true)){
      var info=SafeUndo.Info(handle);if(!r.Matches(info))throw new IOException(L.Text("tools1.undoChanged"));
      if(!r.Directory&&r.Hash!=null){long length=((long)info.SizeHigh<<32)|info.SizeLow;if(length!=r.Length)throw new IOException(L.Text("tools1.undoChanged"));
       if(verifyHash||info.Write!=r.Written)using(var input=new FileStream(handle,FileAccess.Read))if(MediaNames.Hash(input,token)!=r.Hash)throw new IOException(L.Text("tools1.undoChanged"));
      }
     }
     if(r.Directory&&!r.Move){foreach(var child in Directory.EnumerateFileSystemEntries(r.Target))if(!removals.Contains(child)||!result.Available.Any(x=>String.Equals(x.Target,child,StringComparison.OrdinalIgnoreCase)))throw new IOException(L.Text("undoHistory.children"));}
     result.Available.Add(r);
    }catch(OperationCanceledException){throw;}catch(Exception ex){result.Problems.Add(r.Target+"\n"+ex.Message);}
   }
   return result;
  }
  internal static void ApplyAvailable(UndoGroup original,UndoReceipt[] selected,CancellationToken token){
   SafeUndo.ApplySelection(original,selected,token);
  }
 }
}
