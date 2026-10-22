using System;
using System.Linq;
using System.Windows.Controls;
namespace CommanderCZ {
    public partial class MainWindow {
        void OrganizeMainMenus(MenuItem file, MenuItem selection, MenuItem edit, MenuItem commands, MenuItem network, MenuItem display) {
            GroupMenu(file, "menuGroup.archives", "action.compress", "action.extract", "action.testArchive");
            GroupMenu(file, "menuGroup.integrity", "action.compareFiles", "action.createChecksums", "action.verifyChecksums");
            GroupMenu(file, "menuGroup.properties", "action.properties", "action.openWith", "action.changeAttributes", "tags.edit");
            GroupMenu(file, "menuGroup.rename", "action.rename", "action.batchRename");
            GroupMenu(file, "menuGroup.fileTools", "imageTools.title", "parts.split", "parts.combine");
            GroupMenu(selection, "menuGroup.masks", "selection.markByMask", "selection.unmarkByMask", "selection.markSameExtension");
            GroupMenu(selection, "menuGroup.rememberSelection", "selection.saveMarks", "selection.restoreMarks");
            GroupMenu(edit, "menuGroup.copyDetails", "clipboard.copyNames", "clipboard.copyFullPaths", "clipboardFile.title");
            GroupMenu(commands, "menuGroup.navigation", "action.refresh", "action.parentFolder", "panel.openSameFolderInOtherPanel");
            GroupMenu(commands, "menuGroup.tabs", "panel.swap", "tabs.new", "tabs.close", "tabs.toggleLock");
            GroupMenu(commands, "menuGroup.search", "action.findFiles", "duplicates.title", "everything.title", "tags.results");
            GroupMenu(commands, "menuGroup.workspace", "favorites.openMenu", "workspace.title", "projection.addToBasket", "projection.basket", "projection.removeFromBasket");
            GroupMenu(commands, "menuGroup.operations", "queue.copyTitle", "queue.restoreSaved", "queue.saveAndExit", "queue.networkCopyTitle");
            GroupMenu(commands, "menuGroup.compare", "action.compareFiles", "action.compareFolders", "action.synchronizeFolders");
            GroupMenu(commands, "menuGroup.external", "action.openExplorer", "action.openPowerShellHere");
            var duplicate = commands.Items.OfType<MenuItem>().FirstOrDefault(x => Convert.ToString(x.Header) == L.Text("action.batchRename")); if (duplicate != null) commands.Items.Remove(duplicate);
            GroupMenu(network, "menuGroup.networkDrives", "network.mapDriveMenu", "network.browseDevices");
            GroupMenu(network, "menuGroup.networkSession", "network.openActiveSshTerminal", "network.disconnectActivePanel");
            GroupMenu(display, "menuGroup.views", "panel.detailsView", "panel.shortView", "action.preview", "panel.folderTree");
            GroupMenu(display, "menuGroup.filters", "panel.quickSearchFilter", "tags.filter", "panel.allFiles");
            GroupMenu(display, "menuGroup.listings", "projection.flat", "projection.normal", "projection.cancelLoading");
            OrderFileMenu(file);
            foreach (var menu in new[] { file, selection, edit, commands, network, display }) CleanMenu(menu);
        }
        static void OrderFileMenu(MenuItem file) {
            var remaining = file.Items.OfType<MenuItem>().ToList(); file.Items.Clear();
            foreach (string key in new[] { "menu.view", "action.edit", "action.copy", "action.move", "action.newFolder", "action.delete", "", "menuGroup.rename", "menuGroup.properties", "menuGroup.archives", "menuGroup.integrity", "menuGroup.fileTools", "", "action.exit" }) {
                if (key.Length == 0) { file.Items.Add(new Separator()); continue; }
                var item = remaining.FirstOrDefault(x => Convert.ToString(x.Header) == L.Text(key)); if (item == null) continue;
                if (key == "action.exit") { foreach (var extra in remaining.Where(x => x != item).ToArray()) { file.Items.Add(extra); remaining.Remove(extra); } }
                remaining.Remove(item); file.Items.Add(item);
            }
            foreach (var item in remaining) file.Items.Add(item);
        }
        static void GroupMenu(MenuItem root, string key, params string[] children) {
            var titles = children.Select(L.Text).ToArray();
            var items = root.Items.OfType<MenuItem>().Where(x => titles.Contains(Convert.ToString(x.Header))).ToArray();
            if (items.Length == 0) return;
            int index = root.Items.IndexOf(items[0]); var group = new MenuItem { Header = L.Text(key) };
            foreach (var item in items) { root.Items.Remove(item); group.Items.Add(item); }
            root.Items.Insert(index, group);
        }
        static void CleanMenu(MenuItem menu) {
            bool separator = true;
            foreach (var item in menu.Items.Cast<object>().ToArray()) {
                if (item is Separator) { if (separator) menu.Items.Remove(item); separator = true; }
                else { separator = false; var child = item as MenuItem; if (child != null) CleanMenu(child); }
            }
            if (menu.Items.Count > 0 && menu.Items[menu.Items.Count - 1] is Separator) menu.Items.RemoveAt(menu.Items.Count - 1);
        }
    }
}
