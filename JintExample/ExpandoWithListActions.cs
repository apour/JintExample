using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;

public static class ExpandoWithListActions
{
    /// <summary>
    /// Converts a strongly-typed object graph into ExpandoObjects recursively.
    /// For every property of type List&lt;T&gt;, adds two dynamic methods on the parent node:
    ///   - AddTo_{PropName}(object? item)
    ///   - AddDefaultTo_{PropName}()
    /// The list property itself becomes List&lt;object?&gt; to preserve indexing.
    /// </summary>
    public static ExpandoObject ToExpando(object? root, bool includeNulls = false, int maxDepth = 64)
    {
        var visiting = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var result = ConvertNode(root, "Root", 0, maxDepth, includeNulls, visiting);

        // ensure an Expando root even for primitives
        if (result is ExpandoObject eo) return eo;
        dynamic wrapper = new ExpandoObject();
        wrapper.Value = result;
        return wrapper;
    }

    // ---------------- internals ----------------

    private static object? ConvertNode(
        object? value,
        string path,
        int depth,
        int maxDepth,
        bool includeNulls,
        HashSet<object> visiting)
    {
        if (depth > maxDepth) return "[[MaxDepthReached]]";
        if (value is null) return null;

        var type = value.GetType();

        // terminal types: leave as-is
        if (IsTerminal(type)) return value;

        // cycle detection for reference types
        if (!type.IsValueType)
        {
            if (!visiting.Add(value)) return "[[CircularReference]]";
        }

        try
        {
            // IDictionary<string, T> -> Expando with keyed members
            if (TryAsStringDictionary(value, out var stringDict))
            {
                var exp = new ExpandoObject();
                var edict = (IDictionary<string, object?>)exp;

                foreach (var (k, v) in stringDict)
                {
                    var child = ConvertNode(v, $"{path}.{k}", depth + 1, maxDepth, includeNulls, visiting);
                    if (child is null && !includeNulls) continue;
                    edict[k] = child;
                }

                return exp;
            }

            // IDictionary (non-string keys) -> list of { Key, Value }
            if (value is IDictionary dict)
            {
                var list = new List<object?>();
                foreach (DictionaryEntry e in dict)
                {
                    dynamic pair = new ExpandoObject();
                    pair.Key = e.Key?.ToString();
                    pair.Value = ConvertNode(e.Value, $"{path}[{pair.Key}]", depth + 1, maxDepth, includeNulls, visiting);
                    list.Add(pair);
                }
                return list;
            }

            // IEnumerable (not string) -> list
            if (value is IEnumerable enumerable && value is not string)
            {
                var list = new List<object?>();
                int i = 0;
                foreach (var item in enumerable)
                {
                    list.Add(ConvertNode(item, $"{path}[{i}]", depth + 1, maxDepth, includeNulls, visiting));
                    i++;
                }
                return list;
            }

            // Complex object -> Expando of properties
            var expando = new ExpandoObject();
            var dictExp = (IDictionary<string, object?>)expando;

            foreach (var prop in GetPublicReadableNonIndexerProperties(type))
            {
                var propType = prop.PropertyType;

                object? raw;
                try { raw = prop.GetValue(value); }
                catch { continue; }

                // === Only for List<T> properties attach actions ===
                if (IsConcreteListOfT(propType, out var elemType))
                {
                    // 1) Convert the list content for expando
                    var expandoList = new List<object?>();
                    if (raw is IEnumerable rawEnum)
                    {
                        int i = 0;
                        foreach (var item in rawEnum)
                        {
                            expandoList.Add(ConvertNode(item, $"{path}.{prop.Name}[{i}]", depth + 1, maxDepth, includeNulls, visiting));
                            i++;
                        }
                    }
                    dictExp[prop.Name] = expandoList;

                    // 2) Add dynamic actions to the parent expando for this list:
                    // CreateArray_{PropName}
                    var createArrayName = $"CreateArray{prop.Name}";
                    dictExp[UniqueName(dictExp, createArrayName)] = (Action<object?>)((item) =>
                    {
                        // ensure underlying list exists
                        var listObj = (IList?)prop.GetValue(value);
                        if (listObj == null)
                        {
                            var newList = Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType))!;
                            prop.SetValue(value, newList);
                            listObj = (IList)newList;
                        }

                        // mirror into expando side
                        //var converted = ConvertNode(toAdd, $"{path}.{prop.Name}[{listObj.Count - 1}]", depth + 1, maxDepth, includeNulls, visiting);
                        //expandoList.Add(converted);
                    });

                    
                    // AddTo_{PropName}(object? item)
                    var addName = $"AddTo_{prop.Name}";
                    dictExp[UniqueName(dictExp, addName)] = (Action<object?>)((item) =>
                    {
                        // ensure underlying list exists
                        var listObj = (IList?)prop.GetValue(value);
                        if (listObj == null)
                        {
                            var newList = Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType))!;
                            prop.SetValue(value, newList);
                            listObj = (IList)newList;
                        }

                        object? toAdd = item;
                        if (item != null && !elemType.IsInstanceOfType(item))
                        {
                            // Try simple convertible change type when possible (will throw if invalid)
                            toAdd = ChangeTypeIfPossible(item, elemType);
                        }

                        // If still mismatched, try making a default and copy simple properties (optional policy)
                        if (toAdd != null && !elemType.IsInstanceOfType(toAdd))
                            throw new InvalidOperationException($"Cannot add item of type {item!.GetType().Name} to List<{elemType.Name}>.");

                        listObj.Add(toAdd);

                        // mirror into expando side
                        var converted = ConvertNode(toAdd, $"{path}.{prop.Name}[{listObj.Count - 1}]", depth + 1, maxDepth, includeNulls, visiting);
                        expandoList.Add(converted);
                    });

                    // AddDefaultTo_{PropName}()
                    var addDefaultName = $"AddDefaultTo_{prop.Name}";
                    dictExp[UniqueName(dictExp, addDefaultName)] = (Action)(() =>
                    {
                        if (!TryCreateInstance(elemType, out var newItem))
                            throw new InvalidOperationException($"Type {elemType.FullName} has no public parameterless constructor.");

                        // reuse AddTo_{PropName} logic by invoking the delegate we just added
                        var addKey = dictExp.Keys.First(k => k.StartsWith(addName, StringComparison.Ordinal));
                        var addDelegate = (Action<object?>)dictExp[addKey]!;
                        addDelegate(newItem);
                    });

                    continue; // done with this property (we already set expando list + actions)
                }

                // Non-list property: recurse normally
                var child = ConvertNode(raw, $"{path}.{prop.Name}", depth + 1, maxDepth, includeNulls, visiting);
                if (child is null && !includeNulls) continue;
                dictExp[prop.Name] = child;
            }

            return expando;
        }
        finally
        {
            if (!type.IsValueType)
                visiting.Remove(value);
        }
    }

    // -------- Helpers --------

    private static bool IsConcreteListOfT(Type t, out Type elementType)
    {
        elementType = null!;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
        {
            elementType = t.GetGenericArguments()[0];
            return true;
        }
        return false;
    }

    private static IEnumerable<PropertyInfo> GetPublicReadableNonIndexerProperties(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
         .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

    private static bool IsTerminal(Type t)
    {
        if (t.IsPrimitive || t.IsEnum) return true;
        if (t == typeof(string) || t == typeof(decimal) || t == typeof(Guid) ||
            t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(TimeSpan)) return true;

        var ut = Nullable.GetUnderlyingType(t);
        return ut != null && IsTerminal(ut);
    }

    private static bool TryAsStringDictionary(object value, out IEnumerable<(string Key, object? Value)> dict)
    {
        dict = Enumerable.Empty<(string, object?)>();

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

        var kvType = typeof(KeyValuePair<,>).MakeGenericType(typeof(string), iDict.GetGenericArguments()[1]);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(kvType);
        if (!enumerableType.IsAssignableFrom(type)) return false;

        var list = new List<(string, object?)>();
        foreach (var item in (IEnumerable)value)
        {
            var it = item.GetType();
            var k = (string)it.GetProperty("Key")!.GetValue(item)!;
            var v = it.GetProperty("Value")!.GetValue(item);
            list.Add((k, v));
        }
        dict = list;
        return true;
    }

    private static bool TryCreateInstance(Type t, out object? instance)
    {
        instance = null;
        if (t.IsAbstract || t.IsInterface) return false;
        var ctor = t.GetConstructor(Type.EmptyTypes);
        if (ctor == null) return false;
        try { instance = Activator.CreateInstance(t); return true; }
        catch { return false; }
    }

    private static object? ChangeTypeIfPossible(object value, Type target)
    {
        // Allow trivial conversions (e.g., int -> long, string -> int if parseable)
        try
        {
            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
                return Convert.ChangeType(value, target);
        }
        catch { /* ignore */ }
        return value; // give back unchanged; caller validates
    }

    private static string UniqueName(IDictionary<string, object?> dict, string baseName)
    {
        if (!dict.ContainsKey(baseName)) return baseName;
        int i = 1;
        while (dict.ContainsKey($"{baseName}_{i}")) i++;
        return $"{baseName}_{i}";
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}