using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
namespace CommanderCZ {
    internal sealed class ClipboardFileContent {
        internal string Text;
        internal BitmapSource Image;
        internal string Extension {get{return Image==null?".txt":".png";}}
        internal static ClipboardFileContent Read(IDataObject data) {
            if(data==null)return null;
            if(data.GetDataPresent(DataFormats.Bitmap)){
                var image=data.GetData(DataFormats.Bitmap) as BitmapSource;
                if(image!=null){image=image.CloneCurrentValue();image.Freeze();return new ClipboardFileContent{Image=image};}
            }
            if(data.GetDataPresent(DataFormats.FileDrop)||data.GetDataPresent("CommanderCZ.VirtualSelection"))return null;
            string text=data.GetDataPresent(DataFormats.UnicodeText)?data.GetData(DataFormats.UnicodeText) as string:
                data.GetDataPresent(DataFormats.Text)?data.GetData(DataFormats.Text) as string:null;
            return text==null?null:new ClipboardFileContent{Text=text};
        }
        internal string WriteNew(string folder,string name) {
            FileOperations.ValidateName(name);
            if(!name.EndsWith(Extension,StringComparison.OrdinalIgnoreCase))name+=Extension;
            FileOperations.ValidateName(name);
            string destination=Path.Combine(folder,name);
            if(File.Exists(destination)||Directory.Exists(destination))throw new IOException(L.Text("clipboardFile.exists"));
            string temporary=Path.Combine(folder,".compass-clipboard-"+Guid.NewGuid().ToString("N")+".tmp");
            try {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                    if(Image!=null){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Image));encoder.Save(stream);}
                    else {var bytes=new UTF8Encoding(false).GetBytes(Text??"");stream.Write(bytes,0,bytes.Length);}
                    stream.Flush(true);
                }
                File.Move(temporary,destination);return destination;
            }finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
    public partial class FilePanel {
        internal bool CanCreateClipboardFile {get{return CanUseFolderView&&!LocalLoading&&!IsLoading;}}
        internal async Task RevealClipboardFile(string folder,string path) {
            if(CurrentPath!=folder)return;
            Refresh();if(NavigationTask!=null)await NavigationTask;
            if(CurrentPath!=folder)return;
            var entry=List.Items.Cast<Entry>().FirstOrDefault(x=>String.Equals(x.FullPath,path,StringComparison.OrdinalIgnoreCase));
            if(entry==null){SetQuickFilter("");entry=List.Items.Cast<Entry>().FirstOrDefault(x=>String.Equals(x.FullPath,path,StringComparison.OrdinalIgnoreCase));} if(entry!=null){List.SelectedItem=entry;List.ScrollIntoView(entry);}FocusFiles();
        }
    }
    public partial class MainWindow {
        internal async void PasteClipboardAsFile() {
            var panel=active;
            if(!panel.CanCreateClipboardFile){MessageBox.Show(this,L.Text("clipboardFile.folderRequired"),L.Text("clipboardFile.title"));return;}
            ClipboardFileContent content;
            try{content=ClipboardFileContent.Read(Clipboard.GetDataObject());}
            catch(Exception ex){MessageBox.Show(this,ex.Message,L.Text("clipboard.unavailableTitle"));return;}
            if(content==null){MessageBox.Show(this,L.Text("clipboardFile.unsupported"),L.Text("clipboardFile.title"));return;}
            string folder=panel.CurrentPath;
            string name=L.Text("clipboardFile.defaultName")+" "+DateTime.Now.ToString("yyyy-MM-dd HHmmss")+content.Extension;
            while(true){
                name=Ask(L.Text("clipboardFile.title"),L.Text(content.Image==null?"clipboardFile.textName":"clipboardFile.imageName"),name,true);
                if(name==null)return;
                try{
                    string path;fileToolsRunning++;try{path=await Task.Run(()=>content.WriteNew(folder,name));}finally{fileToolsRunning--;}
                    await panel.RevealClipboardFile(folder,path);return;
                }catch(Exception ex){MessageBox.Show(this,ex.Message,L.Text("clipboardFile.title"),MessageBoxButton.OK,MessageBoxImage.Warning);}
            }
        }
    }
}