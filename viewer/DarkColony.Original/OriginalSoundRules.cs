using DarkColony.Assets;
namespace DarkColony.Original;

// Presentation state only: this CRT stream must never consume the world's table RNG.
// 431480,431f44,432168,4322c4,432458,44e57d. Explicit seed, not a campaign-seed claim.
public sealed class OriginalSoundRules {
 sealed class Category {public int[] Sounds=Array.Empty<int>();public int Cursor,Priority=200;}
 readonly Dictionary<(int,string),Category> lists=new();
 readonly Dictionary<(int,int),Category> ambient=new();
 readonly Dictionary<string,(int Priority,int Sound)> queued=new();
 uint random;long musicPoll,ambientPoll;
 public OriginalSoundRules(byte[] slist,byte[] ambience,uint seed) {
  random=seed;
  foreach(var line in OriginalScenarioImport.Lines(slist,"SLIST")) {
   var p=line.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);int end=Array.IndexOf(p,"-1");
   if(end<2)throw new InvalidDataException("SLIST terminator");
   lists[(int.Parse(p[0]),p[1])]=new(){Sounds=p[2..end].Select(int.Parse).ToArray(),Priority=p[1] is "ACK" or "SEL"?int.Parse(p[end+1]):200};
  }
  foreach(var line in OriginalScenarioImport.Lines(ambience,"AMB")) {
   var p=line.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
   if(p.Length<3||p[^1]!=-1)throw new InvalidDataException("AMB terminator");
   ambient[(p[0],p[1])]=new(){Sounds=p[2..^1]};
  }
 }
 public int NextCrt(){random=unchecked(random*0x41c64e6d+0x3039);return (int)((random>>16)&32767);}
 int? Immediate(Category? category) {
  if(category==null||category.Sounds.Length==0)return null;
  int id=category.Sounds[category.Cursor];category.Cursor=NextCrt()%category.Sounds.Length;return id;
 }
 public int? Immediate(int id,string kind)=>Immediate(lists.GetValueOrDefault((id,kind)));
 public void Queue(int type,string kind) {
  if(kind is not("SEL" or "ACK"))throw new ArgumentException("Only SEL/ACK are queued");
  if(!lists.TryGetValue((type,kind),out var category)||category.Sounds.Length==0)return;
  if(category.Priority>=queued.GetValueOrDefault(kind,(200,0)).Item1)return;
  int old=category.Cursor;queued[kind]=(category.Priority,category.Sounds[old]);
  do{category.Cursor=NextCrt()%category.Sounds.Length;}while(category.Sounds.Length>1&&category.Cursor==old);
 }
 public int[] Flush() {
  var result=new List<int>();foreach(string kind in new[]{"SEL","ACK"})if(queued.Remove(kind,out var entry))result.Add(entry.Sound);return result.ToArray();
 }
 public int? Ambient(long milliseconds,int phase,int terrainType) {
  // Two independent raw-wall timers; >5000 branch does not update the >7000 timer.
  if(milliseconds-musicPoll>5000)musicPoll=milliseconds;
  else if(milliseconds-ambientPoll>7000)ambientPoll=milliseconds;
  else return null;
  return Immediate(ambient.GetValueOrDefault((phase,terrainType)));
 }
 public static Dictionary<int,int> TerrainTypes(MapAsset map) {
  // 4538b2..45396a: count MAP attribute>>10 for each remapped tile, first maximum.
  var histogram=new Dictionary<int,ushort[]>();
  foreach(var cell in map.Cells)foreach(int id in cell.ForegroundId==0?new[]{(int)cell.BackgroundId}:new[]{(int)cell.BackgroundId,cell.ForegroundId}) {
   if(!histogram.TryGetValue(id,out var counts))histogram[id]=counts=new ushort[32];
   int type=cell.Attributes>>10;if(type>=32)throw new InvalidDataException("Unestablished ambient terrain type");counts[type]++;
  }
  return histogram.ToDictionary(p=>p.Key,p=>Array.IndexOf(p.Value,p.Value.Max()));
 }
 public static int DominantTerrain(IEnumerable<int> types) {
  var counts=new int[32];int total=0;foreach(int type in types){counts[type]++;total++;}
  if(total==0)return -1;counts[1]/=4;return Array.IndexOf(counts,counts.Max());
 }
}
