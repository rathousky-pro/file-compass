using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
 public sealed class CopyPreset { public string Name;public CopyOptions Options;public override string ToString(){return Name;} }
 public sealed class CopyPresetSettings { public List<CopyPreset> Items=new List<CopyPreset>(); }
 public partial class CopySetupDialog {
  internal static void ValidatePreset(CopyOptions options,bool single){if(options==null||String.IsNullOrWhiteSpace(options.Target)||!Path.IsPathRooted(options.Target)||!Enum.IsDefined(typeof(CopyRule),options.Rule)||(!single&&options.TargetIsFile))throw new IOException(L.Text("tools1.invalidPreset"));}
  void AddPresets(StackPanel body){
   var section=new StackPanel();body.Children.Add(section);section.Children.Add(CopyUi.Label(L.Text("tools1.presets")));
   var presets=new ComboBox{IsEditable=false,Margin=new Thickness(2,3,2,6)};section.Children.Add(presets);var summary=CopyUi.Label("");section.Children.Add(summary);
   Action reload=delegate{try{var data=WorkflowStore.Load<CopyPresetSettings>("copy-presets.xml");if(data==null||data.Items==null||data.Items.Count>100||data.Items.Any(x=>x==null||String.IsNullOrWhiteSpace(x.Name)||x.Name.Length>100))throw new IOException(L.Text("tools1.invalidPreset"));foreach(var item in data.Items)ValidatePreset(item.Options,true);presets.ItemsSource=data.Items.OrderBy(x=>x.Name).ToArray();}catch(Exception ex){summary.Text=ex.Message;}};
   presets.SelectionChanged+=delegate{var preset=presets.SelectedItem as CopyPreset;summary.Text=preset==null?"":preset.Options.Target+"\n"+preset.Options.Filter+" · "+preset.Options.Rule+" · SHA-256: "+preset.Options.Verify+"\n"+L.Text(moving?"action.move":"action.copy")+" · "+L.Text("copy.preserveModifiedTime")+": "+preset.Options.Times+" · "+L.Text("copy.preserveAttributes")+": "+preset.Options.Attributes+" · "+L.Text("copy.copyEmptyFolders")+": "+preset.Options.EmptyFolders+"\n"+L.Text("copy.skipHiddenAndSystem")+": "+preset.Options.SkipHidden+" · "+L.Text("copy.copyNtfsPermissions")+": "+preset.Options.Permissions+" · "+L.Text("copy.allowReadOnlyOverwrite")+": "+preset.Options.ReadOnly;};
   var buttons=new WrapPanel();section.Children.Add(buttons);
   buttons.Children.Add(CopyUi.Button(L.Text("tools1.load"),delegate{try{var preset=presets.SelectedItem as CopyPreset;if(preset==null)return;var o=preset.Options;ValidatePreset(o,single);destination.Text=o.TargetIsFile?o.Target:o.Target.TrimEnd('\\')+"\\*.*";filter.Text=o.Filter;fileTarget.IsChecked=o.TargetIsFile;verify.IsChecked=o.Verify;permissions.IsChecked=o.Permissions;readOnly.IsChecked=o.ReadOnly;times.IsChecked=o.Times;attributes.IsChecked=o.Attributes;hidden.IsChecked=o.SkipHidden;empty.IsChecked=o.EmptyFolders;rule.SelectedIndex=(int)o.Rule;summary.Text=L.Text("tools1.presetLoaded")+"\n"+o.Target+" · "+o.Filter+" · "+o.Rule+" · SHA-256: "+o.Verify;}catch(Exception ex){summary.Text=ex.Message;}}));
   buttons.Children.Add(CopyUi.Button(L.Text("action.save"),delegate{try{var o=CreateOptions(false);ValidatePreset(o,single);string name=CopyUi.Prompt(this,L.Text("tools1.presetName"),"");if(String.IsNullOrWhiteSpace(name))return;name=name.Trim();if(name.Length>100)throw new IOException(L.Text("tools1.invalidPreset"));var old=WorkflowStore.Load<CopyPresetSettings>("copy-presets.xml").Items.FirstOrDefault(x=>String.Equals(x.Name,name,StringComparison.OrdinalIgnoreCase));if(old!=null&&MessageBox.Show(this,L.Text("tools1.replacePreset"),L.Text("tools1.presets"),MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
    WorkflowStore.Edit<CopyPresetSettings>("copy-presets.xml",data=>{data.Items.RemoveAll(x=>String.Equals(x.Name,name,StringComparison.OrdinalIgnoreCase));if(data.Items.Count>=100)throw new IOException(L.Text("tools1.presetLimit"));data.Items.Add(new CopyPreset{Name=name,Options=o});});reload();summary.Text=L.Text("tools1.saved");}catch(Exception ex){summary.Text=ex.Message;}}));
   buttons.Children.Add(CopyUi.Button(L.Text("action.delete"),delegate{try{var selected=presets.SelectedItem as CopyPreset;if(selected==null)return;if(MessageBox.Show(this,selected.Name,L.Text("action.delete"),MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;WorkflowStore.Edit<CopyPresetSettings>("copy-presets.xml",data=>data.Items.RemoveAll(x=>x.Name==selected.Name));reload();}catch(Exception ex){summary.Text=ex.Message;}}));reload();
  }
 }
}
