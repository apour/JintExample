using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

public static class ObjectGraphPruner
{
    /// <summary>
    /// Recursively removes default-like content from the object graph:
    /// - Removes default-like items from collections.
    /// - Sets empty collections to null (configurable).
    /// - Sets default-like reference properties to null.
    /// Returns true if the root itself is default-like after pruning (caller may ignore).
    /// </summary>
    public static bool PruneDefaults(object? root, bool nullEmptyCollections = true, int maxDepth = 64)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return PruneNode(root, parent: null, propOnParent: null, depth: 0, maxDepth, nullEmptyCollections, visited);
    }

    private static bool PruneNode(object? node, object? parent, PropertyInfo? propOnParent,
                                  int depth, int maxDepth, bool nullEmptyCollections, HashSet<object> visited)
    {
        if (node is null) return true;
        if (depth > maxDepth) return true;

        var type = node.GetType();

        // Terminal types: evaluate default directly
        if (IsTerminal(type))
            return IsTerminalDefault(node, type);

        // Avoid cycles for reference types
        if (!type.IsValueType)
        {
            if (!visited.Add(node)) return false; // assume not default to avoid over-pruning in cycles
        }

        bool allDefault = true;

        foreach (var prop in GetCandidateProperties(type))
        {
            var propType = prop.PropertyType;
            var value = prop.GetValue(node);

            // 1) Collections (arrays, List<T>, ICollection<T>, etc.)
            if (TryGetCollectionElementType(propType, out var elementType))
            {
                // Null collections count as default
                if (value == null) continue;

                // Prune items
                var items = EnumerateItems(value).ToList();
                var keep = new List<object?>(items.Count);

                foreach (var item in items)
                {
                    bool itemDefault = PruneNode(item, node, prop, depth + 1, maxDepth, nullEmptyCollections, visited);
                    if (!itemDefault)
                        keep.Add(item);
                }

                // Apply filtered result
                if (propType.IsArray)
                {
                    // Build a new array with the kept items
                    var newArray = Array.CreateInstance(elementType, keep.Count);
                    for (int i = 0; i < keep.Count; i++) newArray.SetValue(keep[i], i);

                    if (nullEmptyCollections && keep.Count == 0 && prop.CanWrite)
                    {
                        prop.SetValue(node, null);
                    }
                    else if (prop.CanWrite)
                    {
                        prop.SetValue(node, newArray);
                    }
                    // If not writable, we can’t change; still affects default-ness below.
                }
                else
                {
                    // Mutable collections: clear + add back significant items (if supported)
                    ReplaceCollectionItems(value, elementType, keep);

                    // After pruning, if empty -> optionally null out
                    int countAfter = GetCollectionCount(value);
                    if (countAfter == 0 && nullEmptyCollections && prop.CanWrite)
                    {
                        prop.SetValue(node, null);
                    }
                }

                // Re-check if collection remains non-empty
                var current = prop.GetValue(node);
                if (current != null && GetCollectionCount(current) > 0)
                    allDefault = false;

                continue;
            }

            // 2) Complex reference type (non-terminal)
            if (!IsTerminal(propType))
            {
                if (value == null)
                {
                    // null is default-like
                    continue;
                }

                bool childDefault = PruneNode(value, node, prop, depth + 1, maxDepth, nullEmptyCollections, visited);

                if (childDefault && prop.CanWrite && !propType.IsValueType)
                {
                    // Remove default-like reference children
                    prop.SetValue(node, null);
                }
                else
                {
                    // If child remains, parent can't be default-like
                    if (prop.GetValue(node) != null)
                        allDefault = false;
                }

                continue;
            }

            // 3) Terminal property: contributes to default-ness
            if (!IsTerminalDefault(value, propType))
                allDefault = false;
        }

        return allDefault;
    }

    // ---------------------------
    // Helpers
    // ---------------------------

    private static IEnumerable<PropertyInfo> GetCandidateProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

    private static bool IsTerminal(Type t)
    {
        if (t.IsPrimitive || t.IsEnum) return true;

        if (t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) ||
            t == typeof(DateTimeOffset) || t == typeof(TimeSpan) || t == typeof(Guid))
            return true;

        var ut = Nullable.GetUnderlyingType(t);
        if (ut != null) return IsTerminal(ut);

        return false;
    }

    private static bool IsTerminalDefault(object? value, Type t)
    {
        if (value == null) return true;

        if (t == typeof(string))
            return string.IsNullOrWhiteSpace((string)value);

        if (t.IsEnum) return Equals(value, Activator.CreateInstance(t));

        if (t.IsPrimitive || t == typeof(decimal))
            return value.Equals(Activator.CreateInstance(t)!);

        if (t == typeof(DateTime))
            return (DateTime)value == default;

        if (t == typeof(DateTimeOffset))
            return (DateTimeOffset)value == default;

        if (t == typeof(TimeSpan))
            return (TimeSpan)value == default;

        if (t == typeof(Guid))
            return (Guid)value == default;

        var ut = Nullable.GetUnderlyingType(t);
        if (ut != null)
        {
            // Nullable<T>: null was already handled; non-null -> compare underlying default
            var defaultUnderlying = Activator.CreateInstance(ut)!;
            return value.Equals(defaultUnderlying);
        }

        return false;
    }

    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        elementType = null!;

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (type.IsGenericType)
        {
            // If implements ICollection<T>, take T
            var iColl = type.GetInterfaces()
                            .Append(type)
                            .FirstOrDefault(i => i.IsGenericType &&
                                                 i.GetGenericTypeDefinition() == typeof(ICollection<>));
            if (iColl != null)
            {
                elementType = iColl.GetGenericArguments()[0];
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<object?> EnumerateItems(object collection)
    {
        if (collection is IEnumerable enumerable)
        {
            foreach (var item in enumerable) yield return item;
        }
    }

    private static int GetCollectionCount(object collection)
    {
        if (collection is Array arr) return arr.Length;

        var prop = collection.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(int))
            return (int)prop.GetValue(collection)!;

        int c = 0;
        foreach (var _ in EnumerateItems(collection)) c++;
        return c;
    }

    private static void ReplaceCollectionItems(object collection, Type elementType, List<object?> keep)
    {
        // Try Clear()
        var clear = collection.GetType().GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        var add = collection.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance, new[] { elementType });

        if (clear != null && add != null)
        {
            clear.Invoke(collection, null);
            foreach (var item in keep) add.Invoke(collection, new[] { item });
            return;
        }

        // Fallback: IList
        if (collection is IList list)
        {
            list.Clear();
            foreach (var item in keep) list.Add(item);
            return;
        }

        // If immutable or no Clear/Add — best effort: do nothing (caller may be read-only collection)
        // In such cases we won't be able to prune collection items.
    }

    // Reference equality comparer for cycle detection
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}