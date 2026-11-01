using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FileCompass.NetworkTransport;
namespace FileCompass {
    internal sealed class EditorSaveLocation:IDisposable {
        internal string Folder;internal RemoteConnection Remote;internal Action Saved;
        internal EditorSaveLocation Clone(){return new EditorSaveLocation{Folder=Folder,Remote=Remote==null?null:Remote.TransferConnection(),Saved=Saved};}
        public void Dispose(){if(Remote!=null){Remote.Dispose();Remote=null;}}
    }
    internal sealed partial class CodeEditorWindow {
        internal string DefaultFolder;
        internal Func<EditorDocument,string> ChooseLocalPath,ChooseLocalName,ChooseRemoteName;
        internal Func<string,bool> ConfirmLocalOverwrite,ConfirmRemoteOverwrite;
        internal EditorDocument NewDocument(EditorSaveLocation location=null){
            if(location==null&&Current!=null){location=Current.Location==null?new EditorSaveLocation{Folder=String.IsNullOrEmpty(Current.File.Path)?DefaultFolder:Path.GetDirectoryName(Current.File.Path)}:Current.Location.Clone();}
            var doc=Add(EditorTextFile.New());doc.Location=location??new EditorSaveLocation{Folder=DefaultFolder};
            doc.EncodingChoice=0;doc.EolChoice=1;SyncOptions();RefreshStatus();return doc;
        }
        string PickLocalPath(EditorDocument doc){
            if(ChooseLocalPath!=null)return ChooseLocalPath(doc);
            string folder=doc.Location==null?(String.IsNullOrEmpty(doc.File.Path)?DefaultFolder:Path.GetDirectoryName(doc.File.Path)):doc.Location.Folder;
            if(String.IsNullOrWhiteSpace(folder))throw new IOException(L.Text("tree.openFolderFirst"));
            string name=ChooseLocalName!=null?ChooseLocalName(doc):SettingsUx.Prompt(this,L.Text("rename.fileNameLabel"),String.IsNullOrEmpty(doc.File.Path)?"":Path.GetFileName(doc.File.Path),folder,"codeEditor.save");
            if(name==null)return null;FileOperations.ValidateName(name);
            string target=Path.Combine(folder,name);
            if(Directory.Exists(target))throw new IOException(L.Text("fileOperation.nameExists"));
            if(File.Exists(target)){
                bool yes=ConfirmLocalOverwrite!=null?ConfirmLocalOverwrite(target):MessageBox.Show(this,L.Text("action.overwrite")+"?\n"+target,L.Text("codeEditor.saveAs"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;
                if(!yes)return null;
            }
            return target;
        }
        async Task<bool> SaveRemote(EditorDocument doc,bool saveAs,string text,Encoding selected,byte[] bom){
            string path=doc.RemotePath;var location=doc.Location;
            if(saveAs||String.IsNullOrEmpty(path)){
                string name=ChooseRemoteName!=null?ChooseRemoteName(doc):SettingsUx.Prompt(this,L.Text("codeEditor.saveAs"),String.IsNullOrEmpty(path)?"":RemoteConnection.Name(path),location.Remote.Profile.Name+" · "+location.Folder,"codeEditor.save");
                if(name==null)return false;FileOperations.ValidateName(name);path=RemoteConnection.Combine(location.Folder,name);
            }
            byte[] bytes=EditorBytes(text,selected,bom);string target=path;RemoteFileInfo expected=doc.RemotePath==target?doc.RemoteExpected:null;
            bool choosing=saveAs||doc.RemotePath==null;
            if(choosing){
                expected=await Task.Run(()=>{using(var c=location.Remote.TransferConnection()){c.OpenTransfer();return c.Session.FileExists(target)?c.Session.GetFileInfo(target):null;}});
                if(expected!=null){if(expected.IsDirectory||expected.FileType=='L')throw new IOException(L.Text("remoteEditor.regularFileRequired"));
                    bool yes=ConfirmRemoteOverwrite!=null?ConfirmRemoteOverwrite(target):MessageBox.Show(this,L.Text("action.overwrite")+"?\n"+target,L.Text("codeEditor.saveAs"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;
                    if(!yes)return false;
                }
            }
            var after=await Task.Run(()=>{using(var c=location.Remote.TransferConnection())using(var stream=new MemoryStream(bytes,false)){
                c.OpenTransfer();c.Session.UploadRelay(stream,bytes.Length,DateTime.Now,target,expected,delegate{},delegate{},delegate{});return c.Session.GetFileInfo(target);
            }});
            doc.RemotePath=target;doc.RemoteExpected=after;doc.File.Text=text;doc.File.Encoding=selected;doc.File.Preamble=bom;
            if(location.Saved!=null)location.Saved();return true;
        }
        internal static byte[] EditorBytes(string text,Encoding selected,byte[] bom){if(text.Length>EditorTextFile.Limit)throw new IOException(L.Text("codeEditor.large"));byte[] body=selected.GetBytes(text);if((long)body.Length+bom.Length>EditorTextFile.Limit)throw new IOException(L.Text("codeEditor.large"));return bom.Concat(body).ToArray();}
    }
    public partial class MainWindow {
        void NewTextFile(){
            if(RejectExtWrite())return;
            if(active.IsShellBrowser||active.IsArchive){MessageBox.Show(this,L.Text("tree.openFolderFirst"),L.Text("codeEditor.new"));return;}
            OpenInternalEditor(null);
        }
    }
}
