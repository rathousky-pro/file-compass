using System;
using System.Runtime.InteropServices;
namespace FileCompass {
    internal static class SensitiveMemory {
        internal static void Clear(IntPtr pointer,int length){var zero=new byte[Math.Min(4096,Math.Max(1,length))];for(int offset=0;offset<length;offset+=zero.Length)Marshal.Copy(zero,0,IntPtr.Add(pointer,offset),Math.Min(zero.Length,length-offset));}
        internal static void Free(IntPtr pointer,int length){if(pointer==IntPtr.Zero)return;Clear(pointer,length);Marshal.FreeHGlobal(pointer);}
    }
}
