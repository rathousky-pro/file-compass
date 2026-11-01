#if APP_TESTS
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace FileCompass {
 internal static class SelectionBracketTests {
  internal static void Pump(Window window){NavigationTests.Wait(Task.Delay(100));window.UpdateLayout();}
  internal static int Run(string root){Directory.CreateDirectory(root);var checks=new List<string>();MainWindow main=null;string motif=AppearanceTheme.Motif;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add(label);};try{
   SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));FluentTheme.Ensure();L.ChangeLanguage("cs-CZ");AppearanceTheme.Apply("Light");var folder=Directory.CreateDirectory(Path.Combine(root,"files")).FullName;Directory.CreateDirectory(Path.Combine(folder,"0-folder"));for(int i=1;i<=9;i++)File.WriteAllText(Path.Combine(folder,"item"+i.ToString("D2")+".txt"),"fixture");
   main=new MainWindow(folder,new ProgramSettings{AutoRefresh=false}){Width=1320,Height=820,Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);var panel=FluentUiTests.Children<FilePanel>(main).First();foreach(string style in new[]{"Classic","Clean"}){AppearanceTheme.SetMotif(style);panel.Navigate(folder,false);NavigationTests.Wait(panel.NavigationTask);panel.VerifySelectionBrackets(main,root,style,check);}
   File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(main!=null)main.Close();AppearanceTheme.SetMotif(motif);}}
 }
 public partial class FilePanel {
  Border BracketFrame(Entry entry){var row=(ListViewItem)List.ItemContainerGenerator.ContainerFromItem(entry);return row==null?null:(Border)row.Template.FindName("RowFrame",row);}
  internal void VerifySelectionBrackets(Window window,string root,string style,Action<bool,string> check){
   var rows=List.Items.Cast<Entry>().Where(x=>!x.IsParent).ToArray();MarkAll(false);foreach(int index in new[]{0,1,2,4,6,7,8})ToggleMark(rows[index]);List.SelectedItem=rows[8];SelectionBracketTests.Pump(window);
   check(new[]{0,1,2,4,6,7,8}.All(i=>BracketFrame(rows[i]).BorderBrush.ToString()==FluentTheme.Accent.ToString()),style+": every marked file and folder has a blue bracket, including rows without the cursor");
   check(BracketFrame(rows[0]).CornerRadius.TopLeft>0&&BracketFrame(rows[0]).CornerRadius.BottomLeft==0&&BracketFrame(rows[1]).CornerRadius.TopLeft==0&&BracketFrame(rows[1]).CornerRadius.BottomLeft==0&&BracketFrame(rows[2]).CornerRadius.TopLeft==0&&BracketFrame(rows[2]).CornerRadius.BottomLeft>0,style+": adjacent marked rows have only one start and one end cap");
   check(BracketFrame(rows[4]).CornerRadius.TopLeft>0&&BracketFrame(rows[4]).CornerRadius.BottomLeft>0,style+": an isolated marked file retains both bracket caps");
   check(Math.Abs(BracketFrame(rows[0]).TranslatePoint(new Point(0,BracketFrame(rows[0]).ActualHeight),window).Y-BracketFrame(rows[1]).TranslatePoint(new Point(),window).Y)<0.01,style+": adjacent bracket segments join without a vertical gap");NextStageTests.Draw(window,Path.Combine(root,style+"-selection-groups.png"));
   ToggleMark(rows[1]);SelectionBracketTests.Pump(window);check(BracketFrame(rows[0]).CornerRadius.BottomLeft>0&&BracketFrame(rows[2]).CornerRadius.TopLeft>0,style+": removing a middle row splits the bracket into separate groups");ToggleMark(rows[0]);SelectionBracketTests.Pump(window);check(BracketFrame(rows[0]).BorderBrush.ToString()!=FluentTheme.Accent.ToString(),style+": clearing a mark removes its non-cursor bracket");
   MarkAll(true);SetQuickFilter("item01");SelectionBracketTests.Pump(window);check(BracketFrame(List.Items.Cast<Entry>().Single(e=>!e.IsParent)).CornerRadius.TopLeft>0&&BracketFrame(List.Items.Cast<Entry>().Single(e=>!e.IsParent)).CornerRadius.BottomLeft>0,style+": filtering recomputes bracket boundaries from visible rows");SetQuickFilter("");MarkAll(false);
   var mock=new[]{new Entry{Name="A",FullPath="A",Bytes=1,IsMarked=true},new Entry{Name="B",FullPath="B",Bytes=3},new Entry{Name="C",FullPath="C",Bytes=2,IsMarked=true}};List.ItemsSource=mock;sortProperty="Name";descending=false;Sort();SelectionBracketTests.Pump(window);check(BracketFrame(mock[0]).CornerRadius.BottomLeft>0,style+": unmarked rows break a group in name order");sortProperty="Size";Sort();SelectionBracketTests.Pump(window);check(BracketFrame(mock[0]).CornerRadius.BottomLeft==0&&BracketFrame(mock[2]).CornerRadius.TopLeft==0,style+": sorting joins newly adjacent marked rows");
   var many=Enumerable.Range(0,400).Select(i=>new Entry{Name="virtual"+i.ToString("D4"),FullPath=Path.Combine(root,"virtual"+i),Bytes=1,IsMarked=true}).ToArray();List.ItemsSource=many;sortProperty="Name";Sort();List.SelectedItem=many[210];List.ScrollIntoView(many[210]);SelectionBracketTests.Pump(window);var middle=BracketFrame(many[210]);check(middle!=null&&middle.CornerRadius.TopLeft==0&&middle.CornerRadius.BottomLeft==0,style+": a bracket continuing outside the viewport has no false caps");List.ScrollIntoView(many[399]);SelectionBracketTests.Pump(window);check(BracketFrame(many[399]).CornerRadius.BottomLeft>0,style+": virtualized scrolling renders the actual end of the selection");
   ApplySelectionMode(true);List.SelectedItems.Clear();List.SelectedItems.Add(many[398]);List.SelectedItems.Add(many[399]);SelectionBracketTests.Pump(window);check(many[398].IsMarked&&many[399].IsMarked&&BracketFrame(many[398]).CornerRadius.BottomLeft==0&&BracketFrame(many[399]).CornerRadius.TopLeft==0,style+": Windows multi-selection uses the same contiguous bracket");ApplySelectionMode(false);sortProperty="Name";
  }
 }
}
#endif
