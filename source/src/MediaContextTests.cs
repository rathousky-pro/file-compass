#if APP_TESTS
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Windows.Threading;
namespace FileCompass {
 internal static class MediaContextTests {
  internal static int Run(string root){Directory.CreateDirectory(root);var checks=new List<string>();MainWindow main=null;MediaConverterWindow converter=null;
   Action<bool,string> check=(ok,text)=>{if(!ok)throw new Exception(text);checks.Add(text);};
   try{
    System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));L.ChangeLanguage("cs-CZ");FluentTheme.Ensure();
    string data=Directory.CreateDirectory(Path.Combine(root,"files")).FullName;string png=Path.Combine(data,"picture.png"),wav=Path.Combine(data,"sound.wav");
    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,2,96,96,PixelFormats.Bgr24,null,new byte[12],6)));using(var file=File.Create(png))encoder.Save(file);
    using(var writer=new BinaryWriter(File.Create(wav))){writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(16036);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(8000);writer.Write(16000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(16000);writer.Write(new byte[16000]);}
    File.WriteAllText(Path.Combine(data,"notes.txt"),"plain text");Directory.CreateDirectory(Path.Combine(data,"folder"));
    main=new MainWindow(data,new ProgramSettings{AutoRefresh=false}){Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);
    var panel=FluentUiTests.Children<FilePanel>(main).First();var menu=panel.List.ContextMenu;var action=menu.Items.OfType<MenuItem>().Single(m=>Convert.ToString(m.Tag)=="media.contextConvert");
    Func<string,Entry> entry=name=>panel.List.Items.Cast<Entry>().Single(e=>Path.GetFileName(e.FullPath)==name);
    Action<string> select=name=>{panel.MarkAll(false);panel.SelectContextEntry(entry(name));menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));};
    select("notes.txt");check(action.Visibility==Visibility.Collapsed,"ordinary text files do not show Convert");select("folder");check(action.Visibility==Visibility.Collapsed,"folders do not show Convert");
    select("picture.png");check(action.IsEnabled&&action.Visibility==Visibility.Visible&&action.Icon!=null,"image context offers Convert with an icon");action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));converter=Application.Current.Windows.OfType<MediaConverterWindow>().Single();NavigationTests.Wait(converter.AnalysisTask);
    check(converter.Mode.SelectedIndex==0&&converter.Rows.Count==1&&converter.Rows[0].Path==png,"image context opens the image converter with the selected file");converter.ImageFormat.SelectedItem="JPEG";check(converter.ImageOptions().Format=="JPEG"&&converter.ImageFormat.Items.Count>=6,"target image format can be selected immediately");check(Directory.GetFiles(data).Length==3,"opening conversion does not create outputs or start a batch");NextStageTests.Draw(converter,Path.Combine(root,"image-format.png"));converter.Close();converter=null;
    select("sound.wav");check(action.IsEnabled&&action.Visibility==Visibility.Visible,"audio context offers Convert");action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));converter=Application.Current.Windows.OfType<MediaConverterWindow>().Single();NavigationTests.Wait(converter.AnalysisTask);
    check(converter.Mode.SelectedIndex==1&&converter.Rows.Count==1&&converter.Rows[0].Path==wav,"audio context opens the audio converter with the selected file");converter.Profile.SelectedItem=MediaCatalog.Profile("flac");check(converter.AudioOptions().Profile=="flac","target audio format can be selected");NextStageTests.Draw(converter,Path.Combine(root,"audio-format.png"));converter.Close();converter=null;
    panel.MarkAll(false);panel.ToggleMark(entry("picture.png"));panel.ToggleMark(entry("sound.wav"));panel.SelectContextEntry(entry("sound.wav"));menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));check(action.IsEnabled,"mixed marked image/audio selection remains convertible");action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));converter=Application.Current.Windows.OfType<MediaConverterWindow>().Single();NavigationTests.Wait(converter.AnalysisTask);check(converter.Rows.Count==2&&converter.Mode.SelectedIndex==1,"mixed selection preserves both files and follows the context file's mode");converter.Close();converter=null;
    panel.ToggleMark(entry("notes.txt"));menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));check(action.Visibility==Visibility.Collapsed,"selection with unsupported files does not offer conversion");
    main.Close();main=null;File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
   }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(converter!=null)converter.Close();if(main!=null)main.Close();}
  }
 }
}
#endif
