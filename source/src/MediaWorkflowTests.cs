using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FileCompass
{
    internal static class MediaWorkflowTests
    {
        static void Check(bool value, string message, List<string> checks) { if (!value) throw new Exception(message); checks.Add(message); }
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>(); string oldTags = FileTagStore.TestPath;
            try
            {
                FileTagStore.TestPath = Path.Combine(root, "tags.xml"); FluentTheme.Ensure();
                string photo = Path.Combine(root, "photo.jpg");
                var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Rgb24, null, new byte[12], 6);
                var metadata = new BitmapMetadata("jpg"); metadata.SetQuery("/app1/ifd/exif/{ushort=36867}", "2024:06:15 13:45:20");
                var jpeg = new JpegBitmapEncoder(); jpeg.Frames.Add(BitmapFrame.Create(image, null, metadata, null)); using (var output = File.Create(photo)) jpeg.Save(output);
                byte[] original = File.ReadAllBytes(photo);
                var photoRow = BatchRenameEngine.Preview(new[] { photo }, new RenameOptions { Name = "[EXIF.Date]_[EXIF.Time]_[C]", Digits = 2 }).Single();
                Check(photoRow.NewName == "2024-06-15_13-45-20_01.jpg", "real JPEG EXIF date and time are used", checks);
                Check(original.SequenceEqual(File.ReadAllBytes(photo)), "metadata preview does not modify source bytes", checks);
                string music = Path.Combine(root, "music.mp3");
                using (var output = File.Create(music)) for (int i = 0; i < 12; i++) { var frame = new byte[417]; frame[0] = 255; frame[1] = 251; frame[2] = 144; frame[3] = 100; output.Write(frame, 0, frame.Length); }
                using (var media = TagLib.File.Create(music)) { media.Tag.Performers = new[] { "Žluťoučký/interpret" }; media.Tag.Title = "[N]:Píseň"; media.Tag.Album = "Album"; media.Tag.Track = 7; media.Tag.Year = 2020; media.Save(); }
                var musicRow = BatchRenameEngine.Preview(new[] { music }, new RenameOptions { Name = "[MP3.Artist] - [MP3.Track] - [MP3.Title]" }).Single();
                Check(musicRow.NewName == "Žluťoučký, interpret - 07 - [N]_Píseň.mp3", "ID3 Unicode, track padding, invalid characters and literal mask tokens are preserved safely: " + musicRow.NewName + " / " + musicRow.Status, checks);
                var skipped = BatchRenameEngine.Preview(new[] { music }, new RenameOptions { Name = "[EXIF.Date]" }).Single();
                Check(!skipped.Changed && skipped.Warning != null, "missing metadata keeps the original filename and explains why", checks);
                var cancelled = new CancellationTokenSource(); cancelled.Cancel(); bool stopped = false; try { RenameMetadata.Read(photo, cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
                Check(stopped, "metadata requests honor cancellation", checks);
                var store = FileTagStore.Current; store.Change(new[] { photo, music }, FileTagStore.Normalize("Důležité; Vyřídit; důležité"), 0);
                Check(store.Snapshot().All(x => x.Tags.Length == 2), "multiple tags normalize and persist without case duplicates", checks);
                var row = new Entry { FullPath = photo }; FileTagStore.Apply(new[] { row }); Check(row.TagNames.Length == 2, "panel entries display persisted tags", checks);
                store.Change(new[] { photo }, new[] { "Vyřídit" }, 2); Check(store.Snapshot().Single(x => x.Path == photo).Tags.Length == 1, "remove does not erase unrelated tags", checks);
                BatchRenameEngine.Apply(new[] { photoRow }, CancellationToken.None);
                Check(store.Snapshot().Any(x => x.Path == photoRow.NewPath && x.Tags.Length == 1), "tags follow actual batch renames", checks);
                var restore = new RenameRow { Path = photoRow.NewPath, OldName = photoRow.NewName, NewName = "photo.jpg" }; BatchRenameEngine.Apply(new[] { restore }, CancellationToken.None);
                Check(store.Snapshot().Any(x => x.Path == photo), "tags follow undo rename", checks);
                store.Change(new[] { music }, new string[0], 1); Check(!store.Snapshot().Any(x => x.Path == music), "replacing with empty tags clears only the selected file", checks);
                byte[] validTags = File.ReadAllBytes(FileTagStore.TestPath); File.WriteAllText(FileTagStore.TestPath, "<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///C:/Windows/win.ini'>]><FileTagData>&a;</FileTagData>");
                Check(store.Snapshot().Length == 0 && FileTagStore.LastError != null, "corrupt tag data does not prevent file browsing", checks);
                bool rejected = false; try { store.Change(new[] { music }, new[] { "Test" }, 0); } catch (InvalidOperationException) { rejected = true; } catch (System.Xml.XmlException) { rejected = true; }
                Check(rejected && File.ReadAllText(FileTagStore.TestPath).StartsWith("<!DOCTYPE"), "corrupt tag database is never silently overwritten and external entities are rejected", checks); File.WriteAllBytes(FileTagStore.TestPath, validTags);
                string markdown = "# Nadpis\n\n**Tučné** a `kód`\n\n- jedna\n- dvě\n\n| Název | Stav |\n| --- | --- |\n| Soubor | Hotovo |\n\n![remote](https://invalid.invalid/image.png)\n\n<script>alert('test')</script>\n\n[link](file:///C:/Windows/win.ini)";
                var preview = new RichFilePreview(); preview.Show("readme.md", markdown, RichFilePreview.Parse(markdown));
                var viewer = FluentUiTests.Children<FlowDocumentScrollViewer>(preview).Single(); var document = viewer.Document;
                Check(document.Blocks.OfType<System.Windows.Documents.Table>().Any() && document.Blocks.OfType<System.Windows.Documents.List>().Any(), "Markdown headings, lists, emphasis and tables render as native document elements", checks);
                Check(!FluentUiTests.Children<Hyperlink>(document).Any() && !FluentUiTests.Children<Image>(document).Any(), "Markdown never creates active links, loads images or executes HTML", checks);
                preview.Show("sample.cs", "class Test { string name = \"hello\"; }", null);
                var editor = FluentUiTests.Children<ICSharpCode.AvalonEdit.TextEditor>(preview).Single();
                Check(editor.IsReadOnly && editor.ShowLineNumbers && editor.SyntaxHighlighting != null, "source preview provides read-only syntax highlighting and line numbers", checks);
                preview.Show("readme.md", markdown, RichFilePreview.Parse(markdown)); var window = new Window { Content = preview, Width = 820, Height = 620 }; FluentTheme.Dialog(window); NetworkTests.Render(window, Path.Combine(root, "markdown-preview.png")); window.Close();
                var renameWindow = new BatchRenameWindow(null, new[] { music }, delegate { }); NavigationTests.Wait(renameWindow.PreviewTask); NetworkTests.Render(renameWindow, Path.Combine(root, "metadata-rename.png")); renameWindow.Close();
                File.WriteAllLines(Path.Combine(root, "media-workflow-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(root, "media-workflow-result.txt"), "FAIL after " + checks.Count + " checks\n" + ex); return 1; }
            finally { FileTagStore.TestPath = oldTags; }
        }
    }
}

