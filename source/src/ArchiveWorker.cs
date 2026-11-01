using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using SharpSevenZip;
using SharpSevenZip.Exceptions;
using SharpSevenZip.EventArguments;

namespace FileCompass
{
    // The isolated worker owns the native codec. Only a private pipe carries passwords.
    internal static partial class ArchiveWorker
    {
        internal static void Send(BinaryWriter writer, string text)
        { byte[] bytes = Encoding.UTF8.GetBytes(text ?? ""); writer.Write(bytes.Length); writer.Write(bytes); writer.Flush(); }
        internal static string Receive(BinaryReader reader, int maximum)
        { int length = reader.ReadInt32(); if (length < 0 || length > maximum) throw new IOException(L.Text("archive.workerMessageLimit")); byte[] data = reader.ReadBytes(length); if (data.Length != length) throw new EndOfStreamException(); return Encoding.UTF8.GetString(data); }

        internal static string Run(string[] arguments, string directory, CancellationToken token, Action<string> progress)
        {
            var settings = ArchiveSettings.Load(); settings.ValidateLimits();
            if (arguments.Length > 0 && (arguments[0] == "a" || arguments[0] == "d")) ArchiveMemoryPlan.Choose(settings.MaximumMemoryMiB, ArchiveMemoryPlan.AvailableBytes(), 0, 1, false);
            string name = "FileCompass-archive-" + Guid.NewGuid().ToString("N");
            var acl = new PipeSecurity(); acl.SetAccessRuleProtection(true, false);
            acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
            acl.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
            using (PluginIntegrity.Acquire(@"7zip\7z.dll"))
            using (var scratch = new ArchiveTemp())
            using (var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, acl))
            using (var job = new ArchiveProcessJob(settings.MaximumMemoryMiB))
            using (var child = Process.Start(WorkerStart(name)))
            {
                var errors = Task.Run(delegate { return DrainErrors(child.StandardError); });
                try
                {
                    job.Assign(child);
                    var clock = Stopwatch.StartNew();
                    var connect = pipe.WaitForConnectionAsync();
                    while (!connect.Wait(50)) { Check(child, clock, settings, token, errors); }
                    connect.GetAwaiter().GetResult();
                    uint client; if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out client) || client != child.Id) throw new IOException(L.Text("archive.workerPipeClientRejected"));
                    using (var writer = new BinaryWriter(pipe, Encoding.UTF8, true))
                    {
                        using (var xml = new StringWriter()) { new XmlSerializer(typeof(ArchiveSettings)).Serialize(xml, settings); Send(writer, xml.ToString()); }
                        Send(writer, directory ?? ""); Send(writer, scratch.Path); writer.Write(arguments.Length);
                        foreach (string argument in arguments) Send(writer, argument);
                        writer.Flush();
                    }
                    var read = Task.Run(delegate
                    {
                        bool reported = false; var notification = Stopwatch.StartNew();
                        using (var reader = new BinaryReader(pipe, Encoding.UTF8, true))
                        {
                            while (true)
                            {
                                byte kind = reader.ReadByte(); string text = Receive(reader, kind == 1 ? 4096 : checked(settings.MaximumListingMiB * 1048576));
                                if (kind == 1) { if (progress != null && (!reported || notification.ElapsedMilliseconds >= 100 || text.StartsWith("@archive-compression|", StringComparison.Ordinal) || text.TrimEnd().EndsWith("100%", StringComparison.Ordinal))) { progress(text); reported = true; notification.Restart(); } continue; }
                                if (kind == 2) return text;
                                if (kind == 3) throw new ArchivePasswordException();
                                if (kind == 5) throw new ArchiveMemoryException();
                                throw new IOException(text);
                            }
                        }
                    });
                    while (!read.Wait(50)) { if (child.HasExited && read.Wait(1000)) break; Check(child, clock, settings, token, errors); }
                    return read.GetAwaiter().GetResult();
                }
                catch (AggregateException ex)
                { var cause = ex.Flatten().InnerExceptions[0]; if(cause is OperationCanceledException)throw cause;if (cause is IOException && !(cause is EndOfStreamException)) throw cause; throw new IOException(L.Text("archive.workerFailedOrMemoryExceeded"), cause); }
                finally {
                    // The result can arrive just before normal process teardown. Give it
                    // time to exit instead of racing TerminateProcess against teardown.
                    try { if (!child.WaitForExit(500)) { child.Kill(); child.WaitForExit(); } }
                    catch (System.ComponentModel.Win32Exception) { if (!child.WaitForExit(1000)) throw; }
                    errors.GetAwaiter().GetResult();
                }
            }
        }
        static ProcessStartInfo WorkerStart(string name){
            var start=new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--archive-worker "+name){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
#if APP_TESTS
            if(AppPaths.TestProfileDirectory!=null)start.EnvironmentVariables["FILECOMPASS_TEST_PROFILE"]=AppPaths.TestProfileDirectory;
#endif
            return start;
        }
        static string DrainErrors(StreamReader reader)
        {
            var text = new StringBuilder(); var buffer = new char[4096]; int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0) { text.Append(buffer, 0, read); if (text.Length > 16000) text.Remove(0, text.Length - 8000); }
            return text.ToString();
        }
        static void Check(Process child, Stopwatch clock, ArchiveSettings settings, CancellationToken token, Task<string> errors)
        {
            OperationPause.HoldChild(child,token,clock);
            token.ThrowIfCancellationRequested();
            if (clock.Elapsed.TotalSeconds > settings.MaximumSeconds) throw new IOException(L.Text("archive.operationTimeout"));
            if (child.HasExited) throw new IOException(L.Text("archive.workerExitedPrefix") + child.ExitCode + L.Text("archive.workerExitMemorySuffix") + errors.GetAwaiter().GetResult());
        }
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetNamedPipeClientProcessId(IntPtr pipe, out uint process);

        internal static int Serve(string pipeName)
        {
            Guid id;
            if (!pipeName.StartsWith("FileCompass-archive-", StringComparison.Ordinal) || !Guid.TryParseExact(pipeName.Substring("FileCompass-archive-".Length), "N", out id)) return 87;
            using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None, TokenImpersonationLevel.Identification))
            {
                pipe.Connect(15000);
                using (var reader = new BinaryReader(pipe, Encoding.UTF8, true))
                using (var writer = new BinaryWriter(pipe, Encoding.UTF8, true))
                {
                ArchiveBudget budget = null;
                string secret = null;
                try
                {
                    ArchiveSettings settings;
                    using (var input = new StringReader(Receive(reader, 65536)))
                    using (var xml = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) settings = (ArchiveSettings)new XmlSerializer(typeof(ArchiveSettings)).Deserialize(xml);
                    settings.ValidateLimits(); string directory = Receive(reader, 32768); string scratch = Receive(reader, 32768); int count = reader.ReadInt32();
                    if (count < 1 || count > 10000) throw new IOException(L.Text("archive.invalidCommand"));
                    string[] arguments = new string[count]; for (int i = 0; i < count; i++) arguments[i] = Receive(reader, 65536);
                    secret = arguments.FirstOrDefault(x => x.StartsWith("-p", StringComparison.Ordinal)); if (secret != null) secret = secret.Substring(2);
                    using (PluginIntegrity.Acquire(@"7zip\7z.dll"))
                    {
                        SharpSevenZipBase.SetLibraryPath(ArchiveEngine.SevenZipLibraryPath);
                        budget = new ArchiveBudget(settings){ParentMonitored=true};
                        string result = Execute(arguments, directory, scratch, budget, delegate(string text) { lock (writer) { writer.Write((byte)1); Send(writer, text); } });
                        writer.Write((byte)2); Send(writer, result);
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    if (budget != null && budget.Failure != null) ex = budget.Failure;
                    bool memory = ArchiveMemoryPlan.IsFailure(ex);
                    bool password = !memory && (ex is ArchivePasswordException || ex is SharpSevenZipArchiveException || (ex is SharpSevenZipException && !String.IsNullOrEmpty(secret)));
                    string message = ex.Message; if (!String.IsNullOrEmpty(secret)) message = message.Replace(secret, "***");
                    writer.Write((byte)(memory ? 5 : password ? 3 : 4)); Send(writer, memory ? L.Text("archive.memoryFailed") : password ? L.Text("archive.incorrectPassword") : L.Text("archive.summaryPrefix") + message); return 1;
                }
                }
            }
        }

        static string Execute(string[] args, string directory, string scratch, ArchiveBudget budget, Action<string> progress)
        {
            string command = args[0]; string password = args.FirstOrDefault(x => x.StartsWith("-p", StringComparison.Ordinal)); password = password == null ? null : password.Substring(2);
            int separator = Array.IndexOf(args, "--");
            int archiveIndex = separator >= 0 ? separator + 1 : Array.FindIndex(args, 1, x => !x.StartsWith("-", StringComparison.Ordinal));
            if (archiveIndex < 0 || archiveIndex >= args.Length) throw new IOException(L.Text("archive.archivePathMissing"));
            string archive = Path.GetFullPath(args[archiveIndex]);
            if (command == "a" || command == "d") { WriteArchive(archive, args, archiveIndex, directory, scratch, password, command == "d", budget, progress); return ""; }
            using (var extractor = Open(archive, password))
            {
                var entries = Entries(extractor, budget);
                if (command == "l")
                {
                    var result = new StringBuilder("Type = " + Format(extractor.Format) + "\nMultivolume = " + (extractor.VolumeFileNames.Count > 1 ? "+" : "-") + "\n----------\n");
                    foreach (var item in entries)
                    {
                        result.Append("Path = ").Append(item.FileName).Append("\nFolder = ").Append(item.IsDirectory ? "+" : "-").Append("\nSize = ").Append(item.Size).Append("\nModified = ").Append(item.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)).Append("\nEncrypted = ").Append(item.Encrypted ? "+" : "-").Append("\n\n");
                        if (result.Length > budget.Settings.MaximumListingMiB * 1048576L / 3) throw new IOException(L.Text("archive.workerListingLimit"));
                    }
                    return result.ToString();
                }
                if (command != "x" && command != "t") throw new IOException(L.Text("archive.unsupportedLibraryCommandPrefix") + command);
                string selection = args.FirstOrDefault(x => x.StartsWith("-i@", StringComparison.Ordinal));
                var wanted = selection == null ? null : new HashSet<string>(File.ReadAllLines(selection.Substring(3), Encoding.UTF8).Select(ArchiveEngine.SafeMember), StringComparer.OrdinalIgnoreCase);
                string destination = args.FirstOrDefault(x => x.StartsWith("-o", StringComparison.Ordinal)); destination = destination == null ? null : destination.Substring(2);
                foreach (var item in entries)
                {
                    if (wanted != null && !wanted.Contains(item.FileName)) continue;
                    budget.Check(); progress(L.Text("archive.extractingMemberPrefix") + item.FileName);
                    string path = command == "x" ? ArchiveEngine.DiskPath(destination, item.FileName) : null;
                    if (path != null) RejectLinks(path);
                    if (item.IsDirectory) { if (path != null) Directory.CreateDirectory(path); continue; }
                    if (path != null) Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var output = path == null ? Stream.Null : new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var bounded = new ArchiveOutputStream(output, budget, path))
                    {
                        try { extractor.ExtractFile(item.Index, bounded); }
                        catch (SharpSevenZipException) { if (item.Encrypted) throw new ArchivePasswordException(); throw; }
                    }
                    if (path != null) File.SetLastWriteTime(path, item.LastWriteTime);
                }
                return "";
            }
        }
        static SharpSevenZipExtractor Open(string path, string password)
        {
            SharpSevenZipExtractor extractor = null;
            try { extractor = password == null ? new SharpSevenZipExtractor(path) : new SharpSevenZipExtractor(path, password); var count = extractor.FilesCount; return extractor; }
            catch (SharpSevenZipException) {
                if(extractor!=null)extractor.Dispose();
                if(Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase))foreach(var format in new[]{InArchiveFormat.SevenZip,InArchiveFormat.Zip,InArchiveFormat.Rar,InArchiveFormat.Arj}){
                    extractor=null;try{extractor=password==null?new SharpSevenZipExtractor(path,format):new SharpSevenZipExtractor(path,password,format);var count=extractor.FilesCount;return extractor;}catch(SharpSevenZipException){if(extractor!=null)extractor.Dispose();}
                }
                throw new ArchivePasswordException();
            }
            catch { if (extractor != null) extractor.Dispose(); throw; }
        }
        static List<ArchiveFileInfo> Entries(SharpSevenZipExtractor extractor, ArchiveBudget budget)
        {
            if (extractor.FilesCount > budget.Settings.MaximumItems) throw new IOException(L.Text("archive.itemLimitExceeded"));
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
            var result = new List<ArchiveFileInfo>();
            foreach (var original in extractor.ArchiveFileData)
            {
                budget.Check(); var item = original; item.FileName = ArchiveEngine.SafeMember(item.FileName);
                if (!names.Add(item.FileName)) throw new IOException(L.Text("archive.duplicateMemberNames"));
                if ((item.Attributes & (uint)FileAttributes.ReparsePoint) != 0 || ((item.Attributes >> 16) & 0xf000) == 0xa000) throw new IOException(L.Text("archive.containsLink"));
                if (item.Size > (ulong)budget.MaximumBytes || total > budget.MaximumBytes - (long)item.Size) throw new IOException(L.Text("archive.expandedSizeLimit"));
                total += (long)item.Size; result.Add(item);
            }
            return result;
        }
        static string Format(InArchiveFormat format) { return format == InArchiveFormat.SevenZip ? "7z" : format.ToString().ToLowerInvariant(); }
        static void RejectLinks(string path)
        {
            for (string p = Path.GetFullPath(path); !String.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
                if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("archive.destinationContainsLink"));
        }

        static void WriteArchive(string archive, string[] args, int archiveIndex, string directory, string scratch, string password, bool delete, ArchiveBudget budget, Action<string> progress)
        {
            using (var staging = new ArchiveTemp(scratch))
            {
                string list = args.FirstOrDefault(x => x.StartsWith("-i@", StringComparison.Ordinal));
                string[] members = list == null ? args.Skip(archiveIndex + 1).ToArray() : File.ReadAllLines(list.Substring(3), Encoding.UTF8);
                string[] directFiles = null;
                if (args.Contains("-filecompass-direct") && !File.Exists(archive) && !delete) {
                    directFiles = members.Select(member => { if (member != Path.GetFileName(member) || member == "." || member == "..") throw new IOException(L.Text("archive.archivePathMissing")); string file = Path.Combine(directory, member); RejectLinks(file); if (!File.Exists(file)) throw new FileNotFoundException(member); return file; }).ToArray();
                }
                var memberSet = new HashSet<string>(members.Select(x => x.Replace('\\', '/')), StringComparer.OrdinalIgnoreCase);
                if (File.Exists(archive))
                {
                    using (var old = Open(archive, password))
                    {
                        foreach (var item in Entries(old, budget))
                        {
                            bool omit = delete ? ArchiveEngine.MemberSelected(memberSet, item.FileName) : memberSet.Contains(item.FileName);
                            if (omit) continue;
                            string path = ArchiveEngine.DiskPath(staging.Path, item.FileName);
                            if (item.IsDirectory) { Directory.CreateDirectory(path); continue; }
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                            using (var bounded = new ArchiveOutputStream(output, budget, path))
                                try { old.ExtractFile(item.Index, bounded); } catch (SharpSevenZipException) { if (item.Encrypted) throw new ArchivePasswordException(); throw; }
                            File.SetLastWriteTime(path, item.LastWriteTime);
                        }
                    }
                }
                var preparation = new ArchivePreparationReporter(progress, directory);
                if (!delete && directFiles == null)
                    foreach (string member in members)
                    {
                        string source = Path.IsPathRooted(member) ? member : Path.Combine(directory.Length == 0 ? Environment.CurrentDirectory : directory, member);
                        string relative = Path.IsPathRooted(member) ? Path.GetFileName(member) : member;
                        Copy(source, ArchiveEngine.DiskPath(staging.Path, relative), budget, preparation);
                    }
                if (!delete && directFiles == null) preparation.Complete();
                string extension = Path.GetExtension(archive).ToLowerInvariant();
                int itemCount = 0;
                foreach (string item in directFiles ?? Directory.EnumerateFileSystemEntries(staging.Path, "*", SearchOption.AllDirectories)) { budget.Check(); if (++itemCount > budget.Settings.MaximumItems) throw new IOException(L.Text("archive.createdArchiveItemLimit")); }
                if (itemCount == 0 && extension == ".tar") { File.WriteAllBytes(archive, new byte[1024]); return; }
                if (itemCount == 0) { WriteEmpty(archive, extension == ".zip"); return; }
                bool streamFormat = new[] { ".gz", ".tgz", ".bz2", ".tbz2", ".xz", ".txz" }.Contains(extension);
                OutArchiveFormat outputFormat = extension == ".zip" ? OutArchiveFormat.Zip : extension == ".tar" ? OutArchiveFormat.Tar : extension == ".gz" || extension == ".tgz" ? OutArchiveFormat.GZip : extension == ".bz2" || extension == ".tbz2" ? OutArchiveFormat.BZip2 : extension == ".xz" || extension == ".txz" ? OutArchiveFormat.XZ : OutArchiveFormat.SevenZip;
                if (streamFormat && (directFiles == null || directFiles.Length != 1)) throw new IOException(L.Text("archive.singleStreamRequired"));
                var compressor = new SharpSevenZipCompressor { ArchiveFormat = outputFormat, PreserveDirectoryRoot = false, DirectoryStructure = true, IncludeEmptyDirectories = true, EncryptHeaders = extension != ".zip" && password != null, ZipEncryptionMethod = ZipEncryptionMethod.Aes256 };
                string level = args.FirstOrDefault(x => x.StartsWith("-mx=", StringComparison.Ordinal)); int value = level == null ? 6 : Int32.Parse(level.Substring(4));
                compressor.CompressionLevel = ArchiveCompressionQuality.Native(value);
                if(outputFormat==OutArchiveFormat.Zip){compressor.CompressionMethod=value==0?CompressionMethod.Copy:CompressionMethod.Deflate;compressor.CustomParameters["mt"]="1";}
                if ((extension == ".7z" || outputFormat == OutArchiveFormat.XZ) && value > 0) {
                    var plan = ArchiveMemoryPlan.Choose(budget.Settings.MaximumMemoryMiB, ArchiveMemoryPlan.AvailableBytes(), itemCount, value, args.Contains("-filecompass-low-memory"));
                    compressor.CompressionMethod = CompressionMethod.Lzma2;
                    compressor.CustomParameters[outputFormat == OutArchiveFormat.XZ ? "d" : "0d"] = plan.DictionaryMiB.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m";
                    compressor.CustomParameters["mt"] = plan.Threads.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                if (outputFormat == OutArchiveFormat.BZip2) compressor.CustomParameters["mt"] = "2";
                compressor.EventSynchronization = EventSynchronizationStrategy.AlwaysSynchronous;
                string[] streamFiles = directFiles;
                long currentFileDone=0,currentFileTotal=-1;
                long readBytes = 0, directBytes = streamFiles == null ? 0 : streamFiles.Sum(x => new FileInfo(x).Length); FileStream activeOutput = null;
                var notifications = Stopwatch.StartNew(); int lastPercent = -1; string currentMember = null;
                long stagedBytes=streamFiles==null?Directory.EnumerateFiles(staging.Path,"*",SearchOption.AllDirectories).Sum(x=>new FileInfo(x).Length):directBytes;
                string currentDisk=null;
                compressor.FileCompressionStarted+=delegate(object sender,FileNameEventArgs e){
                    if(streamFiles!=null)return;string disk=Path.IsPathRooted(e.FileName)?e.FileName:Path.Combine(staging.Path,e.FileName);
                    if(!File.Exists(disk))return;currentDisk=disk;currentMember=disk.Substring(staging.Path.TrimEnd(Path.DirectorySeparatorChar).Length+1).Replace('\\','/');
                    progress(new ArchiveCompressionProgress{Percent=Math.Max(0,lastPercent),Original=stagedBytes,Packed=activeOutput==null?0:activeOutput.Length,CurrentFile=currentMember}.Encode());
                };
                compressor.FileCompressionFinished+=delegate{
                    if(streamFiles!=null||currentDisk==null)return;long length=new FileInfo(currentDisk).Length;
                    progress(new ArchiveCompressionProgress{Percent=Math.Max(0,lastPercent),Original=stagedBytes,Packed=activeOutput==null?0:activeOutput.Length,CurrentFile=currentMember,FileDone=length,FileTotal=length}.Encode());currentDisk=null;
                };
                compressor.Compressing += delegate(object sender, ProgressEventArgs e) { budget.Check(); if (e.PercentDone != lastPercent && (lastPercent < 0 || e.PercentDone == 100 || notifications.ElapsedMilliseconds >= 80)) { lastPercent = e.PercentDone; notifications.Restart(); progress(streamFiles != null && activeOutput != null ? new ArchiveCompressionProgress { Percent = directBytes == 0 ? Math.Min(99, (int)e.PercentDone) : Math.Min(99, 100.0 * Interlocked.Read(ref readBytes) / directBytes), Original = directBytes, Processed = Interlocked.Read(ref readBytes), Packed = activeOutput.Length, CurrentFile = currentMember, FileDone=Interlocked.Read(ref currentFileDone), FileTotal=Interlocked.Read(ref currentFileTotal) }.Encode() : new ArchiveCompressionProgress{Percent=Math.Min(99,(int)e.PercentDone),Original=stagedBytes,Packed=activeOutput==null?0:activeOutput.Length,CurrentFile=currentMember}.Encode()); } };
                progress(L.Text("archive.compressingStatus") + "0%");
                using (var output = new FileStream(archive, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                using (var bounded = new ArchiveOutputStream(output, budget, archive, false))
                    if (streamFiles == null) {activeOutput=output;compressor.CompressDirectory(staging.Path, bounded, password ?? "", "*", true);activeOutput=null;}
                    else {
                        activeOutput = output; var streams = new Dictionary<string, StreamWithAttributes>(); var opened = new List<Stream>(); bool readReported = false;
                        try { foreach (string file in streamFiles) { string memberName = directFiles != null ? Path.GetFileName(file) : file.Substring(staging.Path.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/'); long memberRead=0,memberLength=new FileInfo(file).Length; var stream = new ArchiveReadProgressStream(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read), delegate(long count) { currentMember = memberName; memberRead+=count; Interlocked.Exchange(ref currentFileDone,memberRead); Interlocked.Exchange(ref currentFileTotal,memberLength); long processed = Interlocked.Add(ref readBytes, count); budget.Check(); if (!readReported || notifications.ElapsedMilliseconds >= 100) { readReported = true; notifications.Restart(); progress(new ArchiveCompressionProgress { Percent = directBytes == 0 ? 0 : Math.Min(99, 100.0 * processed / directBytes), Original = directBytes, Processed = processed, Packed = output.Length, CurrentFile = memberName, FileDone=memberRead, FileTotal=memberLength }.Encode()); } }); opened.Add(stream); var metadata = new FileInfo(file); streams.Add(memberName, new StreamWithAttributes(stream, metadata.CreationTime, metadata.LastWriteTime, metadata.LastAccessTime)); } compressor.CompressStreamDictionary(streams, bounded, password ?? ""); }
                        finally { activeOutput = null; foreach (var stream in opened) stream.Dispose(); }
                    }
                progress(L.Text("archive.compressingStatus") + "100%");
            }
        }
        static void WriteEmpty(string archive, bool zip)
        {
            using (var writer = new BinaryWriter(File.Create(archive)))
            {
                if (zip) { writer.Write(new byte[] { 0x50, 0x4b, 5, 6 }); writer.Write(new byte[18]); }
                else
                {
                    byte[] header = new byte[20]; uint crc = 0xffffffff;
                    foreach (byte value in header) { crc ^= value; for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u); }
                    writer.Write(new byte[] { 0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c, 0, 4 }); writer.Write(~crc); writer.Write(header);
                }
            }
        }
        static void Copy(string source, string target, ArchiveBudget budget, ArchivePreparationReporter preparation)
        {
            budget.Item(); RejectLinks(source); preparation.Visit(source);
            if (Directory.Exists(source)) { Directory.CreateDirectory(target); foreach (string child in Directory.EnumerateFileSystemEntries(source)) Copy(child, Path.Combine(target, Path.GetFileName(child)), budget, preparation); }
            else { Directory.CreateDirectory(Path.GetDirectoryName(target)); using (var input = File.OpenRead(source)) using (var output = new FileStream(target, FileMode.Create, FileAccess.Write)) using (var bounded = new ArchiveOutputStream(output, budget, target)) { var buffer = new byte[1048576]; int n; while ((n = input.Read(buffer, 0, buffer.Length)) > 0) { bounded.Write(buffer, 0, n); preparation.Copied(n); } } File.SetLastWriteTime(target, File.GetLastWriteTime(source)); }
        }
    }

    internal sealed class ArchiveBudget
    {
        internal readonly ArchiveSettings Settings; internal readonly long MaximumBytes; long written; int items; readonly Stopwatch clock = Stopwatch.StartNew();
        internal IOException Failure;
        // The controlling process enforces the wall-time limit excluding user pauses.
        internal bool ParentMonitored;
        internal ArchiveBudget(ArchiveSettings settings) { Settings = settings; MaximumBytes = checked(settings.MaximumExtractedMiB * 1048576); }
        IOException Fail(string text) { return Failure = new IOException(text); }
        internal void Check() { if (!ParentMonitored&&clock.Elapsed.TotalSeconds > Settings.MaximumSeconds) throw Fail(L.Text("archive.workerTimeout")); }
        internal void Item() { Check(); if (++items > Settings.MaximumItems) throw Fail(L.Text("archive.itemLimitExceeded")); }
        internal void Write(int count, string path, bool extracted)
        {
            Check(); if (extracted) { if (written > MaximumBytes - count) throw Fail(L.Text("archive.actualExpandedDataLimit")); written += count; }
            if (path != null) { ulong free, total, available; if (!GetDiskFreeSpaceEx(Path.GetDirectoryName(path), out free, out total, out available)) throw Fail(L.Text("archive.freeSpaceQueryFailed")); if (free < (ulong)(Settings.MinimumFreeMiB * 1048576L + count)) throw Fail(L.Text("archive.freeSpaceReserveReached")); }
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool GetDiskFreeSpaceEx(string path, out ulong free, out ulong total, out ulong available);
    }
    internal sealed class ArchiveOutputStream : Stream
    {
        readonly Stream output; readonly ArchiveBudget budget; readonly string path; readonly bool extracted;
        internal ArchiveOutputStream(Stream output, ArchiveBudget budget, string path, bool extracted = true) { this.output = output; this.budget = budget; this.path = path; this.extracted = extracted; }
        public override void Write(byte[] buffer, int offset, int count) { budget.Write(count, path, extracted); output.Write(buffer, offset, count); }
        public override bool CanRead { get { return output.CanRead; } } public override bool CanSeek { get { return output.CanSeek; } } public override bool CanWrite { get { return true; } }
        public override long Length { get { return output.Length; } } public override long Position { get { return output.Position; } set { output.Position = value; } }
        public override void Flush() { output.Flush(); } public override long Seek(long offset, SeekOrigin origin) { return output.Seek(offset, origin); }
        public override void SetLength(long value) { if (value > budget.MaximumBytes) throw new IOException(L.Text("archive.outputFileLengthLimit")); output.SetLength(value); }
        public override int Read(byte[] buffer, int offset, int count) { return output.Read(buffer, offset, count); }
    }

    internal sealed class ArchiveProcessJob : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] struct Basic { public long ProcessTime, JobTime; public uint Flags; public UIntPtr Minimum, Maximum; public uint Active; public UIntPtr Affinity; public uint Priority, Scheduling; }
        [StructLayout(LayoutKind.Sequential)] struct Io { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] struct Limits { public Basic Basic; public Io Io; public UIntPtr ProcessMemory, JobMemory, PeakProcess, PeakJob; }
        IntPtr handle;
        internal ArchiveProcessJob(int memoryMiB)
        {
            handle = CreateJobObject(IntPtr.Zero, null); if (handle == IntPtr.Zero) throw new IOException(L.Text("archive.workerCreationFailed"));
            var limits = new Limits { Basic = new Basic { Flags = 0x2100 }, ProcessMemory = (UIntPtr)((ulong)memoryMiB * 1048576) };
            if (!SetInformationJobObject(handle, 9, ref limits, Marshal.SizeOf(typeof(Limits)))) { Dispose(); throw new IOException(L.Text("archive.workerMemoryLimitFailed")); }
        }
        internal void Assign(Process process) { if (!AssignProcessToJobObject(handle, process.Handle)) throw new IOException(L.Text("archive.workerIsolationFailed")); }
        public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetInformationJobObject(IntPtr job, int type, ref Limits limits, int size);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    }
}
