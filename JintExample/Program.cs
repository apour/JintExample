using Jint;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace JsInteropDemo
{
    class Program
    {
        static void Main()
        {
            var typesInQiifDataObject = new HashSet<Type>();
            AssemblyFinder.RecursiveFindAllTypes(new Receipt(), ref typesInQiifDataObject);
            

            var backend = new BackendService();

            var engine = new Engine(options =>
                options.Strict(true)
                       .LimitMemory(64_000_000)     // 64 MB
                       .TimeoutInterval(TimeSpan.FromSeconds(2))
                       .AllowClr()                   // povolí CLR přístup (nutné pro TypeReference)
            );

            // 1) Zviditelnit C# typ Receipt do JS (aby šlo „new Receipt(...)“)
            //engine.SetValue("Receipt", TypeReference.CreateTypeReference(engine, typeof(Receipt)));

            foreach (var type in typesInQiifDataObject)
            {
                engine.SetValue(type.Name, TypeReference.CreateTypeReference(engine, type));
            }

            // 2) Předat instanci backend služby do JS prostoru
            engine.SetValue("backend", backend);

            // 3) (Volitelně) jednoduchý print z JS do konzole
            engine.SetValue("print", new Action<object?>(o => Console.WriteLine(o)));

            // 4) Ukázkový JavaScript, který vytvoří Receipt, naplní pole a zavolá C# služby
            var jsCode = @"
            // Vytvoření prázdného pole (objectGroup) a přidání záznamu
            const objectGroup = [];

            // Vytvoříme instanci C# třídy (funguje díky TypeReference)
            const receipt = new Receipt('INV-2026-0001', 'faktura.pdf');
            objectGroup.push(receipt);

            // Volání metody backendu (C#) z JS
            backend.IncludeDocumentInOutput('file-123', true);

            // Uložení do backendu (C#), předáváme přímo Receipt
            backend.Save(receipt);

            // Můžeme vytvořit další instanci a zapsat ji
            const receipt2 = new Receipt('INV-2026-0002', 'priloha.jpg');
            objectGroup.push(receipt2);
            backend.Save(receipt2);

            print('JS done. Items in objectGroup: ' + objectGroup.length);
        ";

            try
            {
                engine.Execute(jsCode);

                Console.WriteLine($"[C#] Backend count = {backend.Count()}");
                foreach (var r in backend.All())
                {
                    Console.WriteLine($"[C#] Stored: {r}");
                }
            }
            catch (JavaScriptException jse)
            {
                Console.WriteLine($"[JS Error] {jse.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex}");
            }
        }
    }
}