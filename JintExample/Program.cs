using Jint;
using JsonQiifConverterGenerated.Qiif;
using System.Reflection;

namespace JsInteropDemo
{
    class Program
    {
        private static dynamic ExpandoGraphArrayOnlyExample(QiifDataObject qiifDataObject)
        {
            dynamic dyn = ExpandoWithListActions.ToExpando(qiifDataObject);
            return dyn;
        }
 
        static void SetTypesToJSEngine(Engine engine, QiifDataObject qiifDataObject)
        {
            var typesInQiifDataObject = new HashSet<Type>();
            AssemblyFinder.RecursiveFindAllTypes(qiifDataObject, ref typesInQiifDataObject);
            
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
        }
        
        static void FillQiifDataObjectExample(QiifDataObject obj)
        {
            obj.TradeTransaction = new TradeTransaction();
            obj.TradeTransaction.Evidence = new Evidence();
        }
        
        static void ExampleCall()
        {
            var jsCode = " "
                     + " qiifDataObjectDynamic.TradeTransaction.Evidence.CreateArrayOtherGroup(); " 
                     + " let docRef = new DocumentReference(); docRef.Iri = 'test';" 
                     + " qiifDataObjectDynamic.TradeTransaction.Evidence.OtherGroup.push(docRef); "
                     + " qiifDataObjectDynamic.ProcessControl = new ProcessControl(); "
                     + " qiifDataObjectDynamic.ProcessControl.BusinessProcessId = { 'Identifier' : 'bussinessProcessIdD-Identifier'}; "
                     + " return qiifDataObjectDynamic.TradeTransaction.Evidence.OtherGroup.length;";
            
            var qiifDataObject = new QiifDataObject();
            FillQiifDataObjectExample(qiifDataObject);
            
            var backend = new BackendService();
            var engine = new Engine(options =>
                options.Strict(true)
                    .LimitMemory(64_000_000)     // 64 MB
                    .TimeoutInterval(TimeSpan.FromSeconds(20000))
                    .AllowClr()                   // povolí CLR přístup (nutné pro TypeReference)
                    .DebugMode());
            SetTypesToJSEngine(engine, qiifDataObject);
            
            engine.SetValue("backend", backend);
            
            // Wrap the CLR instance in a dynamic ExpandoObject so we can add JS-callable methods at runtime
            var qiifWrapperDynamic = ExpandoGraphArrayOnlyExample(qiifDataObject);
            engine.SetValue("qiifDataObjectDynamic", qiifWrapperDynamic);

            // Simple JS code to manipulate the object and call backend methods
            var res = engine.Execute(jsCode);

            // Access the internal _completionValue field of the Engine to see the raw result of JS execution
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
                var scriptResult = toObject.Invoke(completionJsValue, null);
                Console.WriteLine($"Script result: {scriptResult}");

                QiifDataObject result = new QiifDataObject();
                ExpandoBackMapper.UpdateObjectFromExpando(result, qiifWrapperDynamic);
            }
            
            engine.Dispose();
            engine = null;
        }
        
        static void Main()
        {
            ExampleCall();
        }
    }
}