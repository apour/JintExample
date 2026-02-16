using System.Collections;
using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;

public static class ExpandoBackMapper
{
    public static void UpdateObjectFromExpando(object target, ExpandoObject expando)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        UpdateNode(target, expando, visited);
    }


    private static void UpdateNode(object? target, object? exp, HashSet<object> visited)
    {
        if (target == null || exp == null)
            return;

        // Avoid cycles
        if (!target.GetType().IsValueType)
        {
            if (!visited.Add(target))
                return;
        }

        // if (exp is not IDictionary<string, object?> expDict)
        //     return;
        
        Func<string, object?> expDictLookup = key =>
        {
            if (exp is IDictionary<string, object?> expDict && expDict.TryGetValue(key, out var value))
                return value;
            
            var expType = exp.GetType();
            if (expType == typeof(ExpandoObject))
                return null;
            var prop = expType.GetProperty(key);
            if (prop == null)
                return null; 
            return prop.GetValue(exp);
        };
        CopyProperties(target, visited, expDictLookup);
    }

    private static void CopyProperties(object target, HashSet<object> visited, Func<string, object?> expDictLookup)
    {
        Type type = target.GetType();
        // Walk every property in target class
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite)
                continue;

            var expValue = expDictLookup.Invoke(prop.Name);
            // if (!expDict.TryGetValue(prop.Name, out var expValue))
            //     continue;

            var propType = prop.PropertyType;

            // ---------- 1) Null assign ----------
            if (expValue == null)
            {
                prop.SetValue(target, null);
                continue;
            }

            // ---------- 2) Terminal types ----------
            if (IsSimple(propType))
            {
                object? converted = ChangeTypeIfPossible(expValue, propType);
                prop.SetValue(target, converted);
                continue;
            }

            // ---------- 3) List<T> ----------
            if (TryGetListElementType(propType, out var elemType))
            {
                IList list = EnsureListInstance(prop, target);

                list.Clear();

                if (expValue is IEnumerable expEnum)
                {
                    foreach (var item in expEnum)
                    {
                        object? newElem = null;
                        if (item == null)
                        {
                            list.Add(null);
                        }
                        else if (IsSimple(elemType))
                        {
                            newElem = ChangeTypeIfPossible(item, elemType);
                            list.Add(newElem);
                        }
                        else
                        {
                            newElem = Activator.CreateInstance(elemType);
                            UpdateNode(newElem, item, visited);
                            list.Add(newElem);
                        }
                    }
                }

                continue;
            }

            // ---------- 4) Dictionary<string, T> ----------
            if (TryGetDictionaryValueType(propType, out var dictValueType))
            {
                var dict = EnsureDictionaryInstance(prop, target);

                dict.Clear();

                if (expValue is IDictionary<string, object?> expKv)
                {
                    foreach (var kv in expKv)
                    {
                        object? newVal;
                        if (kv.Value == null)
                        {
                            newVal = null;
                        }
                        else if (IsSimple(dictValueType))
                        {
                            newVal = ChangeTypeIfPossible(kv.Value, dictValueType);
                        }
                        else
                        {
                            newVal = Activator.CreateInstance(dictValueType);
                            UpdateNode(newVal, kv.Value, visited);
                        }

                        dict[kv.Key] = newVal;
                    }
                }

                continue;
            }

            // ---------- 5) Complex nested object ----------
            // or Fallback: try to assign directly if types are compatible
            if (expValue is ExpandoObject || propType.IsAssignableFrom(expValue.GetType()))
            {
                object? nested = prop.GetValue(target);
                if (nested == null)
                {
                    nested = Activator.CreateInstance(propType);
                    prop.SetValue(target, nested);
                }

                UpdateNode(nested, expValue, visited);
                continue;
            }
        }
    }

    // ----------------- Helpers -----------------

    private static bool IsSimple(Type t)
    {
        if (t.IsPrimitive || t.IsEnum) return true;
        if (t == typeof(string) || t == typeof(decimal) ||
            t == typeof(DateTime) || t == typeof(Guid) ||
            t == typeof(DateTimeOffset) || t == typeof(TimeSpan))
            return true;

        var ut = Nullable.GetUnderlyingType(t);
        return ut != null && IsSimple(ut);
    }

    private static object? ChangeTypeIfPossible(object value, Type type)
    {
        try
        {
            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(type))
                return Convert.ChangeType(value, type);
        }
        catch { }

        return value;
    }

    private static bool TryGetListElementType(Type t, out Type elemType)
    {
        elemType = null!;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
        {
            elemType = t.GetGenericArguments()[0];
            return true;
        }
        return false;
    }

    private static IList EnsureListInstance(PropertyInfo prop, object target)
    {
        var list = prop.GetValue(target) as IList;
        if (list != null)
            return list;

        var generic = typeof(List<>).MakeGenericType(prop.PropertyType.GetGenericArguments()[0]);
        list = (IList)Activator.CreateInstance(generic)!;
        prop.SetValue(target, list);
        return list;
    }

    private static bool TryGetDictionaryValueType(Type t, out Type valueType)
    {
        valueType = null!;
        if (t.IsGenericType &&
            t.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
            t.GetGenericArguments()[0] == typeof(string))
        {
            valueType = t.GetGenericArguments()[1];
            return true;
        }
        return false;
    }

    private static IDictionary<string, object?> EnsureDictionaryInstance(PropertyInfo prop, object target)
    {
        var dict = prop.GetValue(target) as IDictionary<string, object?>;
        if (dict != null)
            return dict;

        var valType = prop.PropertyType.GetGenericArguments()[1];
        var newType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valType);
        dict = (IDictionary<string, object?>)Activator.CreateInstance(newType)!;

        prop.SetValue(target, dict);
        return dict;
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}