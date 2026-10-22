using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace CommanderCZ
{
    public sealed class ArchivePasswordException : IOException
    { public ArchivePasswordException() : base(L.Text("archive.passwordMissingOrIncorrect")) { } }
    public sealed class ArchiveItem
    {
        public string Path; public bool Directory, Encrypted; public long Bytes; public DateTime Modified;
    }
    public sealed class ArchiveDocument
    {
        public string FilePath, Password, Type, Stamp;
        public ArchiveDocument Parent;
        public bool MultiVolume;
        public string ParentMember;
        public List<ArchiveItem> Items;
        public string Display { get { return Parent == null ? FilePath : Parent.Display + "\\" + ParentMember.Replace('/', '\\'); } }
        public ArchiveDocument Root { get { return Parent == null ? this : Parent.Root; } }
    }
    public sealed class ArchiveTemp : IDisposable
    {
        public readonly string Path;
        public ArchiveTemp(string parent = null) { Path = System.IO.Path.Combine(parent ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CommanderCZ-archives"), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public void Dispose()
        {
            string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CommanderCZ-archives")).TrimEnd('\\') + "\\";
            if (!System.IO.Path.GetFullPath(Path).StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("archive.invalidTempPath"));
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }
    public static class ArchiveEngine
    {
        static readonly ConcurrentDictionary<string, object> Locks = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public static string SevenZipPath { get { return System.IO.Path.Combine(AppPaths.Resource("plugins"), "7zip", "7z.exe"); } }
        public static string SevenZipLibraryPath { get { return System.IO.Path.Combine(AppPaths.Resource("plugins"), "7zip", "7z.dll"); } }
        internal static string OutputSuffix(string path)
        { string lower = path.ToLowerInvariant(); return new[] { ".tar.gz", ".tar.bz2", ".tar.xz" }.FirstOrDefault(lower.EndsWith) ?? System.IO.Path.GetExtension(path); }
        public static bool Recognized(string path)
        { return (path.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".7z.001", StringComparison.OrdinalIgnoreCase)) || new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".tgz", ".tbz", ".txz", ".zst", ".cab", ".iso", ".wim", ".arj", ".lzh", ".lha", ".tbz2", ".bzip2", ".gzip", ".lzma", ".z", ".cpio", ".deb", ".rpm", ".xar", ".dmg", ".udf", ".vhd", ".vhdx", ".squashfs", ".apk", ".jar", ".epub" }.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()); }
        public static string SafeMember(string raw)
        {
            string name = raw.Replace('\\', '/').TrimEnd('/');
            if (String.IsNullOrEmpty(name) || name.StartsWith("/") || name.IndexOfAny(new[] { ':', '\r', '\n', '\0' }) >= 0) throw new IOException(L.Text("archive.unsafeMemberPathPrefix") + raw);
            foreach (string part in name.Split('/')) FileOperations.ValidateName(part);
            return name;
        }
        public static string DiskPath(string root, string member)
        {
            string fullRoot = System.IO.Path.GetFullPath(root).TrimEnd('\\') + "\\";
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(fullRoot, SafeMember(member).Replace('/', '\\')));
            if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("archive.pathOutsideWorkspace"));
            return path;
        }
        static string Quote(string text)
        {
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in text)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { b.Append('\\', slashes * 2 + 1); b.Append(c); }
                else { b.Append('\\', slashes); b.Append(c); }
                slashes = 0;
            }
            b.Append('\\', slashes * 2); return b.Append('"').ToString();
        }
        // Internal request option only: passwords travel over the worker pipe, never a process command line.
        static string PasswordArgument(string password) { return "-p" + password; }
        public static string Run(string executable, IEnumerable<string> arguments, string directory, CancellationToken token, Action<string> progress = null)
        {
            token.ThrowIfCancellationRequested();
            arguments = arguments.ToArray();
            if (String.Equals(System.IO.Path.GetFullPath(executable), System.IO.Path.GetFullPath(SevenZipPath), StringComparison.OrdinalIgnoreCase))
                return ArchiveWorker.Run(arguments.ToArray(), directory, token, progress);
            if (arguments.Any(x => x.StartsWith("-p", StringComparison.OrdinalIgnoreCase) || x.StartsWith("-hp", StringComparison.OrdinalIgnoreCase))) throw new IOException(L.Text("archive.commandLinePasswordRejected"));
            var limits = ArchiveSettings.Load();
            if (!File.Exists(executable)) throw new IOException(L.Text("archive.archiverMissingPrefix") + executable);
            var info = new ProcessStartInfo(executable, String.Join(" ", arguments.Select(Quote))) { WorkingDirectory = directory ?? AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            using (ArchivePluginInstaller.AcquireManagedExecutable(executable))
            using (var job = new ArchiveProcessJob(limits.MaximumMemoryMiB))
            using (var process = new Process { StartInfo = info })
            {
                var output = new StringBuilder();
                bool listing = arguments.FirstOrDefault() == "l";
                process.Start(); job.Assign(process); process.StandardInput.Close(); var clock = Stopwatch.StartNew();
                var errorTask = System.Threading.Tasks.Task.Run(delegate { var errors = new StringBuilder(); var buffer = new char[4096]; int read; while ((read = process.StandardError.Read(buffer, 0, buffer.Length)) > 0) { errors.Append(buffer, 0, read); if (errors.Length > 16000) errors.Remove(0, errors.Length - 8000); } return errors.ToString(); });
                var outputTask = System.Threading.Tasks.Task.Run(delegate
                {
                    var line = new StringBuilder(); var buffer = new char[512]; int count;
                    while ((count = process.StandardOutput.Read(buffer, 0, 1)) > 0)
                    {
                        if (output.Length > limits.MaximumListingMiB * 1048576L) throw new IOException(L.Text("archive.processListingLimit"));
                        if (!listing && output.Length > 16000) output.Remove(0, output.Length - 8000);
                        output.Append(buffer, 0, count);
                        if (progress == null) continue;
                        for (int i = 0; i < count; i++)
                        {
                            char c = buffer[i];
                            if (c == '\r' || c == '\n' || c == '\b') { if (line.Length > 0) { progress(line.ToString()); line.Clear(); } }
                            else { line.Append(c); if (line.Length > 4096) line.Clear(); if (c == '%') { progress(line.ToString()); line.Clear(); } }
                        }
                    }
                    if (progress != null && line.Length > 0) progress(line.ToString());
                });
                while (!process.WaitForExit(100))
                {
                    if (outputTask.IsFaulted || clock.Elapsed.TotalSeconds > limits.MaximumSeconds || token.IsCancellationRequested)
                    { try { process.Kill(); } catch (InvalidOperationException) { } process.WaitForExit(); token.ThrowIfCancellationRequested(); if (outputTask.IsFaulted) outputTask.GetAwaiter().GetResult(); throw new IOException(L.Text("archive.operationTimeout")); }
                }
                process.WaitForExit(); outputTask.GetAwaiter().GetResult(); string error = errorTask.GetAwaiter().GetResult(); token.ThrowIfCancellationRequested();
                if (process.ExitCode != 0)
                {
                    string details = error + "\n" + output;
                    if (details.IndexOf("Wrong password", StringComparison.OrdinalIgnoreCase) >= 0 || details.IndexOf("Enter password", StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new ArchivePasswordException();
                    if (details.Length > 1800) details = details.Substring(details.Length - 1800);
                    throw new IOException(L.Text("archive.processErrorPrefix") + process.ExitCode + "):\n" + details);
                }
                return output.ToString();
            }
        }
        static string[] Options(ArchiveDocument doc) { return new[] { "-sccUTF-8", "-y", "-spd" }.Concat(doc.Password == null ? new string[0] : new[] { PasswordArgument(doc.Password) }).ToArray(); }
        public static ArchiveDocument Open(string file, string password = null, CancellationToken token = default(CancellationToken))
        {
            var doc = new ArchiveDocument { FilePath = System.IO.Path.GetFullPath(file), Password = password };
            Reload(doc, token); return doc;
        }
        public static void Reload(ArchiveDocument doc, CancellationToken token)
        {
            var limits = ArchiveSettings.Load();
            string stamp = ReadStamp(doc.FilePath);
            string listing = Run(SevenZipPath, new[] { "l", "-slt" }.Concat(Options(doc)).Concat(new[] { "--", doc.FilePath }), null, token);
            int start = listing.IndexOf("----------", StringComparison.Ordinal); if (start < 0) throw new IOException(L.Text("archive.unreadableListing"));
            string header = listing.Substring(0, start);
            doc.MultiVolume = doc.FilePath.EndsWith(".001",StringComparison.OrdinalIgnoreCase) || header.Contains("Multivolume = +") || header.Contains("Type = Split");
            doc.Type = header.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Where(x => x.StartsWith("Type = ")).Select(x => x.Substring(7)).FirstOrDefault() ?? System.IO.Path.GetExtension(doc.FilePath).TrimStart('.');
            var items = new Dictionary<string, ArchiveItem>(StringComparer.OrdinalIgnoreCase);
            var record = new Dictionary<string, string>();
            Action commit = delegate
            {
                string raw; if (!record.TryGetValue("Path", out raw)) { record.Clear(); return; }
                string path = SafeMember(raw), value;
                if ((record.TryGetValue("Symbolic Link", out value) && value.Length > 0) || (record.TryGetValue("Hard Link", out value) && value.Length > 0) ||
                    (record.TryGetValue("Attributes", out value) && (value.Contains("l") || value.Contains("Reparse")))) throw new IOException(L.Text("archive.unsafeLinkPrefix") + path);
                bool dir = record.TryGetValue("Folder", out value) && value == "+";
                if (record.TryGetValue("Attributes", out value) && value.StartsWith("D")) dir = true;
                if (record.TryGetValue("Mode", out value) && value.StartsWith("l")) throw new IOException(L.Text("archive.linksUnsupported"));
                if (record.TryGetValue("Mode", out value) && value.StartsWith("d")) dir = true;
                long bytes = 0; if (record.TryGetValue("Size", out value) && value.Length > 0 && (!Int64.TryParse(value, out bytes) || bytes < 0)) throw new IOException(L.Text("archive.invalidMemberSize"));
                DateTime modified; if (!record.TryGetValue("Modified", out value) || !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out modified)) modified = DateTime.MinValue;
                bool encrypted = record.TryGetValue("Encrypted", out value) && value == "+";
                ArchiveItem existing;
                if (items.TryGetValue(path, out existing)) { if (!existing.Directory || !dir) throw new IOException(L.Text("archive.duplicateMemberNamesPrefix") + path); }
                else { if (items.Count >= limits.MaximumItems) throw new IOException(L.Text("archive.itemLimitExceeded")); items.Add(path, new ArchiveItem { Path = path, Directory = dir, Bytes = bytes, Modified = modified, Encrypted = encrypted }); }
                record.Clear();
            };
            foreach (string line in listing.Substring(start + 10).Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Length == 0) { commit(); continue; }
                int split = line.IndexOf(" = ", StringComparison.Ordinal);
                if (split < 0) throw new IOException(L.Text("archive.invalidMultilineMemberName"));
                if (record.ContainsKey(line.Substring(0, split))) throw new IOException(L.Text("archive.ambiguousListing"));
                record.Add(line.Substring(0, split), line.Substring(split + 3));
            }
            commit();
            foreach (var item in items.Values.ToArray())
            {
                string parent = item.Path;
                while (parent.Contains("/"))
                {
                    parent = parent.Substring(0, parent.LastIndexOf('/')); ArchiveItem existing;
                    if (items.TryGetValue(parent, out existing)) { if (!existing.Directory) throw new IOException(L.Text("archive.fileFolderCollisionPrefix") + parent); }
                    else { if (items.Count >= limits.MaximumItems) throw new IOException(L.Text("archive.itemLimitExceeded")); items.Add(parent, new ArchiveItem { Path = parent, Directory = true }); }
                }
            }
            if (ReadStamp(doc.FilePath) != stamp) throw new IOException(L.Text("archive.changedDuringLoading"));
            doc.Items = items.Values.ToList();
            doc.Stamp = stamp;
        }
        public static List<ArchiveItem> Select(ArchiveDocument doc, IEnumerable<string> members)
        {
            var paths = new HashSet<string>(members.Select(SafeMember), StringComparer.OrdinalIgnoreCase);
            var existing = new HashSet<string>(doc.Items.Select(x => x.Path), StringComparer.OrdinalIgnoreCase);
            if (paths.Any(p => !existing.Contains(p))) throw new IOException(L.Text("archive.memberNoLongerExists"));
            return doc.Items.Where(x => MemberSelected(paths, x.Path)).ToList();
        }
        internal static bool MemberSelected(HashSet<string> paths, string path)
        { while (true) { if (paths.Contains(path)) return true; int slash = path.LastIndexOf('/'); if (slash < 0) return false; path = path.Substring(0, slash); } }
        internal static void CheckNesting(ArchiveDocument parent)
        { int depth = 1, maximum = ArchiveSettings.Load().MaximumNesting; for (var p = parent; p != null; p = p.Parent) { if (++depth > maximum) throw new IOException(L.Text("archive.nestingLimitExceeded")); } }
        public static void Test(ArchiveDocument doc, CancellationToken token, Action<string> progress = null)
        {
            CheckCurrent(doc);
            using (var archiveLock = new FileStream(doc.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try { Run(SevenZipPath, new[] { "t", "-bsp1" }.Concat(Options(doc)).Concat(new[] { "--", doc.FilePath }), null, token, progress); }
                catch (ArchivePasswordException) { doc.Password = null; throw; }
            }
        }
        public static void Extract(ArchiveDocument doc, IEnumerable<string> members, string destination, CancellationToken token, Action<string> progress = null)
        {
            CheckCurrent(doc);
            using (var archiveLock = new FileStream(doc.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
            var selected = Select(doc, members); Directory.CreateDirectory(destination);
            using (var manifest = new ArchiveTemp())
            {
                string list = System.IO.Path.Combine(manifest.Path, "items.txt"); File.WriteAllLines(list, selected.Select(x => x.Path), new UTF8Encoding(false));
                try { Run(SevenZipPath, new[] { "x", "-scsUTF-8", "-bsp1", "-o" + destination, "-i@" + list }.Concat(Options(doc)).Concat(new[] { "--", doc.FilePath }), null, token, progress); }
                catch (ArchivePasswordException) { doc.Password = null; throw; }
                foreach (var item in selected)
                {
                    string path = DiskPath(destination, item.Path);
                    if (item.Directory) Directory.CreateDirectory(path);
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("archive.extractedFileIsLink"));
                }
            }
            }
        }
        public static bool Writable(ArchiveDocument doc) { return !doc.MultiVolume && new[] { "zip", "7z", "rar", "rar5" }.Contains(doc.Type.ToLowerInvariant()) && (!doc.Type.StartsWith("Rar", StringComparison.OrdinalIgnoreCase) || (String.IsNullOrEmpty(doc.Password) && (doc.Items == null || !doc.Items.Any(x => x.Encrypted)))) && (doc.Parent == null || Writable(doc.Parent)); }
        static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(stream)); }
        static string ReadStamp(string path) { var info = new FileInfo(path); return info.Length + ":" + info.LastWriteTimeUtc.Ticks + ":" + info.CreationTimeUtc.Ticks + ArchiveExtended.VolumeStamp(path); }
        static void CheckCurrent(ArchiveDocument doc)
        { if (ReadStamp(doc.FilePath) != doc.Stamp || ReadStamp(doc.Root.FilePath) != doc.Root.Stamp) throw new IOException(L.Text("archive.changedBeforeUpdate")); }
        static void CopyInput(string source, string destination, CancellationToken token, ArchiveBudget budget = null, ArchivePreparationReporter preparation = null)
        {
            budget = budget ?? new ArchiveBudget(ArchiveSettings.Load()); budget.Item(); if (preparation != null) preparation.Visit(source);
            token.ThrowIfCancellationRequested();
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("archive.addReparsePointRejectedPrefix") + source);
            if (Directory.Exists(source))
            { Directory.CreateDirectory(destination); foreach (string child in Directory.EnumerateFileSystemEntries(source)) CopyInput(child, System.IO.Path.Combine(destination, System.IO.Path.GetFileName(child)), token, budget, preparation); }
            else
            { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)); using (var input = File.OpenRead(source)) using (var output = File.Create(destination)) { var buffer = new byte[1048576]; int n; while ((n = input.Read(buffer, 0, buffer.Length)) > 0) { token.ThrowIfCancellationRequested(); budget.Write(n, destination, true); output.Write(buffer, 0, n); if (preparation != null) preparation.Copied(n); } } File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source)); }
        }
        public static void Add(ArchiveDocument doc, string prefix, IEnumerable<string> sources, bool overwrite, ArchiveSettings settings, CancellationToken token, Action<string> progress = null)
        {
            lock (Locks.GetOrAdd(doc.Root.FilePath, delegate { return new object(); }))
            using (var input = new ArchiveTemp())
            {
                CheckCurrent(doc);
                var budget = new ArchiveBudget(ArchiveSettings.Load());
                foreach (string source in sources)
                {
                    string name = SafeMember((prefix.Length == 0 ? "" : prefix + "/") + System.IO.Path.GetFileName(source.TrimEnd('\\', '/')));
                    CopyInput(source, DiskPath(input.Path, name), token, budget);
                }
                if (!overwrite)
                    foreach (string file in Directory.GetFiles(input.Path, "*", SearchOption.AllDirectories))
                    {
                        string member = file.Substring(input.Path.Length + 1).Replace('\\', '/');
                        if (doc.Items.Any(x => !x.Directory && x.Path.Equals(member, StringComparison.OrdinalIgnoreCase))) File.Delete(file);
                    }
                var files = Directory.GetFiles(input.Path, "*", SearchOption.AllDirectories).Select(x => x.Substring(input.Path.Length + 1).Replace('\\', '/')).ToList();
                files.AddRange(Directory.GetDirectories(input.Path, "*", SearchOption.AllDirectories).Where(x => Directory.GetFileSystemEntries(x).Length == 0).Select(x => x.Substring(input.Path.Length + 1).Replace('\\', '/')));
                if (files.Count == 0) return;
                foreach (string member in files)
                {
                    var existing = doc.Items.FirstOrDefault(x => x.Path.Equals(member, StringComparison.OrdinalIgnoreCase));
                    if (existing != null && existing.Directory != Directory.Exists(DiskPath(input.Path, member))) throw new IOException(L.Text("archive.fileFolderCollisionPrefix") + member);
                }
                Change(doc, input.Path, files, false, settings, token, progress);
            }
        }
        public static void Remove(ArchiveDocument doc, IEnumerable<string> members, ArchiveSettings settings, CancellationToken token, Action<string> progress = null)
        { var files = Select(doc, members).Select(x => x.Path).ToList(); Change(doc, null, files, true, settings, token, progress); }
        public static void ReplaceMember(ArchiveDocument doc, string member, string source, ArchiveSettings settings, CancellationToken token, Action<string> progress = null)
        {
            member = SafeMember(member);
            using (var temp = new ArchiveTemp())
            { CopyInput(source, DiskPath(temp.Path, member), token); Change(doc, temp.Path, new List<string> { member }, false, settings, token, progress); }
        }
        public static void Rename(ArchiveDocument doc, string member, string name, ArchiveSettings settings, CancellationToken token, Action<string> progress = null)
        {
            FileOperations.ValidateName(name); member = SafeMember(member);
            string target = (member.Contains("/") ? member.Substring(0, member.LastIndexOf('/') + 1) : "") + name;
            if (target == member) return;
            if (doc.Items.Any(x => x.Path.Equals(target, StringComparison.OrdinalIgnoreCase))) throw new IOException(L.Text("archive.memberNameExists"));
            using (var input = new ArchiveTemp())
            {
                var old = Select(doc, new[] { member }); Extract(doc, new[] { member }, input.Path, token, progress);
                string from = DiskPath(input.Path, member), to = DiskPath(input.Path, target);
                if (Directory.Exists(from)) Directory.Move(from, to); else File.Move(from, to);
                var names = old.Select(x => target + x.Path.Substring(member.Length)).ToList();
                Change(doc, input.Path, names, false, settings, token, progress, old.Select(x => x.Path).ToList());
            }
        }
        public static void Create(string target, IEnumerable<string> sources, ArchiveSettings settings, CancellationToken token, Action<string> progress = null, ArchivePackOptions pack = null)
        {
            settings.ValidateCreationTool(target);
            if (pack != null && (pack.Move || pack.VolumeBytes > 0)) { ArchiveExtended.Create(target,sources,settings,token,progress,pack); return; }
            string lower = target.ToLowerInvariant();
            string suffix = new[] { ".tar.gz", ".tar.bz2", ".tar.xz", ".tgz", ".tbz2", ".txz" }.FirstOrDefault(lower.EndsWith);
            if (suffix == null) { CreateCore(target, sources, settings, token, progress, pack); return; }
            pack = pack ?? new ArchivePackOptions();
            if (!String.IsNullOrEmpty(pack.Password)) throw new IOException(L.Text("archive.passwordRequiresZipOr7z"));
            using (var temp = new ArchiveTemp()) {
                string tar = System.IO.Path.Combine(temp.Path, System.IO.Path.GetFileName(target).Substring(0, System.IO.Path.GetFileName(target).Length - suffix.Length) + ".tar");
                Action<string> preparing = delegate(string text) {
                    if (progress == null) return;
                    if (text.StartsWith("@archive-preparation|", StringComparison.Ordinal)) { progress(text); return; }
                    ArchiveCompressionProgress update;
                    progress(L.Text("archive.buildingTar") + " " + (ArchiveCompressionProgress.TryParse(text, out update) ? update.Percent.ToString("F0") + "%" : text));
                };
                CreateCore(tar, sources, settings, token, preparing, pack);
                CreateCore(target, new[] { tar }, settings, token, progress, new ArchivePackOptions { Level = pack.Level, NewestFileDate = pack.NewestFileDate });
            }
        }
        static void CreateCore(string target, IEnumerable<string> sources, ArchiveSettings settings, CancellationToken token, Action<string> progress, ArchivePackOptions pack)
        {
            pack = pack ?? new ArchivePackOptions();
            string extension = System.IO.Path.GetExtension(target).ToLowerInvariant();
            if (!new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".tgz", ".tbz2", ".txz", ".arj", ".lzh", ".lha" }.Contains(extension)) throw new IOException(L.Text("archive.outputExtensionRequired"));
            lock (Locks.GetOrAdd(System.IO.Path.GetFullPath(target), delegate { return new object(); }))
            using (var input = new ArchiveTemp())
            {
                bool legacy = extension == ".arj" || extension == ".lzh" || extension == ".lha";
                if (legacy) settings.LegacyExecutable(extension == ".arj");
                var sourceFiles = sources.Select(System.IO.Path.GetFullPath).ToArray();
                bool streamFormat = new[] { ".gz", ".bz2", ".xz", ".tgz", ".tbz2", ".txz" }.Contains(extension);
                if (streamFormat && (sourceFiles.Length != 1 || !File.Exists(sourceFiles[0]))) throw new IOException(L.Text("archive.singleStreamRequired"));
                if (!String.IsNullOrEmpty(pack.Password) && extension != ".zip" && extension != ".7z") throw new IOException(L.Text("archive.passwordRequiresZipOr7z"));
                string directRoot = sourceFiles.Length > 0 ? System.IO.Path.GetDirectoryName(sourceFiles[0]) : null;
                bool direct = !legacy && sourceFiles.Length > 0 && sourceFiles.Length <= 128 && sourceFiles.All(x => File.Exists(x) && String.Equals(System.IO.Path.GetDirectoryName(x), directRoot, StringComparison.OrdinalIgnoreCase) && !String.Equals(x, System.IO.Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase));
                var preparation = new ArchivePreparationReporter(progress);
                var inputLocks = new List<FileStream>(); string workingInput = direct ? directRoot : input.Path;
                string previous = File.Exists(target) ? Hash(target) : null;
                string stage = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(target), ".CommanderCZ-" + Guid.NewGuid().ToString("N") + extension);
                if (extension == ".rar" && !String.IsNullOrEmpty(pack.Password)) throw new IOException(L.Text("archive.passwordRequiresZipOr7z"));
                var budget = new ArchiveBudget(ArchiveSettings.Load());
                try
                {
                    foreach (string source in sourceFiles)
                    {
                        if (direct) {
                            token.ThrowIfCancellationRequested(); budget.Item();
                            for (string ancestor = source; !String.IsNullOrEmpty(ancestor); ancestor = System.IO.Path.GetDirectoryName(ancestor)) if ((File.GetAttributes(ancestor) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("archive.addLinkRejectedPrefix") + source);
                            inputLocks.Add(new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read));
                        } else StagePackedInput(source, input.Path, pack.ExcludeBase && Directory.Exists(source) ? "" : SafeMember(System.IO.Path.GetFileName(source.TrimEnd('\\', '/'))), pack, token, budget, preparation);
                    }
                    preparation.Complete();
                    var doc = new ArchiveDocument { FilePath = stage, Type = extension.TrimStart('.'), Password = pack.Password };
                    DateTime? newestFile = null; bool archiveDate = pack.NewestFileDate ?? ProgramSettings.Load().ArchiveNewestFileDate;
                    long originalBytes = 0; foreach (string file in direct ? sourceFiles : Directory.EnumerateFiles(input.Path, "*", SearchOption.AllDirectories)) { token.ThrowIfCancellationRequested(); originalBytes = checked(originalBytes + new FileInfo(file).Length); if (archiveDate) { DateTime written = File.GetLastWriteTimeUtc(file); if (!newestFile.HasValue || written > newestFile.Value) newestFile = written; } }
                    if (originalBytes > budget.MaximumBytes) throw new IOException(L.Text("archive.actualExpandedDataLimit"));
                    Action<string> compressionProgress = delegate(string text) {
                        if (progress == null) return;
                        ArchiveCompressionProgress nativeProgress; if (ArchiveCompressionProgress.TryParse(text, out nativeProgress)) { progress(text); return; }

                        var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)\s*%");
                        if (!match.Success) { progress(text); return; }
                        long packed = 0; try { if (File.Exists(stage)) packed = new FileInfo(stage).Length; } catch (IOException) { }
                        progress(new ArchiveCompressionProgress { Percent = Math.Min(99, Double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)), Original = originalBytes, Packed = packed }.Encode());
                    };
                    if (progress != null) progress(new ArchiveCompressionProgress { Original = originalBytes }.Encode());
                    var members = (direct ? sourceFiles : Directory.GetFileSystemEntries(input.Path)).Select(x => System.IO.Path.GetFileName(x)).ToList();
                    var compressionOptions = new[] { extension == ".rar" ? "-m" + Math.Min(5, (pack.Level * 5 + 4) / 9) : "-mx=" + pack.Level }.Concat(direct && extension != ".rar" ? new[] { "-commander-direct" } : new string[0]).ToArray();
                    try { Write(doc, stage, workingInput, members, false, settings, token, compressionProgress, compressionOptions); }
                    catch (ArchiveMemoryException) {
                        if (extension != ".7z") throw;
                        token.ThrowIfCancellationRequested(); if (File.Exists(stage)) File.Delete(stage);
                        if (progress != null) progress(L.Code("state.verifying") + " " + L.Text("archive.memoryRetry"));
                        Write(doc, stage, workingInput, members, false, settings, token, compressionProgress, compressionOptions.Concat(new[] { "-commander-low-memory" }).ToArray());
                    }
                    if (progress != null) progress(L.Text("archive.verifying"));
                    try {
                        Run(SevenZipPath, new[] { "t" }.Concat(Options(doc)).Concat(new[] { "--", stage }), null, token);
                        var created = Open(stage, pack.Password, token);
                        if (legacy) VerifyLegacyArchive(created, input.Path, extension == ".arj" ? "arj" : "lzh", token);
                    } catch (IOException ex) {
                        if (!legacy) throw;
                        throw new IOException(L.Text("archive.legacyVerificationFailed") + "\n" + ex.Message, ex);
                    }
                    token.ThrowIfCancellationRequested();
                    if (newestFile.HasValue) File.SetLastWriteTimeUtc(stage, newestFile.Value);
                    if (previous != null)
                    { if (!File.Exists(target) || Hash(target) != previous) throw new IOException(L.Text("archive.destinationChanged")); File.Replace(stage, target, null); }
                    else File.Move(stage, target);
                    if (progress != null) progress(new ArchiveCompressionProgress { Percent = 100, Original = originalBytes, Packed = new FileInfo(target).Length, Complete = true }.Encode());
                }
                finally { foreach (var sourceLock in inputLocks) sourceLock.Dispose(); if (File.Exists(stage)) File.Delete(stage); }
            }
        }
        static void VerifyLegacyArchive(ArchiveDocument doc, string input, string expectedFormat, CancellationToken token)
        {
            if (doc.Type != expectedFormat) throw new IOException(L.Text("archive.legacyVerificationFailed"));
            using (var verify = new ArchiveTemp()) {
                var names = Directory.GetFiles(input, "*", SearchOption.AllDirectories).Select(x => x.Substring(input.Length + 1).Replace('\\', '/')).ToArray();
                if (doc.Items.Count(x => !x.Directory) != names.Length || names.Any(x => !doc.Items.Any(item => !item.Directory && item.Path == x))) throw new IOException(L.Text("archive.legacyVerificationFailed"));
                Extract(doc, doc.Items.Select(x => x.Path).ToArray(), verify.Path, token);
                foreach (string name in names) { token.ThrowIfCancellationRequested(); if (Hash(DiskPath(input, name)) != Hash(DiskPath(verify.Path, name))) throw new IOException(L.Text("archive.legacyVerificationFailed")); }
                foreach (string dir in Directory.GetDirectories(input, "*", SearchOption.AllDirectories)) {
                    string relative = dir.Substring(input.Length + 1).Replace('\\', '/');
                    if (!Directory.Exists(DiskPath(verify.Path, relative))) throw new IOException(L.Text("archive.legacyVerificationFailed"));
                }
            }
        }
        static void StagePackedInput(string source, string root, string member, ArchivePackOptions options, CancellationToken token, ArchiveBudget budget, ArchivePreparationReporter preparation)
        {
            token.ThrowIfCancellationRequested();
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("archive.addLinkRejectedPrefix") + source);
            if (Directory.Exists(source))
            {
                budget.Item(); preparation.Visit(source);
                if (options.Paths && member.Length > 0) Directory.CreateDirectory(DiskPath(root, member));
                foreach (string child in Directory.GetFileSystemEntries(source))
                {
                    if (Directory.Exists(child) && !options.Recursive) continue;
                    string name = SafeMember(System.IO.Path.GetFileName(child));
                    StagePackedInput(child, root, options.Paths && member.Length > 0 ? member + "/" + name : name, options, token, budget, preparation);
                }
            }
            else
            {
                string target = DiskPath(root, options.Paths ? member : SafeMember(System.IO.Path.GetFileName(source)));
                if (File.Exists(target) || Directory.Exists(target)) throw new IOException(L.Text("archive.duplicateInputNamesPrefix") + member);
                CopyInput(source, target, token, budget, preparation);
            }
        }
        static void Write(ArchiveDocument doc, string stage, string input, List<string> files, bool delete, ArchiveSettings settings, CancellationToken token, Action<string> progress, string[] extra = null)
        {
            using (var lists = new ArchiveTemp())
            {
                if (doc.Type == "arj" || doc.Type == "lzh" || doc.Type == "lha") {
                    if (delete || File.Exists(stage) || !String.IsNullOrEmpty(doc.Password)) throw new IOException(L.Text("archive.readOnlyFormat"));
                    bool arj = doc.Type == "arj";
                    var args = new List<string>(arj ? new[] { "a", "-+", "-r", "-y", "-a1", "-m1", stage } : new[] { "a", stage });
                    if (!arj && files.Any(x => x.StartsWith("-", StringComparison.Ordinal) || x.StartsWith("@", StringComparison.Ordinal))) throw new IOException(L.Text("archive.legacyUnsafeName"));
                    args.AddRange(files.Select(x => arj ? "." + System.IO.Path.DirectorySeparatorChar + x : x));
                    if (args.Sum(x => x.Length + 3) > 28000) throw new IOException(L.Text("archive.legacyCommandTooLong"));
                    Run(settings.LegacyExecutable(arj), args, input, token, progress); return;
                }
                bool rar = doc.Type.StartsWith("Rar", StringComparison.OrdinalIgnoreCase);
                if (rar && (!String.IsNullOrEmpty(doc.Password) || (doc.Items != null && doc.Items.Any(x => x.Encrypted)))) throw new IOException(L.Text("archive.encryptedRarWriteRejected"));
                string manifest = System.IO.Path.Combine(lists.Path, "items.txt"); File.WriteAllLines(manifest, files.Select(x => x.Replace('/', '\\')), rar ? Encoding.Unicode : new UTF8Encoding(false));
                if (rar)
                    Run(settings.RarExecutable(), new[] { delete ? "d" : "a", "-cfg-", "-scul", "-idc", "-y" }.Concat(extra ?? new string[0]).Concat(new[] { stage, "@" + manifest }), input, token, progress);
                else
                    Run(SevenZipPath, new[] { delete ? "d" : "a", "-scsUTF-8", "-bsp1", "-sse", "-i@" + manifest }.Concat(extra ?? new string[0]).Concat(Options(doc)).Concat(new[] { "--", stage }), input, token, progress);
            }
        }
        static void Change(ArchiveDocument doc, string input, List<string> files, bool delete, ArchiveSettings settings, CancellationToken token, Action<string> progress, List<string> beforeDelete = null)
        {
            lock (Locks.GetOrAdd(doc.Root.FilePath, delegate { return new object(); }))
            {
                CheckCurrent(doc);
                if (!Writable(doc)) throw new IOException(L.Text("archive.readOnlyFormat"));
                for (var child = doc; child.Parent != null; child = child.Parent)
                    using (var actual = new ArchiveTemp())
                    {
                        Extract(child.Parent, new[] { child.ParentMember }, actual.Path, token);
                        if (Hash(DiskPath(actual.Path, child.ParentMember)) != Hash(child.FilePath)) throw new IOException(L.Text("archive.nestedArchiveChanged"));
                    }
                string originalHash = Hash(doc.Root.FilePath);
                Commit(doc, input, files, delete, settings, token, progress, originalHash, beforeDelete);
            }
        }
        static void Commit(ArchiveDocument doc, string input, List<string> files, bool delete, ArchiveSettings settings, CancellationToken token, Action<string> progress, string originalHash, List<string> beforeDelete = null)
        {
            string stage = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(doc.FilePath), ".CommanderCZ-" + Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(doc.FilePath));
            try
            {
                token.ThrowIfCancellationRequested(); File.Copy(doc.FilePath, stage);
                if (beforeDelete != null) Write(doc, stage, null, beforeDelete, true, settings, token, progress);
                Write(doc, stage, input, files, delete, settings, token, progress);
                // WinRAR removes the container when its last item is deleted.
                // Keep a valid empty folder-like container (RARLAB RAR5 header specification).
                if (delete && !File.Exists(stage) && doc.Type.StartsWith("Rar", StringComparison.OrdinalIgnoreCase)) EmptyRar(stage);
                if (progress != null) progress(L.Text("archive.verifyingStatus"));
                Run(SevenZipPath, new[] { "t" }.Concat(Options(doc)).Concat(new[] { "--", stage }), null, token);
                // Validate paths in the newly written archive before it replaces the original.
                var checkedDoc = Open(stage, doc.Password, token);
                if (doc.Parent != null)
                {
                    using (var parentInput = new ArchiveTemp())
                    {
                        string member = DiskPath(parentInput.Path, doc.ParentMember); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(member)); File.Copy(stage, member);
                        Commit(doc.Parent, parentInput.Path, new List<string> { doc.ParentMember }, false, settings, token, progress, originalHash);
                    }
                }
                if (doc.Parent == null)
                { token.ThrowIfCancellationRequested(); if (Hash(doc.FilePath) != originalHash) throw new IOException(L.Text("archive.changedOriginalPreserved")); }
                File.Replace(stage, doc.FilePath, null);
                doc.Items = checkedDoc.Items;
                doc.Type = checkedDoc.Type; doc.MultiVolume = checkedDoc.MultiVolume;
                doc.Stamp = ReadStamp(doc.FilePath);
            }
            catch (ArchivePasswordException) { doc.Password = null; throw; }
            finally { if (File.Exists(stage)) File.Delete(stage); }
        }
        static void EmptyRar(string path)
        {
            // https://www.rarlab.com/technote.htm: signature, main header, end header.
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1a, 7, 1, 0 });
                foreach (byte type in new byte[] { 1, 5 })
                {
                    byte[] header = { 3, type, 0, 0 }; uint crc = 0xffffffff;
                    foreach (byte value in header) { crc ^= value; for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u); }
                    writer.Write(~crc); writer.Write(header);
                }
            }
        }
    }
}




