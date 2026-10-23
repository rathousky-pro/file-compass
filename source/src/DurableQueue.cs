using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace CommanderCZ
{
    public sealed class DurableFile
    {
        public string Source,Target; public long Bytes; public DateTime Modified; public bool Directory;
        [XmlIgnore] public bool Done;
    }
    public sealed class DurableTransfer
    {
        public int Version = 1;
        public string Id = Guid.NewGuid().ToString("N"),Kind,Destination,SourceProfileId,TargetProfileId,SourceIdentity,TargetIdentity,State = L.Code("queue.waitingForLoad");
        public string[] Sources;
        public CopyOptions Copy; public NetworkCopyRule Rule; public bool Move;
        public List<DurableFile> Files = new List<DurableFile>();
        [XmlIgnore] internal bool Running;
        [XmlIgnore] internal FileStream Lock;
        public override string ToString() { return State + " · " + (Move ? L.Text("operation.moveTitle") : L.Code("copy.title")) + " · " + Kind + L.Text("common.routeArrow") + Destination + L.Text("transfer.remainingPrefix") + Files.Count(x => !x.Directory && !x.Done); }
    }
    internal sealed class DurableQueue : IDisposable
    {
        internal static string TestDirectory;
        readonly string directory; readonly object gate = new object(); internal readonly List<DurableTransfer> Jobs = new List<DurableTransfer>();
        internal string LastError;
        internal DurableQueue(string root)
        {
            directory = Path.Combine(root,"transfer-queue"); Directory.CreateDirectory(directory);
            foreach (string file in System.IO.Directory.GetFiles(directory,"*.xml")) {
                string id = Path.GetFileNameWithoutExtension(file); Guid parsed; if (!Guid.TryParseExact(id,"N",out parsed)) continue;
                FileStream claim = null;
                try {
                    claim = Claim(id);
                    DurableTransfer job = FileIntegrity.ReadXml<DurableTransfer>(file);
                    if (job.Id != id || job.Version != 1 || job.Files == null) throw new IOException(L.Text("queue.unsupportedRecordPrefix") + file);
                    var done = new HashSet<string>(File.Exists(Receipt(id)) ? File.ReadAllLines(Receipt(id)) : new string[0],StringComparer.Ordinal);
                    foreach (var item in job.Files) item.Done = done.Contains(Mark(id,item.Source,item.Target));
                    job.Lock = claim; claim = null; job.Running = false; Jobs.Add(job);
                } catch (IOException ex) { if (claim != null) LastError = ex.Message; } catch (Exception ex) { LastError = ex.Message; }
                finally { if (claim != null) claim.Dispose(); }
            }
        }
        string FilePath(string id) { return Path.Combine(directory,id + ".xml"); }
        string Receipt(string id) { return Path.Combine(directory,id + ".done"); }
        FileStream Claim(string id) { return new FileStream(Path.Combine(directory,id + ".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
        static string Key(string source,string target) { using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(source + "\0" + target))); }
        // Completion receipts are job-bound and signed with the user key; a forged .done file is not proof of completion.
        static string Mark(string id,string source,string target) { return FileIntegrity.Mac(id + "\0" + source + "\0" + target); }
        internal static string Identity(NetworkProfile profile)
        { if (profile == null) return null; return Key(profile.Id + "|" + profile.Protocol + "|" + profile.Host.ToLowerInvariant() + "|" + profile.Port + "|" + profile.UserName + "|" + profile.FtpTls + "|" + profile.FtpImplicitTls,profile.HostKeyFingerprint); }
        void Save(DurableTransfer job)
        {
            FileIntegrity.WriteXml(FilePath(job.Id), job);
        }
        internal DurableTransfer Begin(string kind,string[] sources,string destination,CopyOptions copy,NetworkCopyRule rule,bool move,NetworkProfile source,NetworkProfile target)
        {
            lock (gate) {
                var job = new DurableTransfer { Kind = kind,Sources = sources,Destination = destination,Copy = copy == null ? null : copy.Clone(),Rule = rule,Move = move,SourceProfileId = source == null ? null : source.Id,TargetProfileId = target == null ? null : target.Id,SourceIdentity = Identity(source),TargetIdentity = Identity(target),Running = true };
                job.Lock = Claim(job.Id); try { Save(job); Jobs.Add(job); return job; } catch { job.Lock.Dispose(); throw; }
            }
        }
        internal void Plan(DurableTransfer job,IEnumerable<DurableFile> files)
        { lock (gate) { job.Files = files.ToList(); job.State = L.Code("state.readyToResume"); Save(job); } }
        internal void Done(DurableTransfer job,string source,string target = null)
        {
            lock (gate) {
                var completed = job.Files.Where(x => !x.Done && x.Source == source && (target == null || x.Target == target)).ToArray(); if (completed.Length == 0) return;
                using (var stream = new FileStream(Receipt(job.Id),FileMode.Append,FileAccess.Write,FileShare.Read)) {
                    foreach (var item in completed) { var line = Encoding.ASCII.GetBytes(Mark(job.Id,item.Source,item.Target) + "\n"); stream.Write(line,0,line.Length); } stream.Flush(true);
                } foreach (var item in completed) item.Done = true;
            }
        }
        internal void Finish(DurableTransfer job,bool remove)
        { lock (gate) { job.Running = false; if (remove) Remove(job); else { job.State = L.Code("state.interruptedResumable"); Save(job); } } }
        internal void Remove(DurableTransfer job)
        { lock (gate) { if (File.Exists(FilePath(job.Id))) File.Delete(FilePath(job.Id)); FileIntegrity.DeleteSignature(FilePath(job.Id)); if (File.Exists(Receipt(job.Id))) File.Delete(Receipt(job.Id)); if (job.Lock != null) { job.Lock.Dispose(); job.Lock = null; } Jobs.Remove(job); try { File.Delete(Path.Combine(directory,job.Id + ".lock")); } catch (IOException) { } } }
        internal DurableTransfer[] Pending() { lock (gate) return Jobs.Where(x => !x.Running).ToArray(); }
        public void Dispose() { lock (gate) foreach (var job in Jobs) if (job.Lock != null) { job.Lock.Dispose(); job.Lock = null; } }
    }
}

