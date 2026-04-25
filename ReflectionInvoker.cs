using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace reflection_inspector
{
    /// <summary>
    /// Represents a saved/favorite call entry.
    /// </summary>
    public class SavedCall
    {
        public string Label;
        public string Namespace;
        public string ClassName;
        public string MemberName;
        public string MemberKind; // "method" | "field-get" | "field-set" | "prop-get" | "prop-set"
        public string InstanceVar; // blank = static
        public string[] ArgStrings;
        public string[] ArgTypes;
    }

    /// <summary>
    /// Handles reflection-based method/field/property invocation with string argument parsing.
    /// </summary>
    public static class ReflectionInvoker
    {
        // ── Supported primitive arg types ──────────────────────────────────────
        public static readonly string[] SupportedTypes =
        {
            "string", "int", "float", "double", "bool", "long", "uint", "byte", "short"
        };

        // ── Instance registry ─────────────────────────────────────────────────
        // Register named instances from Mod.cs so the Inspector can resolve them by name.
        // Usage: ReflectionInvoker.RegisterInstance("playerClass", playerClass);
        private static readonly System.Collections.Generic.Dictionary<string, object> s_instances
            = new System.Collections.Generic.Dictionary<string, object>();

        public static void RegisterInstance(string name, object instance)
        {
            if (instance == null)
                s_instances.Remove(name);
            else
                s_instances[name] = instance;
        }

        public static void UnregisterInstance(string name) => s_instances.Remove(name);

        public static object GetInstance(string name)
        {
            s_instances.TryGetValue(name, out var obj);
            return obj;
        }

        public static IEnumerable<string> RegisteredInstanceNames => s_instances.Keys;

        // ── Favorites / saved calls ────────────────────────────────────────────
        private static readonly List<SavedCall> s_favorites = new List<SavedCall>();
        public static IReadOnlyList<SavedCall> Favorites => s_favorites;

        public static void AddFavorite(SavedCall call)
        {
            s_favorites.Add(call);
        }

        public static void RemoveFavorite(int index)
        {
            if (index >= 0 && index < s_favorites.Count)
                s_favorites.RemoveAt(index);
        }

        // ── Call log ───────────────────────────────────────────────────────────
        public struct LogEntry
        {
            public enum Kind { Info, Ok, Warn, Error }
            public Kind Level;
            public string Time;
            public string Message;
        }

        private static readonly List<LogEntry> s_log = new List<LogEntry>();
        public static IReadOnlyList<LogEntry> Log => s_log;

        public static void ClearLog() => s_log.Clear();

        public static void AddLogPublic(LogEntry.Kind level, string msg) => AddLog(level, msg);

        private static void AddLog(LogEntry.Kind level, string msg)
        {
            s_log.Add(new LogEntry
            {
                Level = level,
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Message = msg
            });

            // Keep log bounded
            if (s_log.Count > 200)
                s_log.RemoveAt(0);
        }

        // ── Type resolution ────────────────────────────────────────────────────
        /// <summary>
        /// Tries to find a Type by searching all loaded assemblies.
        /// Accepts both "Namespace.Class" and just "Class".
        /// </summary>
        public static Type ResolveType(string namespaceName, string className)
        {
            string fullName = string.IsNullOrWhiteSpace(namespaceName)
                ? className
                : $"{namespaceName}.{className}";

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }

            // Fallback: search by class name alone
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in asm.GetTypes())
                    if (t.Name == className) return t;
            }

            return null;
        }

        // ── Arg parsing ────────────────────────────────────────────────────────
        /// <summary>
        /// Parses a string value into the target CLR type.
        /// Returns null on failure and logs a warning.
        /// </summary>
        public static object ParseArg(string typeName, string value, out string error)
        {
            error = null;
            try
            {
                switch (typeName)
                {
                    case "string": return value;
                    case "int": return int.Parse(value);
                    case "uint": return uint.Parse(value);
                    case "long": return long.Parse(value);
                    case "short": return short.Parse(value);
                    case "byte": return byte.Parse(value);
                    case "float": return float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    case "double": return double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    case "bool":
                        if (value.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                        if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
                        throw new FormatException("expected true/false");
                    default:
                        error = $"unsupported type '{typeName}'";
                        return null;
                }
            }
            catch (Exception e)
            {
                error = $"cannot parse '{value}' as {typeName}: {e.Message}";
                return null;
            }
        }

        // ── Main invoke entry point ────────────────────────────────────────────
        /// <summary>
        /// Overload that accepts ArgSlot[] — supports primitive, registry, and built objects.
        /// </summary>
        public static string InvokeSlots(
            string namespaceName,
            string className,
            string memberName,
            string memberKind,
            object instanceObj,
            ArgSlot[] slots)
        {
            // Resolve each slot to a (typeName, value) pair for the existing Invoke path,
            // but for non-primitive slots we build/resolve the object ahead of time and
            // pass it directly via the overload below.
            var argTypes = new string[slots.Length];
            var argObjs = new object[slots.Length];

            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                switch (s.Kind)
                {
                    case ArgSlotKind.Primitive:
                        string parseErr;
                        argObjs[i] = ParseArg(s.TypeName, s.Value, out parseErr);
                        argTypes[i] = s.TypeName;
                        if (parseErr != null)
                        {
                            string em = $"[ERROR] Slot {i}: {parseErr}";
                            AddLog(LogEntry.Kind.Error, em);
                            return em;
                        }
                        break;

                    case ArgSlotKind.Registry:
                        argObjs[i] = GetInstance(s.Value);
                        argTypes[i] = s.TypeName;
                        if (argObjs[i] == null)
                            AddLog(LogEntry.Kind.Warn, $"Slot {i}: registry key '{s.Value}' not found — passing null");
                        break;

                    case ArgSlotKind.Built:
                        if (s.Draft == null)
                        {
                            string em = $"[ERROR] Slot {i}: ArgSlotKind.Built but Draft is null";
                            AddLog(LogEntry.Kind.Error, em);
                            return em;
                        }
                        string buildErr;
                        argObjs[i] = ObjectBuilder.Build(s.Draft, out buildErr);
                        argTypes[i] = s.Draft.TypeName;
                        if (buildErr != null)
                        {
                            AddLog(LogEntry.Kind.Error, buildErr);
                            return buildErr;
                        }
                        break;
                }
            }

            return InvokeWithObjects(namespaceName, className, memberName, memberKind, instanceObj, argObjs);
        }

        /// <summary>
        /// Low-level invoke with pre-resolved object array (skips ParseArg).
        /// </summary>
        public static string InvokeWithObjects(
            string namespaceName,
            string className,
            string memberName,
            string memberKind,
            object instanceObj,
            object[] parsedArgs)
        {
            var type = ResolveType(namespaceName, className);
            if (type == null)
            {
                string msg = $"[ERROR] Type not found: {namespaceName}.{className}";
                AddLog(LogEntry.Kind.Error, msg);
                return msg;
            }

            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                object result = null;

                switch (memberKind)
                {
                    case "method":
                        {
                            MethodInfo method = null;

                            // Match by parameter types extracted from the resolved objects
                            if (parsedArgs != null && parsedArgs.Length > 0)
                            {
                                var paramTypes = new Type[parsedArgs.Length];
                                bool ok = true;
                                for (int i = 0; i < parsedArgs.Length; i++)
                                {
                                    if (parsedArgs[i] == null) { ok = false; break; }
                                    paramTypes[i] = parsedArgs[i].GetType();
                                }
                                if (ok)
                                    method = type.GetMethod(memberName, flags, null, paramTypes, null);
                            }

                            // Fallback by count
                            if (method == null)
                            {
                                int want = parsedArgs != null ? parsedArgs.Length : 0;
                                MethodInfo first = null;
                                foreach (var m in type.GetMethods(flags))
                                {
                                    if (m.Name != memberName) continue;
                                    if (first == null) first = m;
                                    if (m.GetParameters().Length == want) { method = m; break; }
                                }
                                if (method == null) method = first;
                            }

                            if (method == null)
                            {
                                string msg = $"[ERROR] Method '{memberName}' not found on {type.FullName}";
                                AddLog(LogEntry.Kind.Error, msg);
                                return msg;
                            }
                            result = method.Invoke(instanceObj, parsedArgs ?? new object[0]);
                            break;
                        }
                    case "field-get":
                        {
                            var f = type.GetField(memberName, flags);
                            if (f == null) { string m = $"[ERROR] Field '{memberName}' not found"; AddLog(LogEntry.Kind.Error, m); return m; }
                            result = f.GetValue(instanceObj);
                            break;
                        }
                    case "field-set":
                        {
                            var f = type.GetField(memberName, flags);
                            if (f == null || parsedArgs == null || parsedArgs.Length == 0) { string m = $"[ERROR] Field '{memberName}' not found or no value"; AddLog(LogEntry.Kind.Error, m); return m; }
                            f.SetValue(instanceObj, parsedArgs[0]);
                            result = "<set ok>";
                            break;
                        }
                    case "prop-get":
                        {
                            var p = type.GetProperty(memberName, flags);
                            if (p == null) { string m = $"[ERROR] Property '{memberName}' not found"; AddLog(LogEntry.Kind.Error, m); return m; }
                            result = p.GetValue(instanceObj);
                            break;
                        }
                    case "prop-set":
                        {
                            var p = type.GetProperty(memberName, flags);
                            if (p == null || parsedArgs == null || parsedArgs.Length == 0) { string m = $"[ERROR] Property '{memberName}' not found or no value"; AddLog(LogEntry.Kind.Error, m); return m; }
                            p.SetValue(instanceObj, parsedArgs[0]);
                            result = "<set ok>";
                            break;
                        }
                }

                string resultStr = FormatResult(result);
                AddLog(LogEntry.Kind.Ok, $"{type.Name}.{memberName}  =>  {resultStr}");
                return resultStr;
            }
            catch (TargetInvocationException tie)
            {
                string msg = $"[EXCEPTION] {tie.InnerException?.GetType().Name}: {tie.InnerException?.Message}";
                AddLog(LogEntry.Kind.Error, msg);
                return msg;
            }
            catch (Exception e)
            {
                string msg = $"[EXCEPTION] {e.GetType().Name}: {e.Message}";
                AddLog(LogEntry.Kind.Error, msg);
                return msg;
            }
        }

        // ── Main invoke entry point ────────────────────────────────────────────
        /// <summary>
        /// Resolves the type, parses args, invokes the member, and logs the result.
        /// instanceObj: pass null for static calls, or the actual instance.
        /// </summary>
        public static string Invoke(
            string namespaceName,
            string className,
            string memberName,
            string memberKind,
            object instanceObj,
            string[] argTypes,
            string[] argValues)
        {
            // 1. Resolve type
            var type = ResolveType(namespaceName, className);
            if (type == null)
            {
                string msg = $"[ERROR] Type not found: {namespaceName}.{className}";
                AddLog(LogEntry.Kind.Error, msg);
                return msg;
            }

            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic;

            // 2. Parse arguments
            object[] parsedArgs = null;
            if (argTypes != null && argTypes.Length > 0)
            {
                parsedArgs = new object[argTypes.Length];
                for (int i = 0; i < argTypes.Length; i++)
                {
                    string err;
                    parsedArgs[i] = ParseArg(argTypes[i], argValues[i], out err);
                    if (err != null)
                    {
                        string msg = $"[ERROR] Arg {i}: {err}";
                        AddLog(LogEntry.Kind.Error, msg);
                        return msg;
                    }
                }
            }

            // 3. Dispatch by member kind
            try
            {
                object result = null;
                string callSig = BuildSignature(namespaceName, className, memberName, memberKind, argTypes, argValues);

                switch (memberKind)
                {
                    case "method":
                        {
                            MethodInfo method = null;

                            // First try: match by name + exact parameter types (avoids AmbiguousMatchException)
                            if (argTypes != null && argTypes.Length > 0)
                            {
                                var paramTypes = new Type[argTypes.Length];
                                bool typesResolved = true;
                                for (int i = 0; i < argTypes.Length; i++)
                                {
                                    paramTypes[i] = ResolveClrType(argTypes[i]);
                                    if (paramTypes[i] == null) { typesResolved = false; break; }
                                }
                                if (typesResolved)
                                    method = type.GetMethod(memberName, flags, null, paramTypes, null);
                            }

                            // Second try: no-arg overload
                            if (method == null && (argTypes == null || argTypes.Length == 0))
                                method = type.GetMethod(memberName, flags, null, Type.EmptyTypes, null);

                            // Fallback: pick by name + parameter COUNT (handles obfuscated overloads)
                            if (method == null)
                            {
                                int wantedArgCount = argTypes != null ? argTypes.Length : 0;
                                MethodInfo firstByName = null;
                                foreach (var m in type.GetMethods(flags))
                                {
                                    if (m.Name != memberName) continue;
                                    if (firstByName == null) firstByName = m; // last resort
                                    if (m.GetParameters().Length == wantedArgCount) { method = m; break; }
                                }
                                if (method == null) method = firstByName; // no count match, take first
                            }

                            if (method == null)
                            {
                                string msg = $"[ERROR] Method '{memberName}' not found on {type.FullName}";
                                AddLog(LogEntry.Kind.Error, msg);
                                return msg;
                            }
                            result = method.Invoke(instanceObj, parsedArgs ?? new object[0]);
                            break;
                        }
                    case "prop-get":
                        {
                            var prop = type.GetProperty(memberName, flags);
                            if (prop == null)
                            {
                                string msg = $"[ERROR] Property '{memberName}' not found";
                                AddLog(LogEntry.Kind.Error, msg);
                                return msg;
                            }
                            result = prop.GetValue(instanceObj);
                            break;
                        }
                    case "prop-set":
                        {
                            var prop = type.GetProperty(memberName, flags);
                            if (prop == null || parsedArgs == null || parsedArgs.Length == 0)
                            {
                                string msg = $"[ERROR] Property '{memberName}' not found or no value provided";
                                AddLog(LogEntry.Kind.Error, msg);
                                return msg;
                            }
                            prop.SetValue(instanceObj, parsedArgs[0]);
                            result = "<set ok>";
                            break;
                        }
                    case "field-get":
                        {
                            var field = type.GetField(memberName, flags);
                            if (field == null)
                            {
                                string msg = $"[ERROR] Field '{memberName}' not found";
                                AddLog(LogEntry.Kind.Error, msg);
                                return msg;
                            }
                            result = field.GetValue(instanceObj);
                            break;
                        }
                    case "field-set":
                        {
                            var field = type.GetField(memberName, flags);
                            if (field == null || parsedArgs == null || parsedArgs.Length == 0)
                            {
                                string msg = $"[ERROR] Field '{memberName}' not found or no value provided";
                                AddLog(LogEntry.Kind.Error, msg);
                                return msg;
                            }
                            field.SetValue(instanceObj, parsedArgs[0]);
                            result = "<set ok>";
                            break;
                        }
                    default:
                        {
                            string msg = $"[ERROR] Unknown member kind: {memberKind}";
                            AddLog(LogEntry.Kind.Error, msg);
                            return msg;
                        }
                }

                string resultStr = FormatResult(result);
                AddLog(LogEntry.Kind.Ok, $"{callSig}  =>  {resultStr}");
                return resultStr;
            }
            catch (TargetInvocationException tie)
            {
                string msg = $"[EXCEPTION] {tie.InnerException?.GetType().Name}: {tie.InnerException?.Message}";
                AddLog(LogEntry.Kind.Error, msg);
                return msg;
            }
            catch (Exception e)
            {
                string msg = $"[EXCEPTION] {e.GetType().Name}: {e.Message}";
                AddLog(LogEntry.Kind.Error, msg);
                return msg;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        /// <summary>Maps a type name string (as used in the UI) to the actual CLR Type.</summary>
        private static Type ResolveClrType(string typeName)
        {
            switch (typeName)
            {
                case "string": return typeof(string);
                case "int": return typeof(int);
                case "uint": return typeof(uint);
                case "long": return typeof(long);
                case "short": return typeof(short);
                case "byte": return typeof(byte);
                case "float": return typeof(float);
                case "double": return typeof(double);
                case "bool": return typeof(bool);
                default: return null;
            }
        }
        private static string BuildSignature(
            string ns, string cls, string member, string kind,
            string[] argTypes, string[] argValues)
        {
            string fqn = string.IsNullOrWhiteSpace(ns) ? cls : $"{ns}.{cls}";
            if (kind == "method")
            {
                var sb = new StringBuilder($"{fqn}.{member}(");
                if (argTypes != null)
                    for (int i = 0; i < argTypes.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append($"({argTypes[i]}){argValues[i]}");
                    }
                sb.Append(")");
                return sb.ToString();
            }
            return $"{fqn}.{member} [{kind}]";
        }

        private static string FormatResult(object result)
        {
            if (result == null) return "<null>";
            var type = result.GetType();
            if (type.IsArray)
            {
                var arr = (Array)result;
                var sb = new StringBuilder("[");
                for (int i = 0; i < Math.Min(arr.Length, 32); i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(arr.GetValue(i));
                }
                if (arr.Length > 32) sb.Append("...");
                sb.Append("]");
                return sb.ToString();
            }
            return result.ToString();
        }
    }
}