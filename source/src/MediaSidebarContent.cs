using System.Collections;
using System.Collections.Generic;
using System.Windows.Controls;
namespace FileCompass {
 internal sealed partial class MediaConverterWindow {
  DockPanel sidebarSharedBody;
  void RegisterSidebarBody(DockPanel body){sidebarSharedBody=body;AddLogicalChild(body);}
  protected override IEnumerator LogicalChildren {get{var children=new List<object>();var existing=base.LogicalChildren;while(existing.MoveNext())children.Add(existing.Current);if(sidebarSharedBody!=null)children.Add(sidebarSharedBody);return children.GetEnumerator();}}
 }
}
