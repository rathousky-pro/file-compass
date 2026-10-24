using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
namespace FileCompass {
    internal static partial class FileMetadata {
        static object ShellCall(object obj,string method,params object[] args){return obj.GetType().InvokeMember(method,BindingFlags.InvokeMethod,null,obj,args);}
        static void Shell(string path,FileDetails d) {
            object shell=null,folder=null,item=null;
            try {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));folder=ShellCall(shell,"NameSpace",Path.GetDirectoryName(path));if(folder==null)return;
                item=ShellCall(folder,"ParseName",Path.GetFileName(path));if(item==null)return;
                foreach(var pair in new[]{new[]{"title","System.Title"},new[]{"author","System.Author"},new[]{"subject","System.Subject"},new[]{"keywords","System.Keywords"},new[]{"pages","System.Document.PageCount"},new[]{"cachedWords","System.Document.WordCount"},new[]{"company","System.Company"},new[]{"application","System.ApplicationName"},new[]{"comment","System.Comment"},new[]{"target","System.Link.TargetParsingPath"},new[]{"arguments","System.Link.Arguments"}}){
                    object value=ShellCall(item,"ExtendedProperty",pair[1]);if(value==null||Convert.ToString(value)=="0")continue;var values=value as string[];d.Add(pair[0],values==null?Convert.ToString(value):String.Join(", ",values));
                }
            }finally{foreach(var obj in new[]{item,folder,shell})if(obj!=null&&Marshal.IsComObject(obj))Marshal.ReleaseComObject(obj);}
        }
    }
}

