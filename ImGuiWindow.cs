using GameSDK;
using GameSDK.ModHost;
using System.Collections.Generic;

namespace reflection_inspector
{
    public static class G_Fields
    {
        public static int n_health;
        public static string n_name;
        public static UnityEngine.Vector3 n_screen_position;
        public static bool n_visible;
        public static System.Numerics.Vector2 boxTL, boxTR, boxBL, boxBR;
    }

    public static class ImGuiWindow
    {
        // ── Registration ───────────────────────────────────────────────────────
        private static int _callbackId;
        private static bool _windowOpen = true;
        private static bool _builderOpen = false;

        public static void Register(ModLogger logger)
        {
            _callbackId = ImGuiManager.RegisterCallback("reflection_inspector", Draw, ImGuiPriority.Normal);
            logger.Info($"ImGui window registered (callback #{_callbackId})");
        }

        // ── Inspector state ────────────────────────────────────────────────────
        private static string _ns = "";
        private static string _cls = "FKALGHJIADI";
        private static string _member = "CDJLLHJOCNM";
        private static string _instanceVar = "";
        private static string _lastResult = "";
        private static string _favLabel = "";

        private static readonly string[] _kindOptions =
            { "method", "prop-get", "prop-set", "field-get", "field-set" };
        private static int _kindIndex = 0;

        // Slots replace the old parallel string arrays
        private static readonly List<ArgSlot> _slots = new List<ArgSlot>();

        private static bool _secTarget = true;
        private static bool _secArgs = true;
        private static bool _secInvoke = true;
        private static bool _secFavorites = false;
        private static bool _secLog = true;

        // ── Object Builder state ───────────────────────────────────────────────
        private static string _builderTypeName = "AttackProperties";
        private static ObjectDraft _activeDraft = null;
        private static string _builderRegName = "";
        private static bool _secBuilderType = true;
        private static bool _secBuilderMembers = true;
        private static bool _secBuilderBuild = true;

        // Which slot is waiting for a draft to be injected (-1 = none)
        private static int _pendingSlotIndex = -1;

        // ── Draw ──────────────────────────────────────────────────────────────
        private static void Draw()
        {
            // ── Inspector window ──────────────────────────────────────────────
            if (_windowOpen)
            {
                ImGui.SetNextWindowSize(new System.Numerics.Vector2(500, 540), ImGuiCond.FirstUseEver);
                if (ImGui.Begin("Reflection Inspector", ref _windowOpen, ImGuiWindowFlags.None))
                {
                    DrawTarget();
                    DrawArgs();
                    DrawInvoke();
                    DrawFavorites();
                    DrawLog();
                }
                ImGui.End();
            }

            // ── Object Builder window ─────────────────────────────────────────
            if (_builderOpen)
            {
                ImGui.SetNextWindowSize(new System.Numerics.Vector2(480, 500), ImGuiCond.FirstUseEver);
                if (ImGui.Begin("Object Builder", ref _builderOpen, ImGuiWindowFlags.None))
                    DrawBuilder();
                ImGui.End();
            }

            // ESP removed for repository release (overlay/visuals excluded)
        }

        // ══════════════════════════════════════════════════════════════════════
        // INSPECTOR SECTIONS
        // ══════════════════════════════════════════════════════════════════════

        private static void DrawTarget()
        {
            _secTarget = ImGui.CollapsingHeader("Target", ImGuiTreeNodeFlags.DefaultOpen);
            if (!_secTarget) return;

            ImGui.Indent(8f);

            ImGui.Text("Namespace"); ImGui.SameLine(110f, 4f);
            ImGui.SetNextItemWidth(220f);
            ImGui.InputText("##ns", ref _ns, 128, ImGuiInputTextFlags.None);

            ImGui.Text("Class    "); ImGui.SameLine(110f, 4f);
            ImGui.SetNextItemWidth(220f);
            ImGui.InputText("##cls", ref _cls, 128, ImGuiInputTextFlags.None);

            ImGui.Text("Member   "); ImGui.SameLine(110f, 4f);
            ImGui.SetNextItemWidth(220f);
            ImGui.InputText("##member", ref _member, 128, ImGuiInputTextFlags.None);

            ImGui.Text("Kind     "); ImGui.SameLine(110f, 4f);
            ImGui.SetNextItemWidth(130f);
            if (ImGui.BeginCombo("##kind", _kindOptions[_kindIndex], ImGuiComboFlags.None))
            {
                for (int i = 0; i < _kindOptions.Length; i++)
                {
                    bool sel = i == _kindIndex;
                    if (ImGui.Selectable(_kindOptions[i], sel, ImGuiSelectableFlags.None,
                            new System.Numerics.Vector2(0f, 0f)))
                        _kindIndex = i;
                }
                ImGui.EndCombo();
            }

            ImGui.Text("Instance "); ImGui.SameLine(110f, 4f);
            ImGui.SetNextItemWidth(220f);
            ImGui.InputText("##ivar", ref _instanceVar, 128, ImGuiInputTextFlags.None);
            ImGui.SameLine(0f, 4f);
            ImGui.TextDisabled("(blank=static)");

            var regNames = new List<string>(ReflectionInvoker.RegisteredInstanceNames);
            if (regNames.Count > 0)
            {
                ImGui.Text("         "); ImGui.SameLine(110f, 4f);
                ImGui.SetNextItemWidth(220f);
                if (ImGui.BeginCombo("##ivar_pick", "pick registered...", ImGuiComboFlags.None))
                {
                    foreach (var n in regNames)
                    {
                        bool sel = _instanceVar == n;
                        if (ImGui.Selectable(n, sel, ImGuiSelectableFlags.None,
                                new System.Numerics.Vector2(0f, 0f)))
                            _instanceVar = n;
                    }
                    ImGui.EndCombo();
                }
            }

            ImGui.Unindent(8f);
            ImGui.Spacing();
        }

        private static void DrawArgs()
        {
            _secArgs = ImGui.CollapsingHeader("Arguments", ImGuiTreeNodeFlags.DefaultOpen);
            if (!_secArgs) return;

            ImGui.Indent(8f);

            if (_slots.Count == 0)
                ImGui.TextDisabled("No arguments.");

            bool removePending = false;
            int removeIdx = 0;

            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                ImGui.PushID(i);

                ImGui.Text($"[{i}]"); ImGui.SameLine(0f, 4f);

                // Kind selector
                ImGui.SetNextItemWidth(72f);
                string kindLabel = s.Kind == ArgSlotKind.Primitive ? "prim"
                                 : s.Kind == ArgSlotKind.Registry ? "reg"
                                 : "built";
                if (ImGui.BeginCombo("##sk", kindLabel, ImGuiComboFlags.None))
                {
                    if (ImGui.Selectable("prim", s.Kind == ArgSlotKind.Primitive, ImGuiSelectableFlags.None, new System.Numerics.Vector2(0f, 0f))) s.Kind = ArgSlotKind.Primitive;
                    if (ImGui.Selectable("reg", s.Kind == ArgSlotKind.Registry, ImGuiSelectableFlags.None, new System.Numerics.Vector2(0f, 0f))) s.Kind = ArgSlotKind.Registry;
                    if (ImGui.Selectable("built", s.Kind == ArgSlotKind.Built, ImGuiSelectableFlags.None, new System.Numerics.Vector2(0f, 0f))) s.Kind = ArgSlotKind.Built;
                    ImGui.EndCombo();
                }

                ImGui.SameLine(0f, 4f);

                if (s.Kind == ArgSlotKind.Primitive)
                {
                    // Type combo
                    ImGui.SetNextItemWidth(72f);
                    if (ImGui.BeginCombo("##at", s.TypeName, ImGuiComboFlags.None))
                    {
                        foreach (var t in ReflectionInvoker.SupportedTypes)
                        {
                            bool sel = s.TypeName == t;
                            if (ImGui.Selectable(t, sel, ImGuiSelectableFlags.None,
                                    new System.Numerics.Vector2(0f, 0f)))
                                s.TypeName = t;
                        }
                        ImGui.EndCombo();
                    }
                    ImGui.SameLine(0f, 4f);
                    string v = s.Value;
                    ImGui.SetNextItemWidth(160f);
                    ImGui.InputText("##av", ref v, 256, ImGuiInputTextFlags.None);
                    s.Value = v;
                }
                else if (s.Kind == ArgSlotKind.Registry)
                {
                    // Show registry key input + picker
                    string v = s.Value;
                    ImGui.SetNextItemWidth(120f);
                    ImGui.InputText("##rv", ref v, 128, ImGuiInputTextFlags.None);
                    s.Value = v;
                    ImGui.SameLine(0f, 4f);

                    var rn = new List<string>(ReflectionInvoker.RegisteredInstanceNames);
                    if (rn.Count > 0)
                    {
                        ImGui.SetNextItemWidth(100f);
                        if (ImGui.BeginCombo("##rp", "pick...", ImGuiComboFlags.None))
                        {
                            foreach (var n in rn)
                            {
                                bool sel = s.Value == n;
                                if (ImGui.Selectable(n, sel, ImGuiSelectableFlags.None,
                                        new System.Numerics.Vector2(0f, 0f)))
                                    s.Value = n;
                            }
                            ImGui.EndCombo();
                        }
                    }
                }
                else // Built
                {
                    if (s.Draft != null)
                    {
                        ImGui.TextSuccess($"draft: {s.Draft.TypeName} ({s.Draft.Members.Count} members)");
                        ImGui.SameLine(0f, 4f);
                        if (ImGui.SmallButton($"Edit##be{i}"))
                        {
                            _activeDraft = s.Draft;
                            _builderTypeName = s.Draft.TypeName;
                            _pendingSlotIndex = i;
                            _builderOpen = true;
                        }
                    }
                    else
                    {
                        ImGui.TextWarning("no draft");
                        ImGui.SameLine(0f, 4f);
                        if (ImGui.SmallButton($"New##bn{i}"))
                        {
                            _pendingSlotIndex = i;
                            _builderOpen = true;
                            _activeDraft = null;
                        }
                    }
                }

                ImGui.SameLine(0f, 4f);
                if (ImGui.SmallButton($"X##arg{i}"))
                {
                    removePending = true;
                    removeIdx = i;
                }

                ImGui.PopID();
            }

            if (removePending)
                _slots.RemoveAt(removeIdx);

            ImGui.Spacing();
            if (_slots.Count < 8)
            {
                if (ImGui.Button("+ Add argument##addarg", new System.Numerics.Vector2(120f, 0f)))
                    _slots.Add(new ArgSlot());
            }

            ImGui.SameLine(0f, 8f);
            if (ImGui.Button("Object Builder##openbuilder", new System.Numerics.Vector2(120f, 0f)))
            {
                _pendingSlotIndex = -1;
                _builderOpen = true;
            }

            ImGui.Unindent(8f);
            ImGui.Spacing();
        }

        private static void DrawInvoke()
        {
            _secInvoke = ImGui.CollapsingHeader("Invoke", ImGuiTreeNodeFlags.DefaultOpen);
            if (!_secInvoke) return;

            ImGui.Indent(8f);

            // Preview
            var parts = new List<string>();
            foreach (var s in _slots)
            {
                if (s.Kind == ArgSlotKind.Primitive) parts.Add($"({s.TypeName}){s.Value}");
                else if (s.Kind == ArgSlotKind.Registry) parts.Add($"[reg:{s.Value}]");
                else parts.Add(s.Draft != null ? $"[{s.Draft.TypeName}]" : "[null]");
            }
            string kindStr = _kindOptions[_kindIndex];
            string preview = kindStr == "method"
                ? $"{_cls}.{_member}({string.Join(", ", parts.ToArray())})"
                : $"{_cls}.{_member} [{kindStr}]";
            ImGui.TextDisabled(preview);

            ImGui.Spacing();

            if (ImGui.Button("Invoke##invoke_btn", new System.Numerics.Vector2(70f, 0f)))
                InvokeCurrentTarget();

            if (!string.IsNullOrEmpty(_lastResult))
            {
                ImGui.SameLine(0f, 8f);
                ImGui.Text("=>");
                ImGui.SameLine(0f, 4f);
                ImGui.TextSuccess(_lastResult);
            }

            ImGui.Spacing();
            ImGui.Separator();

            ImGui.Text("Save as:"); ImGui.SameLine(0f, 4f);
            ImGui.SetNextItemWidth(150f);
            ImGui.InputText("##flabel", ref _favLabel, 64, ImGuiInputTextFlags.None);
            ImGui.SameLine(0f, 4f);
            if (ImGui.Button("Save##savefav", new System.Numerics.Vector2(46f, 0f))
                && !string.IsNullOrWhiteSpace(_favLabel))
            {
                // Favorites store primitive slots only (custom objects must be rebuilt)
                var types = new List<string>();
                var vals = new List<string>();
                foreach (var s in _slots) { types.Add(s.TypeName); vals.Add(s.Value); }
                ReflectionInvoker.AddFavorite(new SavedCall
                {
                    Label = _favLabel,
                    Namespace = _ns,
                    ClassName = _cls,
                    MemberName = _member,
                    MemberKind = kindStr,
                    InstanceVar = _instanceVar,
                    ArgTypes = types.ToArray(),
                    ArgStrings = vals.ToArray()
                });
                _favLabel = "";
            }

            ImGui.Unindent(8f);
            ImGui.Spacing();
        }

        private static void DrawFavorites()
        {
            _secFavorites = ImGui.CollapsingHeader("Favorites", ImGuiTreeNodeFlags.None);
            if (!_secFavorites) return;

            ImGui.Indent(8f);
            var favs = ReflectionInvoker.Favorites;
            if (favs.Count == 0) { ImGui.TextDisabled("No saved calls yet."); ImGui.Unindent(8f); ImGui.Spacing(); return; }

            bool removePending = false; int removeIdx = 0;
            for (int i = 0; i < favs.Count; i++)
            {
                var f = favs[i];
                ImGui.PushID(i);
                ImGui.BulletText(f.Label);
                ImGui.SameLine(0f, 4f);
                ImGui.TextDisabled($"{f.ClassName}::{f.MemberName} [{f.MemberKind}]");
                ImGui.SameLine(0f, 8f);
                if (ImGui.SmallButton($"Load##fav{i}")) LoadFavorite(f);
                ImGui.SameLine(0f, 4f);
                if (ImGui.SmallButton($"Run##fav{i}")) { LoadFavorite(f); InvokeCurrentTarget(); }
                ImGui.SameLine(0f, 4f);
                if (ImGui.SmallButton($"Del##fav{i}")) { removePending = true; removeIdx = i; }
                ImGui.PopID();
            }
            if (removePending) ReflectionInvoker.RemoveFavorite(removeIdx);
            ImGui.Unindent(8f);
            ImGui.Spacing();
        }

        private static void DrawLog()
        {
            _secLog = ImGui.CollapsingHeader("Log", ImGuiTreeNodeFlags.DefaultOpen);
            if (!_secLog) return;

            ImGui.Indent(8f);
            ImGui.Text($"{ReflectionInvoker.Log.Count} entries");
            ImGui.SameLine(0f, 8f);
            if (ImGui.SmallButton("Clear##logclear")) ReflectionInvoker.ClearLog();
            ImGui.Spacing();

            ImGui.BeginChild("##log", new System.Numerics.Vector2(0f, 150f), 1, ImGuiWindowFlags.None);
            foreach (var entry in ReflectionInvoker.Log)
            {
                ImGui.TextDisabled(entry.Time); ImGui.SameLine(0f, 6f);
                switch (entry.Level)
                {
                    case ReflectionInvoker.LogEntry.Kind.Ok: ImGui.TextSuccess(entry.Message); break;
                    case ReflectionInvoker.LogEntry.Kind.Error: ImGui.TextError(entry.Message); break;
                    case ReflectionInvoker.LogEntry.Kind.Warn: ImGui.TextWarning(entry.Message); break;
                    default: ImGui.TextInfo(entry.Message); break;
                }
            }
            ImGui.EndChild();
            ImGui.Unindent(8f);
            ImGui.Spacing();
        }

        // ══════════════════════════════════════════════════════════════════════
        // OBJECT BUILDER WINDOW
        // ══════════════════════════════════════════════════════════════════════

        private static void DrawBuilder()
        {
            // ── Type picker ───────────────────────────────────────────────────
            _secBuilderType = ImGui.CollapsingHeader("Type", ImGuiTreeNodeFlags.DefaultOpen);
            if (_secBuilderType)
            {
                ImGui.Indent(8f);

                ImGui.Text("Class name"); ImGui.SameLine(100f, 4f);
                ImGui.SetNextItemWidth(200f);
                ImGui.InputText("##btype", ref _builderTypeName, 128, ImGuiInputTextFlags.None);
                ImGui.SameLine(0f, 4f);

                if (ImGui.Button("Load type##bload", new System.Numerics.Vector2(80f, 0f)))
                {
                    _activeDraft = ObjectBuilder.CreateDraft(_builderTypeName);
                    if (_activeDraft.ResolvedType != null)
                        ReflectionInvoker.AddLogPublic(ReflectionInvoker.LogEntry.Kind.Ok,
                            $"[Builder] Loaded {_builderTypeName}: {_activeDraft.Members.Count} members");
                }

                // Register-as name
                ImGui.Text("Register as"); ImGui.SameLine(100f, 4f);
                ImGui.SetNextItemWidth(200f);
                if (_activeDraft != null)
                    ImGui.InputText("##breg", ref _activeDraft.RegisterAs, 64, ImGuiInputTextFlags.None);
                else
                    ImGui.TextDisabled("(load a type first)");

                ImGui.Unindent(8f);
                ImGui.Spacing();
            }

            if (_activeDraft == null)
            {
                ImGui.TextDisabled("Load a type to start editing.");
                return;
            }

            // ── Member editor ─────────────────────────────────────────────────
            _secBuilderMembers = ImGui.CollapsingHeader(
                $"Members ({_activeDraft.Members.Count})##bm", ImGuiTreeNodeFlags.DefaultOpen);

            if (_secBuilderMembers)
            {
                ImGui.Indent(8f);

                if (_activeDraft.Members.Count == 0)
                    ImGui.TextDisabled("No editable members found.");

                ImGui.BeginChild("##bmembers", new System.Numerics.Vector2(0f, 220f), 1, ImGuiWindowFlags.None);

                for (int i = 0; i < _activeDraft.Members.Count; i++)
                {
                    var md = _activeDraft.Members[i];
                    ImGui.PushID(i);

                    // Member name (truncated)
                    string label = md.Name.Length > 18 ? md.Name.Substring(0, 16) + ".." : md.Name;
                    ImGui.Text(label); ImGui.SameLine(140f, 4f);

                    if (md.IsList)
                        DrawListMember(md, i);
                    else if (ObjectBuilder.IsPrimitive(md.TypeName))
                        DrawPrimitiveMember(md);
                    else
                        DrawObjectMember(md, i);

                    ImGui.PopID();
                }

                ImGui.EndChild();
                ImGui.Unindent(8f);
                ImGui.Spacing();
            }

            // ── Build / inject ────────────────────────────────────────────────
            _secBuilderBuild = ImGui.CollapsingHeader("Build##bb", ImGuiTreeNodeFlags.DefaultOpen);
            if (_secBuilderBuild)
            {
                ImGui.Indent(8f);

                if (ImGui.Button("Build & Register##bbuild", new System.Numerics.Vector2(130f, 0f)))
                {
                    string err;
                    var obj = ObjectBuilder.Build(_activeDraft, out err);
                    if (err != null)
                        ReflectionInvoker.AddLogPublic(ReflectionInvoker.LogEntry.Kind.Error, err);
                    else
                        ReflectionInvoker.AddLogPublic(ReflectionInvoker.LogEntry.Kind.Ok,
                            $"[Builder] Built {_activeDraft.TypeName}" +
                            (!string.IsNullOrWhiteSpace(_activeDraft.RegisterAs)
                                ? $" → registered as '{_activeDraft.RegisterAs}'"
                                : ""));
                }

                // Inject into waiting slot
                if (_pendingSlotIndex >= 0 && _pendingSlotIndex < _slots.Count)
                {
                    ImGui.SameLine(0f, 8f);
                    if (ImGui.Button($"Inject into slot [{_pendingSlotIndex}]##binject",
                            new System.Numerics.Vector2(160f, 0f)))
                    {
                        _slots[_pendingSlotIndex].Draft = _activeDraft;
                        _slots[_pendingSlotIndex].Kind = ArgSlotKind.Built;
                        _slots[_pendingSlotIndex].TypeName = _activeDraft.TypeName;
                        _pendingSlotIndex = -1;
                        _builderOpen = false;
                        ReflectionInvoker.AddLogPublic(ReflectionInvoker.LogEntry.Kind.Ok,
                            $"[Builder] Draft injected into slot");
                    }
                }
                else
                {
                    ImGui.SameLine(0f, 8f);
                    ImGui.TextDisabled("(open from a slot to inject)");
                }

                ImGui.Unindent(8f);
                ImGui.Spacing();
            }
        }

        // ── Member draw helpers ───────────────────────────────────────────────

        private static void DrawPrimitiveMember(MemberDraft md)
        {
            // Type badge
            ImGui.TextDisabled(md.TypeName); ImGui.SameLine(0f, 6f);

            // Value input
            string v = md.Value;
            ImGui.SetNextItemWidth(140f);
            ImGui.InputText("##mv", ref v, 128, ImGuiInputTextFlags.None);
            md.Value = v;
        }

        private static void DrawObjectMember(MemberDraft md, int idx)
        {
            ImGui.TextDisabled(md.TypeName); ImGui.SameLine(0f, 6f);

            // Kind toggle: registry or built
            string kindLbl = md.Kind == ArgSlotKind.Registry ? "reg" : "built";
            ImGui.SetNextItemWidth(50f);
            if (ImGui.BeginCombo($"##msk{idx}", kindLbl, ImGuiComboFlags.None))
            {
                if (ImGui.Selectable("reg", md.Kind == ArgSlotKind.Registry, ImGuiSelectableFlags.None, new System.Numerics.Vector2(0f, 0f))) md.Kind = ArgSlotKind.Registry;
                if (ImGui.Selectable("built", md.Kind == ArgSlotKind.Built, ImGuiSelectableFlags.None, new System.Numerics.Vector2(0f, 0f))) md.Kind = ArgSlotKind.Built;
                ImGui.EndCombo();
            }
            ImGui.SameLine(0f, 4f);

            if (md.Kind == ArgSlotKind.Registry)
            {
                string v = md.Value;
                ImGui.SetNextItemWidth(90f);
                ImGui.InputText($"##mrv{idx}", ref v, 64, ImGuiInputTextFlags.None);
                md.Value = v;
                ImGui.SameLine(0f, 4f);

                var rn = new List<string>(ReflectionInvoker.RegisteredInstanceNames);
                if (rn.Count > 0)
                {
                    ImGui.SetNextItemWidth(80f);
                    if (ImGui.BeginCombo($"##mrp{idx}", "...", ImGuiComboFlags.None))
                    {
                        foreach (var n in rn)
                        {
                            bool sel = md.Value == n;
                            if (ImGui.Selectable(n, sel, ImGuiSelectableFlags.None, new System.Numerics.Vector2(0f, 0f)))
                                md.Value = n;
                        }
                        ImGui.EndCombo();
                    }
                }
            }
            else
            {
                if (md.NestedDraft != null)
                    ImGui.TextSuccess($"draft({md.NestedDraft.Members.Count})");
                else
                {
                    if (ImGui.SmallButton($"New draft##mnd{idx}"))
                        md.NestedDraft = ObjectBuilder.CreateDraft(md.TypeName);
                }
            }
        }

        private static void DrawListMember(MemberDraft md, int idx)
        {
            ImGui.TextDisabled(md.TypeName); ImGui.SameLine(0f, 6f);

            // Add item button
            if (ImGui.SmallButton($"+##la{idx}"))
                md.ListValues.Add("");

            // Show items inline (up to 4, then truncate)
            int show = System.Math.Min(md.ListValues.Count, 4);
            for (int j = 0; j < show; j++)
            {
                ImGui.SameLine(0f, 4f);
                string lv = md.ListValues[j];
                ImGui.SetNextItemWidth(60f);
                ImGui.InputText($"##li{idx}_{j}", ref lv, 64, ImGuiInputTextFlags.None);
                md.ListValues[j] = lv;
                ImGui.SameLine(0f, 2f);
                if (ImGui.SmallButton($"x##lx{idx}_{j}"))
                {
                    md.ListValues.RemoveAt(j);
                    break;
                }
            }
            if (md.ListValues.Count > 4)
            {
                ImGui.SameLine(0f, 4f);
                ImGui.TextDisabled($"(+{md.ListValues.Count - 4} more)");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private static void InvokeCurrentTarget()
        {
            object instance = null;
            if (!string.IsNullOrWhiteSpace(_instanceVar))
            {
                instance = ReflectionInvoker.GetInstance(_instanceVar.Trim());
                if (instance == null)
                    ReflectionInvoker.AddLogPublic(ReflectionInvoker.LogEntry.Kind.Warn,
                        $"Instance '{_instanceVar}' not found in registry — calling as static");
            }

            _lastResult = ReflectionInvoker.InvokeSlots(
                _ns, _cls, _member,
                _kindOptions[_kindIndex],
                instance,
                _slots.ToArray()
            );
        }

        private static void LoadFavorite(SavedCall f)
        {
            _ns = f.Namespace ?? ""; _cls = f.ClassName ?? "";
            _member = f.MemberName ?? ""; _instanceVar = f.InstanceVar ?? "";
            _kindIndex = 0;
            for (int k = 0; k < _kindOptions.Length; k++)
                if (_kindOptions[k] == f.MemberKind) { _kindIndex = k; break; }

            _slots.Clear();
            if (f.ArgTypes != null)
                for (int i = 0; i < f.ArgTypes.Length; i++)
                    _slots.Add(new ArgSlot
                    {
                        Kind = ArgSlotKind.Primitive,
                        TypeName = f.ArgTypes[i],
                        Value = f.ArgStrings != null && i < f.ArgStrings.Length ? f.ArgStrings[i] : ""
                    });
        }
    }
}