using System;
using System.IO;
using System.Linq;
using System.Threading;
using CommanderCZ.NetworkTransport;

namespace CommanderCZ
{
    internal static partial class RelayTests
    {
        static NetworkTransferJob Interrupted(RemoteConnection source,RemoteConnection target,string destination,bool keep,Entry[] entries = null)
        {
            var job = Planned(source,target,entries ?? new[] { new Entry { FullPath = "/slow.bin" } },destination,NetworkCopyRule.Overwrite);
            job.KeepPartialOnCancel = keep;
            job.Changed = delegate { if (!job.Cancellation.IsCancellationRequested && Interlocked.Read(ref job.RelayUploaded) > 524288) job.Cancel(); };
            bool cancelled = false; try { Run(job,source,target); } catch (OperationCanceledException) { cancelled = true; }
            if (!cancelled) throw new Exception("Relay interruption was not triggered"); job.Finish(keep ? "Přerušeno" : "Zrušeno"); return job;
        }
        static RemoteFileInfo Partial(RemoteConnection target,string directory)
        { return target.Session.ListDirectory(directory).Files.Single(x => x.Name.StartsWith(".CommanderCZ-relay-")); }
        static void ResumeChecks(RemoteConnection source,RemoteConnection target,RemoteConnection sftp,string directory,int first,int second,int fourth,Action<bool,string> check)
        {
            string local = Path.Combine(directory,"resume-original.txt"), output = Path.Combine(directory,"resume-output.bin"); File.WriteAllText(local,"original target");
            var stop = new NetworkStopDialog(null); NetworkTests.Render(stop,Path.Combine(directory,"relay-stop.png"));
            check(FluentUiTests.Children<System.Windows.Controls.Button>(stop).Any(x => (string)x.Content == "Přerušit a ponechat pro navázání") && FluentUiTests.Children<System.Windows.Controls.Button>(stop).Any(x => (string)x.Content == "Zrušit a odstranit rozpracovaná data"),"stop dialog offers keep, discard and continue"); stop.Close();
            target.Session.CreateDirectory("/resume-live"); target.Session.PutFiles(local,"/resume-live/slow.bin").Check();
            var interrupted = Interrupted(source,target,"/resume-live",true,new[] { new Entry { FullPath = "/tree" },new Entry { FullPath = "/slow.bin" } });
            var partial = Partial(target,"/resume-live"); target.Session.GetFiles("/resume-live/slow.bin",output).Check();
            check(partial.Length > 0 && partial.Length < 8388608 && File.ReadAllText(output) == "original target","keep interruption leaves remote partial and preserves previous final target");
            check(Directory.GetFiles(TransferCheckpoint.TestRoot,"*.xml").All(x => !File.ReadAllText(x).Contains("test-password")),"durable relay manifests contain no passwords");
            check(interrupted.SnapshotItems().Any(x => x.Source == "/tree/český [soubor].txt" && x.State == "Dokončeno") && interrupted.SnapshotItems().Any(x => x.State == "Přerušeno"),"multi-file interruption retains completed files and marks unfinished ones");
            using (var restartedSource = Open(first)) using (var restartedTarget = Open(second)) {
                var resume = Planned(restartedSource,restartedTarget,new[] { new Entry { FullPath = "/tree" },new Entry { FullPath = "/slow.bin" } },"/resume-live",NetworkCopyRule.Resume);
                bool initial = false; resume.Changed = delegate { if (resume.RelayResumeOffset > 0 && resume.Update().File > 0) initial = true; };
                Run(resume,restartedSource,restartedTarget); restartedTarget.Session.GetFiles("/resume-live/slow.bin",output).Check(); var bytes = File.ReadAllBytes(output);
                check(resume.RelayResumeOffset == partial.Length && initial,"fresh connections resume from actual remote prefix and progress includes retained bytes");
                check(resume.RelayProbeBytes > 0 && resume.RelayProbeBytes <= 262144,"relay resume reads at most 128 KiB from each server without hashing the whole partial");
                check(bytes.Length == 8388608 && bytes.Select((x,i) => x == (byte)(i % 256)).All(x => x),"resumed FTP to FTP file is byte exact");
                check(resume.SnapshotItems().Any(x => x.Source == "/tree/český [soubor].txt" && x.State == "Přeskočeno") && !restartedTarget.Session.HasRelayCheckpoint(restartedSource.Session,"/slow.bin","/resume-live/slow.bin"),"resume skips completed files and clears its manifest after commit");
            }
            target.Session.CreateDirectory("/discard-live"); target.Session.PutFiles(local,"/discard-live/slow.bin").Check(); Interrupted(source,target,"/discard-live",false);
            check(!target.Session.ListDirectory("/discard-live").Files.Any(x => x.Name.StartsWith(".CommanderCZ-relay-")) && !target.Session.HasRelayCheckpoint(source.Session,"/slow.bin","/discard-live/slow.bin"),"discard interruption removes owned stage and manifest");
            target.Session.CreateDirectory("/corrupt-live"); target.Session.PutFiles(local,"/corrupt-live/slow.bin").Check(); Interrupted(source,target,"/corrupt-live",true); partial = Partial(target,"/corrupt-live");
            byte[] corrupt = new byte[partial.Length]; for (int i = 0; i < corrupt.Length; i++) corrupt[i] = (byte)(i % 256); corrupt[0] ^= 1; string corruptLocal = Path.Combine(directory,"corrupt-stage.bin"); File.WriteAllBytes(corruptLocal,corrupt); target.Session.PutFiles(corruptLocal,partial.FullName).Check();
            bool refused = false; try { Run(Planned(source,target,new[] { new Entry { FullPath = "/slow.bin" } },"/corrupt-live",NetworkCopyRule.Resume),source,target); } catch (IOException) { refused = true; }
            target.Session.GetFiles("/corrupt-live/slow.bin",output).Check(); check(refused && File.ReadAllText(output) == "original target","corrupt retained prefix is rejected without overwriting original target");
            target.Session.CreateDirectory("/changed-target-live"); target.Session.PutFiles(local,"/changed-target-live/slow.bin").Check(); Interrupted(source,target,"/changed-target-live",true); File.WriteAllText(local,"new target written after interruption"); target.Session.PutFiles(local,"/changed-target-live/slow.bin").Check();
            refused = false; try { Run(Planned(source,target,new[] { new Entry { FullPath = "/slow.bin" } },"/changed-target-live",NetworkCopyRule.Resume),source,target); } catch (IOException) { refused = true; }
            target.Session.GetFiles("/changed-target-live/slow.bin",output).Check(); check(refused && File.ReadAllText(output) == "new target written after interruption","changed final target rejects resume and preserves newer contents");
            sftp.Session.CreateDirectory("/resume-live"); Interrupted(source,sftp,"/resume-live",true); partial = Partial(sftp,"/resume-live");
            var sshResume = Planned(source,sftp,new[] { new Entry { FullPath = "/slow.bin" } },"/resume-live",NetworkCopyRule.Resume); Run(sshResume,source,sftp); sftp.Session.GetFiles("/resume-live/slow.bin",output).Check();
            check(sshResume.RelayResumeOffset == partial.Length && new FileInfo(output).Length == 8388608,"FTP to SFTP resumes via stream offset without a local spool");
            target.Session.CreateDirectory("/sftp-source-live"); Interrupted(sftp,target,"/sftp-source-live",true); partial = Partial(target,"/sftp-source-live");
            sshResume = Planned(sftp,target,new[] { new Entry { FullPath = "/slow.bin" } },"/sftp-source-live",NetworkCopyRule.Resume); Run(sshResume,sftp,target); target.Session.GetFiles("/sftp-source-live/slow.bin",output).Check();
            check(sshResume.RelayResumeOffset == partial.Length && new FileInfo(output).Length == 8388608,"SFTP to FTP resumes both streams from the retained prefix");
            target.Session.CreateDirectory("/changed-source-live"); File.WriteAllText(local,"original target"); target.Session.PutFiles(local,"/changed-source-live/mutable.bin").Check();
            source.Session.PutFiles(output,"/mutable.bin").Check(); Interrupted(source,target,"/changed-source-live",true,new[] { new Entry { FullPath = "/mutable.bin" } });
            File.WriteAllText(local,"changed source length"); source.Session.PutFiles(local,"/mutable.bin").Check(); refused = false;
            try { Run(Planned(source,target,new[] { new Entry { FullPath = "/mutable.bin" } },"/changed-source-live",NetworkCopyRule.Resume),source,target); } catch (IOException) { refused = true; }
            target.Session.GetFiles("/changed-source-live/mutable.bin",output).Check(); check(refused && File.ReadAllText(output) == "original target","changed source metadata rejects relay resume before appending");
            target.Session.CreateDirectory("/refused-resume-live"); File.WriteAllText(local,"original target"); target.Session.PutFiles(local,"/refused-resume-live/slow.bin").Check(); Interrupted(source,target,"/refused-resume-live",true); partial = Partial(target,"/refused-resume-live");
            File.WriteAllText(local,"deny append"); target.Session.PutFiles(local,"/__deny_relay_append__").Check(); refused = false;
            try { Run(Planned(source,target,new[] { new Entry { FullPath = "/slow.bin" } },"/refused-resume-live",NetworkCopyRule.Resume),source,target); } catch (IOException) { refused = true; }
            target.Session.RemoveFiles("/__deny_relay_append__"); target.Session.GetFiles("/refused-resume-live/slow.bin",output).Check();
            check(refused && File.ReadAllText(output) == "original target" && Partial(target,"/refused-resume-live").Length == partial.Length,"FTP refusing append fails explicitly and preserves partial and original target");
            target.Session.CreateDirectory("/lease-live"); string locked = "/lease-live/slow.bin"; var sourceInfo = source.Session.GetFileInfo("/slow.bin");
            using (var lease = target.Session.PrepareRelay(source.Session,"/slow.bin",locked,sourceInfo,null,false)) {
                refused = false; try { using (var other = target.Session.PrepareRelay(sftp.Session,"/slow.bin",locked,sftp.Session.GetFileInfo("/slow.bin"),null,false)) { } } catch (IOException) { refused = true; }
                check(refused,"different source jobs cannot concurrently write the same final destination");
            }
            check(target.Session.DiscardRelayPartial(source.Session,"/slow.bin",locked) == null,"owned manifest without remote bytes can be safely discarded");
            if (fourth > 0) using (var scp = Open(fourth,false,true)) {
                scp.Session.CreateDirectory("/resume-live"); Interrupted(source,scp,"/resume-live",true); var scpResume = Planned(source,scp,new[] { new Entry { FullPath = "/slow.bin" } },"/resume-live",NetworkCopyRule.Resume); Run(scpResume,source,scp);
                scp.Session.GetFiles("/resume-live/slow.bin",output).Check(); var bytes = File.ReadAllBytes(output);
                check(scpResume.RelayResumeOffset == 0 && bytes.Length == 8388608 && bytes.Select((x,i) => x == (byte)(i % 256)).All(x => x),"SCP incomplete relay restarts safely from zero and produces exact contents");
            }
        }
    }
}

