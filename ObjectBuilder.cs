using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace reflection_inspector
{
    // ── What kind of value an ArgSlot holds ───────────────────────────────────
    public enum ArgSlotKind
    {
        Primitive,   // string, int, float, bool, etc.
        Registry,    // reference to a named instance in ReflectionInvoker registry
        Built,       // object assembled by ObjectBuilder
    }

    // ── One argument slot in the Inspector ────────────────────────────────────
    public class ArgSlot
    {
        public ArgSlotKind Kind = ArgSlotKind.Primitive;
        public string TypeName = "string";   // for Primitive: "int","float"... for Built/Registry: class name
        public string Value = "";          // Primitive value OR Registry key name
        public ObjectDraft Draft = null;        // non-null when Kind == Built
    }

    // ── A draft object being assembled ────────────────────────────────────────
    public class ObjectDraft
    {
        public string TypeName = "";   // short class name, e.g. "AttackProperties"
        public Type ResolvedType = null;

        // Each member that can be set
        public List<MemberDraft> Members = new List<MemberDraft>();

        // Optional: name to register in the instance registry after building
        public string RegisterAs = "";
    }

    public class MemberDraft
    {
        public string Name;
        public string TypeName;    // CLR type name for display
        public MemberInfo Info;        // FieldInfo or PropertyInfo
        public ArgSlotKind Kind = ArgSlotKind.Primitive;
        public string Value = "";
        public ObjectDraft NestedDraft = null;  // for future nesting

        // For List<T> members
        public bool IsList = false;
        public string ListItemType = "";
        public List<string> ListValues = new List<string>();
    }

    // ── Static ObjectBuilder ──────────────────────────────────────────────────
    public static class ObjectBuilder
    {
        private const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // ── Create a blank draft for a given type name ─────────────────────────
        public static ObjectDraft CreateDraft(string typeName)
        {
            var draft = new ObjectDraft { TypeName = typeName };
            draft.ResolvedType = ReflectionInvoker.ResolveType("", typeName);
            if (draft.ResolvedType == null)
            {
                ReflectionInvoker.AddLogPublic(ReflectionInvoker.LogEntry.Kind.Warn,
                    $"[Builder] Type '{typeName}' not found — draft will be empty");
                return draft;
            }
            PopulateMembers(draft);
            return draft;
        }

        // ── Populate MemberDraft list from the resolved type ──────────────────
        public static void PopulateMembers(ObjectDraft draft)
        {
            draft.Members.Clear();
            if (draft.ResolvedType == null) return;

            // Fields
            foreach (var f in draft.ResolvedType.GetFields(MemberFlags))
            {
                if (f.IsLiteral || f.IsInitOnly) continue; // skip const/readonly
                draft.Members.Add(MakeMemberDraft(f.Name, f.FieldType, f));
            }

            // Writable properties
            foreach (var p in draft.ResolvedType.GetProperties(MemberFlags))
            {
                if (!p.CanWrite) continue;
                draft.Members.Add(MakeMemberDraft(p.Name, p.PropertyType, p));
            }
        }

        private static MemberDraft MakeMemberDraft(string name, Type type, MemberInfo info)
        {
            var md = new MemberDraft
            {
                Name = name,
                Info = info,
                Kind = ArgSlotKind.Primitive,
                Value = "",
            };

            // Check if it's a List<T>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                md.IsList = true;
                md.TypeName = $"List<{type.GetGenericArguments()[0].Name}>";
                md.ListItemType = type.GetGenericArguments()[0].Name;
            }
            else
            {
                md.TypeName = FriendlyName(type);
            }

            return md;
        }

        // ── Build the actual object from the draft ────────────────────────────
        public static object Build(ObjectDraft draft, out string error)
        {
            error = null;
            if (draft.ResolvedType == null)
            {
                error = $"[Builder] Type '{draft.TypeName}' not resolved";
                return null;
            }

            object instance;
            try
            {
                instance = Activator.CreateInstance(draft.ResolvedType);
            }
            catch (Exception e)
            {
                error = $"[Builder] Cannot instantiate '{draft.TypeName}': {e.Message}";
                return null;
            }

            foreach (var md in draft.Members)
            {
                if (string.IsNullOrEmpty(md.Value) && md.ListValues.Count == 0
                    && md.Kind != ArgSlotKind.Built && md.Kind != ArgSlotKind.Registry)
                    continue; // skip untouched fields

                try
                {
                    object val = ResolveValue(md, out string memberErr);
                    if (memberErr != null)
                    {
                        ReflectionInvoker.AddLogPublic(ReflectionInvoker.LogEntry.Kind.Warn,
                            $"[Builder] {draft.TypeName}.{md.Name}: {memberErr} — skipped");
                        continue;
                    }
                    SetMember(instance, md.Info, val);
                }
                catch (Exception e)
                {
                    ReflectionInvoker.AddLogPublic(ReflectionInvoker.LogEntry.Kind.Warn,
                        $"[Builder] {draft.TypeName}.{md.Name}: {e.Message} — skipped");
                }
            }

            // Register if requested
            if (!string.IsNullOrWhiteSpace(draft.RegisterAs))
                ReflectionInvoker.RegisterInstance(draft.RegisterAs, instance);

            return instance;
        }

        // ── Resolve a single MemberDraft value to a CLR object ────────────────
        private static object ResolveValue(MemberDraft md, out string error)
        {
            error = null;

            if (md.Kind == ArgSlotKind.Registry)
            {
                var obj = ReflectionInvoker.GetInstance(md.Value);
                if (obj == null) error = $"Registry key '{md.Value}' not found";
                return obj;
            }

            if (md.Kind == ArgSlotKind.Built && md.NestedDraft != null)
                return Build(md.NestedDraft, out error);

            if (md.IsList)
                return BuildList(md, out error);

            // Primitive
            return ReflectionInvoker.ParseArg(FriendlyNameToKey(md.TypeName), md.Value, out error);
        }

        private static object BuildList(MemberDraft md, out string error)
        {
            error = null;
            // Resolve List<T> item type
            Type itemType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                itemType = asm.GetType(md.ListItemType);
                if (itemType != null) break;
            }
            if (itemType == null)
            {
                // Try primitive fallback
                itemType = ResolvePrimitiveType(md.ListItemType);
            }
            if (itemType == null)
            {
                error = $"Cannot resolve list item type '{md.ListItemType}'";
                return null;
            }

            var listType = typeof(List<>).MakeGenericType(itemType);
            var list = (IList)Activator.CreateInstance(listType);

            foreach (var v in md.ListValues)
            {
                string parseErr;
                var item = ReflectionInvoker.ParseArg(md.ListItemType, v, out parseErr);
                if (parseErr != null)
                {
                    ReflectionInvoker.AddLogPublic(ReflectionInvoker.LogEntry.Kind.Warn,
                        $"[Builder] List item '{v}': {parseErr} — skipped");
                    continue;
                }
                list.Add(item);
            }
            return list;
        }

        private static void SetMember(object instance, MemberInfo info, object value)
        {
            if (info is FieldInfo fi) fi.SetValue(instance, value);
            else if (info is PropertyInfo pi) pi.SetValue(instance, value);
        }

        // ── Type name helpers ─────────────────────────────────────────────────
        public static string FriendlyName(Type t)
        {
            if (t == typeof(int)) return "int";
            if (t == typeof(uint)) return "uint";
            if (t == typeof(float)) return "float";
            if (t == typeof(double)) return "double";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(string)) return "string";
            if (t == typeof(long)) return "long";
            if (t == typeof(short)) return "short";
            if (t == typeof(byte)) return "byte";
            return t.Name;
        }

        private static string FriendlyNameToKey(string name)
        {
            // Already in key form for primitives, passthrough
            return name;
        }

        private static Type ResolvePrimitiveType(string name)
        {
            switch (name)
            {
                case "int": return typeof(int);
                case "uint": return typeof(uint);
                case "float": return typeof(float);
                case "double": return typeof(double);
                case "bool": return typeof(bool);
                case "string": return typeof(string);
                case "long": return typeof(long);
                case "short": return typeof(short);
                case "byte": return typeof(byte);
                default: return null;
            }
        }

        public static bool IsPrimitive(string typeName)
        {
            switch (typeName)
            {
                case "int":
                case "uint":
                case "float":
                case "double":
                case "bool":
                case "string":
                case "long":
                case "short":
                case "byte":
                    return true;
                default: return false;
            }
        }
    }
}