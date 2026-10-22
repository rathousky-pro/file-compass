using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Media.Imaging;

namespace CommanderCZ.NetworkTransport
{
    public sealed partial class Session
    {
        internal byte[] ReadSlice(string path,long offset,int length)
        {
            if (length < 0 || length > 16 * 1024 * 1024 || offset < 0) throw new IOException(L.Text("remotePreview.invalidRange"));
            CheckAbort(); if (options.Protocol == Protocol.Scp) throw new IOException(L.Text("remotePreview.ftpSftpRequired"));
            using (var stream = new MemoryStream(length)) {
                if (length > 0) { if (options.Protocol == Protocol.Ftp) Curl.ReadRange(path,stream,offset,length); else ssh.Transfer(path,stream,offset + length,false,delegate { CheckAbort(); },offset); }
                CheckAbort(); if (stream.Length != length) throw new IOException(L.Text("remotePreview.fileChangedDuringRead")); return stream.ToArray();
            }
        }
    }
}
namespace CommanderCZ
{
    internal sealed partial class RemotePreview
    {
        internal static PreviewData Load(RemoteConnection template,string path,Encoding encoding,CancellationToken token,int decodeWidth = 1400,int imageLimit = 16 * 1024 * 1024)
        {
            using (var connection = template.TransferConnection()) using (token.Register(delegate { connection.Session.Abort(); })) {
                token.ThrowIfCancellationRequested(); connection.OpenTransfer(); var before = connection.Session.GetFileInfo(path);
                if (before.IsDirectory || before.FileType == 'L') return new PreviewData { Status = L.Text("remotePreview.folderOrLinkUnsupported") };
                string kind = FileViewers.Kind(path); bool image = kind == "image";
                if (image && before.Length > imageLimit) return new PreviewData { Status = L.Text("remotePreview.imageSizeLimitHelp") };
                if (!image && kind != "text" && Path.HasExtension(path)) return new PreviewData { Status = L.Text("remotePreview.openTypeWithF3") };
                int length = (int)Math.Min(before.Length,image ? 16 * 1024 * 1024 : 131072); var bytes = connection.Session.ReadSlice(path,0,length);
                if (!NetworkWork.Same(before,connection.Session.GetFileInfo(path))) throw new IOException(L.Text("remotePreview.fileChangedDuringPreview")); token.ThrowIfCancellationRequested();
                if (image) { var bitmap = new BitmapImage(); using (var stream = new MemoryStream(bytes)) { bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = decodeWidth; bitmap.StreamSource = stream; bitmap.EndInit(); } bitmap.Freeze(); return new PreviewData { Image = bitmap,Status = L.Text("remotePreview.imageConnectionHint") }; }
                using (var reader = new StreamReader(new MemoryStream(bytes),encoding,true)) return new PreviewData { Text = reader.ReadToEnd(),Status = before.Length > length ? L.Text("remotePreview.textPrefixLimitHelp") : L.Text("remotePreview.readOnlyText") };
            }
        }
    }
}

