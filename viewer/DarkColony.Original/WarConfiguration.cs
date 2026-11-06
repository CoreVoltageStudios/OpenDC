using DarkColony.Assets;
namespace DarkColony.Original;

public enum WarController { Ai, AiPlus, Local, None, Remote }
public sealed record WarSlot(WarController Controller,int Race,string Name,int Color,int Team);
public sealed record WarOptions(int Storage=0,int Artifacts=0,bool Erupting=true,bool Renewable=false,
 int Quantity=4,int Flow=4,int CommanderRank=0);
public sealed record WarMap(string Id,string Scenario,string Title,string Terrain,int Capacity);

// A concrete offline match contract. The same validation/initialization serves UI,
// diagnostic entry, restart and restoration; no transport or campaign profile here.
public sealed record WarConfiguration(string MapId,WarSlot[] Slots,WarOptions Options,int Seed) {
 public const int Revision=1;
 [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
 public bool Multiplayer {get;init;}
 public static WarConfiguration Default(string map="D2PLAY01",int seed=0)=>new(map,
  Enumerable.Range(0,8).Select(i=>new WarSlot(i==0?WarController.Local:i==1?WarController.Ai:WarController.None,
   i%2,i==0?"Player":$"Computer {i+1}",i,i)).ToArray(),new(),seed);
 public WarMap Validate(string root) {
  var map=WarCatalog.Find(root,MapId);
  if(Slots==null||Slots.Length!=8||Slots.Any(s=>s==null))throw new InvalidDataException("A War match needs eight player slots.");
  if(!Multiplayer&&Slots.Any(s=>s.Controller==WarController.Remote))throw new InvalidDataException("Remote players require a network session.");
  if(Slots.Count(s=>s.Controller==WarController.Local)!=1)throw new InvalidDataException("Choose exactly one local player.");
  if(Slots.Count(s=>s.Controller!=WarController.None)>map.Capacity)throw new InvalidDataException($"This map supports at most {map.Capacity} active players.");
  foreach(var s in Slots)if(!Enum.IsDefined(s.Controller)||s.Race is <0 or >1||s.Color is <0 or >7||s.Team is <0 or >7||string.IsNullOrWhiteSpace(s.Name)||s.Name.Length>16||s.Name.Any(char.IsControl))throw new InvalidDataException("Player names must contain 1–16 characters; faction, colour and team must be valid.");
  var active=Slots.Where(s=>s.Controller!=WarController.None).ToArray();
  if(active.Select(s=>s.Color).Distinct().Count()!=active.Length)throw new InvalidDataException("Each active player needs a different colour.");
  if(Options==null||Options.Storage is <0 or >3||Options.Artifacts is <0 or >3||Options.Quantity is <1 or >20||Options.Flow is <1 or >20||Options.CommanderRank is <0 or >3)throw new InvalidDataException("Match options are outside their supported ranges.");
  return map;
 }
 public WarConfiguration Copy()=>this with{Slots=Slots.ToArray()};
 public int[] AssignStarts(NativeReference reference,int capacity){
  var starts=Slots.Select((s,i)=>(s,i)).Where(p=>p.s.Controller!=WarController.None).Select(p=>p.i).Concat(Enumerable.Repeat(-1,8)).Take(8).ToArray();
  var random=new NativeRandom(reference,Seed);
  //40156b: forward Fisher-Yates, including empty positions and final draw.
  for(int i=0;i<capacity;i++){int j=i+random.Next()%(capacity-i);(starts[i],starts[j])=(starts[j],starts[i]);}
  return starts;
 }
 public OriginalDefinitions Load(string root,int[]? restoredStarts=null){
  var map=Validate(root);var reference=new NativeReference(File.ReadAllBytes(Path.Combine(root,File.Exists(Path.Combine(root,"native-tables.json"))?"native-tables.json":"DC16.EXE")));
  var starts=AssignStarts(reference,map.Capacity);
  if(restoredStarts!=null&&!starts.SequenceEqual(restoredStarts))throw new InvalidDataException("Saved starting-position assignment does not match the match seed.");
  var overrides=new ScenarioMatchOverrides(starts.Select(i=>i<0?0:Slots[i].Race).ToArray(),starts.Select(i=>i<0?4:Slots[i].Controller is WarController.Local or WarController.Remote?0:3).ToArray(),
   starts.Select(i=>i<0?0:Slots[i].Color).ToArray(),starts.Select(i=>i>=0).ToArray(),Options.Flow*64,Options.Quantity*64,Options.Artifacts);
  var data=new OriginalDefinitions(root,OriginalScenarioImport.Load(root,map.Scenario,overrides));data.PrepareWarAssets();return data;
 }
}

public static class WarCatalog {
 static readonly Dictionary<string,WarMap[]> catalogs=new(StringComparer.OrdinalIgnoreCase);
 public static WarMap[] Read(string root){string path=Path.GetFullPath(root);lock(catalogs){if(!catalogs.TryGetValue(path,out var maps))catalogs[path]=maps=ReadFiles(path);return maps;}}
 public static bool IsWar(string scenario)=>scenario.Replace('\\','/').ToUpperInvariant().StartsWith("SCENARIO/MPLAYER/");
 static WarMap[] ReadFiles(string root)=>Directory.EnumerateFiles(Path.Combine(root,"SCENARIO/MPLAYER"))
  .Where(p=>Path.GetExtension(p).Equals(".SCN",StringComparison.OrdinalIgnoreCase)).Select(p=>{
   var rows=OriginalScenarioImport.Lines(File.ReadAllBytes(p),"War SCN");var n=rows[3].Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
   int capacity=int.Parse(n[1]);if(capacity is not (2 or 4 or 6 or 8))throw new InvalidDataException("Unsupported War map capacity");
   string terrain=rows[0].Text.ToUpperInvariant().Replace(".BTS","");
   return new WarMap(Path.GetFileNameWithoutExtension(p).ToUpperInvariant(),"SCENARIO/MPLAYER/"+Path.GetFileName(p).ToUpperInvariant(),rows[2].Text,terrain,capacity);
  }).OrderBy(m=>m.Title,StringComparer.OrdinalIgnoreCase).ToArray();
 public static WarMap Find(string root,string id)=>Read(root).SingleOrDefault(m=>m.Id.Equals(id,StringComparison.OrdinalIgnoreCase))??throw new InvalidDataException("This War map is unavailable.");
}
