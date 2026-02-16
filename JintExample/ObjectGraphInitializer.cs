using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

public static class ObjectGraphInitializer
{
    public static void EnsureCollectionsHaveAtLeastOneItem(object root, int maxDepth = 64)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Visit(root, 0, maxDepth, visited);
    }

    private static void Visit(object? obj, int depth, int maxDepth, HashSet<object> visited)
    {
        if (obj is null) return;
        if (depth > maxDepth) return;

        var type = obj.GetType();

        if (IsTerminal(type))
            return;

        // Prevent cycles
        if (!type.IsValueType) // only reference types need cycle protection
        {
            if (!visited.Add(obj)) return;
        }

        foreach (var prop in GetCandidateProperties(type))
        {
            var propType = prop.PropertyType;
            var value = prop.GetValue(obj);

            // 1) Handle collections (List<T> / ICollection<T>)
            if (TryGetCollectionElementType(propType, out var elementType))
            {
                // Ensure property instance exists
                if (value == null)
                {
                    var newCollection = CreateCollectionInstance(propType);
                    if (newCollection != null)
                    {
                        prop.SetValue(obj, newCollection);
                        value = newCollection;
                    }
                    else
                    {
                        // Can't construct; skip
                        continue;
                    }
                }

                // At this point, value is a collection
                var collection = (IList?)value ?? value as IEnumerable; // we’ll add via reflection if not IList
                int count = GetCollectionCount(value);

                if (count == 0)
                {
                    // Create one element and add
                    var newElement = CreateInstanceSafely(elementType);
                    if (newElement != null)
                    {
                        AddToCollection(value!, elementType, newElement);
                        // Recurse into the newly created element
                        Visit(newElement, depth + 1, maxDepth, visited);
                    }
                }
                else
                {
                    // Recurse into existing elements
                    foreach (var item in EnumerateItems(value))
                    {
                        Visit(item, depth + 1, maxDepth, visited);
                    }
                }

                continue;
            }

            // 2) For complex reference types, ensure they are instantiated and recurse
            if (!IsTerminal(propType))
            {
                if (value == null)
                {
                    var child = CreateInstanceSafely(propType);
                    if (child != null)
                    {
                        prop.SetValue(obj, child);
                        Visit(child, depth + 1, maxDepth, visited);
                    }
                }
                else
                {
                    Visit(value, depth + 1, maxDepth, visited);
                }
            }
        }
    }

    // Determines if the type should be treated as a leaf (no recursion/initialization)
    private static bool IsTerminal(Type t)
    {
        if (t.IsPrimitive || t.IsEnum)
            return true;

        if (t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(DateTimeOffset) ||
            t == typeof(TimeSpan) || t == typeof(Guid))
            return true;

        // Nullable<T> with a terminal underlying type is also terminal
        if (Nullable.GetUnderlyingType(t) is Type ut)
            return IsTerminal(ut);

        return false;
    }

    private static IEnumerable<PropertyInfo> GetCandidateProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                   .Where(p => p.CanRead && p.CanWrite);
    }

    // Checks if type is a generic ICollection<T> or a List<T> and returns the T
    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        elementType = null!;

        // Handle arrays
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        // Directly generic type? e.g., List<T>, HashSet<T>
        if (type.IsGenericType)
        {
            // If implements ICollection<T>, take that T
            var iColl = type.GetInterfaces()
                            .Append(type) // include the type itself in case it is ICollection<T>
                            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
            if (iColl != null)
            {
                elementType = iColl.GetGenericArguments()[0];
                return true;
            }
        }

        // Non-generic or no match
        return false;
    }

    private static object? CreateCollectionInstance(Type collectionType)
    {
        // Arrays need special handling (we'll create 1-length array later on add)
        if (collectionType.IsArray)
        {
            // caller will handle adding; here we return null to signal unsupported for property set
            // because you generally cannot set an array property to a new array unless we construct the full array.
            // If you want array support, you can change strategy: construct 1-length array here.
            return Array.CreateInstance(collectionType.GetElementType()!, 0);
        }

        // Must have a public parameterless constructor
        if (collectionType.IsAbstract || collectionType.IsInterface)
        {
            // Try to find a default concrete type: List<T> for ICollection<T>
            if (TryGetCollectionElementType(collectionType, out var elementType))
            {
                var listType = typeof(List<>).MakeGenericType(elementType);
                return Activator.CreateInstance(listType);
            }

            return null;
        }

        return Activator.CreateInstance(collectionType);
    }

    private static int GetCollectionCount(object collection)
    {
        if (collection is Array arr) return arr.Length;

        var prop = collection.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(int))
        {
            return (int)prop.GetValue(collection)!;
        }

        // Fallback: enumerate
        int c = 0;
        foreach (var _ in EnumerateItems(collection)) c++;
        return c;
    }

    private static IEnumerable<object?> EnumerateItems(object collection)
    {
        if (collection is IEnumerable enumerable)
        {
            foreach (var item in enumerable) yield return item;
        }
    }

    private static void AddToCollection(object collection, Type elementType, object? element)
    {
        if (collection is Array arr)
        {
            // Create a new array with one element and (optionally) copy old items
            var newArr = Array.CreateInstance(arr.GetType().GetElementType()!, (arr?.Length ?? 0) + 1);
            if (arr != null && arr.Length > 0)
                Array.Copy(arr, newArr, arr.Length);
            newArr.SetValue(element, newArr.Length - 1);

            // If the property was an array, we can't replace it here; this helper is called without property context.
            // In practice, prefer List<T> over arrays for mutable graphs; or modify the code to handle array replacement at the property level.
            return;
        }

        // Try ICollection<T>.Add
        var addMethod = collection.GetType()
                                  .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                  .FirstOrDefault(m => m.Name == "Add"
                                                    && m.GetParameters() is { Length: 1 } ps
                                                    && ps[0].ParameterType.IsAssignableFrom(elementType));
        if (addMethod != null)
        {
            addMethod.Invoke(collection, new[] { element });
            return;
        }

        // Try non-generic IList
        if (collection is IList list)
        {
            list.Add(element);
        }
    }

    private static object? CreateInstanceSafely(Type t)
    {
        // Avoid trying to instantiate interfaces/abstracts
        if (t.IsInterface || t.IsAbstract) return null;

        // Nullable<T>: instantiate underlying type default
        var nullableUnderlying = Nullable.GetUnderlyingType(t);
        if (nullableUnderlying != null)
        {
            // value types default(T) = Activator.CreateInstance
            return Activator.CreateInstance(nullableUnderlying);
        }

        try
        {
            // Prefer parameterless ctor
            var ctor = t.GetConstructor(Type.EmptyTypes);
            if (ctor != null)
                return Activator.CreateInstance(t);

            // As a policy decision, skip types without parameterless ctor.
            // You could extend this to pick the "simplest" ctor and fill with defaults.
            return null;
        }
        catch
        {
            return null;
        }
    }

    // Reference equality comparer for cycle detection
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}