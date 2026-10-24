using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace FileCompass {
    internal sealed partial class QuickPreviewPane {
        const long PreparedPreviewLimit=64L*1024*1024;
        ArchiveTemp previewTemp;
        internal string PreviewTempPath {get{return previewTemp==null?null:previewTemp.Path;}}
        static void ReleasePreviewTemp(ArchiveTemp temp) {
            if(temp==null)return;
            try{temp.Dispose();}
            catch(IOException){Task.Run(async delegate{for(int i=0;i<12;i++){await Task.Delay(250);try{temp.Dispose();return;}catch(IOException){}catch(UnauthorizedAccessException){}}});}
            catch(UnauthorizedAccessException){Task.Run(async delegate{for(int i=0;i<12;i++){await Task.Delay(250);try{temp.Dispose();return;}catch(IOException){}catch(UnauthorizedAccessException){}}});}
        }
        void ClearPreparedPreview(){var temp=previewTemp;previewTemp=null;ReleasePreviewTemp(temp);}
        internal void ShowArchiveFile(ArchiveDocument archive,string path) {
            // Snapshot the archive context so a failed password attempt cannot mutate the panel.
            var snapshot=new ArchiveDocument{FilePath=archive.FilePath,Password=archive.Password,Type=archive.Type,Stamp=archive.Stamp,Parent=archive.Parent,MultiVolume=archive.MultiVolume,ParentMember=archive.ParentMember,Items=archive.Items};
            LoadingTask=LoadPreparedPreview(path,archive.Display+" / "+path,L.Text("preview.extractingTemporary"),delegate(ArchiveTemp temp,CancellationToken token) {
                string member=ArchiveEngine.SafeMember(path.TrimStart('/'));
                var item=snapshot.Items.SingleOrDefault(x=>x.Path==member);
                if(item==null||item.Directory)throw new IOException(L.Text("layout.selectFile"));
                if(item.Bytes<0||item.Bytes>PreparedPreviewLimit)throw new IOException(L.Text("preview.preparedSizeLimit"));
                ArchiveEngine.Extract(snapshot,new[]{member},temp.Path,token);
                string file=ArchiveEngine.DiskPath(temp.Path,member);
                if(new FileInfo(file).Length>PreparedPreviewLimit)throw new IOException(L.Text("preview.preparedSizeLimit"));
                File.SetAttributes(file,File.GetAttributes(file)&~FileAttributes.ReadOnly);
                return file;
            });
        }
        internal void ShowDownloadedPreview(string path,RemoteConnection remote) {
            var transfer=remote.TransferConnection();
            LoadingTask=LoadPreparedPreview(path,remote.Display(path),L.Text("preview.downloadingTemporary"),delegate(ArchiveTemp temp,CancellationToken token) {
                using(token.Register(delegate{transfer.Session.Abort();})) {
                    token.ThrowIfCancellationRequested();transfer.OpenTransfer();token.ThrowIfCancellationRequested();
                    var before=transfer.Session.GetFileInfo(path);
                    if(before.IsDirectory||before.FileType=='L')throw new IOException(L.Text("remotePreview.folderOrLinkUnsupported"));
                    if(before.Length<0||before.Length>PreparedPreviewLimit)throw new IOException(L.Text("preview.preparedSizeLimit"));
                    string name=RemoteConnection.Name(path);FileOperations.ValidateName(name);
                    string file=Path.Combine(temp.Path,name);
                    using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                        transfer.Session.DownloadRelay(path,output,before.Length,delegate{token.ThrowIfCancellationRequested();});
                    if(!NetworkWork.Same(before,transfer.Session.GetFileInfo(path)))throw new IOException(L.Text("remotePreview.fileChangedDuringPreview"));
                    AttachmentSecurity.MarkInternet(file);
                    token.ThrowIfCancellationRequested();return file;
                }
            },transfer);
        }
        async Task LoadPreparedPreview(string name,string location,string phase,Func<ArchiveTemp,CancellationToken,string> prepare,IDisposable transfer=null) {
            ShowMessage(phase);title.Text=Path.GetFileName(name);title.ToolTip=location;
            var token=cancellation.Token;int request=version;ArchiveTemp temp=null;
            try {
                await Task.Delay(150,token);
                string file=await Task.Run(delegate {
                    token.ThrowIfCancellationRequested();temp=new ArchiveTemp();
                    return prepare(temp,token);
                });
                token.ThrowIfCancellationRequested();if(request!=version)return;
                var loading=Load(file,0); // Load cancels the preparation token and creates its own request.
                previewTemp=temp;temp=null;
                title.ToolTip=location;
                await loading;
            }catch(OperationCanceledException){}
             catch(Exception ex){if(request==version)ShowMessage(L.Text("preview.loadFailedPrefix")+ex.Message);}
             finally{if(transfer!=null)transfer.Dispose();ReleasePreviewTemp(temp);}
        }
    }
}
