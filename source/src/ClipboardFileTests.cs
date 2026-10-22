using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace CommanderCZ {
    public partial class MainWindow {
        internal async System.Threading.Tasks.Task CheckClipboardCreation(string root,Action<bool,string> check) {
            check(AdvancedToolsTests.Visual<System.Windows.Controls.Menu>(this).SelectMany(x=>x.Items.OfType<System.Windows.Controls.MenuItem>()).SelectMany(x=>x.Items.OfType<System.Windows.Controls.MenuItem>()).Any(x=>Convert.ToString(x.Header)==L.Text("clipboardFile.title")&&x.InputGestureText=="Ctrl+Shift+V"),"Edit menu exposes the new command and keyboard shortcut");
            SetActive(right);check(active.CanCreateClipboardFile,"The active ordinary folder accepts clipboard files");
            active.SetQuickFilter("*.does-not-match");string path=new ClipboardFileContent{Text="active panel"}.WriteNew(active.CurrentPath,"Selected.txt");
            await active.RevealClipboardFile(root,path);
            check(((Entry)active.List.SelectedItem).FullPath==path&&active.QuickFilter=="","New file is revealed and selected in the active panel even with a previous filter");
            check(active.List.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>().Any(x=>Convert.ToString(x.Header)==L.Text("clipboardFile.title")),"Panel context menu includes clipboard file creation");
        }
    }
    internal static class ClipboardFileTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root);var checks=new List<string>();MainWindow main=null;
            Action<bool,string> check=(valid,name)=>{if(!valid)throw new Exception(name);checks.Add(name);};
            try{
                var data=new DataObject();string original="Žluťoučký kůň\r\nDruhý řádek\t✓";
                data.SetData(DataFormats.UnicodeText,original);
                var content=ClipboardFileContent.Read(data);
                string text=content.WriteNew(root,"Čeština");
                check(Path.GetExtension(text)==".txt"&&File.ReadAllText(text,Encoding.UTF8)==original,"Unicode text and line endings survive creation with automatic TXT extension");
                check(!File.ReadAllBytes(text).Take(3).SequenceEqual(new byte[]{239,187,191}),"Text uses UTF-8 without BOM");
                bool refused=false;try{content.WriteNew(root,"Čeština.txt");}catch(IOException){refused=true;}
                check(refused&&File.ReadAllText(text)==original,"Existing destination cannot be overwritten");
                foreach(string bad in new[]{"../outside","CON","a.","","a\\b"}){refused=false;try{content.WriteNew(root,bad);}catch(IOException){refused=true;}check(refused,"Invalid name rejected: "+bad);}
                var image=BitmapSource.Create(2,1,96,96,PixelFormats.Bgra32,null,new byte[]{0,0,255,255,0,255,0,128},8);
                data.SetData(DataFormats.Bitmap,image);content=ClipboardFileContent.Read(data);
                check(content.Image!=null&&content.Image.IsFrozen,"Image wins over text and is detached for background writing");
                string png=content.WriteNew(root,"Screenshot.png");
                var decoder=new PngBitmapDecoder(new Uri(png),BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
                check(decoder.Frames[0].PixelWidth==2&&decoder.Frames[0].PixelHeight==1,"Image is encoded as a real PNG at its original dimensions");
                byte[] pixels=new byte[8];decoder.Frames[0].CopyPixels(pixels,8,0);check(pixels[7]==128,"PNG retains transparency");
                var files=new DataObject();files.SetData(DataFormats.FileDrop,new[]{text});files.SetText(text);
                check(ClipboardFileContent.Read(files)==null,"Copied files are not mistaken for pasted path text");
                check(ClipboardFileContent.Read(new DataObject())==null&&ClipboardFileContent.Read(null)==null,"Empty and unsupported clipboard formats are handled");
                var empty=new DataObject();empty.SetText("");var blank=ClipboardFileContent.Read(empty);
                check(blank!=null&&new FileInfo(blank.WriteNew(root,"Empty.txt")).Length==0,"Empty text can be saved");
                check(!Directory.GetFiles(root,".compass-clipboard-*.tmp").Any(),"No temporary files remain after writes or errors");
                System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher)); main=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual}; main.Show(); NavigationTests.WaitPanels(main); NavigationTests.Wait(main.CheckClipboardCreation(root,check)); File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),String.Join(Environment.NewLine,checks)+Environment.NewLine+"FAIL: "+ex);return 1;}finally{if(main!=null)main.Close();}
        }
    }
}