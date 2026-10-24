using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace FileCompass
{
    internal sealed partial class SyncRow
    {
        internal bool Reverse;
        public event PropertyChangedEventHandler PropertyChanged;
        internal void NotifyAction()
        {
            if (PropertyChanged != null) foreach (var name in new[] { "Action", "Direction", "ChosenAction" }) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
        public string Direction { get { return Action == L.Text("state.conflict") ? "≠" : Action == L.Text("action.keep") ? "=" : Action == L.Text("action.remove") ? "×" : Reverse ? "←" : "→"; } }
        public string[] AvailableActions {
            get {
                var values = new List<string> { L.Text("sync.skipAction") };
                if (Source != null && (Target == null || Source.Directory == Target.Directory) && !(Source.Directory && Target != null)) values.Add("→");
                if (Target != null && (Source == null || Source.Directory == Target.Directory) && !(Target.Directory && Source != null)) values.Add("←");
                if (Action == L.Text("action.remove")) values.Add("×");
                if (Action == L.Text("state.conflict")) values.Add("≠");
                return values.ToArray();
            }
        }
        public string ChosenAction {
            get { return Direction == "=" ? L.Text("sync.skipAction") : Direction; }
            set {
                if (value == "≠") { Action = L.Text("state.conflict"); return; }
                if (value == "×") { Reverse = false; Action = L.Text("action.remove"); return; }
                if (value != "→" && value != "←") { Reverse = false; Action = L.Text("action.keep"); return; }
                var from = value == "←" ? Target : Source; var to = value == "←" ? Source : Target;
                if (from == null || (to != null && from.Directory != to.Directory)) throw new IOException(L.Text("sync.invalidAction"));
                Reverse = value == "←";
                Action = from.Directory ? L.Text("compare.createFolderPlan") : to == null ? L.Text("action.copy") : L.Code("action.overwrite");
            }
        }
    }
    internal static class BidirectionalSync
    {
        internal static void Configure(SyncPlan plan)
        {
            plan.Bidirectional = true; plan.Mirror = false;
            foreach (var row in plan.Rows) {
                if (row.Source == null) row.ChosenAction = "←";
                else if (row.Target == null) row.ChosenAction = "→";
                else if (row.Source.Directory != row.Target.Directory) row.Action = L.Text("state.conflict");
                else if (!row.Source.Directory && row.Action != L.Text("action.keep")) {
                    if (row.Source.Modified == row.Target.Modified) row.Action = L.Text("state.conflict");
                    else row.ChosenAction = row.Source.Modified > row.Target.Modified ? "→" : "←";
                }
            }
        }
        internal static List<OperationItem> Apply(SyncPlan plan, CopyControl control, Action<string,double> progress, List<OperationItem> result)
        {
            if (plan.Rows.Any(x => x.Action == L.Text("state.conflict"))) throw new IOException(L.Text("compare.resolveConflictsFirst"));
            if (plan.Rows.Any(x => x.Action == L.Text("action.remove"))) throw new IOException(L.Text("sync.reverseMirrorRejected"));
            // Review both endpoints before either direction can change anything.
            using (var left = (plan.SourceLocation ?? new SyncLocation(plan.SourceRoot)).Open(control.Token))
            using (var right = (plan.TargetLocation ?? new SyncLocation(plan.TargetRoot)).Open(control.Token))
                foreach (var row in plan.Rows) { control.Check(); left.Validate(row.Relative, row.Source); right.Validate(row.Relative, row.Target); }
            result = result ?? new List<OperationItem>();
            foreach (bool reverse in new[] { false, true }) {
                var rows = plan.Rows.Where(x => x.Reverse == reverse).Select(x => new SyncRow {
                    Relative = x.Relative, Difference = x.Difference, Action = x.Action,
                    Source = reverse ? x.Target : x.Source, Target = reverse ? x.Source : x.Target,
                    SourcePath = reverse ? x.TargetPath : x.SourcePath, TargetPath = reverse ? x.SourcePath : x.TargetPath
                }).ToList();
                if (rows.Count == 0) continue;
                var part = new SyncPlan { Rows = rows, Mirror = false,
                    SourceRoot = reverse ? plan.TargetRoot : plan.SourceRoot, TargetRoot = reverse ? plan.SourceRoot : plan.TargetRoot,
                    SourceLocation = reverse ? plan.TargetLocation : plan.SourceLocation, TargetLocation = reverse ? plan.SourceLocation : plan.TargetLocation };
                DirectorySync.Apply(part, control, (text, fraction) => progress(text, (reverse ? .5 : 0) + fraction * .5), result);
            }
            return result;
        }
    }
}
