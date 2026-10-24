using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace FileCompass {
internal static class FileIconTests {
static byte[] Pixels(ImageSource image){var visual=new DrawingVisual();using(var d=visual.RenderOpen())d.DrawImage(image,new Rect(0,0,24,24));var bmp=new RenderTargetBitmap(24,24,96,96,PixelFormats.Pbgra32);bmp.Render(visual);var bytes=new byte[24*24*4];bmp.CopyPixels(bytes,24*4,0);return bytes;}
internal static int Run(string root){Directory.CreateDirectory(root);var checks=new List<string>();Window window=null;try{
var panel=new System.Windows.Controls.Primitives.UniformGrid {Columns=4,Margin=new Thickness(12)};
var unknown=DesktopIcons.FileIcon("unknown.filecompass_unknown_type",false);
foreach(string ext in new[]{"rar","zip","7z","tar","gz","arj","cab","iso","pdf","docx","xlsx","txt","md","cs","json","jpg","png","mp3","mp4","html","lnk","ico","exe","filecompass_unknown_type"}){
string path=Path.Combine(root,"sample."+ext);File.WriteAllText(path,"");var icon=DesktopIcons.FileIcon(path,false);if(icon==null||!icon.IsFrozen)throw new Exception("Missing/non-frozen icon: "+ext);
if(new[]{"rar","zip","7z","tar","gz","arj","cab","iso"}.Contains(ext)&&Pixels(icon).SequenceEqual(Pixels(unknown)))throw new Exception("Generic archive icon: "+ext);
var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(10)};row.Children.Add(new Image{Source=icon,Width=24,Height=24});row.Children.Add(new TextBlock{Text="."+ext,Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center});panel.Children.Add(row);checks.Add("loaded ."+ext+(Pixels(icon).SequenceEqual(Pixels(unknown))?" (system generic)":" (type-specific)"));
}
if(Pixels(DesktopIcons.FileIcon("remote.rar",false)).SequenceEqual(Pixels(unknown)))throw new Exception("Virtual archive has no specific icon");checks.Add("remote/archive entries also have a distinct archive icon");
string app=System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,other=Path.Combine(Environment.SystemDirectory,"notepad.exe");if(File.Exists(other)&&Object.ReferenceEquals(DesktopIcons.FileIcon(app,false),DesktopIcons.FileIcon(other,false)))throw new Exception("EXE cache merges distinct apps");checks.Add("executable icon cache is per-file");
window=new Window{Title="File icon audit",Width=800,SizeToContent=SizeToContent.Height,Content=panel,Background=System.Windows.Media.Brushes.White,Left=-18000,Top=-18000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();NextStageTests.Draw(window,Path.Combine(root,"icons.png"));File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count}));return 0;
}catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),String.Join("\n",checks)+"\nFAIL: "+ex);return 1;}finally{if(window!=null)window.Close();}}
}}
