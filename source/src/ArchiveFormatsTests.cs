using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace FileCompass
{
    internal static class ArchiveFormatsTests
    {
        static byte[] DiscFixture() {
            const string value="H4sIAAAAAAAC/+3cwUvbUBgA8K/ioehpY0wZ1oWxgcJ0SZ3usGPn2KEwDx52rRpmoeqwPehl//r2altkylZGN5T4+72mLy/50iah8PHyyosAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGqtD3le1CKb0eTznv9S/eF702u4RL0eC6NNC0+udz8dvr2IlVFrJerDqh7fFx8tLb2cn5scn3Gn9i5bR73uQbbWWs+aebG90cybedY6Pu/2s3an1zk9LAeDsjKXm65upyiKd82t5tbWTp7P39qQ3xC3IvxoHzpJh3tjbpz/I5ux3Mj/r97s/nX+X77e/WyU/xujVuN3+X/ms1ZmK3txGa04il504yC111JrPdXNyKOI7di4WhsuWdpzHOcprp/W29FJx3TiNA6jjEEq5X87x38XJP8j/1MhPyb5HwAAAKis2tUYu/4/AAAAVLv/H8P/13sAAAAAAJXu/zeM/wMAAMBDGP9veAAAAAAAVTZ9jv2pEbXXsTqKWB3P4Ls8johRxOKn3Xb78+b+l/33hTsOAPch/0+bY39qRO3tlPz/OE38Xqbp3ntxFptpqveLtAAAd+djt1dmrbOTb51+P+uedL6W2aDsDxbcGQCorp/US5J8APgAAA==";
            return InflateFixture(value);
        }
        static byte[] InflateFixture(string value) {
            using(var input=new System.IO.Compression.GZipStream(new MemoryStream(Convert.FromBase64String(value)),System.IO.Compression.CompressionMode.Decompress))using(var output=new MemoryStream()){input.CopyTo(output);return output.ToArray();}
        }
        static void CheckDiscImages(string root,List<string> checks) {
            CheckDataIsoWrites(root, checks);
            byte[] iso=DiscFixture();string img=Path.Combine(root,"disc.img");File.WriteAllBytes(img,iso);
            if(!ArchiveEngine.Recognized(img))throw new Exception("ISO contents in IMG were not recognized");
            foreach(string ext in new[]{".raw",".dd",".ima"}){string image=Path.Combine(root,"disc"+ext);File.WriteAllBytes(image,iso);if(!ArchiveEngine.Recognized(image))throw new Exception("Raw image not recognized: "+ext);}
            string invalid=Path.Combine(root,"unrelated.img");File.WriteAllText(invalid,"not a disk image");if(ArchiveEngine.Recognized(invalid))throw new Exception("Unrelated IMG was intercepted");
            string bin=Path.Combine(root,"disc.bin"),cue=Path.Combine(root,"disc.cue");using(var output=File.Create(bin)){for(int p=0;p<iso.Length;p+=2048){var sector=new byte[2352];sector[0]=0;for(int j=1;j<11;j++)sector[j]=255;sector[15]=1;Buffer.BlockCopy(iso,p,sector,16,2048);output.Write(sector,0,sector.Length);}}
            File.WriteAllText(cue,"FILE \"disc.bin\" BINARY\n  TRACK 01 MODE1/2352\n    INDEX 01 00:00:00\n");
            foreach(string path in new[]{img,cue,bin}){if(!ArchiveEngine.Recognized(path))throw new Exception("Image not recognized: "+path);var doc=ArchiveEngine.Open(path);if(ArchiveEngine.Writable(doc))throw new Exception("Image unexpectedly writable");var item=doc.Items.First(x=>!x.Directory&&x.Path.EndsWith("hello.txt",StringComparison.OrdinalIgnoreCase));using(var tmp=new ArchiveTemp()){ArchiveEngine.Extract(doc,new[]{item.Path},tmp.Path,CancellationToken.None);if(File.ReadAllText(ArchiveEngine.DiskPath(tmp.Path,item.Path))!="File Compass image test\n")throw new Exception("Incorrect image extraction: "+path);}}
            checks.Add("ISO data in IMG and raw BIN/CUE opens read-only and extracts exact bytes");
            string mixedBin=Path.Combine(root,"mixed.bin"),mixedCue=Path.Combine(root,"mixed.cue");File.Copy(bin,mixedBin,true);using(var output=new FileStream(mixedBin,FileMode.Append,FileAccess.Write))output.Write(new byte[2352],0,2352);File.WriteAllText(mixedCue,"FILE \"mixed.bin\" BINARY\n TRACK 01 MODE1/2352\n INDEX 01 00:00:00\n TRACK 02 AUDIO\n INDEX 01 00:00:31\n");var mixed=ArchiveEngine.Open(mixedCue);if(!mixed.ImageAudioOmitted||!mixed.Items.Any(x=>!x.Directory&&x.Path.EndsWith("hello.txt",StringComparison.OrdinalIgnoreCase)))throw new Exception("Mixed-mode audio limitation was not reported");checks.Add("mixed-mode CUE opens its data track and reports omitted audio");
            var openedCue=ArchiveEngine.Open(cue);File.SetLastWriteTimeUtc(bin,File.GetLastWriteTimeUtc(bin).AddMinutes(1));bool changed=false;try{ArchiveEngine.Test(openedCue,CancellationToken.None);}catch(IOException){changed=true;}if(!changed)throw new Exception("Changed CUE data was not detected");checks.Add("CUE backing data changes invalidate the open image");
            string unsafeCue=Path.Combine(root,"unsafe.cue");File.WriteAllText(unsafeCue,"FILE \"..\\outside.bin\" BINARY\n TRACK 01 MODE1/2352\n INDEX 01 00:00:00\n");bool rejected=false;try{DiscImageSource.ReadCue(unsafeCue);}catch(IOException){rejected=true;}if(!rejected)throw new Exception("CUE traversal was not rejected");checks.Add("CUE cannot reference data outside the image folder");
            string unrelated=Path.Combine(root,"wrong.bin"),wrongCue=Path.Combine(root,"wrong.cue");File.WriteAllBytes(unrelated,new byte[1]);File.WriteAllText(wrongCue,"FILE \"disc.bin\" BINARY\n TRACK 01 MODE1/2352\n INDEX 01 00:00:00\n");if(DiscImageSource.AssociatedCue(unrelated)!=null)throw new Exception("BIN was paired with an unrelated CUE");checks.Add("BIN does not open an unrelated descriptor");
        }
        static void CheckDataIsoWrites(string root, List<string> checks)
        {
            var token = CancellationToken.None;
            var settings = ArchiveSettings.Load();
            string folder = Directory.CreateDirectory(Path.Combine(root, "ISO files")).FullName;
            Directory.CreateDirectory(Path.Combine(folder, "Empty folder"));
            File.WriteAllText(Path.Combine(folder, "První soubor.txt"), "first");
            string added = Path.Combine(root, "Second file.txt");
            File.WriteAllText(added, "second");
            string path = Path.Combine(root, "data.iso");
            ArchiveEngine.Create(path, new[] { folder }, settings, token);
            var doc = ArchiveEngine.Open(path);
            if (!ArchiveEngine.Writable(doc) || doc.Iso.BootStatus != IsoBootStatus.Data ||
                !doc.Items.Any(x => x.Path == "ISO files/První soubor.txt") ||
                !doc.Items.Any(x => x.Path == "ISO files/Empty folder" && x.Directory))
                throw new Exception("Data ISO did not preserve names and folders or was not writable");
            ArchiveEngine.Add(doc, "ISO files", new[] { added }, false, settings, token);
            if (!doc.Items.Any(x => x.Path == "ISO files/Second file.txt")) throw new Exception("ISO add failed");
            ArchiveEngine.Remove(doc, new[] { "ISO files/První soubor.txt" }, settings, token);
            if (doc.Items.Any(x => x.Path.EndsWith("První soubor.txt"))) throw new Exception("ISO remove failed");
            ArchiveEngine.Remove(doc, new[] { "ISO files" }, settings, token);
            if (doc.Items.Count != 0 || !ArchiveEngine.Writable(doc)) throw new Exception("Deleting the last file did not leave a writable ISO");
            checks.Add("Data ISO creation, add and delete preserve content, Unicode names and empty folders");

            // El Torito ISO produced by pycdlib with a dummy boot payload and a valid catalog.
            byte[] boot = InflateFixture("H4sIAAAAAAAC/+3cTUvcQBgA4EnZw7q9FRG1h4aeWlCZrFgqntp0LQFrRLPgTWwVFKSH6sWLZ3+Lf7LdmC5SrR8XUdfnIZ+TNwkJgWEmeRMCAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAJDkX2LMkpDe4nNZVlvLxWbVX+/9b/vweK//md1w3sFQj6HdDp2mqDN+sXminrwNk83aZGjXs3Y4eflq7E1ovRjun/Kg1o7znYP97+m7/H3ajdnCbDd2Y5rv/do/TFe2D7Z//tg9OtodmcsdXN2HLMs+xsX5LGYxtq4UxEvClQgP7XOn0uHxaOr/3kpaletFVaYba728WC7yT1VRrt75KFNuJAA8Hb//tv8BAACA0ZWcv2PX/gcAAIDRbv+H+vt6HQAAAAAwwq7JsU8ucuxvjUhmhul/U5ciQhPRqX8eMDfIKFzKwkyYbmKnb4otvn0dxAIA4f76/69x1u+fnZ4vtYa1NgAAAPAE/QFEE0HWANgAAA==");
            string bootPath = Path.Combine(root, "boot.iso");
            File.WriteAllBytes(bootPath, boot);
            var bootDoc = ArchiveEngine.Open(bootPath);
            if (bootDoc.Iso.BootStatus != IsoBootStatus.Bootable || ArchiveEngine.Writable(bootDoc))
                throw new Exception("El Torito boot record was not protected");
            bool rejected = false;
            try { ArchiveEngine.Add(bootDoc, "", new[] { added }, false, settings, token); }
            catch (IOException) { rejected = true; }
            if (!rejected || !File.ReadAllBytes(bootPath).SequenceEqual(boot))
                throw new Exception("Bootable ISO was modified");
            checks.Add("El Torito ISO opens as bootable and rejects writes");

            string external = Path.Combine(root, "external.iso");
            File.WriteAllBytes(external, DiscFixture());
            var externalDoc = ArchiveEngine.Open(external);
            ArchiveEngine.Add(externalDoc, "", new[] { added }, false, settings, token);
            if (!externalDoc.Items.Any(item => item.Path.EndsWith("hello.txt", StringComparison.OrdinalIgnoreCase)))
                throw new Exception("Existing datа was lost while editing an externally created ISO");
            byte[] systemArea = File.ReadAllBytes(path);
            systemArea[0] = 0xEB;
            File.WriteAllBytes(external, systemArea);
            if (DataIso.Inspect(external, "iso").CanRebuild) throw new Exception("Unrecognized system area was not protected");
            checks.Add("External data ISO accepts writes and unknown system areas stay locked");
        }
        internal static int Detection(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>();
            try {
                SharpSevenZip.SharpSevenZipBase.SetLibraryPath(ArchiveEngine.SevenZipLibraryPath);
                string folder = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
                File.WriteAllText(Path.Combine(folder, "payload.txt"), "archive detection test");
                string zip = Path.Combine(root, "source.zip"); if (File.Exists(zip)) File.Delete(zip);
                System.IO.Compression.ZipFile.CreateFromDirectory(folder, zip);
                var open = typeof(ArchiveWorker).GetMethod("Open", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var execute = typeof(ArchiveWorker).GetMethod("Execute", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                foreach (string name in new[] { "archive", "archive.custom", "archive.rar" }) {
                    string path = Path.Combine(root, name); File.Copy(zip, path, true);
                    using (var reader = (SharpSevenZip.SharpSevenZipExtractor)open.Invoke(null, new object[] { path, null }))
                        if (reader.FilesCount != 1) throw new IOException("Wrong entry count: " + name);
                    string output = Path.Combine(root, "out-" + name); Directory.CreateDirectory(output);
                    execute.Invoke(null, new object[] { new[] { "x", "-o" + output, "--", path }, "", root, new ArchiveBudget(new ArchiveSettings()), (Action<string>)(x => { }) });
                    if (File.ReadAllText(Path.Combine(output, "payload.txt")) != "archive detection test") throw new IOException("Wrong extracted contents: " + name);
                    checks.Add(name + " detected and extracted");
                }
                string encrypted = Path.Combine(root, "encrypted.custom"); if (File.Exists(encrypted)) File.Delete(encrypted);
                var compressor = new SharpSevenZip.SharpSevenZipCompressor { ArchiveFormat = SharpSevenZip.OutArchiveFormat.SevenZip, EncryptHeaders = true };
                compressor.CompressFilesEncrypted(encrypted, "test-password", Path.Combine(folder, "payload.txt"));
                try { using (var ignored = (SharpSevenZip.SharpSevenZipExtractor)open.Invoke(null, new object[] { encrypted, null })) { } throw new Exception("Encrypted headers did not request a password"); }
                catch (System.Reflection.TargetInvocationException ex) { if (!(ex.InnerException is ArchivePasswordException)) throw; }
                using (var reader = (SharpSevenZip.SharpSevenZipExtractor)open.Invoke(null, new object[] { encrypted, "test-password" }))
                    if (reader.FilesCount != 1) throw new IOException("Encrypted archive could not be opened");
                checks.Add("renamed encrypted archive requests and accepts its password");
                string invalid = Path.Combine(root, "ordinary.dat"); File.WriteAllText(invalid, "not an archive");
                try { open.Invoke(null, new object[] { invalid, null }); throw new Exception("Non-archive accepted"); }
                catch (System.Reflection.TargetInvocationException ex) { if (!(ex.InnerException is IOException) || ex.InnerException is ArchivePasswordException) throw; }
                checks.Add("ordinary file reports a format error, not a password request");
                CheckDiscImages(root,checks);
                string fixture = Environment.GetEnvironmentVariable("FILECOMPASS_ARCHIVE_FIXTURE");
                if (!String.IsNullOrWhiteSpace(fixture)) {
                    string output = Path.Combine(root, "real-fixture"); Directory.CreateDirectory(output);
                    execute.Invoke(null, new object[] { new[] { "x", "-o" + output, "--", fixture }, "", root, new ArchiveBudget(new ArchiveSettings()), (Action<string>)(x => { }) });
                    using (var zipReader = System.IO.Compression.ZipFile.OpenRead(fixture)) {
                        var sample = zipReader.Entries.First(x => !x.FullName.EndsWith("/", StringComparison.Ordinal));
                        string extracted = ArchiveEngine.DiskPath(output, sample.FullName);
                        using (var hash = System.Security.Cryptography.SHA256.Create()) using (var a = sample.Open()) using (var b = File.OpenRead(extracted))
                            if (!hash.ComputeHash(a).SequenceEqual(hash.ComputeHash(b))) throw new IOException("Fixture contents differ");
                    }
                    checks.Add("real renamed ZIP extracts and matches its content");
                }
                File.WriteAllLines(Path.Combine(root, "archive-detection-result.txt"), checks.Concat(new[] { "PASS" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "archive-detection-result.txt"), String.Join("\n", checks) + "\nFAIL: " + ex); return 1; }
        }
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>();
            Action<bool,string> check = delegate(bool valid, string name) { if (!valid) throw new Exception(name); checks.Add(name); };
            try {
                var settings = new ArchiveSettings(); settings.Save();
                string folder = Directory.CreateDirectory(Path.Combine(root, "vstup")).FullName;
                string file = Path.Combine(folder, "český.txt"); File.WriteAllText(file, String.Concat(Enumerable.Repeat("Původní data!", 10000)));
                Directory.CreateDirectory(Path.Combine(folder, "prázdná"));
                foreach (string extension in new[] { "zip", "7z", "tar", "gz", "bz2", "xz", "tar.gz", "tar.bz2", "tar.xz", "tgz", "tbz2", "txz" }) {
                    bool single = new[] { "gz", "bz2", "xz" }.Contains(extension);
                    string archive = Path.Combine(root, "balík." + extension); var updates = new List<string>();
                    ArchiveEngine.Create(archive, new[] { single ? file : folder }, settings, CancellationToken.None, updates.Add);
                    var doc = ArchiveEngine.Open(archive); ArchiveEngine.Test(doc, CancellationToken.None);
                    check(doc.Items.Count > 0, extension + " correct format opens and passes integrity test: " + doc.Type);
                    using (var temp = new ArchiveTemp()) {
                        ArchiveEngine.Extract(doc, doc.Items.Select(x => x.Path).ToArray(), temp.Path, CancellationToken.None);
                        string payload = Directory.GetFiles(temp.Path, "*", SearchOption.AllDirectories).First();
                        if (!single && extension != "zip" && extension != "7z" && extension != "tar") {
                            var tar = ArchiveEngine.Open(payload); check(tar.Type == "tar", extension + " contains a real TAR");
                            using (var inside = new ArchiveTemp()) { ArchiveEngine.Extract(tar, tar.Items.Select(x => x.Path), inside.Path, CancellationToken.None); check(File.ReadAllText(Directory.GetFiles(inside.Path, "*", SearchOption.AllDirectories).Single()) == File.ReadAllText(file), extension + " nested payload roundtrip"); }
                        } else check(File.ReadAllText(payload) == File.ReadAllText(file), extension + " exact payload roundtrip");
                    }
                    check(updates.Count > 1, extension + " reports progress");
                }
                bool rejected = false; try { ArchiveEngine.Create(Path.Combine(root, "bad.gz"), new[] { folder }, settings, CancellationToken.None); } catch (IOException) { rejected = true; }
                check(rejected && !File.Exists(Path.Combine(root, "bad.gz")), "single-stream format rejects folders without creating output");
                var tarDoc = ArchiveEngine.Open(Path.Combine(root, "balík.tar"));
                check(!ArchiveEngine.Writable(tarDoc), "TAR existing archive remains read-only rather than allowing incomplete updates");
                check(ArchivePackDialog.ChangeFormat("balík.tar.gz", "zip") == "balík.zip", "switching compound format removes whole extension");
                check(ArchiveEngine.OutputSuffix("BALÍK.TAR.GZ") == ".tar.gz", "separate archives retain compound extension");
                bool cancelled = false; using (var cancellation = new CancellationTokenSource()) { cancellation.Cancel(); try { ArchiveEngine.Create(Path.Combine(root, "cancel.tar.xz"), new[] { folder }, settings, cancellation.Token); } catch (OperationCanceledException) { cancelled = true; } }
                check(cancelled && !File.Exists(Path.Combine(root, "cancel.tar.xz")), "cancelled creation leaves no target");
                File.WriteAllLines(Path.Combine(root, "archive-formats-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "archive-formats-result.txt"), String.Join("\n", checks) + "\nFAIL: " + ex); return 1; }
        }
    }
}

