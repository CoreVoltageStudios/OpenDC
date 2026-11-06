using Godot;
using System.Diagnostics;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyMenuPerformance(){
  if(IsolatedPlayerData==null)throw new InvalidOperationException("Isolated profile required");
  missionHarnessManual=true;for(int t=0;t<600;t++){mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();}
  for(int i=0;i<4;i++)SaveMissionSlot(i);missionPanel="";
  var samples=new List<object>();
  foreach(bool cache in new[]{false,true}){cacheSaveCatalog=cache;for(int pass=0;pass<3;pass++){long start=Stopwatch.GetTimestamp(),bytes=GC.GetAllocatedBytesForCurrentThread();SaveMissionSlot(0);samples.Add(new{cache,action="save",pass,ms=PerfElapsed(start),allocated=GC.GetAllocatedBytesForCurrentThread()-bytes});}}
  SaveMissionSlot(3);var expected=mission.Snapshot();ReturnMainMenu();
  foreach(bool cache in new[]{false,true}){
   cacheSaveCatalog=cache;saveCatalogCache.Clear();
   foreach(string panel in new[]{"load","campaign","load","campaign"}){
    long begin=Stopwatch.GetTimestamp(),allocated=GC.GetAllocatedBytesForCurrentThread();PersistenceAction(panel);
    samples.Add(new{cache,panel,ms=PerfElapsed(begin),allocated=GC.GetAllocatedBytesForCurrentThread()-allocated,continueSlot});
    await MenuCapture($"catalog-{cache}-{samples.Count}");CloseRelayPanel();
   }
  }
  Check(continueSlot==3,"Catalog continues most recent real compatible save");
  // Same length and mtime edits must invalidate, not just overwrite/rename events.
  string path=SavePath(3);byte[] original=File.ReadAllBytes(path);var stamp=File.GetLastWriteTimeUtc(path);
  byte[] corrupt=(byte[])original.Clone();corrupt[0]=(byte)'!';File.WriteAllBytes(path,corrupt);File.SetLastWriteTimeUtc(path,stamp);RefreshPersistenceCatalog();
  Check(saveInvalid[3]&&continueSlot!=3,"Changed contents invalidate cached eligibility even with preserved length/mtime");
  File.WriteAllBytes(path,original);File.SetLastWriteTimeUtc(path,stamp);RefreshPersistenceCatalog();Check(!saveInvalid[3]&&continueSlot==3,"Compatible restored file revalidates without resetting progress");
  var loaded=ReadMissionSlot(3);Check(System.Text.Json.Nodes.JsonNode.DeepEquals(loaded.Item2.Snapshot(),expected),"Actual load still restores complete saved mission");
  File.WriteAllText(Path.Combine(output,"menu-performance.json"),JsonSerializer.Serialize(samples,new JsonSerializerOptions{WriteIndented=true}));
 }
}
