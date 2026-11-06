using DarkColony.ContentImport;
using DarkColony.Original;
using DarkColony.Assets;
using System.Text.Json;
public static class LifecycleChecks {
 public static async Task Run(string storePath,string source,string output){
  var store=new ContentStore(storePath);var before=store.ActiveRoot!;Directory.CreateDirectory(output);var original=new NativeReference(File.ReadAllBytes(Path.Combine(source,"DC16.EXE")));var imported=new NativeReference(File.ReadAllBytes(Path.Combine(before,"private/DC/native-tables.json")));if(original.ContentHash!=imported.ContentHash)throw new Exception("Native normalized fingerprint differs");
  // Mutate only this isolated imported generation, never its original source.
  string target=Path.Combine(before,"private/DC/ANIM.DAT"),backup=Path.Combine(output,"ANIM.DAT.before");File.Copy(target,backup);var bytes=File.ReadAllBytes(target);bytes[^1]^=1;File.WriteAllBytes(target,bytes);if(!store.Validate(false,CancellationToken.None).Contains("private/DC/ANIM.DAT"))throw new Exception("Changed-file quick validation missed corruption");
  using(var p=await ContentImporter.InspectAsync(store,new[]{source},CancellationToken.None)){await ContentImporter.ImportAsync(p,false,CancellationToken.None);}
  if(store.ActiveRoot==before||store.Validate(true,CancellationToken.None).Length!=0||!File.Exists(Path.Combine(store.ActiveRoot!,"private/CouncilWars/odc-content.json")))throw new Exception("Base repair did not preserve expansion");
  string current=store.ActiveRoot!;var council=Path.Combine(current,"private/CouncilWars/ANIM.DAT");var data=File.ReadAllBytes(council);bytes=(byte[])data.Clone();bytes[^1]^=1;File.WriteAllBytes(council,bytes);var errors=store.Validate(false,CancellationToken.None);if(errors.Length!=1||errors[0]!="private/CouncilWars/ANIM.DAT")throw new Exception("Expansion failure not isolated");File.WriteAllBytes(council,data);if(store.Validate(true,CancellationToken.None).Length!=0)throw new Exception("Expansion restoration failed");
  // Existing baseline saves encode source/content identity, not import roots.
  var a=new OriginalDefinitions(source,OriginalScenarioImport.Load(source,"SCENARIO/HUMAN/HUMAN09.SCN"));var bRoot=Path.Combine(current,"private/DC");var b=new OriginalDefinitions(bRoot,OriginalScenarioImport.Load(bRoot,"SCENARIO/HUMAN/HUMAN09.SCN"));var mission=new OriginalMission(a,11);for(int i=0;i<128;i++)mission.Step();var restored=OriginalMission.Restore(b,mission.Snapshot());if(!System.Text.Json.Nodes.JsonNode.DeepEquals(mission.Snapshot(),restored.Snapshot()))throw new Exception("Development save cannot restore with imported content");
  File.WriteAllText(Path.Combine(output,"lifecycle.json"),JsonSerializer.Serialize(new{nativeFingerprint=original.ContentHash,nativeMatches=true,changedFileDetected=true,baseRepaired=true,councilRetained=true,expansionCorruptionIsolated=true,existingSourceSaveCompatible=true,previousGenerationPreserved=Directory.Exists(before),active=store.ActiveRoot}));Console.WriteLine("LIFECYCLE_PASS");
 }
}
