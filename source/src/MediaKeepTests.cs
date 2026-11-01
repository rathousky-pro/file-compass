#if APP_TESTS
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace FileCompass {
 internal static class MediaKeepTests {
  internal static int Run(string root){Directory.CreateDirectory(root);var log=new List<string>();MediaConverterWindow window=null;MainWindow main=null;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);log.Add(label);File.AppendAllText(Path.Combine(root,"progress.txt"),label+Environment.NewLine);};try{
   SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));FluentTheme.Ensure();L.ChangeLanguage("cs");AppearanceTheme.SetMotif("Clean");AppearanceTheme.Apply("Light");
   string png=Path.Combine(root,"source.png"),jpg=Path.Combine(root,"source.jpeg"),wav=Path.Combine(root,"source.wav"),mp3=Path.Combine(root,"source.mp3");var pixels=new byte[40*20*4];for(int i=3;i<pixels.Length;i+=4)pixels[i]=255;var bitmap=BitmapSource.Create(40,20,96,96,PixelFormats.Bgra32,null,pixels,160);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(png))encoder.Save(file);
   MediaImages.Call(png,jpg,new MediaImageOptions{Format="JPEG"},false,CancellationToken.None);using(var writer=new BinaryWriter(File.Create(wav))){int count=48000;writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(48000);writer.Write(96000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);for(int i=0;i<count;i++)writer.Write((short)(Math.Sin(i*Math.PI*440/24000)*4000));}
   MediaAudio.Convert(wav,mp3,new MediaAudioOptions{Profile="mp3",Channels=0,SampleRate=0},CancellationToken.None,null);
   MediaOrientationTests.Verify(root,png,check);
   var sources=new[]{png,jpg,wav,mp3};var hashes=sources.ToDictionary(x=>x,x=>FileParts.Hash(x,CancellationToken.None));
   foreach(string source in new[]{png,jpg}){string target=Path.Combine(root,"keep-"+Path.GetFileName(source));var info=MediaImages.Call(source,target,new MediaImageOptions{KeepFormat=true,Resize=4,Width=12,Rotation=90},false,CancellationToken.None);check(info.Width==12&&info.Height==24&&info.Format.IndexOf(source==png?"png":"jpeg",StringComparison.OrdinalIgnoreCase)>=0,"keep-format image resize and rotation preserve encoder: "+Path.GetExtension(source));}
   foreach(string source in new[]{wav,mp3}){string target=Path.Combine(root,"keep-"+Path.GetFileName(source));var input=MediaProcess.Probe(source,CancellationToken.None);var options=new MediaAudioOptions{KeepFormat=true,Channels=0,SampleRate=0,Speed=1.25};MediaAudio.Convert(source,target,options,CancellationToken.None,null);var output=MediaProcess.Probe(target,CancellationToken.None);check(output.Audio[0].Codec==input.Audio[0].Codec&&Math.Abs(output.Duration-input.Duration/1.25)<.2,"keep-format audio speed edit preserves codec and container: "+Path.GetExtension(source));}
   var pass=MediaKeepFormat.Audio(mp3,MediaProcess.Probe(mp3,CancellationToken.None),new MediaAudioOptions{KeepFormat=true,Channels=0,SampleRate=0});check(pass.Copy,"unchanged keep-format audio uses stream copy rather than a lossy re-encode");
   window=new MediaConverterWindow(null,sources,root,true,delegate{},delegate{}){Width=1180,Height=820,Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();NavigationTests.Wait(window.AnalysisTask);CleanOptions.Reveal(window);window.ImageFormat.SelectedIndex=0;window.SettingsTabs.SelectedItem=window.GeometryTab;
   foreach(int mode in new[]{1,2,3,4,5}){window.resize.SelectedIndex=mode;window.UpdateLayout();var controls=new Control[]{window.resize,window.width,window.height};check(controls.All(x=>x.IsVisible)&&controls.All(x=>Math.Abs(x.TranslatePoint(new Point(),window).Y-window.resize.TranslatePoint(new Point(),window).Y)<1),"resize selector and parameters remain on one row: "+mode);}
   window.rotation.SelectedIndex=4;check(window.ImageOptions().Orientation==1&&window.ImageOptions().Rotation==0,"landscape choice maps to automatic orientation");window.rotation.SelectedIndex=5;check(window.ImageOptions().Orientation==2&&window.ImageOptions().Rotation==0,"portrait choice maps to automatic orientation");window.rotation.SelectedIndex=1;check(window.ImageOptions().Orientation==0&&window.ImageOptions().Rotation==90,"explicit rotation remains independent of automatic orientation");window.rotation.SelectedIndex=0;
   window.resize.SelectedIndex=4;window.width.Text="16";window.height.Text="ignored";check(window.ImageOptions().Width==16&&!window.height.IsEnabled,"width-only conversion ignores the derived height");window.resize.SelectedIndex=5;window.height.Text="18";check(window.ImageOptions().Height==18&&!window.width.IsEnabled,"height-only conversion uses its own height field");window.resize.SelectedIndex=0;window.UpdateLayout();NextStageTests.Draw(window,Path.Combine(root,"geometry-tab.png"));
   NavigationTests.Wait(window.ConvertRows());check(window.Rows.Where(x=>x.Image).All(x=>!x.Included&&File.Exists(x.Target)&&Path.GetExtension(x.Target)==Path.GetExtension(x.Path)),"mixed PNG/JPEG batch keeps each original extension");
   window.Mode.SelectedIndex=1;window.Profile.SelectedItem=MediaCatalog.KeepProfile;window.UpdateLayout();check(window.AudioOptions().KeepFormat&&window.AudioOptions().Channels==0&&window.AudioOptions().SampleRate==0,"keep audio defaults retain original channels and sample rate");NavigationTests.Wait(window.ConvertRows());check(window.Rows.Where(x=>!x.Image).All(x=>!x.Included&&File.Exists(x.Target)&&Path.GetExtension(x.Target)==Path.GetExtension(x.Path)),"mixed WAV/MP3 batch keeps each original extension");window.Close();window=null;
   check(sources.All(x=>FileParts.Hash(x,CancellationToken.None)==hashes[x]),"all keep-format edits preserve the original source bytes");
   main=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){Left=-18000,Top=-18000,ShowActivated=false};main.Show();NavigationTests.WaitPanels(main);check(!FluentUiTests.Children<MenuItem>(main).Any(x=>Convert.ToString(x.Header)==L.Text("imageTools.title")),"standalone quick-image menu is removed");
   File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"PASS: "+log.Count+" checks"}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(window!=null)window.Close();if(main!=null)main.Close();}}
 }
}
#endif
