using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FileCompass.NetworkTransport
{
    internal static class NfsNative
    {
        const string Dll = "FileCompassNetwork.dll";
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void ItemCallback(IntPtr name, int directory, ulong size, long unixTime, IntPtr state);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        static extern int cn_nfs_list(byte[] server, byte[] export, int version, byte[] path,
            ItemCallback callback, IntPtr state, StringBuilder error, int errorSize);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int DataCallback(IntPtr buffer,int count,IntPtr state);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
        static extern int cn_nfs_read(byte[] host,byte[] export,int version,byte[] path,
            ulong offset,ulong length,DataCallback callback,IntPtr state,StringBuilder error,int errorSize);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
        static extern int cn_nfs_write(byte[] host,byte[] export,int version,byte[] path,
            ulong length,DataCallback callback,IntPtr state,StringBuilder error,int errorSize);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
        static extern int cn_nfs_action(byte[] host,byte[] export,int version,int action,
            byte[] path,byte[] second,StringBuilder error,int errorSize);
        static void Result(int code,StringBuilder error)
        {
            if(code<0)throw new IOException("NFS: "+error.ToString());
        }
        internal static void Read(string host,string export,int version,string path,Stream output,long length,long offset,Action<long> progress,Action check)
        {
            if(length<offset || offset<0)throw new ArgumentException("Invalid NFS read range.");
            NativeNetwork.Ensure();
            Exception failure=null;long done=0;
            DataCallback callback=delegate(IntPtr ptr,int count,IntPtr state) {
                if(failure!=null)return -1;
                try {
                    check();
                    byte[] bytes=new byte[count];Marshal.Copy(ptr,bytes,0,count);
                    output.Write(bytes,0,count);done+=count;progress(done);return 0;
                } catch(Exception ex){failure=ex;return -1;}
            };
            var error=new StringBuilder(2048);
            int result=cn_nfs_read(Utf8(host),Utf8(export),version,Utf8(path),(ulong)offset,(ulong)(length-offset),callback,IntPtr.Zero,error,error.Capacity);
            GC.KeepAlive(callback);
            if(failure!=null)throw new IOException("NFS read interrupted.",failure);
            Result(result,error);
        }
        internal static void Write(string host,string export,int version,string path,Stream input,long length,Action<long> progress,Action check)
        {
            if(length<0)throw new ArgumentException("Invalid NFS write length.");
            NativeNetwork.Ensure();
            Exception failure=null;long done=0;
            DataCallback callback=delegate(IntPtr ptr,int count,IntPtr state){
                if(failure!=null)return -1;
                try{
                    check();
                    byte[] buffer=new byte[count];int read=input.Read(buffer,0,count);
                    if(read<=0)return -1;
                    Marshal.Copy(buffer,0,ptr,read);done+=read;progress(done);return read;
                } catch(Exception ex){failure=ex;return -1;}
            };
            var error=new StringBuilder(2048);
            int result=cn_nfs_write(Utf8(host),Utf8(export),version,Utf8(path),(ulong)length,callback,IntPtr.Zero,error,error.Capacity);
            GC.KeepAlive(callback);
            if(failure!=null)throw new IOException("NFS write interrupted.",failure);
            Result(result,error);
        }
        internal static void Action(string host,string export,int version,int action,string first,string second=null)
        {
            NativeNetwork.Ensure();
            var error=new StringBuilder(2048);
            Result(cn_nfs_action(Utf8(host),Utf8(export),version,action,Utf8(first),Utf8(second??""),error,error.Capacity),error);
        }
        static byte[] Utf8(string value) { return Encoding.UTF8.GetBytes(value + "\0"); }
        static string Name(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return "";
            int length = 0; while (length < 4096 && Marshal.ReadByte(pointer,length) != 0) length++;
            if (length == 4096) throw new IOException("NFS entry name is too long.");
            var buffer = new byte[length]; Marshal.Copy(pointer,buffer,0,length);
            return Encoding.UTF8.GetString(buffer);
        }
        internal static RemoteDirectoryInfo List(string host, string export, int version, string path)
        {
            if (version != 3 && version != 4 && version != 42) throw new ArgumentException(L.Text("nfs.unsupportedVersion"));
            NativeNetwork.Ensure();
            var result = new RemoteDirectoryInfo();
            Exception callbackError = null;
            ItemCallback callback = delegate(IntPtr pointer, int directory, ulong size, long unixTime, IntPtr state)
            {
                if (callbackError != null) return;
                try
                {
                    string name = Name(pointer);
                    if (name == "." || name == ".." || name.Length == 0 || name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0) return;
                    DateTime modified = new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
                    if (unixTime > 0 && unixTime < 253402300799L) modified = modified.AddSeconds(unixTime).ToLocalTime();
                    result.Files.Add(new RemoteFileInfo { Name=name, FullName=FileCompass.RemoteConnection.Combine(path,name),
                        FileType=directory == 1 ? 'D' : directory == 2 ? 'L' : '-', Length=(long)Math.Min(size,(ulong)Int64.MaxValue),LastWriteTime=modified });
                }
                catch(Exception ex) { callbackError = ex; }
            };
            var error = new StringBuilder(2048);
            int code=cn_nfs_list(Utf8(host),Utf8(export),version,Utf8(path),callback,IntPtr.Zero,error,error.Capacity);
            GC.KeepAlive(callback);
            if (callbackError != null) throw new IOException("Invalid NFS directory entry.",callbackError);
            if (code != 0) throw new IOException("NFS connection failed: " + error.ToString());
            return result;
        }
    }
}
