using DarkColony.Assets;
namespace DarkColony.Original;

public sealed record OriginalCampaignMission(int Index,string Scenario,string Title,string Region,string TextStem,string VictoryMovie,string DefeatMovie,int[] RawParameters,int RawFlag,int[] MusicTracks) {
 // Every GSCENE record is catalogued; only this build's bounded set can be launched,
 // unlocked, continued into or packaged. Progress never makes a later record playable.
 public string CampaignId=>OriginalCampaign.Identity(Scenario);
 public bool Implemented=>OriginalCampaign.Implemented(CampaignId,Index);
}
// Authoritative Taar mission catalog. Campaign start, Continue, direct launches and
// packaging resolve scenario paths here instead of formatting file names themselves.
public static class OriginalCampaign {
 public const int SupportedTaarMissions=15;
 public const int SupportedHumanMissions=15;
 public static bool Implemented(string campaign,int index)=>index>=0&&index<SupportedCount(campaign);
 public static bool IsCouncil(string campaign)=>campaign is "cw-human" or "cw-taar";
 public static bool IsTraining(string campaign)=>campaign is "training-human" or "training-taar";
 public static bool IsHuman(string campaign)=>campaign is "human" or "cw-human" or "training-human";
 public static int CatalogCount(string campaign)=>IsTraining(campaign)?7:IsCouncil(campaign)?8:15;
 public static int SupportedCount(string campaign)=>campaign switch{"taar"=>SupportedTaarMissions,"human"=>SupportedHumanMissions,"cw-human"=>8,"cw-taar"=>8,"training-human" or "training-taar"=>7,_=>throw new InvalidDataException("Unknown campaign")};
 public static string Identity(string scenario)=>Canonical(scenario) is var s&&s.StartsWith("SCENARIO/ALIEN/")?"taar":s.StartsWith("SCENARIO/HUMAN/")?"human":s.StartsWith("SCENARIO/AEROGEN/")?"cw-human":s.StartsWith("SCENARIO/COUNCIL/")?"cw-taar":s.StartsWith("SCENARIO/TEST/HTRAIN")?"training-human":s.StartsWith("SCENARIO/TEST/ATRAIN")?"training-taar":throw new InvalidDataException("Unknown campaign scenario");
 public static string Label(string campaign)=>(IsTraining(campaign)?"Training · ":IsCouncil(campaign)?"Council Wars · ":"")+(IsHuman(campaign)?"Human":"Taar");
 static string CatalogPath(string campaign)=>"GAMESTAT/"+(campaign switch{"taar"=>"GSCENE","human"=>"HSCENE","cw-human"=>"HXSCENE","cw-taar"=>"GXSCENE","training-human"=>"HTSCENE","training-taar"=>"GTSCENE",_=>throw new InvalidDataException("Unknown campaign")})+".TXT";
 public static OriginalCampaignMission[] For(OriginalDefinitions data)=>Identity(data.Scenario.Scenario) is var campaign&&campaign=="taar"?Taar(data):Parse(data.Read(CatalogPath(campaign)),campaign);
 public static OriginalCampaignMission[] Catalog(string root,string campaign)=>campaign=="taar"?Taar(root):Parse(File.ReadAllBytes(Path.Combine(root,CatalogPath(campaign))),campaign);
 // Saves, persistence migrations and package manifests use these exact identities.
 static readonly string[] implemented=Enumerable.Range(1,SupportedTaarMissions).Select(n=>$"SCENARIO/ALIEN/ALIEN{n:00}.SCN").ToArray();
 public static bool Supported(string scenario)=>Enumerable.Range(1,7).Any(n=>scenario==$"SCENARIO/TEST/HTRAIN{n}.SCN"||scenario==$"SCENARIO/TEST/ATRAIN{n}.SCN")||implemented.Contains(scenario)||Enumerable.Range(1,SupportedHumanMissions).Any(n=>scenario==$"SCENARIO/HUMAN/HUMAN{n:00}.SCN")||Enumerable.Range(1,SupportedCount("cw-human")).Any(n=>scenario==$"SCENARIO/AEROGEN/AERO{n:00}.SCN")||Enumerable.Range(1,SupportedCount("cw-taar")).Any(n=>scenario==$"SCENARIO/COUNCIL/COUN{n:00}.SCN");
 public static bool HasEconomy(OriginalDefinitions data)=>data.Scenario.Entities.Any(e=>e.Kind=="city"&&e.Owner==0);
 // GSCENE spells paths relative to SCENARIO/ in lower case; the canonical form is upper case.
 public static string Canonical(string scenario)=>scenario.Replace('\\','/').ToUpperInvariant();
 // Closed test.5 private bundles predate campaign metadata. Preserve that exact
 // A01 package contract without requiring a development checkout or more content.
 static OriginalCampaignMission[] FirstStrikeOnly=>new[]{new OriginalCampaignMission(0,"SCENARIO/ALIEN/ALIEN01.SCN","FIRST STRIKE","CYDONIA REGION","SCENARIO/ALIEN/ALIEN01","AVI/AVHD4.AVI","AVI/HVAD2.AVI",new[]{5,160,60},0,new[]{2})};
 // GSCENE's record order is campaign order (429a6c), independent of file numbering.
 public static OriginalCampaignMission[] Taar(OriginalDefinitions data){
  if(!File.Exists(Path.Combine(data.DataRoot,"GAMESTAT/GSCENE.TXT"))&&data.Scenario.Scenario.EndsWith("ALIEN01.SCN",StringComparison.OrdinalIgnoreCase))return FirstStrikeOnly;
  return Parse(data.Read("GAMESTAT/GSCENE.TXT"));
 }
 // Catalog before any scenario is imported (direct launch selection, packaging).
 public static OriginalCampaignMission[] Taar(string dataRoot){
  string path=Path.Combine(dataRoot,"GAMESTAT/GSCENE.TXT");
  return File.Exists(path)?Parse(File.ReadAllBytes(path)):FirstStrikeOnly;
 }
 // The single gate for starting or continuing into a campaign index.
 public static OriginalCampaignMission Playable(OriginalCampaignMission[] catalog,int index)=>
  index>=0&&index<catalog.Length&&catalog[index].Implemented?catalog[index]:throw new InvalidDataException("This mission is not available in this build");
 static OriginalCampaignMission[] Parse(byte[] gscene,string campaign="taar"){
  var lines=OriginalScenarioImport.Lines(gscene,"GSCENE").Select(l=>l.Text).ToArray();
  int count=int.Parse(lines[8]),at=9;var missions=new List<OriginalCampaignMission>();
  for(int index=0;index<count;index++){
   string scenario=Canonical("SCENARIO/"+lines[at++]),title=lines[at++],region=lines[at++],stem=lines[at++],win=lines[at++],lose=lines[at++];
   int[] numbers=lines[at++].Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();int flag=int.Parse(lines[at++]);
   int[] music=lines[at++].Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).TakeWhile(t=>t!=-1).ToArray();
   missions.Add(new(index,scenario,title,region,stem,win,lose,numbers,flag,music));
  }
  // The implemented set must be the leading GSCENE records, in order.
  if(missions.Count!=CatalogCount(campaign)||missions.Select(m=>m.Scenario).Where((s,i)=>s!=$"SCENARIO/{(campaign switch{"human"=>"HUMAN/HUMAN","taar"=>"ALIEN/ALIEN","cw-human"=>"AEROGEN/AERO","cw-taar"=>"COUNCIL/COUN","training-human"=>"TEST/HTRAIN","training-taar"=>"TEST/ATRAIN",_=>""})}{(i+1).ToString(IsTraining(campaign)?"0":"00")}.SCN").Any())
   throw new InvalidDataException("GSCENE campaign order disagrees with this build's implemented missions");
  return missions.ToArray();
 }
}
