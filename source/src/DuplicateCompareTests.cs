using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace FileCompass {
 internal static class DuplicateCompareTests {
  [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
  static extern bool CreateHardLink(string link,string existing,IntPtr security);
  static void Png(string path,Color color,int width,int height){var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(new SolidColorBrush(color),null,new Rect(0,0,width,height));var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(path))encoder.Save(output);}
  internal static int Run(string root){
   Directory.CreateDirectory(root);var checks=new List<string>();DuplicateWindow duplicates=null;FileDiffWindow compare=null;
   Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add(label);};
   try{
    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
    string a=Path.Combine(root,"a"),b=Path.Combine(root,"b");Directory.CreateDirectory(a);Directory.CreateDirectory(b);
    File.WriteAllText(Path.Combine(a,"one.txt"),"identical content");File.WriteAllText(Path.Combine(b,"renamed.txt"),"identical content");
    File.WriteAllText(Path.Combine(a,"different.txt"),"different bytes!");File.WriteAllText(Path.Combine(b,"unique.txt"),"unique");
    File.WriteAllBytes(Path.Combine(a,"empty.txt"),new byte[0]);File.WriteAllBytes(Path.Combine(b,"empty.txt"),new byte[0]);
    var warnings=new List<string>();
    var found=DuplicateEngine.Scan(new[]{a,b,a},CancellationToken.None,delegate{},warnings.Add);
    check(found.Count==4&&found.Select(x=>x.Group).Distinct().Count()==2,"Size and full content produce two duplicate groups");
    check(found.Select(x=>x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()==4,"Overlapping roots do not duplicate results");
    check(found.Any(x=>x.Path.EndsWith("renamed.txt"))&&!found.Any(x=>x.Path.EndsWith("different.txt")),"Different names match; equal lengths with different bytes do not");
    check(warnings.Count==0,"Readable fixture does not produce warnings");
    string hardlink=Path.Combine(a,"hardlink.txt");check(CreateHardLink(hardlink,Path.Combine(a,"one.txt"),IntPtr.Zero),"Hardlink fixture created");
    var aliases=DuplicateEngine.Scan(new[]{a,b},CancellationToken.None,delegate{},delegate{});check(aliases.Count==4,"Physical file identities exclude hardlink/alias duplicates");
    DuplicateEngine.SelectCopies(found);check(found.Count(x=>x.Selected)==2&&found.GroupBy(x=>x.Group).All(x=>x.Count(y=>!y.Selected)==1),"Copy selection retains exactly one file per group");
    int recycled=0;DuplicateEngine.Recycle(found,CancellationToken.None,path=>{File.Delete(path);recycled++;});
    check(recycled==2&&found.Count==2&&found.All(x=>File.Exists(x.Path)),"Approved recycle callback removes only selected copies");
    var allSelected=found.Select(x=>new DuplicateFile{Group=x.Group,Path=x.Path,Bytes=x.Bytes,Modified=x.Modified,Digest=x.Digest,Selected=true}).ToList();
    bool rejected=false;try{DuplicateEngine.Recycle(allSelected,CancellationToken.None,path=>{throw new Exception("Unexpected delete");});}catch(IOException){rejected=true;}check(rejected,"Deleting all members of a group is rejected before any callback");
    string retained=Path.Combine(a,"change-original.txt"),copy=Path.Combine(b,"change-copy.txt");
    File.WriteAllText(retained,"before");File.WriteAllText(copy,"before");var changed=DuplicateEngine.Scan(new[]{retained,copy},CancellationToken.None,delegate{},delegate{});DuplicateEngine.SelectCopies(changed);File.WriteAllText(retained,"after!");
    rejected=false;try{DuplicateEngine.Recycle(changed,CancellationToken.None,path=>{throw new Exception("Unexpected delete");});}catch(IOException){rejected=true;}check(rejected&&File.Exists(copy),"Changed retained content blocks the whole deletion plan");
    using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool cancelled=false;try{DuplicateEngine.Scan(new[]{root},cancel.Token,delegate{},delegate{});}catch(OperationCanceledException){cancelled=true;}check(cancelled,"Duplicate scan honours cancellation");}
    string left=Path.Combine(root,"left.png"),right=Path.Combine(root,"right.png");Png(left,Colors.SteelBlue,320,200);Png(right,Colors.Coral,240,200);
    var loaded=ComparedImage.Load(left,CancellationToken.None);check(loaded.Width==320&&loaded.Height==200&&loaded.Source.IsFrozen,"Image comparison loads bounded immutable images");
    using(File.Open(left,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}check(true,"Image decoding releases source handles");
    compare=new FileDiffWindow(null,left,right);compare.Show();NavigationTests.Wait(compare.ComparisonTask);compare.UpdateLayout();
    var images=AdvancedToolsTests.Visual<ImageComparisonView>(compare).Single();check(images.Visibility==Visibility.Visible,"Compare Files auto-detects a pair of images");
    check(AdvancedToolsTests.Visual<Image>(images).Count(x=>x.Source!=null)==4,"Both side and overlay images are loaded");
    NextStageTests.Draw(compare,Path.Combine(root,"images-side.png"));
    var mode=AdvancedToolsTests.Visual<ComboBox>(images).Single();mode.SelectedIndex=1;compare.UpdateLayout();var wipe=AdvancedToolsTests.Visual<Slider>(images).Single();wipe.Value=.25;
    check(wipe.IsEnabled,"Overlay mode enables the comparison slider");NextStageTests.Draw(compare,Path.Combine(root,"images-overlay.png"));
    var mainMode=AdvancedToolsTests.Visual<ComboBox>(compare).Single(x=>x.Items.Contains(L.Text("imageCompare.mode")));mainMode.SelectedIndex=3;mainMode.SelectedIndex=2;NavigationTests.Wait(compare.ComparisonTask);check(compare.LastResult!=null&&compare.LastResult.BinaryMode,"Mode changes cancel image loading and restore explicit binary comparison");
    compare.Close();compare=null;
    File.WriteAllText(Path.Combine(a,"one.txt"),"text line\noriginal\n");File.WriteAllText(Path.Combine(b,"two.txt"),"text line\nchanged\n");
    compare=new FileDiffWindow(null,Path.Combine(a,"one.txt"),Path.Combine(b,"two.txt"));compare.Show();NavigationTests.Wait(compare.ComparisonTask);
    check(compare.LastResult!=null&&!compare.LastResult.Identical&&compare.LastResult.Rows.Any(x=>x.IsDifferent),"Existing side-by-side text difference highlighting is preserved");NextStageTests.Draw(compare,Path.Combine(root,"text-diff.png"));compare.Close();compare=null;
    File.WriteAllText(Path.Combine(a,"ui-copy.txt"),"duplicate window fixture");File.WriteAllText(Path.Combine(b,"ui-copy.txt"),"duplicate window fixture");
    duplicates=new DuplicateWindow(null,a+";"+b);duplicates.Show();NavigationTests.Wait(duplicates.Scan());duplicates.UpdateLayout();
    check(duplicates.Results.Items.Count>=2,"Dedicated duplicate window displays grouped results");
    check(AdvancedToolsTests.Visual<Button>(duplicates).Any(x=>Convert.ToString(x.Content)==L.Text("duplicates.selectCopies")),"Window exposes retain-one copy selection");
    NextStageTests.Draw(duplicates,Path.Combine(root,"duplicates.png"));
    File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count}));return 0;
   }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}
   finally{if(compare!=null)compare.Close();if(duplicates!=null)duplicates.Close();}
  }
 }
}
