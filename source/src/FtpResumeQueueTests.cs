using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileCompass.NetworkTransport;
namespace FileCompass {
    public partial class MainWindow {
        internal async Task VerifyFtpResumeQueue(RemoteConnection browsing,string root,Action<bool,string> check) {
            string first=Path.Combine(root,"ui-first.bin"), second=Path.Combine(root,"ui-second.bin");
            File.WriteAllBytes(first,Enumerable.Range(0,8388608).Select(x=>(byte)x).ToArray());File.WriteAllBytes(second,new byte[8388608]);
            browsing.Session.CreateDirectory("/ui-resume");
            var copying=StartNetworkCopy(new[]{new Entry{FullPath=first},new Entry{FullPath=second}},browsing,false,"/ui-resume",null,NetworkCopyRule.Overwrite);
            var original=networkJobs.Last(); await original.PlanningCompletion;
            await Task.Delay(1000);
            original.CancelItem(original.SnapshotItems().First(x=>x.Source==first));
            DateTime end=DateTime.UtcNow.AddSeconds(10);
            while(!original.SnapshotItems().Any(x=>x.Source==first && x.State==L.Code("state.cancelled")) && DateTime.UtcNow<end)await Task.Delay(20);
            check(original.SnapshotItems().Any(x=>x.Source==first && x.State==L.Code("state.cancelled")) && !original.Finished,"interrupted file leaves another transfer running in the same queue: "+String.Join(";",original.SnapshotItems().Select(x=>Path.GetFileName(x.Source)+"="+x.State)));
            string missing=Path.Combine(root,"missing.txt"), complete=Path.Combine(root,"complete.txt"), stale=Path.Combine(root,"stale.txt"), seed=Path.Combine(root,"seed.txt");
            File.WriteAllText(missing,"missing file must be sent");File.WriteAllText(complete,"already complete");File.WriteAllText(stale,"incomplete target must be replaced");File.WriteAllText(seed,"x");
            browsing.Session.PutFiles(complete,"/ui-resume/complete.txt");browsing.Session.PutFiles(seed,"/ui-resume/stale.txt");
            var resuming=StartNetworkCopy(new[]{new Entry{FullPath=first},new Entry{FullPath=missing},new Entry{FullPath=complete},new Entry{FullPath=stale}},browsing,false,"/ui-resume",null,NetworkCopyRule.Resume);
            var resumed=networkJobs.Last();
            check(resumed!=original,"explicit resume is not silently discarded by the previous queue duplicate filter");
            await resuming;await copying;
            check(resumed.SnapshotItems().Any(x=>x.Source==first && x.State==L.Code("state.completed")),"dialog resume completes the interrupted FTP upload");
            var states=resumed.SnapshotItems();
            check(states.Single(x=>x.Source==missing).State==L.Code("state.completed"),"resume selection copies a missing file");
            check(states.Single(x=>x.Source==complete).State==L.Code("state.skipped"),"resume selection skips an existing complete file");
            check(states.Single(x=>x.Source==stale).State==L.Code("state.completed"),"resume selection replaces an existing file of the wrong size");
            string output=Path.Combine(root,"ui-uploaded.bin");browsing.Session.GetFiles("/ui-resume/ui-first.bin",output);
            check(File.ReadAllBytes(output).SequenceEqual(File.ReadAllBytes(first)),"resumed UI upload has byte-exact original content");
            var saved=durableQueue.Pending().Single(x=>x.Sources.Contains(first));string savedId=saved.Id;
            check(saved.Files.Any(x=>x.Source==first&&!x.Done) && saved.Files.Any(x=>x.Source==second&&x.Done),"interrupted batch retains its full plan and completed receipts");
            durableQueue.Dispose();durableQueue=new DurableQueue(DurableQueue.TestDirectory??root);
            var loaded=durableQueue.Pending().Single(x=>x.Id==savedId);
            check(loaded.Files.Any(x=>x.Source==first&&!x.Done) && loaded.Files.Any(x=>x.Source==second&&x.Done),"saved batch and receipts survive reopening persistent storage");
            var recovered=RecoveredBatches(loaded);check(recovered.Single().Rule==NetworkCopyRule.Resume,"recovered batch uses finish-missing/resume-partial/skip-complete behavior");
            await StartNetworkCopy(loaded.Sources.Select(x=>new Entry{FullPath=x}).ToArray(),browsing,false,loaded.Destination,null,loaded.Rule,loaded.Move,loaded);
            check(!durableQueue.Pending().Any(x=>x.Id==savedId),"restored batch clears its saved plan only after completion");

        }
    }
}
