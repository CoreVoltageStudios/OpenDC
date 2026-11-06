using DarkColony.Assets;
namespace DarkColony.Original;

public enum ProductionIssuer { Player, Strategy }
public sealed record ProductionResult(bool Accepted,string Reason,int Dependency,int Charged=0);

public sealed partial class OriginalWorld {
 // 438580/4386fc: prerequisites are authoritative, distinct from the UI cache.
 public int DependencyState(int team,int id)=>DependencyState(team,id,new HashSet<int>());
 int DependencyState(int team,int id,HashSet<int> visiting) {
  if(team is <0 or >7)return 2;
  var d=Data.Scenario.Dependencies.FirstOrDefault(d=>d.Id==id);var t=Teams[team];
  if(d==null||t.DisabledDependencies[id]||!visiting.Add(id))return 2;
  try {
  if(d.Kind==0){var city=t.Cities[d.Subject];if(t.Race!=d.RaceOrTier||city.InitializationPending)return 2;if(city.Health>0&&city.Level>=d.Level)return 0;}
  if(d.Kind==1&&Data.Scenario.Types[d.Subject].Race!=t.Race)return 2;
  if(d.Kind==2){if(Data.Scenario.Types[d.Subject].Race!=t.Race)return 2;var tiers=d.Level==0?t.WeaponTiers:t.ArmorTiers;if(tiers[d.Subject]>=d.RaceOrTier)return 0;}
  foreach(int parent in d.Parents)if(DependencyState(team,parent,visiting)!=0){visiting.Remove(id);return 2;}
  return 1;
  } finally { visiting.Remove(id); }
 }
 public void RefreshDependencies(int team){foreach(var d in Data.Scenario.Dependencies)Teams[team].AvailabilityCache[d.Id]=DependencyState(team,d.Id);}
 public OriginalDependency[] ProductionCatalog(int team){
  bool Reachable(int id,HashSet<int> stack){var d=Data.Scenario.Dependencies.FirstOrDefault(d=>d.Id==id);if(d==null||Teams[team].DisabledDependencies[id]||!stack.Add(id))return false;
   bool faction=d.Kind==0?d.RaceOrTier==Teams[team].Race:Data.Scenario.Types[d.Subject].Race==Teams[team].Race;
   bool valid=faction&&d.Parents.All(p=>Reachable(p,stack));stack.Remove(id);return valid;}
  return Data.Scenario.Dependencies.Where(d=>Reachable(d.Id,new())).ToArray();
 }
 public ProductionResult RequestProduction(int team,int dependency,ProductionIssuer issuer=ProductionIssuer.Player) {
  ProductionResult Reject(string reason)=>new(false,reason,dependency);
  if(Faulted)return Reject("Simulation fault");
  if(issuer==ProductionIssuer.Player&&PlayerOrdersAllowed?.Invoke(PlayerCommandTeam)==false)return Reject("Player eliminated");
  if(team is <0 or >7||issuer==ProductionIssuer.Player&&team!=PlayerCommandTeam)return Reject("Not controlled by this player");
  var d=Data.Scenario.Dependencies.FirstOrDefault(d=>d.Id==dependency);
  if(d==null)return Reject("Unknown product");
  int state=DependencyState(team,dependency);if(state!=1)return Reject(state==0?"Already available":"Prerequisite unavailable or construction pending");
  var t=Teams[team];if(t.Money<d.Cost)return Reject("Insufficient Petra-7");
  if(d.Kind==1){var q=t.Production[Data.Scenario.Types[d.Subject].Fields[20]];if(q.Types.Count>=800)return Reject("Native production queue full");if(!Data.Clips.ContainsKey(d.Subject))return Reject("Product assets not prepared");}
  if(d.Kind==0){
   int type=OriginalScenarioImport.CityType(t.Race,d.Level,d.Subject);
   if(!Data.Clips.ContainsKey(type))return Reject("City assets not prepared");
   foreach(var (x,z) in OriginalScenarioImport.CityFootprint(t.Base,d.Subject)){
    var c=new Cell(x,z);if(!Navigation.Inside(c))return Reject("City footprint outside map");
    int occupant=Ground[At(c)];if(occupant>=0&&occupant!=t.Cities[d.Subject].NativeId)return Reject("City site occupied");
   }
  }
  // 4569ac..4569db deduct before message10; 41c828 records expenditure and FIFO.
  // Player and future strategy callers commit through this one atomic operation.
  t.Money-=d.Cost;t.SpentResources+=d.Cost;
  if(d.Kind==1)t.Production[Data.Scenario.Types[d.Subject].Fields[20]].Types.Add(d.Subject);
  else if(d.Kind==2)(d.Level==0?t.WeaponTiers:t.ArmorTiers)[d.Subject]=d.RaceOrTier; // 41ca34: immediate tier, no invented research timer.
  else CreateCity(t,d);
  RefreshDependencies(team);Emit("production-request",null,team,dependency);return new(true,"Committed",dependency,d.Cost);
 }
 int[]? cityQueues;readonly Dictionary<int,Cell> productionOffsets=new();
 int QueueForCity(int slot)=>(cityQueues??=Data.Reference.Ints(0x41ae60,15))[slot];
 public NativeEntity? ProductionSource(int team,int category)=>Teams[team].Cities.FirstOrDefault(c=>c.Entity is {Living:true}&&QueueForCity(c.Slot)==category)?.Entity;
 public Cell ProductionExit(int team,int type) {
  int category=Data.Scenario.Types[type].Fields[20],variant=Data.Scenario.Types[type].Fields[22];
  if(!productionOffsets.TryGetValue(type,out var offset)){var delta=Data.Reference.Ints(0x41ae00+(uint)(category*24+variant*8),2);productionOffsets[type]=offset=new(delta[0],delta[1]);}
  var b=Teams[team].Base;return new(b[0]+offset.X,b[1]+offset.Z);
 }
 void CreateCity(NativeTeamState t,OriginalDependency d) {
  var c=t.Cities[d.Subject];int type=OriginalScenarioImport.CityType(t.Race,d.Level,c.Slot);
  var anchor=OriginalScenarioImport.CityAnchor(t.Base,c.Slot);bool newLife=c.Entity is not {Living:true};
  c.Level=d.Level;c.InitializationPending=true;
  if(newLife){c.Entity=NewEntity(c.NativeId,type,t.Id,anchor.X,anchor.Z,Data.Scenario.Types[type].MaximumHealth,0,"city/"+t.Id+"/"+c.Slot);c.Entity.Energy=0;
   int q=QueueForCity(c.Slot);if(q<4){var queue=t.Production[q];ReleaseProductionExit(queue);queue.Types.Clear();queue.Building=false;queue.Cooldown=0;}
   foreach(var (x,z) in OriginalScenarioImport.CityFootprint(t.Base,c.Slot))Ground[At(new(x,z))]=c.NativeId;
  }else{c.Entity!.Type=type;c.Entity.Health=Data.Scenario.Types[type].MaximumHealth;}
  // 445419 explicitly writes city height=0. GAMESTAT field12 is also set on
  // buildings; the generic dynamic-unit initializer's600 is not a city height.
  // This must apply to purchased/rebuilt cities as well as source-loaded cities.
  c.Entity!.Height=0;c.Entity.Energy=0;
  c.Entity.Actions.Clear();c.Entity.Actions.Add(new(19));c.Entity.Body.Start(Data.Clips[type].Stand,2);
  Emit("city-construction",c.Entity,t.Id,d.Id);
 }
 void CityConstructionStep(NativeEntity e,NativeAction a) {
  var t=Teams[e.Owner];var c=t.Cities[e.Slot%15];
  if(a.Word==0){if(t.ConstructionSlot!=-1&&t.ConstructionSlot!=e.Slot)return;t.ConstructionSlot=e.Slot;
   if(Tick<4||Data.Clips[e.Type].Build==null){e.Body.Start(Data.Clips[e.Type].Stand,2);a.Word=5;}
   else{e.Body.Start(Data.Clips[e.Type].Build!,2);StartConstructionTransport(e,false);a.Word=1;}
  }
  if(a.Word is 2 or 5){if(e.Body.Mode!=2)return;e.Body.Start(Data.Clips[e.Type].Stand,0);c.InitializationPending=false;RefreshDependencies(t.Id);
   if(a.Word==2){StartConstructionTransport(e,true);a.Word=3;}
  }
  if(a.Word is 4 or 5){t.ConstructionSlot=-1;ResetOrders(e);Emit("city-ready",e);}
 }
 void StartConstructionTransport(NativeEntity city,bool up) {
  int team=city.Owner,slot=team*15+6,type=Teams[team].Race==1?93:92;
  var delta=Data.Reference.Ints(0x48ab40+(uint)(city.Slot%15)*8,2);
  var e=NewEntity(slot,type,8,city.X+delta[0]*8,city.Z+delta[1]*8,Data.Scenario.Types[type].MaximumHealth,0,"city-carrier/"+city.Identity);
  e.Actions.Add(new(20){Word=city.Slot,Mode=0,Destination=e.Cell});
  QueueVertical(e,up,type==92?900:1400);Emit("construction-carrier",e,city.Slot,up?1:0);
 }
 bool ConstructionTransportStep(NativeEntity e,NativeAction a) {
  var city=Slots[a.Word];var action=city?.Actions.LastOrDefault(x=>x.Kind==19);
  if(city is not {Living:true}||action==null){Retire(e);return false;}
  if(action.Word==1){if(a.Mode==0){a.Mode=1;return true;}city.Body.Start(Data.Clips[city.Type].Build!,1);action.Word=2;}
  else action.Word=4;
  Retire(e);return false;
 }
 void CityStep(NativeEntity e) {
  var t=Teams[e.Owner];var c=t.Cities[e.Slot%15];var clips=Data.Clips[e.Type];int max=Data.Scenario.Types[e.Type].MaximumHealth;
  if(e.Body.Mode!=1)e.Body.Start(e.Health>(max*11>>4)?clips.Stand:e.Health>(max*5>>4)?clips.Burnt??clips.Stand:clips.Scratched??clips.Stand,0);
  int category=QueueForCity(c.Slot);if(category==4)return;var q=t.Production[category];
  if(q.Cooldown>0)q.Cooldown--;
  if(q.Types.Count==0)return;
  int type=q.Types[0];var exit=ProductionExit(t.Id,type);var layer=Data.Scenario.Types[type].Flying==0?Ground:Air;
  if(q.Building){if(e.Overlay.Mode==1)return;
   ReleaseProductionExit(q);SpawnExact(type,t.Id,exit,"production/"+e.Identity+"/"+Tick);q.Types.RemoveAt(0);q.Building=false;Emit("production-complete",e,t.Id,type);return;
  }
  int occupant=Occupant(exit,layer);if(occupant!=-1){if(occupant>=0&&Slots[occupant] is {} blocker)blocker.YieldDirection=0;return;}
  if(q.Cooldown!=0)return;
  if(Enumerable.Range(0,Live.GetLength(1)).Sum(typeId=>Live[t.Id,typeId])>=ArmyLimit){
   int cost=Data.Scenario.Dependencies.First(d=>d.Kind==1&&d.Subject==type).Cost;
   t.Money+=cost;t.SpentResources-=cost;q.Types.RemoveAt(0);Emit("production-cap-refund",e,t.Id,cost);return;
  }
  if(Data.Clips[type].Build is {} build){layer[At(exit)]=-2;q.ReservedExit=exit;q.Building=true;q.StartedTick=Tick;e.Overlay.Start(build,1);Emit("production-start",e,t.Id,type);}
  else {SpawnExact(type,t.Id,exit,"production/"+e.Identity+"/"+Tick);q.Types.RemoveAt(0);Emit("production-complete",e,t.Id,type);}
 }
 void ReleaseProductionExit(NativeProductionQueue q){if(q.ReservedExit is {} c){foreach(var layer in new[]{Ground,Air})if(layer[At(c)]==-2)layer[At(c)]=-1;q.ReservedExit=null;}}
 NativeEntity SpawnExact(int type,int owner,Cell cell,string source) {
  int slot=-1;for(int id=152;id<NextDynamicId;id++)if(Slots[id] is {State:0})slot=id;
  if(slot==-1){if(NextDynamicId>=799)throw new InvalidOperationException("Native entity capacity exhausted");slot=NextDynamicId++;}
  var e=NewEntity(slot,type,owner,cell.X*256+128,cell.Z*256+128,Data.Scenario.Types[type].MaximumHealth,0,source);
  if(owner!=8)Layer(e)[At(cell)]=slot;Emit("spawn",e);return e;
 }
 void PopulateSources() {
  // 4401fc before TRO every8 updates; registered sources do not consume IDs at load.
  foreach(var source in Session.Sources){var s=source.Definition;
   if(source.Initialized==0){for(int i=0;i<s.Count;i++)source.Members[i]=Spawn(s.Type,9,new(s.X,s.Z),"source/"+s.Id+"/"+i).Slot;source.Initialized=1;}
   for(int i=0;i<s.Count;i++){
    if(source.Members[i]<0)continue;var e=Slots[source.Members[i]];
    if(e is not {Living:true}){source.Members[i]=-1;continue;}
    if(e.Owner==9&&(Random.Next()&3)==0){int x,z;do{x=s.X+(Random.Next()&15)-7;z=s.Z+(Random.Next()&15)-7;}while(!Navigation.Inside(new(x,z)));
     e.PendingOrder=new(e.Slot,e.Incarnation,OriginalOrder.MoveAttack,x*256,z*256,Issuer:9);
    }
   }
   if((Random.Next()&127)==0){int index=Array.IndexOf(source.Members,-1);if(index>=0){source.Members[index]=Spawn(s.Type,9,new(s.X,s.Z),"source/"+s.Id+"/"+index).Slot;return;}}
  }
 }
 bool VentStep(NativeEntity vent,NativeAction a) {
  // DC16 41350d..413534: zero flow selects BL=2 (hidden), not a looping
  // plume. The static vent rim is separate terrain art. Activation below
  // restores the plume and the ordinary 50-tick deployment countdown.
  if(vent.ResourceFlow==0){vent.Body.Start(Data.Clips[40].Stand,2);return false;}
  int id=Occupant(vent.Cell,Ground);var miner=id>=0?Slots[id]:null;
  if(vent.Body.Mode==2&&(miner==null||miner.Type is 6 or 14)){vent.Angle=0;a.Word=50;vent.Body.Start(Data.Clips[40].Stand,0);return false;}
  if(miner is not {Type:6 or 14} || miner.Cell!=vent.Cell || miner.PendingOrder!=null || miner.Actions.Any(x=>x.Kind is 4 or 5 or 6 or 8)){a.Word=50;return false;}
  if(a.Word==0)return false;if(--a.Word>0)return false;
  vent.Angle=miner.Angle;miner.Body.Start(Data.Clips[miner.Type].Special,1);vent.Body.Start(Data.Clips[40].Stand,2);
  miner.Type=miner.Type==6?47:48;miner.Actions.Add(new(12){Word=vent.Slot,Mode=1});Emit("harvest-deploy",miner,vent.Slot);return true;
 }
 void HarvestStep(NativeEntity e,NativeAction a) {
  if(e.PendingOrder?.Order==OriginalOrder.Deploy){e.PendingOrder=null;BeginDeploy(e);return;}
  var vent=Slots[a.Word]??throw new InvalidOperationException("Missing vent identity");
  if(e.Body.Mode==2){a.Mode=0;e.Body.Start(Data.Clips[e.Type].Stand,0);}
  int flow=vent.ResourceFlow??throw new InvalidOperationException("Harvest target is not a vent");
  if(vent.Health<=flow){KillEconomicEntity(vent);ResetOrders(e);
   if(!DamageProtected(e)){if(e.Health<=270){KillEconomicEntity(e);return;}e.Health-=270;}BeginDeploy(e);Emit("vent-exhausted",e,vent.Slot);return;
  }
  if((DayCounter&15)!=0)return;
  var t=Teams[e.Owner];int full=t.Ai==0?flow:flow*t.MiningMultiplier/256,transfer=SplitHarvest(e,a,full);
  if(t.Cities[0].Health>0){t.Money+=transfer;EarnedResources[t.Id]+=transfer;Emit("harvest",e,vent.Slot,transfer);}
  vent.Health-=full;
 }
 public bool CanDeploy(NativeEntity e)=>e.Type is 1 or 9 or 4 or 12 or 63 or 64 or 65 or 67 or 77 or 78 || e.Type==66&&!e.Actions.Any(a=>a.Kind==24) || (e.Type is 43 or 44 ? Occupant(e.Cell,Other)==-1:e.Type is 47 or 48&&!Session.PreventHarvesterRetraction);
 void BeginDeploy(NativeEntity e){e.Actions.Clear();e.Actions.Add(new(13){Word=50});e.Body.Start(Data.Clips[e.Type].Special,1);Emit("deploy-start",e);if(e.Type is >=63 and <=67)AudioCue("artifact-activate",e);}
 void CompleteDeploy(NativeEntity e) {
  if(e.Type is 49 or 50){e.Body.Start(Data.Clips[e.Type].Stand,0);ResetOrders(e);return;} // Native generic completion417df9.
  if(IsCommander(e)){ApplyInspire(e);return;}
  if(e.Type==63){ActivateSolaris(e);return;}
  if(e.Type==64){ActivateMaktor(e);return;}
  if(e.Type==66){ActivateKaox(e);return;}
  if(e.Type==65){ActivateLunatek(e);return;}
  if(e.Type==67){ActivateTektaara(e);return;}
  if(e.Type is 4 or 12 or 77 or 78){CompleteSiphonDeployment(e);return;}
  // 417d24..417d3b: mobile Firestorm/Xenowort -> fixed41/42, same anchor/health.
  if(e.Type is 1 or 9){e.Type=e.Type==1?41:42;ResetOrders(e);e.Body.Start(Data.Clips[e.Type].Stand,0);Emit("turret-deployed",e);return;}
  if(e.Type is 47 or 48){e.Type=e.Type==47?6:14;ResetOrders(e);e.Body.Start(Data.Clips[e.Type].Stand,0);Emit("harvest-retract",e);return;}
  if(e.Type is 43 or 44){ClearOccupancy(e);e.Type+=2;Other[At(e.Cell)]=e.Slot;ResetOrders(e);e.Body.Start(Data.Clips[e.Type].Stand,0);Emit("mine-armed",e);return;}
  throw new NotSupportedException("Unimplemented deployed role "+e.Type);
 }
 void KillEconomicEntity(NativeEntity e){e.Health=0;e.State=10;e.PendingOrder=null;e.Actions.Clear();e.Actions.Add(new(10));e.DeathWord=0;ClearOccupancy(e);Emit("death",e);}
 public int ArmyLimit {get;private set;}
 void UpdateArmyLimit(){
  // 41e6dc: reserved source population, non-city owners and 100 spare slots.
  Span<bool> cities=stackalloc bool[10];Span<int> counts=stackalloc int[10];int cityTeams=0;
  for(int t=0;t<8;t++)if(Teams[t].Cities.Take(5).Any(c=>c.Health>0)){cities[t]=true;cityTeams++;}
  foreach(var e in Entities)if(e.Slot>=152)counts[e.Owner]++;
  int remaining=648-Session.Sources.Sum(s=>s.Definition.Count)-100;
  for(int t=0;t<9;t++)if(!cities[t])remaining-=counts[t];
  ArmyLimit=Math.Min(Session.MaximumArmy,cityTeams>0?remaining/cityTeams:remaining);
 }
 void EconomicDeath(NativeEntity e){
  if(e.Slot>=120||e.Slot%15>=6)return;
  var team=Teams[e.Slot/15];var city=team.Cities[e.Slot%15];city.InitializationPending=false;
  if(team.ConstructionSlot==e.Slot)team.ConstructionSlot=-1;
  int category=QueueForCity(city.Slot);if(category<4){var q=team.Production[category];ReleaseProductionExit(q);q.Building=false;}
  RefreshDependencies(team.Id);
 }
}
