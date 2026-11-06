using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 object PresentationResources()=>new {
  sharedEntries=nativeSharedArt.Count,sharedBytes=nativeSharedBytes,
  bodies=nativeBodies.Count,poses=nativePoseFrames.Values.Sum(v=>v.Count),missiles=nativeMissileTextures.Count,
  terrain=nativeTerrain.Count,samplingLayouts=nativeSamplingLayout.Count,interfaceTextures=interfaceTextures.Count,
  retiringAudio=retiringMissionAudio.Count,diseaseLoops=diseaseAudio.Count,spriteBanks=nativeData.Sprites.Count,
  sourceBytes=nativeData.Sources.Sum(s=>(long)s.Bytes.Length),events=nativeWorld?.Events.Count??0,
  audioCues=nativeWorld?.AudioCues.Count??0,
  godotResources=Godot.Performance.GetMonitor(Godot.Performance.Monitor.ObjectResourceCount),
  textureBytes=Godot.Performance.GetMonitor(Godot.Performance.Monitor.RenderTextureMemUsed)
 };
 async Task VerifyPresentationLifetime(){
  missionHarnessManual=true;var rows=new List<object>();var replaced=new List<WeakReference>();
  async Task Record(string stage){
   for(int i=0;i<15;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   DrainRetiredMissionAudio();var process=System.Diagnostics.Process.GetCurrentProcess();
   rows.Add(new{stage,privateBytes=process.PrivateMemorySize64,managedBytes=GC.GetTotalMemory(false),resources=PresentationResources(),replacedDefinitionsAlive=replaced.Count(r=>r.IsAlive)});
   File.WriteAllText(Path.Combine(output,"presentation-lifetime.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
  }
  await Record("developed");
  var authoritative=mission.Snapshot();TraceMissionReinforcements();DispatchMissionSounds();
  // Controlled consumed-history fixture; not gameplay/performance samples.
  while(nativeWorld.Events.Count<8200)nativeWorld.Events.Add(new(nativeWorld.Tick,"history-fixture",-1,-1,0,0));
  reinforcementWorldCursor=nativeWorld.Events.Count;TrimPresentationHistory(true);
  if(nativeWorld.Events.Count!=4096||reinforcementWorldCursor!=4096)throw new InvalidOperationException("Consumed history did not compact with its cursor");
  if(!System.Text.Json.Nodes.JsonNode.DeepEquals(authoritative,mission.Snapshot()))throw new InvalidOperationException("Presentation history changed authoritative state");
  await Record("history-compacted");ResetFirstStrike();await Record("restart");
  int selectedMap=mission.CampaignIndex+1,replacement=0;
  var scenarios=IsHuman?new[]{"SCENARIO/HUMAN/HUMAN01.SCN","SCENARIO/ALIEN/ALIEN02.SCN","SCENARIO/HUMAN/HUMAN05.SCN"}:(selectedMap>=7?new[]{2,4,selectedMap}:new[]{2,4}).Select(map=>$"SCENARIO/ALIEN/ALIEN{map:00}.SCN").ToArray();
  for(int cycle=0;cycle<3;cycle++)foreach(string scenario in scenarios){
   if(replacement++>0)replaced.Add(new WeakReference(nativeData));
   string map=Path.GetFileNameWithoutExtension(scenario);
   var data=PersistenceData(scenario);InstallMission(data,new(data,0),false);
   for(int i=0;i<120;i++){mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();}
   QueueRedraw();await Record($"replace-{cycle}-a{map}");ReturnMainMenu();
   await Record($"menu-{cycle}-a{map}");
   if(nativeSharedArt.Count!=0||nativeBodies.Count!=0||nativeTerrain.Count!=0||nativeMissileTextures.Count!=0||retiringMissionAudio.Count!=0||diseaseAudio.Count!=0)
    throw new InvalidOperationException("Mission presentation resources retained after returning to menu");
   // Diagnostic collection outside timed gameplay distinguishes live retention
   // from allocator high-water marks. Never force GC during play.
   GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Record($"collected-{cycle}-a{map}");
   if(replaced.Any(r=>r.IsAlive))throw new InvalidOperationException("Replaced mission definitions retained after diagnostic collection");
  }
 }
}
