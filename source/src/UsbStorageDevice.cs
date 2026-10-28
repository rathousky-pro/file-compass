using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;using System.Linq;using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;

namespace FileCompass {
    // Device metadata only: every handle is opened with desiredAccess=0.
    // No WMI, raw reads, dismount, formatting or ejection occurs in this class.
    internal static class UsbStorageDevice {
        [StructLayout(LayoutKind.Sequential)] struct InterfaceData { internal uint Size;internal Guid Class;internal uint Flags;internal IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)] struct DeviceData { internal uint Size;internal Guid Class;internal uint Node;internal IntPtr Reserved; }
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr SetupDiGetClassDevsW(ref Guid id,string enumerator,IntPtr parent,uint flags);
        [DllImport("setupapi.dll",SetLastError=true)]static extern bool SetupDiEnumDeviceInterfaces(IntPtr devices,IntPtr device,ref Guid id,uint index,ref InterfaceData data);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr devices,ref InterfaceData data,IntPtr detail,uint size,out uint required,ref DeviceData device);
        [DllImport("setupapi.dll")]static extern bool SetupDiDestroyDeviceInfoList(IntPtr devices);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFileW(string path,uint access,uint sharing,IntPtr security,uint mode,uint flags,IntPtr template);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool DeviceIoControl(SafeFileHandle handle,uint code,byte[] input,int inputSize,[Out] byte[] output,int outputSize,out int returned,IntPtr overlapped);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Get_Device_IDW(uint instance,StringBuilder id,int length,uint flags);
        internal sealed class Device { internal uint Node,Number;internal ExtSource Source; }
        static string Field(byte[] data,int returned,int field) {
            uint start=BitConverter.ToUInt32(data,field);if(start<36 || start>=(uint)returned)return "";
            int end=(int)start;while(end<returned && data[end]!=0 && end-start<256)end++;
            return Encoding.ASCII.GetString(data,(int)start,end-(int)start).Trim();
        }
        internal static Device Find(uint number) {var result=Enumerate(number).FirstOrDefault();if(result==null)throw new IOException(L.Text("ext.changed"));return result;}
        internal static Device[] Enumerate(uint? wanted=null) {var devices=new List<Device>();
            Guid disk=new Guid("53f56307-b6bf-11d0-94f2-00a0c91efb8b");IntPtr set=SetupDiGetClassDevsW(ref disk,null,IntPtr.Zero,0x12);
            if(set==new IntPtr(-1))throw new Win32Exception(Marshal.GetLastWin32Error());
            try {
                for(uint index=0;index<1024;index++) {
                    var entry=new InterfaceData{Size=(uint)Marshal.SizeOf(typeof(InterfaceData))};
                    if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref disk,index,ref entry)){int error=Marshal.GetLastWin32Error();if(error==259)break;throw new Win32Exception(error);}
                    var info=new DeviceData{Size=(uint)Marshal.SizeOf(typeof(DeviceData))};uint required;
                    SetupDiGetDeviceInterfaceDetailW(set,ref entry,IntPtr.Zero,0,out required,ref info);
                    if(required<8 || required>65536)continue;IntPtr detail=Marshal.AllocHGlobal((int)required);
                    try {
                        Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);
                        if(!SetupDiGetDeviceInterfaceDetailW(set,ref entry,detail,required,out required,ref info))continue;
                        string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));
                        using(var handle=CreateFileW(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
                            if(handle.IsInvalid)continue;var deviceNumber=new byte[12];int returned;
                            if(!DeviceIoControl(handle,0x2d1080,null,0,deviceNumber,deviceNumber.Length,out returned,IntPtr.Zero) || returned<12 || BitConverter.ToUInt32(deviceNumber,0)!=7 || (wanted.HasValue&&BitConverter.ToUInt32(deviceNumber,4)!=wanted.Value))continue;
                            var id=new StringBuilder(512);if(CM_Get_Device_IDW(info.Node,id,id.Capacity,0)!=0)throw new IOException(L.Text("ext.changed"));
                            uint number=BitConverter.ToUInt32(deviceNumber,4);var result=new Device{Node=info.Node,Number=number};var descriptor=new byte[65536];
                            if(DeviceIoControl(handle,0x2d1400,new byte[12],12,descriptor,descriptor.Length,out returned,IntPtr.Zero) && returned>=36 && BitConverter.ToUInt32(descriptor,28)==7) {
                                var geometry=new byte[256];int geometryLength;long length=DeviceIoControl(handle,0x700a0,null,0,geometry,geometry.Length,out geometryLength,IntPtr.Zero)&&geometryLength>=32?BitConverter.ToInt64(geometry,24):0;
                                string product=Field(descriptor,returned,16),serial=Field(descriptor,returned,24).Replace("|","_");
                                result.Source=new ExtSource{Device=@"\\.\PHYSICALDRIVE"+number,Physical=true,Removable=descriptor[10]!=0,Length=length,Sector=geometryLength>=24?BitConverter.ToInt32(geometry,20):512,Name=(String.IsNullOrEmpty(product)?"USB":product)+(length>0?" · "+CopyUi.Bytes(length):""),Token=id+"|"+serial};
                            }
                            devices.Add(result);
                        }
                    } finally {Marshal.FreeHGlobal(detail);}
                }
                return devices.ToArray();
            } finally {SetupDiDestroyDeviceInfoList(set);}
        }
    }
}
