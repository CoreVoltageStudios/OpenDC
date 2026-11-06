using DarkColony.Assets;
using DarkColony.Original;
using DarkColony.ContentImport;
using System.Text.Json;
using System.Text.Json.Nodes;
public static class GameChecks {
 public static void Inventory(string storePath,string output){
  var store=new ContentStore(storePath);string root=store.ActiveRoot!;Directory.CreateDirectory(output);var rows=new List<object>();
  foreach(string campaign in new[]{"taar","human","cw-taar","cw-human","training-taar","training-human"}){
   string data=OriginalContent.Root(root,campaign);if(!Directory.Exists(data))continue;
   foreach(var entry in OriginalCampaign.Catalog(data,campaign)){
    var definitions=new OriginalDefinitions(data,OriginalScenarioImport.Load(data,entry.Scenario));var mission=new OriginalMission(definitions,7);for(int i=0;i<64;i++)mission.Step();var snap=mission.Snapshot();var restored=OriginalMission.Restore(definitions,snap);if(!JsonNode.DeepEquals(snap,restored.Snapshot()))throw new Exception("Restore "+entry.Scenario);
    foreach(string movie in new[]{entry.VictoryMovie,entry.DefeatMovie}.Where(s=>s!="0"&&Path.GetFileName(s).ToUpperInvariant()!="PP.AVI")){string path=Path.Combine(root,OriginalCampaign.IsCouncil(campaign)?"generated/council-wars-first/video":"generated/original-video/v1",Path.GetFileNameWithoutExtension(movie).ToLowerInvariant()+".odcv");if(!File.Exists(path))throw new FileNotFoundException("Required outcome movie "+movie,path);}
    rows.Add(new{campaign,entry.Scenario,entry.Title,content=SaveCodec.Content(definitions),tick=mission.World.Tick,restore=true});Console.WriteLine("INIT "+entry.Scenario);
   }
  }
  foreach(var map in WarCatalog.Read(Path.Combine(root,"private/DC"))){var config=WarConfiguration.Default(map.Id,7);var data=config.Load(Path.Combine(root,"private/DC"));var m=new OriginalMission(data,7,war:config);for(int i=0;i<8;i++)m.Step();OriginalMission.Restore(data,m.Snapshot());rows.Add(new{campaign="war",map.Id,map.Capacity,tick=m.World.Tick,restore=true});Console.WriteLine("INIT "+map.Id);}
  File.WriteAllText(Path.Combine(output,"inventory.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
 }
 public static void Continuation(string mode,string storePath,string folder){string root=new ContentStore(storePath).ActiveRoot!;Directory.CreateDirectory(folder);
  foreach(var item in new[]{("taar","SCENARIO/ALIEN/ALIEN04.SCN"),("human","SCENARIO/HUMAN/HUMAN09.SCN"),("cw-human","SCENARIO/AEROGEN/AERO08.SCN"),("cw-taar","SCENARIO/COUNCIL/COUN01.SCN"),("training-human","SCENARIO/TEST/HTRAIN6.SCN"),("training-taar","SCENARIO/TEST/ATRAIN7.SCN"),("war","D4PLAY04")}){
   string dataRoot=OriginalContent.Root(root,item.Item1);var config=item.Item1=="war"?WarConfiguration.Default(item.Item2,19):null;var data=config?.Load(dataRoot)??new OriginalDefinitions(dataRoot,OriginalScenarioImport.Load(dataRoot,item.Item2));string prefix=Path.Combine(folder,item.Item1);
   if(mode=="write"){var m=new OriginalMission(data,19,war:config);for(int i=0;i<128;i++)m.Step();SaveCodec.Write(prefix+"-save.json",m.Snapshot());m.Events.Clear();m.World.Events.Clear();for(int i=0;i<384;i++)m.Step();File.WriteAllText(prefix+"-expected.json",m.Snapshot().ToJsonString());File.WriteAllText(prefix+"-events.json",JsonSerializer.Serialize(new{mission=m.Events,world=m.World.Events}));}
   else{var m=OriginalMission.Restore(data,SaveCodec.Open(prefix+"-save.json")["payload"]!.AsObject());for(int i=0;i<384;i++)m.Step();if(!JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(prefix+"-expected.json")),m.Snapshot())||File.ReadAllText(prefix+"-events.json")!=JsonSerializer.Serialize(new{mission=m.Events,world=m.World.Events}))throw new Exception("Cross-process divergence "+item.Item1);Console.WriteLine("CONTINUATION_PASS "+item.Item1);}
  }
 }
}
