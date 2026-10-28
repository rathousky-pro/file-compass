using System;using System.IO;using System.Reflection;using System.Collections;using System.Collections.Generic;using System.Runtime.CompilerServices;using DiscUtils.Ext;
namespace FileCompass {
 // Pinned DiscUtils 1.0.90 adapter: inspect original inode modes before any API
 // which resolves symlinks. Fail closed if its internal metadata contract changes.
 internal static class ExtMetadata {
  sealed class Adapter {
   readonly object inner;readonly MethodInfo directory,inode;
   internal Adapter(ExtFileSystem fs){inner=typeof(DiscUtils.Vfs.VfsFileSystemFacade).GetField("_wrapped",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(fs);directory=inner.GetType().GetMethod("GetDirectory",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(string)},null);inode=inner.GetType().GetMethod("GetInode",BindingFlags.Instance|BindingFlags.NonPublic);if(directory==null||inode==null)throw new IOException(L.Text("ext.incompatibleAdapter"));}
   object Inode(uint number){return inode.Invoke(inner,new object[]{number});}
   ExtEntry Info(uint number,string path,string name){var item=Inode(number);int mode=Convert.ToInt32(item.GetType().GetField("Mode").GetValue(item))>>12;long size=(long)item.GetType().GetField("FileSize").GetValue(item);uint modified=(uint)item.GetType().GetField("ModificationTime").GetValue(item);return new ExtEntry{Name=name,Path=path,Inode=number,Directory=mode==4,Readable=mode==4||mode==8,Size=mode==8?size:0,Modified=DateTimeOffset.FromUnixTimeSeconds(modified).LocalDateTime,Kind=L.Text(mode==4?"ext.folder":mode==8?"ext.file":"ext.special")};}
   IEnumerable<ExtEntry> Entries(string path){object dir=directory.Invoke(inner,new object[]{path});var values=(IEnumerable)dir.GetType().GetProperty("AllEntries").GetValue(dir);int count=0;foreach(object pair in values){if(++count>20000)throw new IOException(L.Text("ext.limit"));var valueProperty=pair.GetType().GetProperty("Value");object entry=valueProperty==null?pair:valueProperty.GetValue(pair);string name=(string)entry.GetType().GetProperty("FileName").GetValue(entry);object record=entry.GetType().GetProperty("Record").GetValue(entry);uint number=(uint)record.GetType().GetField("Inode").GetValue(record);yield return Info(number,ExtReader.Join(path,name),name);}}
   internal ExtEntry Node(string path){path=ExtReader.Normal(path);if(path=="/")return Info(2,"/","");string parent="/";var segments=path.Trim('/').Split('/');for(int i=0;i<segments.Length;i++){object dir=directory.Invoke(inner,new object[]{parent});object entry=dir.GetType().GetMethod("GetEntryByName").Invoke(dir,new object[]{segments[i]});if(entry==null)throw new FileNotFoundException(path);object record=entry.GetType().GetProperty("Record").GetValue(entry);uint number=(uint)record.GetType().GetField("Inode").GetValue(record);var found=Info(number,ExtReader.Join(parent,segments[i]),segments[i]);if(i==segments.Length-1)return found;if(!found.Directory||!found.Readable)throw new IOException(L.Text("ext.special"));parent=found.Path;}throw new IOException(L.Text("ext.invalidPath"));}
   internal IEnumerable<ExtEntry> List(string path){var node=Node(path);if(!node.Directory)throw new IOException(L.Text("ext.special"));return Entries(ExtReader.Normal(path));}
  }
  static readonly ConditionalWeakTable<ExtFileSystem,Adapter> adapters=new ConditionalWeakTable<ExtFileSystem,Adapter>();
  internal static ExtEntry Read(ExtFileSystem fs,string path){return adapters.GetValue(fs,x=>new Adapter(x)).Node(path);}
  internal static IEnumerable<ExtEntry> List(ExtFileSystem fs,string path){return adapters.GetValue(fs,x=>new Adapter(x)).List(path);}
 }
}
