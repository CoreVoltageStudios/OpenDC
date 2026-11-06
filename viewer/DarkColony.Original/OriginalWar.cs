using DarkColony.Assets;
namespace DarkColony.Original;

public sealed class WarState {
 public WarConfiguration Configuration {get;set;}=null!;
 public int[] Starts {get;set;}=Array.Empty<int>();
 public int[] VisionOffers {get;set;}=new int[8];
 public bool[] Eliminated {get;set;}=new bool[8];
 public bool Surrendered {get;set;}
 public int LocalTeam=>Array.FindIndex(Starts,i=>i>=0&&Configuration.Slots[i].Controller==WarController.Local);
 public WarSlot? Slot(int team)=>Starts[team]<0?null:Configuration.Slots[Starts[team]];
}

public sealed partial class OriginalMission {
 public WarState? War {get;private set;}
 public bool IsWar=>War!=null;
 public static OriginalMission StartWar(string root,WarConfiguration setup){
  var frozen=setup.Copy();return new(frozen.Load(root),frozen.Seed,war:frozen);
 }
 void InitializeWar(){
  var w=War!;var options=w.Configuration.Options;
  for(int a=0;a<8;a++){
   var slot=w.Slot(a);World.Teams[a].MiningMultiplier=slot?.Controller==WarController.AiPlus?512:256;
   for(int b=0;b<8;b++){
    bool allied=a==b||slot!=null&&w.Slot(b) is {} other&&slot.Team==other.Team;
    World.Teams[a].Allies[b]=allied?1:0;
    //40192a: AI grants sight to its starting allies; people must offer it.
    if(a==b||allied&&slot?.Controller is WarController.Ai or WarController.AiPlus)w.VisionOffers[a]|=1<<b;
   }
  }
  RefreshWarVision();World.InitializeWarStorage(options.Storage);
 }
 void RefreshWarVision(){
  for(int a=0;a<8;a++){
   World.VisionBits[a]=1<<a;
   for(int b=0;b<8;b++)if((War!.VisionOffers[a]&(1<<b))!=0&&(War.VisionOffers[b]&(1<<a))!=0)World.VisionBits[a]|=1<<b;
  }
 }
 //41d640 changes one offer. Combat and shared sight require reciprocal consent.
 public bool OfferWarRelationship(int issuer,int other,bool vision,bool enabled){
  if(War==null||AtOutcome||issuer!=World.PlayerCommandTeam||other is <0 or >7||other==issuer||War.Slot(other)==null||War.Eliminated[issuer])return false;
  if(vision){if(enabled)War.VisionOffers[issuer]|=1<<other;else War.VisionOffers[issuer]&=~(1<<other);RefreshWarVision();}
  else World.Teams[issuer].Allies[other]=enabled?1:0;
  Record(vision?"war-vision-offer":"war-alliance-offer",-1,other,$"issuer={issuer};enabled={enabled}");return true;
 }
 public bool WarSurvives(int team){
  foreach(var e in World.Slots)if(e is {Living:true}&&e.Owner==team&&!(e.Slot<120&&e.Slot%15==5)&&e.Type is not (41 or 42 or 45 or 46))return true;
  return false;
 }
 void UpdateWarOutcome(){
  if(War==null||Outcome!=null)return;
  int anchor=-1;bool ended=true;
  for(int a=0;a<8;a++){
   bool alive=WarSurvives(a);
   if(War.Slot(a)!=null&&!alive&&!War.Eliminated[a]){War.Eliminated[a]=true;Record("war-eliminated",-1,a);}
   if(!alive)continue;
   if(anchor<0)anchor=a;
   else if(World.Teams[anchor].Allies[a]==0||World.Teams[a].Allies[anchor]==0)ended=false;
  }
  if(ended)FinishWar(!War.Eliminated[World.Session.LocalTeam]&&WarSurvives(World.Session.LocalTeam),anchor);
 }
 public void SurrenderWar(){if(War==null||Outcome!=null)return;War.Surrendered=true;FinishWar(false,-1);}
 void FinishWar(bool success,int winner){
  outcomeFinalized=true;Outcome=new(winner,success?1:0,Milliseconds-1,success,"",-1,0,0,Array.Empty<int>());
  Record("war-result",-1,winner,War!.Configuration.Multiplayer?"completed":War.Surrendered?"surrender":success?"victory":"defeat");
 }
 short WarOption(int option)=>unchecked((short)(option switch{
  1=>War!.Configuration.Options.Flow*64,2=>War!.Configuration.Options.Quantity*64,
  3=>War!.Configuration.Options.Erupting?1:0,4=>War!.Configuration.Options.Renewable?1:0,
  5=>War!.Configuration.Options.Storage,6=>War!.Configuration.Options.Artifacts,_=>0}));
}

public sealed partial class OriginalWorld {
 internal void InitializeWarStorage(int count){
  int bases=Teams.Count(t=>t.Cities.Take(5).Any(c=>c.Health>0));
  for(int t=0;t<bases;t++)for(int n=0;n<count;n++){
   int type=Random.Next()%2==0?90:85;Cell cell;int attempts=0;
   do{cell=new(Random.Next()%Data.Scenario.Map.Width,Random.Next()%Data.Scenario.Map.Height);
    if(++attempts>65536)throw new InvalidDataException("War map has no valid storage placement.");
   }while(Data.Scenario.Path.At(cell.X,cell.Z)==0);
   var pickup=Spawn(type,9,cell,"war-storage/"+t+"/"+n);pickup.Flag=2;
  }
 }
 public bool ResourceSiteExists(Cell c)=>Entities.Any(e=>e.Type==40&&e.Living&&e.Cell==c);
 internal void ScriptVent(Cell cell,int value,bool amount,int factor){
  var vent=Entities.FirstOrDefault(e=>e.Type==40&&e.State!=0&&e.Cell==cell);
  int scaled=unchecked(value*factor)/256;
  if(amount){if(vent==null){vent=SpawnExact(40,8,cell,"script-vent/"+Tick);vent.ResourceFlow=0;}vent.Health=scaled;}
  else {if(vent==null)throw new InvalidDataException("Missing scripted resource site");
   if(vent.ResourceFlow==0&&value!=0)AudioCue("vent-eruption",vent);
   vent.ResourceFlow=unchecked((short)scaled);
  }
 }
 internal bool AppendArtifact(Cell cell,int type){var site=ArtifactAt(cell);if(site==null)return false;
  //440810 appends without a capacity check. Keep the authored FIFO, not the
  // original fixed-array overwrite when a map supplies more than ten entries.
  var contents=ArtifactContents[site.Slot];if(contents.Count>=128)throw new InvalidDataException("Artifact inventory exceeds supported script bounds");contents.Add(type);return true;
 }
}
