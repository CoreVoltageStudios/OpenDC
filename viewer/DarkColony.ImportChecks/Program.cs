using DarkColony.Media;
using System.Text.Json;
using DarkColony.ContentImport;
if(args.Length==3&&args[0]=="movie") Console.WriteLine(JsonSerializer.Serialize(AviConverter.Convert(args[1],args[2],CancellationToken.None)));
else if(args.Length==3&&args[0]=="inventory")GameChecks.Inventory(args[1],args[2]);
else if(args.Length==2&&args[0]=="war-startup")WarStartupChecks.Run(args[1]);
else if(args.Length==4&&args[0]=="continuation")GameChecks.Continuation(args[1],args[2],args[3]);
else if(args.Length==4&&args[0]=="recovery")await RecoveryChecks.Run(args[1],args[2],args[3]);
else if(args.Length==4&&args[0]=="lifecycle")await LifecycleChecks.Run(args[1],args[2],args[3]);
else if(args.Length==4&&args[0]=="optional-repair")await OptionalRepairChecks.Run(args[1],args[2],args[3]);
else if(args.Length==2&&args[0]=="clean"){var store=new ContentStore(args[1]);Console.WriteLine(JsonSerializer.Serialize(store.IncompleteImports()));store.CleanInterrupted();Console.WriteLine("CLEAN_OWNED_PASS "+store.IncompleteImports().Length);}
else if(args.Length>=3&&args[0] is "inspect" or "import"){
 var store=new ContentStore(args[1]);ImportStage last=(ImportStage)(-1);string message="";void Progress(ImportProgress p){if(p.Stage!=last||p.Message!=message){Console.WriteLine(JsonSerializer.Serialize(p));last=p.Stage;message=p.Message;}}
 using var plan=await ContentImporter.InspectAsync(store,args.Skip(2).ToArray(),CancellationToken.None,Progress);Console.WriteLine(JsonSerializer.Serialize(plan.Findings));if(args[0]=="import"){var result=await ContentImporter.ImportAsync(plan,true,CancellationToken.None,Progress);Console.WriteLine(JsonSerializer.Serialize(new{active=store.ActiveRoot,files=result.Files.Length,bytes=result.Files.Sum(f=>f.Bytes),warnings=result.Warnings}));}
}else if(args.Length==2&&args[0]=="validate")Console.WriteLine(JsonSerializer.Serialize(new ContentStore(args[1]).Validate(true,CancellationToken.None)));
else throw new ArgumentException("movie source output | inspect/import store source [council-source] | validate store | war-startup store");
