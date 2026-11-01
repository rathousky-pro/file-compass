using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
namespace FileCompass {
 internal static class MediaConversion {
  internal static UndoReceipt Convert(MediaRow row,MediaAudioOptions audio,MediaImageOptions image,CancellationToken token,Action<double> progress,int concurrentFiles=1){
   if(String.IsNullOrEmpty(row.Target)||String.Equals(Path.GetFullPath(row.Path),Path.GetFullPath(row.Target),StringComparison.OrdinalIgnoreCase))throw new IOException(L.Text("media.invalidOptions"));PathSafety.RejectLinks(row.Path);PathSafety.RejectLinks(row.Target);if(File.Exists(row.Target)||Directory.Exists(row.Target))throw new IOException(L.Text("parts.outputExists"));
   string stage=Path.Combine(Path.GetDirectoryName(row.Target),".filecompass-media-"+Guid.NewGuid().ToString("N")+Path.GetExtension(row.Target));bool committed=false;
   using(PathSafety.LockAncestors(row.Target))try{
    token.ThrowIfCancellationRequested();if(row.Image)image=MediaKeepFormat.Image(row.Path,image);else audio=MediaKeepFormat.Audio(row.Path,row.Info??MediaProcess.Probe(row.Path,token),audio);if(row.Image){row.Info=MediaImages.Call(row.Path,stage,image,false,token,concurrentFiles);if(progress!=null)progress(.95);}else MediaAudio.Convert(row.Path,stage,audio,token,progress,concurrentFiles);
    token.ThrowIfCancellationRequested();AttachmentSecurity.WriteZone(stage,AttachmentSecurity.ReadZone(row.Path));string hash;using(var output=File.OpenRead(stage))hash=MediaNames.Hash(output,token);var receipt=UndoReceipt.Capture(stage,row.Path,false,System.Convert.FromBase64String(hash));File.Move(stage,row.Target);committed=true;if(progress!=null)progress(1);return UndoReceipt.Committed(receipt,row.Target);
   }finally{if(!committed&&File.Exists(stage))File.Delete(stage);}
  }
 }
}
