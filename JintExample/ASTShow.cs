using System.Collections;
using System.Reflection;
using Esprima;
using Esprima.Ast;

namespace JintExample;

public class AstShow
{
    public static void Print(string code, bool includeLocations = true)
    {
        var parser = new JavaScriptParser(new ParserOptions
        {
            //Loc = includeLocations,
            //Range = includeLocations
        });

        // Parse to an ESTree-compatible AST
        Program program = parser.ParseScript(code); // Use ParseModule for ESM
        PrintNode(program, "");
    }

    private static void PrintNode(Node node, string indent)
    {
        // Header line: Node kind + optional source span
        var span = node.Location != null
            ? $"  [L{node.Location.Start.Line}:{node.Location.Start.Column}–L{node.Location.End.Line}:{node.Location.End.Column}]"
            : string.Empty;

        Console.WriteLine($"{indent}{node.Type}{span}");

        // Print meaningful scalar properties first (identifiers, literals, operators, etc.)
        foreach (var prop in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (typeof(Node).IsAssignableFrom(prop.PropertyType)) continue;
            if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string)) continue;
            var name = prop.Name;
            var value = prop.GetValue(node);

            // Only show useful scalars
            if (value is null) continue;

            switch (value)
            {
                case string s when !string.IsNullOrEmpty(s):
                    Console.WriteLine($"{indent}  - {name}: \"{s}\"");
                    break;
                case bool b:
                    Console.WriteLine($"{indent}  - {name}: {b.ToString().ToLowerInvariant()}");
                    break;
                case int or long or double:
                    Console.WriteLine($"{indent}  - {name}: {value}");
                    break;
                case UnaryOperator uo:
                case BinaryOperator bo:
                // case UpdateOperator udo:
                // case LogicalOperator lo:
                    Console.WriteLine($"{indent}  - {name}: {value}");
                    break;
                case Literal literal when literal.Value is not null:
                    Console.WriteLine($"{indent}  - {name}: {literal.Value} (raw: {literal.Raw})");
                    break;
            }
        }

        // Recurse into child nodes & node arrays
        foreach (var prop in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var val = prop.GetValue(node);
            if (val is null) continue;

            if (val is Node child)
            {
                Console.WriteLine($"{indent}  • {prop.Name}:");
                PrintNode(child, indent + "    ");
            }
            else if (val is IEnumerable list && val is not string)
            {
                var items = list.Cast<object?>().ToList();
                if (items.Count == 0) continue;

                Console.WriteLine($"{indent}  • {prop.Name} [{items.Count}]:");
                int i = 0;
                foreach (var item in items)
                {
                    if (item is Node n)
                    {
                        Console.WriteLine($"{indent}    [{i++}]");
                        PrintNode(n, indent + "      ");
                    }
                    else if (item != null)
                    {
                        // Non-node items (rare), print scalar
                        Console.WriteLine($"{indent}    [{i++}] = {item}");
                    }
                }
            }
        }
    }
}
