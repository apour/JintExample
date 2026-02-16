using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;

public static class ExpandoGraph
{
    /// <summary>
    /// Converts an object graph to an ExpandoObject recursively and attaches an Action
    /// with the given name to every created ExpandoObject node.
    /// </summary>
    /// <param name="root">Root instance of your object graph.</param>
    /// <param name="actionName">Name of the delegate property to attach (e.g., "Run").</param>
    /// <param name="actionFactory">
    /// Factory that receives (originalInstance, path) and returns the Action to attach for that node.
    /// The path is a dotted path like "Root.Address.City" or "Root.Items[0]".
    /// </param>
    /// <param name="includeNulls">Whether to keep null properties in the expando.</param>
    /// <param name="maxDepth">Maximum recursion depth safety cap.</param>
    public static ExpandoObject ToExpandoWithAction(
        object? root,
        string actionName = "Run",
        Func<object?, string, Action>? actionFactory = null,
        bool includeNulls = false,
        int maxDepth = 64)
    {
        // Default action: print the node path and type
        actionFactory ??= (src, path) => () =>
            Console.WriteLine($"[{path}] Action invoked on {src?.GetType().FullName ?? "<null>"}");

        var stack = new HashSet<object>(ReferenceEqualityComparer.Instance); // for cycle detection
        var result = ConvertNode(root, "Root", 0, maxDepth, includeNulls, actionName, actionFactory, stack);

        // Guarantee an Expando root (if root is a primitive, wrap it)
        if (result is ExpandoObject eo) return eo;

        dynamic wrapper = new ExpandoObject();
        wrapper.Value = result;
        // Also attach root action
        AttachAction(wrapper, actionName, actionFactory(root, "Root"));
        return wrapper;
    }

    // ---------------- internals ----------------

    private static object? ConvertNode(
        object? value,
        string path,
        int depth,
        int maxDepth,
        bool includeNulls,
        string actionName,
        Func<object?, string, Action> actionFactory,
        HashSet<object> stack)
    {
        if (depth > maxDepth) return "[[MaxDepthReached]]";
        if (value is null) return null;

        var type = value.GetType();

        // Terminal types: leave as-is
        if (IsTerminal(type)) return value;

        // Cycle detection (reference types only)
        if (!type.IsValueType)
        {
            if (!stack.Add(value))
                return "[[CircularReference]]";
        }

        try
        {
            // IDictionary<string, T> → Expando with keys as members
            if (TryAsStringDictionary(value, out var stringDict))
            {
                var exp = new ExpandoObject();
                var edict = (IDictionary<string, object?>)exp;

                foreach (var (k, v) in stringDict)
                {
                    var childPath = $"{path}.{k}";
                    var child = ConvertNode(v, childPath, depth + 1, maxDepth, includeNulls, actionName, actionFactory, stack);
                    if (child is null && !includeNulls) continue;
                    edict[k] = child;
                }

                // Attach action to this node
                AttachActionWithUniqueName(edict, actionName, actionFactory(value, path));
                return exp;
            }

            // IDictionary (non-string keys) → list of { Key, Value } objects
            if (value is IDictionary dict)
            {
                var list = new List<object?>();
                foreach (DictionaryEntry e in dict)
                {
                    dynamic pair = new ExpandoObject();
                    pair.Key = e.Key?.ToString();
                    pair.Value = ConvertNode(e.Value, $"{path}[{pair.Key}]", depth + 1, maxDepth, includeNulls, actionName, actionFactory, stack);
                    // Attach action to each pair if you want (optional)
                    AttachAction(pair, actionName, actionFactory(e.Value, $"{path}[{pair.Key}]"));
                    list.Add(pair);
                }
                return list;
            }

            // IEnumerable (but not string) → list
            if (value is IEnumerable enumerable && value is not string)
            {
                var list = new List<object?>();
                int i = 0;
                foreach (var item in enumerable)
                {
                    list.Add(ConvertNode(item, $"{path}[{i}]", depth + 1, maxDepth, includeNulls, actionName, actionFactory, stack));
                    i++;
                }
                return list;
            }

            // Complex object → Expando of properties
            var expando = new ExpandoObject();
            var dict2 = (IDictionary<string, object?>)expando;

            foreach (var prop in GetPublicReadableNonIndexerProperties(type))
            {
                object? raw;
                try { raw = prop.GetValue(value); }
                catch { continue; } // skip inaccessible

                var child = ConvertNode(raw, $"{path}.{prop.Name}", depth + 1, maxDepth, includeNulls, actionName, actionFactory, stack);
                if (child is null && !includeNulls) continue;
                dict2[prop.Name] = child;
            }

            // Attach the action to this node
            AttachActionWithUniqueName(dict2, actionName, actionFactory(value, path));
            return expando;
        }
        finally
        {
            if (!type.IsValueType)
                stack.Remove(value);
        }
    }

    private static void AttachAction(object expando, string name, Action action)
    {
        var d = (IDictionary<string, object?>)(ExpandoObject)expando;
        d[name] = action;
    }

    private static void AttachActionWithUniqueName(IDictionary<string, object?> dict, string desiredName, Action action)
    {
        // Avoid colliding with a property that came from the source object
        var name = desiredName;
        if (dict.ContainsKey(name))
        {
            int i = 1;
            while (dict.ContainsKey($"{desiredName}_{i}")) i++;
            name = $"{desiredName}_{i}";
        }
        dict[name] = action;
    }

    private static IEnumerable<PropertyInfo> GetPublicReadableNonIndexerProperties(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
         .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

    private static bool IsTerminal(Type t)
    {
        if (t.IsPrimitive || t.IsEnum) return true;
        if (t == typeof(string) || t == typeof(decimal) || t == typeof(Guid) ||
            t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(TimeSpan))
            return true;

        var ut = Nullable.GetUnderlyingType(t);
        return ut != null && IsTerminal(ut);
    }

    private static bool TryAsStringDictionary(object value, out IEnumerable<(string Key, object? Value)> dict)
    {
        dict = Enumerable.Empty<(string, object?)>();

        // ExpandoObject acts as IDictionary<string, object?>
        if (value is ExpandoObject exp)
        {
            dict = ((IDictionary<string, object?>)exp).Select(kv => (kv.Key, kv.Value));
            return true;
        }

        var type = value.GetType();
        var iDict = type.GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType &&
                                             i.GetGenericTypeDefinition() == typeof(IDictionary<,>) &&
                                             i.GetGenericArguments()[0] == typeof(string));
        if (iDict == null) return false;

        // Enumerate as IEnumerable<KeyValuePair<string, T>>
        var kvType = typeof(KeyValuePair<,>).MakeGenericType(typeof(string), iDict.GetGenericArguments()[1]);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(kvType);
        if (!enumerableType.IsAssignableFrom(type)) return false;

        var result = new List<(string, object?)>();
        foreach (var item in (IEnumerable)value)
        {
            var it = item.GetType();
            var k = (string)it.GetProperty("Key")!.GetValue(item)!;
            var v = it.GetProperty("Value")!.GetValue(item);
            result.Add((k, v));
        }
        dict = result;
        return true;
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}