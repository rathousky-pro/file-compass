using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;

namespace FileCompass
{
    internal enum IsoBootStatus { Unknown, Data, Bootable }

    internal sealed class DataIso
    {
        const int BlockSize = 2048;
        internal IsoBootStatus BootStatus;
        internal bool CanRebuild;
        internal string VolumeName;

        internal static DataIso Inspect(string path, string format)
        {
            var image = new DataIso();
            using (var file = File.OpenRead(path))
            {
                var systemArea = new byte[16 * BlockSize];
                if (file.Read(systemArea, 0, systemArea.Length) != systemArea.Length) return image;
                bool plain = systemArea.All(b => b == 0);
                bool primary = false, terminated = false, supported = true;
                var sector = new byte[BlockSize];
                using (var reader = new BinaryReader(file, Encoding.ASCII, true))
                {
                    for (int block = 16; block < 256 && file.Read(sector, 0, BlockSize) == BlockSize; block++)
                    {
                        string signature = Encoding.ASCII.GetString(sector, 1, 5);
                        if (signature == "BEA01" || signature == "NSR02" || signature == "NSR03") supported = false;
                        if (signature != "CD001")
                        {
                            if (!terminated) supported = false;
                            continue;
                        }
                        int type = sector[0];
                        if (type == 0 && Encoding.ASCII.GetString(sector, 7, 32).TrimEnd('\0', ' ') == "EL TORITO SPECIFICATION")
                        {
                            image.BootStatus = IsoBootStatus.Bootable;
                            return image;
                        }
                        if (terminated) continue;
                        if (sector[6] != 1) supported = false;
                        if (type == 255) { terminated = true; continue; }
                        if (type == 2 && sector[88] == '%' && sector[89] == '/' &&
                            (sector[90] == '@' || sector[90] == 'C' || sector[90] == 'E'))
                        {
                            image.VolumeName = Encoding.BigEndianUnicode.GetString(sector, 40, 32).TrimEnd('\0', ' ');
                            continue;
                        }
                        if (type != 1 || primary) { supported = false; continue; }
                        primary = true;
                        if (image.VolumeName == null) image.VolumeName = Encoding.ASCII.GetString(sector, 40, 32).TrimEnd('\0', ' ');
                        long declaredSize = (long)BitConverter.ToUInt32(sector, 80) * BlockSize;
                        long root = (long)BitConverter.ToUInt32(sector, 158) * BlockSize;
                        if (declaredSize != file.Length || BitConverter.ToUInt16(sector, 128) != BlockSize ||
                            sector[157] != 0 || root < systemArea.Length || root >= file.Length)
                        { supported = false; continue; }
                        long next = file.Position;
                        file.Position = root;
                        int recordLength = reader.ReadByte();
                        byte[] record = reader.ReadBytes(Math.Max(0, recordLength - 1));
                        // System Use data on the root entry can announce Rock Ridge extensions.
                        if (record.Length != 33) supported = false;
                        file.Position = next;
                    }
                }
                if (plain && primary && terminated)
                {
                    image.BootStatus = IsoBootStatus.Data;
                    image.CanRebuild = supported && format.Equals("iso", StringComparison.OrdinalIgnoreCase);
                }
                return image;
            }
        }

        internal static void Build(string folder, string target, string label, CancellationToken token, Action<string> progress)
        {
            var type = Type.GetTypeFromProgID("IMAPI2FS.MsftFileSystemImage");
            if (type == null) throw new IOException(L.Text("discImage.isoWriterUnavailable"));
            object builder = null, root = null, result = null, stream = null;
            IntPtr read = Marshal.AllocCoTaskMem(sizeof(int));
            try
            {
                token.ThrowIfCancellationRequested();
                builder = Activator.CreateInstance(type);
                Set(builder, "FileSystemsToCreate", 3); // ISO 9660 and Joliet
                Set(builder, "ISO9660InterchangeLevel", 2);
                Set(builder, "FreeMediaBlocks", 0);
                Set(builder, "StageFiles", false);
                Set(builder, "VolumeName", label);
                root = Invoke(builder, "Root", BindingFlags.GetProperty);
                if (Directory.EnumerateFileSystemEntries(folder).Any())
                    Invoke(root, "AddTree", BindingFlags.InvokeMethod, folder, false);
                token.ThrowIfCancellationRequested();
                result = Invoke(builder, "CreateResultImage", BindingFlags.InvokeMethod);
                stream = Invoke(result, "ImageStream", BindingFlags.GetProperty);
                var input = (IStream)stream;
                input.Stat(out var stat, 1);
                using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
                {
                    var buffer = new byte[1024 * 1024];
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        input.Read(buffer, buffer.Length, read);
                        int count = Marshal.ReadInt32(read);
                        if (count == 0) break;
                        output.Write(buffer, 0, count);
                        if (progress != null && stat.cbSize > 0)
                            progress(new ArchiveCompressionProgress { Percent = 99.0 * output.Position / stat.cbSize, Packed = output.Position }.Encode());
                    }
                    if (output.Position != stat.cbSize) throw new IOException(L.Text("discImage.isoVerificationFailed"));
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(read);
                foreach (var item in new[] { stream, result, root, builder })
                    if (item != null && Marshal.IsComObject(item)) Marshal.ReleaseComObject(item);
            }
        }

        static object Invoke(object target, string name, BindingFlags operation, params object[] arguments)
        {
            return target.GetType().InvokeMember(name, operation, null, target, arguments);
        }

        static void Set(object target, string name, object value)
        {
            Invoke(target, name, BindingFlags.SetProperty, value);
        }
    }
}
