using Jint;
using Jint.Runtime;
using JsonQiifConverterGenerated.Qiif;
using System.Reflection;
using System.Reflection.Metadata;
using Jint.Runtime.Debugger;
using System.Dynamic;
using System;

namespace JsInteropDemo
{

    public class Person
    {
        public string? Name { get; set; }
        public Address? Address { get; set; }
        public List<Phone> Phones { get; set; } = new();
        public Dictionary<string, string> Tags { get; set; } = new();
        public Person? BestFriend { get; set; } // to show cycles
    }

    public class Address
    {
        public string? City { get; set; }
        public string? Street { get; set; }
    }

    public class Phone
    {
        public string? Type { get; set; }
        public string? Number { get; set; }
    }
    
    class Program
    {
        private static void ExpandoGraphExample()
        {
            var alice = new Person
            {
                Name = "Alice",
                Address = new Address { City = "Hradec Králové", Street = "Main 1" },
                Phones = new List<Phone>
                {
                    new Phone { Type = "mobile", Number = "+420 777 000 111" },
                    new Phone { Type = "work", Number = "+420 495 000 222" }
                },
                Tags = new Dictionary<string, string> { ["role"] = "developer" }
            };
            var bob = new Person { Name = "Bob" };
            alice.BestFriend = bob;
            bob.BestFriend = alice; // cycle

            // Attach a "Run" action to every node that prints path + type
            dynamic dyn = ExpandoGraph.ToExpandoWithAction(
                alice,
                actionName: "Run",
                actionFactory: (obj, path) => () =>
                {
                    Console.WriteLine($"RUN @ {path} (Type: {obj?.GetType().Name ?? "null"})");
                },
                includeNulls: false
            );

            // Call the action on various nodes:
            dyn.Run();                           // Root
            dyn.Address.Run();                   // Root.Address
            dyn.Phones[0].Run();                 // Root.Phones[0]
            dyn.BestFriend.Run();                // Root.BestFriend
            Console.WriteLine(dyn.Tags.role);    // "developer"

            // If any node already had a "Run" property, we avoid collision (e.g., "Run_1")
            // You can call that as well: dyn.Run_1();
        }
        
        private static StepMode OnBreak(object? sender, DebugInformation e)
        {
            Console.WriteLine($"BREAK at {e.Location.SourceFile}:{e.Location.Start.Line}:{e.Location.Start.Column}");
            // Peek values from current scope:
            var locals = e.CurrentScopeChain;
            foreach (var name in locals)
            {
                //locals.
                //var value = locals.GetBindingValue(name);
                //Console.WriteLine($"  {name} = {value}");
            }
            // Continue stepping into next statements after this break
            return StepMode.Into;
        }

        private static StepMode OnStep(object? sender, DebugInformation e)
        {
            // Fires at each step depending on current StepMode (Into/Over/Out)
            var node = e.CurrentNode;         // AST node about to execute
            
            return StepMode.Into;
        }

        static void Main()
        {
            var typesInQiifDataObject = new HashSet<Type>();
            AssemblyFinder.RecursiveFindAllTypes(new QiifDataObject(), ref typesInQiifDataObject);
            

            var backend = new BackendService();

            var engine = new Engine(options =>
                options.Strict(true)
                       .LimitMemory(64_000_000)     // 64 MB
                       .TimeoutInterval(TimeSpan.FromSeconds(20000))
                       .AllowClr()                   // povolí CLR přístup (nutné pro TypeReference)
                       .DebugMode()
                       .DebuggerStatementHandling(DebuggerStatementHandling.Script)
                       .InitialStepMode(StepMode.Into)
            );

            engine.Debugger.Break += OnBreak;
            engine.Debugger.Step += OnStep;

            // 1) Zviditelnit C# typ Receipt do JS (aby šlo „new Receipt(...)“)
            //engine.SetValue("Receipt", TypeReference.CreateTypeReference(engine, typeof(Receipt)));

            foreach (var type in typesInQiifDataObject)
            {
                // Guard against types that are unsuitable for direct JS constructor exposure.
                // - Skip anonymous/empty names
                // - Strip generic arity suffix (e.g. `1) and replace dots so the name becomes a valid JS identifier
                try
                {
                    var jsName = type.Name;
                    if (string.IsNullOrWhiteSpace(jsName))
                        continue;

                    var tick = jsName.IndexOf('`');
                    if (tick >= 0)
                        jsName = jsName.Substring(0, tick);

                    jsName = jsName.Replace('.', '_');

                    // Expose the System.Type itself to JS (avoids dependency on TypeReference symbol);
                    // this doesn't provide a JS `new` constructor, but lets scripts inspect the type or call factories.
                    engine.SetValue(jsName, type);
                }
                catch (Exception ex)
                {
                    // Don't fail startup because of one problematic type; log and continue
                    Console.WriteLine($"[Info] Skipping type '{type.FullName}': {ex.Message}");
                }
            }

            // 2) Předat instanci backend služby do JS prostoru
            engine.SetValue("backend", backend);

            var qiifDataObject = new QiifDataObject();
            qiifDataObject.TradeTransaction = new TradeTransaction();
            qiifDataObject.TradeTransaction.Evidence = new Evidence();
            qiifDataObject.TradeTransaction.Evidence.OtherGroup = new List<DocumentReference>();
            
            // Wrap the CLR instance in a dynamic ExpandoObject so we can add JS-callable methods at runtime
            dynamic qiifWrapper = new ExpandoObject();
            qiifWrapper.target = qiifDataObject; // keep reference to underlying object
            // Add a runtime Test() method that writes to the console
            
            qiifWrapper.Test = new Action(() => Console.WriteLine("[Runtime Test] called on qiifDataObject"));
            qiifWrapper.createArray = new Func<string, object?>((string typeName) =>
            {
                Type? foundType;
                try
                {
                    foundType = Type.GetType("JsonQiifConverterGenerated.Qiif." + typeName);
                }
                catch (Exception e)
                {
                    return null;
                }
                if (foundType == null)
                    return null;
                
                var concreteType = typeof(List<>).MakeGenericType(foundType);
                var instance = Activator.CreateInstance(concreteType);
                return instance;
            });
            // Forward createArray calls to the underlying CLR instance method (if present)
            //qiifWrapper.createArray = new Func<string, object>(qiifDataObject.createArray);
            
            // Expose the wrapper to JS; JS can call qiifDataObject.Test() or qiifDataObject.createArray('note')
            engine.SetValue("qiifDataObject", qiifWrapper);

            // 3) (Volitelně) jednoduchý print z JS do konzole
            engine.SetValue("print", new Action<object?>(o => Console.WriteLine(o)));

            engine.SetValue("createArray", new Func<string, object?>((string typeName) =>
            {
                Type? foundType;
                try
                {
                    foundType = Type.GetType("JsonQiifConverterGenerated.Qiif." + typeName);
                }
                catch (Exception e)
                {
                    return null;
                }
                if (foundType == null)
                    return null;
                
                var concreteType = typeof(List<>).MakeGenericType(foundType);
                var instance = Activator.CreateInstance(concreteType);
                return instance;
            }));
            
            // Expose a small CLR helper to create a strongly-typed List<DocumentReference>
            engine.SetValue("createOtherGroup", new Func<System.Collections.Generic.List<DocumentReference>>(() => new System.Collections.Generic.List<DocumentReference>()));
            // engine.SetValue("createArray", new Func<System.Collections.Generic.List<T>, string typeName>(() =>
            // {
            //     Type t = CreateTypeByString(TypeName);
            //     return new System.Collections.Generic.List<DocumentReference>();
            // }));

            
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

            //jsCode = "  "
            //    + "const receipt = { DocumentId : 5, FileName: 'test1' }; "
            //    + "receipt.Items = [];"
            //    + "let a = { Name: 'test' }; "
            //    + "receipt.Items.push(a); "
            //    + "return receipt.Items.length;";

            // Instead of assigning a plain JS array (which can't be converted to List<DocumentReference>),
            // call the CLR helper to create a strongly-typed List<DocumentReference> and assign it.
            jsCode = " "
                     + " qiifDataObject.Test(); var notes = qiifDataObject.createArray('DocumentReference'); return notes.Count;";
                     //+ "var list = createArray('DocumentReference'); list.push({ Iri: 'test' }); list.push({ Iri: 'test2' }); return list.length;";
 //"qiifDataObject.TradeTransaction.Evidence.OtherGroup = createDocumentReferenceArray();";
             

            try
            {
                CreateInstanceAndPruneRecursive();
                ExpandoGraphExample();

                var res = engine.Execute(jsCode);

                var field = typeof(Engine).GetField("_completionValue",
                        BindingFlags.Instance | BindingFlags.NonPublic);


                // Read the raw field value (likely a JsValue)
                var completionJsValue = field.GetValue(engine);

                if (completionJsValue == null)
                    throw new Exception("completionJsValue is null");

                // Convert JsValue → .NET object if possible
                var toObject = completionJsValue.GetType().GetMethod("ToObject", Type.EmptyTypes);
                if (toObject != null)
                {
                    var value = toObject.Invoke(completionJsValue, null);
                    int n = 5;
                    n++;
                }

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

        private static void CreateInstanceAndPruneRecursive()
        {
            var testQiifDataObject = new QiifDataObject();
            testQiifDataObject.InvoiceDocument = new InvoiceDocument();
            testQiifDataObject.InvoiceDocument.DocumentId = new Id { Identifier = "15A"};

            testQiifDataObject.TradeTransaction = new TradeTransaction();
            testQiifDataObject.TradeTransaction.Participant = new Participant();
            testQiifDataObject.TradeTransaction.LineItemGroup = new List<LineItem>();
            testQiifDataObject.TradeTransaction.LineItemGroup.Add(new LineItem { LineId = new Id { Identifier = "lineId1"}});
                
            ObjectGraphInitializer.EnsureCollectionsHaveAtLeastOneItem(testQiifDataObject);
                
            // Prune: removes purely default-like nodes, no marking
            ObjectGraphPruner.PruneDefaults(testQiifDataObject, nullEmptyCollections: true);
        }
    }
}