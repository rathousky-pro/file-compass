using System;
using System.IO;
using System.Xml.Serialization;
namespace FileCompass.NetworkTransport {
    public sealed class PartialTransferInfo {
        public int Version = 1;
        public string OriginalName, TargetPath, CheckpointIdentity;
        public long SourceLength, SourceModifiedUtcTicks;
    }
    public sealed partial class Session {
        void WriteUploadInfo(string stage, string source, string target, TransferCheckpointData checkpoint) {
            string path = stage + ".fcinfo.xml";
            if (FileExists(path)) {
                var existing=GetFileInfo(path);
                if (existing.IsDirectory || existing.FileType=='L') throw new IOException(L.Text("resume.partialPathNotRegularFile"));
            }
            var info=new PartialTransferInfo { OriginalName=Path.GetFileName(source), TargetPath=target, CheckpointIdentity=checkpoint.Identity, SourceLength=checkpoint.Length, SourceModifiedUtcTicks=checkpoint.Modified };
            using (var bytes=new MemoryStream()) {
                new XmlSerializer(typeof(PartialTransferInfo)).Serialize(bytes,info); bytes.Position=0;
                if (options.Protocol==Protocol.Ftp) Curl.Upload(path,bytes,bytes.Length,delegate { CheckAbort(); });
                else ssh.Transfer(path,bytes,bytes.Length,true,delegate { CheckAbort(); });
            }
        }
    }
}
