using System.Text.Json;
namespace DarkColony.Original;

// Content identity is independent of faction and save-envelope versions. War
// always uses Base. Development preparation is offline; this never imports data.
public static class OriginalContent {
 public static string? UnavailableImportedCouncil {get;set;}
 public const string Base="dark-colony",Council="council-wars";
 public static bool CouncilScenario(string scenario)=>scenario.Replace('\\','/').ToUpperInvariant() is var s&&(s.StartsWith("SCENARIO/AEROGEN/")||s.StartsWith("SCENARIO/COUNCIL/"));
 public static string ForScenario(string scenario)=>CouncilScenario(scenario)?Council:Base;
 public static int CounterCount(string scenario)=>CouncilScenario(scenario)?130:110;
 public static string Root(string repository,string campaign)=>OriginalCampaign.IsCouncil(campaign)?(File.Exists(Path.Combine(repository,"content-manifest.json"))?null:Environment.GetEnvironmentVariable("ODC_COUNCIL_CONTENT"))??Path.Combine(repository,"private/CouncilWars"):Path.Combine(repository,"private/DC");
 public static bool Available(string repository,out string reason){
  try {ValidateCouncil(Root(repository,"cw-taar"));reason="";return true;}
  catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException){reason="Council Wars content is unavailable. Prepare the optional expansion content to play.";return false;}
 }
 public static void ValidateCouncil(string root){
  if(UnavailableImportedCouncil!=null&&Path.GetFullPath(root).Equals(UnavailableImportedCouncil,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Council Wars content needs repair in Options > Game content. Base-game play remains available.");
  string path=Path.Combine(root,"odc-content.json");
  if(!File.Exists(path))throw new FileNotFoundException("This save or mission requires Council Wars content. Base-game play remains available.",path);
  using var doc=JsonDocument.Parse(File.ReadAllText(path));var n=doc.RootElement;
  if(n.ValueKind!=JsonValueKind.Object||!n.TryGetProperty("pack",out var pack)||pack.ValueKind!=JsonValueKind.String||pack.GetString()!=Council||!n.TryGetProperty("revision",out var revision)||revision.ValueKind!=JsonValueKind.Number||!revision.TryGetInt32(out int r)||r!=1||!n.TryGetProperty("native",out var native)||native.ValueKind!=JsonValueKind.String||native.GetString()!=NativeReference.CouncilHash)throw new InvalidDataException("Unsupported Council Wars content identity");
  foreach(string file in new[]{"native-tables.json","GAMESTAT/HXSCENE.TXT","GAMESTAT/GXSCENE.TXT","SCENARIO/AEROGEN/AERO01.SCN","SCENARIO/COUNCIL/COUN01.SCN"})if(!File.Exists(Path.Combine(root,file)))throw new FileNotFoundException("Council Wars content is incomplete: "+file);
 }
}
