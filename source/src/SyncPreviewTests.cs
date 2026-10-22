using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommanderCZ
{
    internal static class SyncPreviewTests
    {
        internal static int Run(string directory)
        {
            var checks = new List<string>(); Action<bool,string> check = delegate(bool value,string message) { if (!value) throw new Exception(message); checks.Add(message); };
            try {
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
                Directory.CreateDirectory(directory); string source = Directory.CreateDirectory(Path.Combine(directory,"source")).FullName, target = Directory.CreateDirectory(Path.Combine(directory,"target")).FullName;
                DateTime stamp = new DateTime(2026,10,1,12,0,0,DateTimeKind.Utc);
                Action<string,string> write = delegate(string path,string value) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path,value); File.SetLastWriteTimeUtc(path,stamp); };
                write(Path.Combine(source,"same.txt"),"same"); write(Path.Combine(target,"same.txt"),"same");
                write(Path.Combine(source,"different.txt"),"aaaa"); write(Path.Combine(target,"different.txt"),"bbbb");
                write(Path.Combine(source,"nested","new.txt"),"new"); write(Path.Combine(target,"extra.txt"),"extra"); Directory.CreateDirectory(Path.Combine(source,"empty"));
                var quick = DirectorySync.Compare(source,target,false,false,delegate { });
                check(quick.Rows.Single(x => x.Relative == "different.txt").Action == "Ponechat", "metadata comparison does not silently read content");
                var plan = DirectorySync.Compare(source,target,true,false,delegate { });
                check(plan.Rows.Single(x => x.Relative == "different.txt").Action == "Přepsat", "content comparison detects same-size same-time differences");
                check(plan.Rows.Single(x => x.Relative == "extra.txt").Action == "Ponechat" && !plan.Rows.Any(x => x.Action == "Odstranit"), "default synchronization never plans deletion");
                check(plan.Rows.Single(x => x.Relative == "empty").Action == "Vytvořit složku", "preview includes empty folders");
                using (var control = new CopyControl()) { var result = DirectorySync.Apply(plan,control,delegate { }); check(result.All(x => x.State == "Dokončeno"), "one-way sync completes through copy engine"); }
                check(File.ReadAllText(Path.Combine(target,"different.txt")) == "aaaa" && File.ReadAllText(Path.Combine(target,"nested","new.txt")) == "new" && Directory.Exists(Path.Combine(target,"empty")) && File.Exists(Path.Combine(target,"extra.txt")), "sync copies nested content and preserves target-only files");
                check(DirectorySync.Compare(source,target,true,false,delegate { }).Rows.All(x => x.Action == "Ponechat"), "repeated comparison produces no further copy operations");
                var mirror = DirectorySync.Compare(source,target,false,true,delegate { });
                check(mirror.Rows.Count(x => x.Action == "Odstranit") == 1, "explicit mirror plans only target-only file");
                // Cancellation before any mutation must preserve the reviewed file.
                using (var control = new CopyControl()) { control.Cancel(); bool cancelled = false; try { DirectorySync.Apply(mirror,control,delegate { }); } catch (OperationCanceledException) { cancelled = true; } check(cancelled && File.Exists(Path.Combine(target,"extra.txt")), "cancelled synchronization performs no writes or deletion"); }
                write(Path.Combine(source,"late.txt"),"first"); var stale = DirectorySync.Compare(source,target,false,true,delegate { }); write(Path.Combine(source,"late.txt"),"changed");
                bool rejected = false; using (var control = new CopyControl()) { try { DirectorySync.Apply(stale,control,delegate { }); } catch (IOException) { rejected = true; } }
                check(rejected && File.Exists(Path.Combine(target,"extra.txt")) && !File.Exists(Path.Combine(target,"late.txt")), "changed source invalidates entire preview before mutation");
                bool overlap = false; try { DirectorySync.Compare(source,Path.Combine(source,"empty"),false,false,delegate { }); } catch (IOException) { overlap = true; } check(overlap,"overlapping roots are rejected");
                string linkedRoot = Path.Combine(directory,"link-source");
                if (Directory.Exists(linkedRoot)) { bool linked = false; try { DirectorySync.Compare(linkedRoot,target,false,true,delegate { }); } catch (IOException) { linked = true; } check(linked,"directory junction aborts mirror planning without following its target"); }
                Directory.CreateDirectory(Path.Combine(target,"conflict")); write(Path.Combine(source,"conflict"),"file");
                var conflict = DirectorySync.Compare(source,target,false,false,delegate { }); check(conflict.Rows.Any(x => x.Action == "Konflikt"),"file-directory collision is explicit");
                rejected = false; using (var control = new CopyControl()) { try { DirectorySync.Apply(conflict,control,delegate { }); } catch (IOException) { rejected = true; } } check(rejected,"conflicting plan cannot execute");
                File.Delete(Path.Combine(source,"conflict")); Directory.Delete(Path.Combine(target,"conflict"));
                var reverse = DirectorySync.Compare(target,source,false,false,delegate { }); check(reverse.Rows.Single(x => x.Relative == "extra.txt").Action == "Kopírovat", "reverse direction has its own source and target");
                write(Path.Combine(source,"locked-target.txt"),"replacement"); write(Path.Combine(target,"locked-target.txt"),"old"); File.SetAttributes(Path.Combine(target,"locked-target.txt"),FileAttributes.ReadOnly);
                var failing = DirectorySync.Compare(source,target,false,true,delegate { });
                using (var control = new CopyControl()) { var result = DirectorySync.Apply(failing,control,delegate { }); check(result.Any(x => x.State == "Chyba") && result.Any(x => x.State == "Přeskočeno") && File.Exists(Path.Combine(target,"extra.txt")), "copy failure prevents all planned mirror deletions"); }
                File.SetAttributes(Path.Combine(target,"locked-target.txt"),FileAttributes.Normal);
                var successMirror = DirectorySync.Compare(source,target,false,true,delegate { });
                using (var control = new CopyControl()) { var result = DirectorySync.Apply(successMirror,control,delegate { }); check(result.All(x => x.State == "Dokončeno") && !File.Exists(Path.Combine(target,"extra.txt")), "explicit successful mirror removes reviewed extra file after writes"); }
                var bitmap = new RenderTargetBitmap(80,60,96,96,PixelFormats.Pbgra32); var drawing = new DrawingVisual(); using (var paint = drawing.RenderOpen()) paint.DrawRectangle(Brushes.DodgerBlue,null,new Rect(0,0,80,60)); bitmap.Render(drawing);
                string photo = Path.Combine(source,"preview.png"); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var output = File.Create(photo)) encoder.Save(output);
                var image = PreviewData.Load(photo,Encoding.UTF8,CancellationToken.None); check(image.Image != null && image.Image.IsFrozen,"image preview is decoded and frozen for UI-thread use");
                using (var stream = new FileStream(photo,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) check(stream.Length > 0,"image preview releases the file after decoding");
                string encoded = Path.Combine(source,"cp1250.txt"); File.WriteAllText(encoded,"Příliš žluťoučký kůň",Encoding.GetEncoding(1250));
                check(PreviewData.Load(encoded,Encoding.GetEncoding(1250),CancellationToken.None).Text == "Příliš žluťoučký kůň", "preview supports Czech Windows-1250 without changing file");
                string large = Path.Combine(source,"large.txt"); File.WriteAllText(large,new string('a',1000000));
                var data = PreviewData.Load(large,Encoding.UTF8,CancellationToken.None); check(data.Text.Length == 131072 && data.Status.Contains("128"),"large text preview is bounded and labeled as partial");
                using (var stream = new FileStream(large,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) check(stream.Length == 1000000,"preview releases source handle");
                var app = new MainWindow(source) { Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false }; app.Show(); NavigationTests.WaitPanels(app); app.UpdateLayout(); app.VerifyQuickPreview(source,target,directory,check);
                var dialog = new DirectorySyncWindow(app,source,target,true) { Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false }; dialog.Show(); NavigationTests.Wait(dialog.Compare()); dialog.UpdateLayout(); NetworkTests.Render(dialog,Path.Combine(directory,"sync-window.png")); dialog.Close();
                var workflow = DirectorySync.Compare(source,target,false,false,delegate { });
                using (var control = new CopyControl()) NavigationTests.Wait(app.RunDirectorySync(workflow,control,delegate { }));
                check(File.Exists(Path.Combine(target,"preview.png")) && File.Exists(Path.Combine(target,"large.txt")),"real application synchronization runs background copy and refreshes panels");
                NavigationTests.Wait(app.JournalWritesForWorkflowTests);
                check(File.ReadAllText(Path.Combine(source,"operations.xml")).Contains("Synchronizace"),"synchronization writes file details to persistent operation history");
                app.Close();
                File.WriteAllLines(Path.Combine(directory,"sync-preview-result.txt"),checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(directory,"sync-preview-result.txt"),"FAIL after " + checks.Count + " checks\n" + String.Join("\n",checks) + "\n" + ex); return 1; }
        }
    }
    public partial class MainWindow
    {
        internal void VerifyQuickPreview(string source,string target,string directory,Action<bool,string> check)
        {
            right.Navigate(target,false); NavigationTests.Wait(right.NavigationTask); SetActive(left);
            left.List.SelectedItem = left.List.Items.Cast<Entry>().Single(x => x.FullPath == Path.Combine(source,"large.txt"));
            string keptPath = right.CurrentPath; object keptItems = right.List.ItemsSource;
            ToggleQuickPreview(); NavigationTests.Wait(quickPreview.LoadingTask);
            check(previewTarget == right && right.List.Visibility == Visibility.Collapsed && right.CurrentPath == keptPath,"Ctrl+Q covers destination list without changing its folder");
            SetActive(right); check(active == left,"preview focus cannot activate hidden destination file operations");
            left.List.SelectedItem = left.List.Items.Cast<Entry>().Single(x => x.FullPath == Path.Combine(source,"preview.png")); NavigationTests.Wait(quickPreview.LoadingTask);
            check(FluentUiTests.Children<Image>(quickPreview).Single().Source != null,"Ctrl+Q displays selected image in other panel");
            left.List.SelectedItem = left.List.Items.Cast<Entry>().Single(x => x.FullPath == Path.Combine(source,"cp1250.txt")); NavigationTests.Wait(quickPreview.LoadingTask);
            check(quickPreview != null,"source cursor changes update preview");
            FluentUiTests.Children<ComboBox>(quickPreview).Single().SelectedIndex = 1; NavigationTests.Wait(quickPreview.LoadingTask);
            check(FluentUiTests.Children<TextBox>(quickPreview).Single(x => x.IsReadOnly).Text == "Příliš žluťoučký kůň","preview encoding control reloads Czech text correctly");
            NetworkTests.Render(this,Path.Combine(directory,"quick-preview.png"));
            CloseQuickPreview(); check(right.List.Visibility == Visibility.Visible && right.CurrentPath == keptPath && ReferenceEquals(right.List.ItemsSource,keptItems),"closing preview restores exact original folder and list");
            ToggleQuickPreview(); CloseQuickPreview(); SwapPanels(); SetActive(right); ToggleQuickPreview(); NavigationTests.Wait(quickPreview.LoadingTask); CloseQuickPreview(); check(left.List.Visibility == Visibility.Visible && right.List.Visibility == Visibility.Visible,"preview remains usable after swapping panels");
        }
    }
}

