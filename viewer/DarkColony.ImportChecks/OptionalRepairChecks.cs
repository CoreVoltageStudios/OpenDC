using DarkColony.ContentImport;
using System.Diagnostics;
using System.Text.Json;

public static class OptionalRepairChecks {
 public static async Task Run(string referenceStore,string source,string output){
  // Copy an isolated imported library; never mutate the reference generation or sources.
  if(Directory.Exists(output))throw new IOException("Choose a fresh optional-repair output");
  Directory.CreateDirectory(output);var reference=new ContentStore(referenceStore);string original=reference.ActiveRoot!;
  string storePath=Path.Combine(output,"store"),id=Guid.NewGuid().ToString("N"),before=Path.Combine(storePath,"generations",id);
  Directory.CreateDirectory(before);
  foreach(string file in Directory.EnumerateFiles(original,"*",SearchOption.AllDirectories)){string dest=Path.Combine(before,Path.GetRelativePath(original,file));Directory.CreateDirectory(Path.GetDirectoryName(dest)!);File.Copy(file,dest);}
  File.WriteAllText(Path.Combine(storePath,"active.json"),JsonSerializer.Serialize(new{generation=id,contract=1}));
  var store=new ContentStore(storePath);var old=store.Receipt!;
  var music=old.Files.Where(r=>r.Path.StartsWith("generated/original-music/")).ToArray();
  if(music.Length!=4||old.CouncilEdition==null)throw new Exception("Fixture requires full base music and Council Wars");
  string broken=Path.Combine(before,"private/CouncilWars/ANIM.DAT");var bytes=File.ReadAllBytes(broken);bytes[^1]^=1;File.WriteAllBytes(broken,bytes);
  string brokenHash=ContentContract.Hash(broken);var timer=Stopwatch.StartNew();
  using(var plan=await ContentImporter.InspectAsync(store,new[]{source},CancellationToken.None)){await ContentImporter.ImportAsync(plan,true,CancellationToken.None);}
  var receipt=store.Receipt!;string active=store.ActiveRoot!;
  if(active==before||receipt.CouncilEdition!=null||Directory.Exists(Path.Combine(active,"private/CouncilWars"))||store.Validate(true,CancellationToken.None).Length!=0)throw new Exception("Base repair remained dependent on damaged optional expansion");
  foreach(var m in music)if(ContentContract.Hash(Path.Combine(active,m.Path))!=m.Sha256)throw new Exception("Valid CD music lost during data-only repair");
  if(!Directory.Exists(before)||ContentContract.Hash(broken)!=brokenHash||reference.ActiveRoot!=original||reference.Validate(true,CancellationToken.None).Length!=0)throw new Exception("Prior generation or reference changed");
  File.WriteAllText(Path.Combine(output,"optional-repair.json"),JsonSerializer.Serialize(new{baseRepairWithDamagedCouncil=true,unavailableCouncilExplained=receipt.Warnings.Any(w=>w.Contains("Council Wars needs repair")),validMusicRetained=music.Length,previousGenerationRetained=true,referenceUnchanged=true,seconds=timer.Elapsed.TotalSeconds,bytes=receipt.Files.Sum(f=>f.Bytes),active}));
  Console.WriteLine("OPTIONAL_REPAIR_PASS");
 }
}
