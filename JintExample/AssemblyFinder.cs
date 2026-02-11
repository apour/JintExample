using System.Collections;
using System.Reflection;

namespace JsInteropDemo;

public static class AssemblyFinder
{
    public static void RecursiveFindAllTypes(object? root, ref HashSet<Type> visited)
    {
        if (root == null)
            return;
        TraverseType(root.GetType(), visited);
    }

    private static void TraverseType(Type type, HashSet<Type> visited)
    {
        // Deny neverending loop
        if (!visited.Add(type))
            return;

        // ➤ 1. Skip metadata types (PropertyInfo, FieldInfo, MemberInfo, Type, MethodInfo…)
        if (typeof(MemberInfo).IsAssignableFrom(type))
            return;

        if (typeof(Type).IsAssignableFrom(type))
            return;

        // ➤ 2. Go through collection → generic argumets
        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            foreach (var arg in type.GetGenericArguments())
            {
                TraverseType(arg, visited);
            }
        }

        // ➤ 3. Go through generic parameters
        foreach (var arg in type.GetGenericArguments())
        {
            TraverseType(arg, visited);
        }

        // ➤ 4. Inner classes
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            TraverseType(nested, visited);
        }

        // ➤ 5. Type properties (not make instance!)
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            TraverseType(prop.PropertyType, visited);
        }

        // ➤ 6. Field types (not make instance!)
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            TraverseType(field.FieldType, visited);
        }
    }
}
