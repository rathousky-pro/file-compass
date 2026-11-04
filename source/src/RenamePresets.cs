using System;using System.IO;using System.Xml;using System.Xml.Serialization;using System.Windows;using System.Windows.Controls;
namespace FileCompass {
 internal static class RenamePresets {
  internal static void Save(string path,RenameOptions options){using(var writer=XmlWriter.Create(path,new XmlWriterSettings{Indent=true}))new XmlSerializer(typeof(RenameOptions)).Serialize(writer,options);}
  internal static RenameOptions Load(string path){using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536})){var options=(RenameOptions)new XmlSerializer(typeof(RenameOptions)).Deserialize(reader);if(options.Name==null||options.Extension==null||options.Find==null||options.Replace==null||options.Start<0||options.Step<1||options.Digits<1||options.Digits>12||options.LetterCase<0||options.LetterCase>3)throw new IOException(L.Text("rename.invalidCounterSettings"));return options;}}
 }
 internal static class RenameRangePicker {
  internal static void Insert(Window owner,TextBox target,string kind){
   var dialog=new Window();CopyUi.Style(dialog,owner,L.Text("rename.partRange"),420);StackPanel actions;var root=SettingsUx.Frame(dialog,L.Text("rename.partRange"),out actions);var body=new StackPanel{Margin=new Thickness(24)};root.Children.Add(body);
   body.Children.Add(new TextBlock{Text=L.Text("rename.rangeHelp"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,16)});
   var from=new TextBox{Text="1"};var to=new TextBox{Text="5"};body.Children.Add(CopyUi.Label(L.Text("rename.rangeStart")));body.Children.Add(new NumberSpinner(from,1,10000));body.Children.Add(CopyUi.Label(L.Text("rename.rangeEnd")));body.Children.Add(new NumberSpinner(to,1,10000));
   var insert=CopyUi.Button(L.Text("rename.insertToken"),delegate{int start,end;if(!Int32.TryParse(from.Text,out start)||!Int32.TryParse(to.Text,out end)||start<1||end<start){to.Focus();return;}string token="["+kind+start+"-"+end+"]";target.SelectedText=token;target.CaretIndex=target.SelectionStart+token.Length;dialog.DialogResult=true;});insert.IsDefault=true;actions.Children.Add(insert);var cancel=CopyUi.Button(L.Text("action.cancel"),()=>dialog.Close());cancel.IsCancel=true;actions.Children.Add(cancel);dialog.ShowDialog();target.Focus();
  }
 }
}
