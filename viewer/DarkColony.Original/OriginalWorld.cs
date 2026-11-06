using DarkColony.Assets;
namespace DarkColony.Original;

public enum OriginalOrder { Stop, MoveOnly, MoveAttack, Attack, Inspire, Deploy, Patrol, Disease }
public sealed record NativeCommand(int Slot,int Incarnation,OriginalOrder Order,int X256=0,int Z256=0,int Target=-1,int Issuer=0);
public sealed record NativeEvent(long Tick,string Kind,int Slot,int Other,int X,int Z,int Value=0);
public sealed class NativeAction {
 public int Kind,Word,Health,Mode,Vx,Vz,Angle;
 [System.Text.Json.Serialization.JsonRequired] public int Siphoner=-1;
 public Cell Previous,Destination;
 public List<Cell> Route=new();
 public Cell? Rejoin;
 public List<Cell> Retained=new();
 public NativeAction(int kind){Kind=kind;}
}
public sealed class NativeEntity {
 public int Slot,Incarnation,Type,Owner,X,Z,Height,Health,Flag,Angle,Energy,State=1;
 public int YieldDirection=-1,DestinationX,DestinationZ,Target=-1,IdleCycles,LastIdleHealth;
 public int? ResourceFlow;
 public int Inspired,InspiringSlot=-1,DeathWord,Berserk;
 public bool PendingBlood;
 public bool StrategyHit;
 public string SourceIdentity="";
 public NativeCommand? PendingOrder;
 public Cell[] Patrol=Array.Empty<Cell>();
 public List<NativeAction> Actions=new();
 public NativeAnimation Body=new(),Blood=new(),Overlay=new();
 public Cell Cell=>new(X>>8,Z>>8);
 public bool Living=>State is not (0 or 10);
 public string Identity=>$"{SourceIdentity}@{Slot}:{Incarnation}";
}
public sealed class NativeMissile {
 public long Id;public int Shooter,Weapon,X,Z,Height,Vx,Vz,Vh,Angle,Delay,Age,State;
 public int ShooterIncarnation; // diagnostic only: native impact attribution reads current slot.
 public int FlightRemaining=-1,VisualPhase;
 public int OriginX,OriginZ,Target=-1;public long LaunchTick; // immutable launch diagnostics, not gameplay inputs.
 public NativeAnimation Animation=new();
}

// Original simulation is separate from the historical fixture CombatWorld.
// Selected static contract: FIRST-STRIKE-SPEC. No rendering clocks or frame delta here.
public sealed partial class OriginalWorld {
 public OriginalDefinitions Data {get;}
 public OriginalSession Session {get;}
 public NativeTeamState[] Teams=>Session.Teams;
 public NativeRandom Random {get;}
 public NativeNavigation Navigation {get;}
 public NativeSight Sight {get;}
 public NativeEntity?[] Slots {get;}=new NativeEntity?[800];
 public int[] Ground {get;} public int[] Air {get;} public int[] Other {get;}
 public bool[,] Relations {get;}=new bool[10,10];
 public int[,] Deaths {get;}
 public int[,] Live {get;}
 public int[,] Kills {get;}
 public int[] NearbyCommanderKills {get;}=new int[8];
 public int[] Captures {get;}=new int[8];
 public int[] EarnedResources {get;}=new int[8]; // Post-start income; native statistic1 also includes initial SCN money. Excludes collected fuel.
 public int[] HealedHealth {get;}=new int[8]; // Native global statistic9, credited to the patient's owner.
 public bool[] CouncilPalaces {get;}=new bool[8]; // CW world+bc0, stride e34; content-scoped persistence.
 public int[] ShotCounters {get;}=new int[800]; // native entity+34 burst position, reset on allocation.
 public int[][] CommanderIds {get;}
 public int[] VisionBits {get;}
 public int[] CommanderRanks {get;}=new int[8]; // profile14e4 -> team19bc; 4298f8 initializes all eight to0.
 public List<NativeMissile> Missiles {get;}=new();
 public List<NativeEvent> Events {get;}=new();
 public List<NativeAudioCue> AudioCues {get;}=new(); // Non-authoritative dispatch snapshots, separate from replay events.
 public long Tick {get;private set;}
 public int Phase {get;private set;} public int DayCounter {get;private set;} public int Coefficient {get;private set;}
 public int NextDynamicId {get;private set;}
 public int Seed {get;}
 public event Action<OriginalWorld>? NormalScan;
 public event Action<OriginalWorld,NativeEntity,int>? TripReserved;
 public event Action<OriginalWorld,NativeEntity>? CommanderRecovery;
 public Func<OriginalWorld,NativeEntity,bool>? ExternalDispatch;
 long nextMissile;int aiCursor;
 public IEnumerable<NativeEntity> Entities=>Slots.Where(e=>e!=null&&e.State!=0).Cast<NativeEntity>();
 public OriginalWorld(OriginalDefinitions data,int seed,int localTeam=0,OriginalProfileContext? profile=null,bool restoring=false,int? matchRank=null) {
  int counters=OriginalContent.CounterCount(data.Scenario.Scenario);Deaths=new int[8,counters];Live=new int[8,counters];Kills=new int[8,counters];
  Session=new(data.Scenario,localTeam,profile);CommanderIds=Teams.Select(t=>t.CommanderIds).ToArray();
  Data=data;Seed=seed;Random=new(data.Reference,seed);Navigation=new(data.Scenario.Path,data.Reference);Sight=new(data.Scenario.Map,data.Reference);
  if(profile!=null){if(profile.Rank is <0 or >3)throw new ArgumentOutOfRangeException(nameof(profile));CommanderRanks[localTeam]=profile.Rank;}
  if(matchRank.HasValue)Array.Fill(CommanderRanks,matchRank.Value);
  var s=data.Scenario;Ground=(int[])s.GroundOccupancy.Clone();Air=(int[])s.AirOccupancy.Clone();Other=(int[])s.OtherOccupancy.Clone();
  NextDynamicId=s.NextDynamicId;VisionBits=(int[])s.SharedVisionBits.Clone();Phase=s.Header.Phase;DayCounter=s.Header.Counter;Coefficient=s.Header.InitialCoefficient;
  for(int a=0;a<10;a++)for(int b=0;b<10;b++)Relations[a,b]=s.InitialRelationshipCache[a][b]!=0;
  if(restoring)return;
  foreach(var site in s.ArtifactSites)ArtifactContents.Add(site.Slot,site.Contents.ToList());
  foreach(var source in s.Entities) {
   var e=NewEntity(source.NativeId,source.Type,source.Owner,source.X256,source.Z256,source.Health,source.Flag,source.Identity);
   e.Height=source.Height;e.Angle=source.Angle;e.Energy=source.InitialEnergy;e.ResourceFlow=source.ResourceFlow;
   if(source.Kind=="city"){var city=Teams[source.Owner].Cities[source.SourceOrdinal];city.Entity=e;e.Body.Start(Data.Clips[e.Type].Stand,2);e.Actions.Clear();e.Actions.Add(new(19){Word=0});}
  }
 }
 NativeEntity NewEntity(int slot,int type,int owner,int x,int z,int health,int flag,string identity) {
  // 41b218..41b250 normalizes ALL commander ranks through their actual owner.
  // In particular A05's scripted72/team7 is human commander69 at native rank0.
  if(owner<8&&type is >=69 and <=76)type=(type<73?69:73)+CommanderRanks[owner];
  if(!Data.Clips.ContainsKey(type))throw new NotSupportedException("Unestablished selected type: "+type);
  var e=new NativeEntity{Slot=slot,Incarnation=(Slots[slot]?.Incarnation??0)+1,Type=type,Owner=owner,X=x,Z=z,Health=health,Flag=flag,
   SourceIdentity=identity,Energy=64,Angle=Data.Scenario.Types[type].InitialAngle,Height=Data.Scenario.Types[type].Flying==0?0:600};
  ShotCounters[slot]=0;e.Body.Start(Data.Clips[type].Stand,0);e.Blood.Start(Data.Clips[type].Stand,2);e.Overlay.Start(Data.Clips[type].Stand,2);
  e.Actions.Add(new(1){Word=-1,Health=health});e.LastIdleHealth=health;Slots[slot]=e;
  if(IsCommander(e)&&owner<8) {int index=Array.IndexOf(CommanderIds[owner],-1);if(index<0)throw new InvalidOperationException("Commander registry full");CommanderIds[owner][index]=slot;}
  return e;
 }
 public OriginalWeapon? Weapon(int type,int team)=>Data.Weapons.GetValueOrDefault(Data.Scenario.Types[type].Fields[5+(team<8?Teams[team].WeaponTiers[type]:0)]);
 public int Armor(int type,int team){int tier=team<8?Teams[team].ArmorTiers[type]:0;return 25600/(tier==0?100:Data.Scenario.Types[type].Fields[7+tier]);}
 public bool IsCommander(NativeEntity e)=>Data.Scenario.Types[e.Type].Fields[26]!=0;
 int At(Cell c)=>c.Z*Data.Scenario.Map.Width+c.X;
 int[] Layer(NativeEntity e)=>Data.Scenario.Types[e.Type].OccupancyClass!=0?Other:Data.Scenario.Types[e.Type].Flying!=0?Air:Ground;
 int Occupant(Cell c,int[] layer)=>Navigation.Inside(c)?layer[At(c)]:-2;
 public bool Command(NativeCommand command) {
  if(Faulted)return false;
  bool accepted=AcceptCommand(command);RecordOrder(command,accepted);return accepted;
 }
 internal Func<int,bool>? PlayerOrdersAllowed;
 internal int? OrderedIssuer;
 internal int PlayerCommandTeam=>OrderedIssuer??Session.LocalTeam;
 bool AcceptCommand(NativeCommand command,bool strategy=false) {
  if(!strategy&&PlayerOrdersAllowed?.Invoke(PlayerCommandTeam)==false)return false;
  if(command.Slot<0||command.Slot>=800||Slots[command.Slot] is not {} e||!e.Living||e.Incarnation!=command.Incarnation||(!strategy&&command.Issuer!=PlayerCommandTeam)||e.Owner!=command.Issuer)return false;
  if(e.Slot<120&&e.Slot%15<6)return false; // Cities accept production operations, not unit orders.
  if(command.Order==OriginalOrder.Inspire&&(!IsCommander(e)||e.Energy<32))return false;
  if(command.Order==OriginalOrder.Deploy&&!CanDeploy(e))return false;
  if(command.Order==OriginalOrder.Disease&&(!CanDisease(e)||!Navigation.Inside(new(command.X256>>8,command.Z256>>8))))return false;
  if(e.Type is 47 or 48&&command.Order!=OriginalOrder.Deploy)return false;
  if(command.Order is OriginalOrder.MoveOnly or OriginalOrder.MoveAttack or OriginalOrder.Patrol&&Data.Scenario.Types[e.Type].Fields[2]==0)return false;
  if(command.Order==OriginalOrder.Attack&&(command.Target<0||command.Target>=800||Slots[command.Target] is not {Living:true} || Slots[command.Target]!.Type==94))return false;
  e.PendingOrder=command;Emit("order",e,command.Target,(int)command.Order);return true;
 }
 void StepCore() {
  // CW4199ef..419a41 precedes statistics, script scanning and entity dispatch.
  // Persistent team flag, fixed city slot5, type-only conversion. No HP reset,
  // allocation, counter transfer, occupancy change or speculative palace weapon.
  if(Data.ContentId==OriginalContent.Council)for(int team=0;team<8;team++)
   if(CouncilPalaces[team]&&Slots[team*15+5] is {Type:81} palace)palace.Type=112;
  Tick++;
  // 419768..4197e4 counts allocated dynamic slots, including State10 corpses.
  // The source's 'live' statistic is not a positive-health/combat-eligible count.
  Array.Clear(Live);foreach(var e in Entities)if(e.Slot>=152&&e.Owner<8)Live[e.Owner,e.Type]++;
  // Native CW419c05 (base41e6dc helper): capacity is updated even without
  // bases. Tektaara consumes this value in cityless missions too.
  UpdateArmyLimit();
  if(Tick==1)RefreshSight();
  DayCounter++;
  for(int a=0;a<8;a++)for(int b=0;b<8;b++)Relations[a,b]=Teams[a].Allies[b]!=0&&Teams[b].Allies[a]!=0;
  if(DayCounter>Data.Scenario.Header.Period){DayCounter=0;Phase=1-Phase;Emit("phase",null,-1,Phase);}
  if(DayCounter<=Data.Scenario.Header.Ramp)Coefficient=Phase==0?256-DayCounter*256/Data.Scenario.Header.Ramp:DayCounter*256/Data.Scenario.Header.Ramp;
  Navigation.WorldUpdate();
  if((Tick&15)==0)RefreshSight();
  if((Tick&7)==0) {
   PopulateSources();
   NormalScan?.Invoke(this);
   for(int t=0;t<8;t++)for(int i=0;i<4;i++)if(CommanderIds[t][i]>=0&&Slots[CommanderIds[t][i]] is not {Living:true})CommanderIds[t][i]=-1;
  }
  if((Tick&15)==0)foreach(var t in Teams)if(t.Cities[0].Health>0){t.Money+=t.AutomaticIncome;EarnedResources[t.Id]+=t.AutomaticIncome;Emit("automatic-income",t.Cities[0].Entity,t.Id,t.AutomaticIncome);}
  for(int id=0;id<=NextDynamicId&&id<800;id++)if(Slots[id] is {State: not 0} e) {
   // Native 426364 direction gate belongs to the deterministic tick, not camera visibility.
   // The pre-gate also handles older saves captured between turn and draw.
   NormalizeAnimations(e);
   DispatchSlot=id;movementDiagnostic=null;reservationDiagnostic=null;Dispatch(e);
   NormalizeAnimations(e);
  }
  DispatchSlot=-1;movementDiagnostic=null;reservationDiagnostic=null;
  StepMissiles();foreach(var m in Missiles)m.Animation.Validate(m.Angle);
  // 41ac5c: all enabled strategic handlers on update4, then one rotating team/4.
  if((Tick&3)==0) {
   if(Tick==4){for(int t=0;t<8;t++)Strategy(t);}
   else {aiCursor=(aiCursor+1)%8;Strategy(aiCursor);}
  }
 }
 static void NormalizeAnimations(NativeEntity e){e.Body.Validate(e.Angle);e.Blood.Validate(e.Angle);e.Overlay.Validate(e.Angle);}
 void Strategy(int team) {
  int mode=Teams[team].Ai;if(mode==0)return;
  // Native AI1 table48b2c0 evaluates five weights even for an unused team.
  // With no entities and both city dependency roots disabled, all weights
  // are zero (including production); the weighted
  // selector41ab50 still consumes five draws. Do not invent active AI1 behaviour.
  if(mode==1&&Teams[team].DisabledDependencies[0]&&Teams[team].DisabledDependencies[14]&&
     !Teams[team].Cities.Any(c=>c.Health>0)&&!Teams[team].Production.Any(q=>q.Types.Count>0)&&
     !Entities.Any(e=>e.Owner==team&&e.State!=0)){
   for(int i=0;i<5;i++)Random.Next();return;
  }
  if(mode==3){Random.Next();StepStrategy3(team);return;}
  if(mode!=4)throw new NotSupportedException($"Original strategic AI mode {mode}, team {team}, tick {Tick}, money {Teams[team].Money}, income {Teams[team].AutomaticIncome}, entities {string.Join(",",Entities.Where(e=>e.Owner==team&&e.State!=0).Select(e=>$"{e.Slot}:{e.Type}:{e.State}"))}");
  Random.Next(); // 41ab50 evaluates one zero-weight entry and still consumes its draw.
 }
 public void RefreshSight() {
  Sight.ClearCurrent();
  foreach(var e in Entities) {
   if(e.Owner>=8||e.Flag is 1 or 2)continue;
   var f=Data.Scenario.Types[e.Type].Fields;int radius=(f[4]*Coefficient+f[3]*(256-Coefficient))>>8;
   if(e.State==10)radius=Math.Max(1,(150-e.DeathWord)*radius/150);
   int viewers=0;for(int t=0;t<8;t++)if((VisionBits[t]&(1<<e.Owner))!=0||t==e.Owner)viewers|=1<<t;
   Sight.Reveal(e.Owner,e.Cell,radius,viewers);
  }
 }
 public bool Visible(int team,Cell c) {
  // Queried for every fog cell on every draw. Preserve the live shared-vision
  // mask and short-circuit order without allocating a closure/iterator per cell.
  int mask=VisionBits[team];for(int t=0;t<8;t++)if((mask&(1<<t))!=0&&Sight.At(t,c))return true;
  return false;
 }
 public void SetVision(int first,int second,bool enabled) {
  if(first is <0 or >7||second is <0 or >7)throw new ArgumentOutOfRangeException();
  // 43dd81..43ddb2 changes both explicit links; no alliance/transitive closure.
  if(enabled){VisionBits[first]|=1<<second;VisionBits[second]|=1<<first;}
  else{VisionBits[first]&=~(1<<second);VisionBits[second]&=~(1<<first);}
  VisionBits[first]|=1<<first;VisionBits[second]|=1<<second;
 }
 public int Acquire(NativeEntity e,int range) {
  var weapon=Weapon(e.Type,e.Owner);if(weapon==null||e.Owner==8)return -1;
  for(int r=0;r<=range;r++)foreach(var delta in Data.Reference.Rings[r]) {
   var cell=new Cell(e.Cell.X+delta.X,e.Cell.Z+delta.Z);if(!Navigation.Inside(cell)||(e.Owner==9?!Enumerable.Range(0,8).Any(t=>Visible(t,cell)):!Visible(e.Owner,cell)))continue;
   foreach(var layer in new[]{Ground,Air,Other}) {
    int id=layer[At(cell)];if(id<0||Slots[id] is not {Living:true} target||target.Owner>=8||target.Type==94||id==e.Slot||e.Berserk==0&&Relations[e.Owner,target.Owner])continue;
    if(Data.DamageFactors[weapon.Class][Data.Scenario.Types[target.Type].Fields[10]]!=0)return id;
   }
  }
  return -1;
 }
 void Dispatch(NativeEntity e) {
  var fields=Data.Scenario.Types[e.Type].Fields;
  if((DayCounter&31)==0)e.Energy=Math.Min(255,e.Energy+fields[24]);
  if((DayCounter&15)==0&&e.Inspired!=0)e.Inspired--;
  if((DayCounter&15)==0&&e.Berserk!=0)e.Berserk--;
  if(!e.Blood.Validate(e.Angle)&&e.PendingBlood) {
   var blood=Data.Clips[e.Type].Blood;if(blood.Length!=0)e.Blood.Start(blood[Random.Next()%blood.Length],1);
   e.PendingBlood=false;
  }
  e.Body.Tick(e.Angle);e.Blood.Tick(e.Angle);e.Overlay.Tick(e.Angle);
  if(ExternalDispatch?.Invoke(this,e)==true)return;
  int paths=0;
  for(int loop=0;loop<100;loop++) {
   if(e.Actions.Count==0)e.Actions.Add(new(1){Word=-1,Health=e.Health});
   var a=e.Actions[^1];bool again=false;
   switch(a.Kind) {
    case 2:case 7:case 14: ResetOrders(e);again=true;break;
    case 8:
     if(e.PendingOrder!=null)ReplaceOrder(e);
     else if(a.Word!=0)Pop(e);
     else {a.Word++;Plan(e,a.Mode);}
     again=true;break;
    case 4: if(Turn(e,a.Angle)){Pop(e);again=true;}break;
    case 3:
     if(e.PendingOrder!=null||e.Health!=a.Health){Pop(e);again=true;}
     else if(a.Word==0)Pop(e);
     else a.Word--;
     break;
    case 5:
     e.Body.Start(Data.Clips[e.Type].Move,0);
     if(a.Word==0){e.YieldDirection=-1;Pop(e);}else {e.X=unchecked((ushort)(e.X+a.Vx));e.Z=unchecked((ushort)(e.Z+a.Vz));a.Word--;}
     break;
    case 6: again=PathStep(e,a,ref paths);break;
    case 10: DeathStep(e);break;
    case 9: again=PatrolStep(e,a);break;
    case 11:
     if(e.Body.Mode==2)e.Body.Start(Data.Clips[e.Type].Stand,0);
     if(a.Word==0)Pop(e);else a.Word--;break;
    case 12: HarvestStep(e,a);break;
    case 13: if(e.Body.Mode==2)CompleteDeploy(e);break;
    case 18: again=DiseaseStep(e,a);break;
    case 19: CityConstructionStep(e,a);break;
    case 20: again=ConstructionTransportStep(e,a);break;
    case 21:again=TransportStep(e);break;
    case 22:VerticalStep(e,a);break;
    case 23:MaktorStep(e,a);break;
    case 24:KaoxStep(e,a);break;
    case 1: again=Idle(e,a);break;
    default:throw new NotSupportedException("Native action "+a.Kind);
   }
   if(!again)return;
  }
  throw new InvalidOperationException("Native unit dispatch exceeded100 iterations");
 }
 static void Pop(NativeEntity e){e.Actions.RemoveAt(e.Actions.Count-1);if(e.Actions.Count==0)e.Actions.Add(new(1));}
 bool Turn(NativeEntity e,int angle) {
  int d=angle-e.Angle;if(d>128)d-=256;if(d< -128)d+=256;
  int amount=Data.Scenario.Types[e.Type].Fields[1];e.Angle=(e.Angle+Math.Sign(d)*Math.Min(Math.Abs(d),amount))&255;
  return e.Angle==angle;
 }
 bool Idle(NativeEntity e,NativeAction a) {
  if(e.Flag is 1 or 2){Capture(e);return false;}
  if(e.PendingOrder!=null){ReplaceOrder(e);return true;}
  if(e.Type==40)return VentStep(e,a);
  if(e.Type==37)return ArtifactStep(e,a);
  if(e.Type is 77 or 78)return SiphonIdle(e,a);
  if(e.Slot<120&&e.Slot%15<6){CityStep(e);return false;}
  var weapon=Weapon(e.Type,e.Owner);int target=weapon==null?-1:Acquire(e,weapon.Range);
  if(e.Type is 6 or 14 && (LiveVentAt(e.Cell)!=null||Excavating(e)))e.YieldDirection=-1;
  if(e.YieldDirection!=-1&&Data.Scenario.Types[e.Type].Fields[2]!=0)return Yield(e,target!=-1);
  if(weapon==null&&e.Type is 49 or 50){HealIdle(e);return false;}
  if(target!=-1){e.IdleCycles=0;Fire(e,Slots[target]!);return false;}
  if(!Excavating(e))e.Body.Start(Data.Clips[e.Type].Stand,0);
  bool hurt=e.LastIdleHealth>e.Health;e.LastIdleHealth=e.Health;if(hurt)e.IdleCycles=0;
  if(weapon!=null&&Data.Scenario.Types[e.Type].Fields[2]!=0) {
   int range=e.Owner>=8||Teams[e.Owner].Ai!=0?16:hurt?9:4;target=Acquire(e,range);
   if(target!=-1){e.IdleCycles=0;e.DestinationX=Slots[target]!.X;e.DestinationZ=Slots[target]!.Z;Plan(e,2);return false;}
  }
  if(weapon!=null) {
   int random=Random.Next();if((random&15)==0&&Data.Scenario.Types[e.Type].Fields[20]==0)e.Actions.Add(new(4){Angle=Random.Next()&255});
   Wait(e,e.IdleCycles<3?15:45);e.IdleCycles=Math.Min(3,e.IdleCycles+1);
  }else Wait(e,7);
  return false;
 }
 void Wait(NativeEntity e,int n)=>e.Actions.Add(new(3){Word=n,Health=e.Health});
 void ReplaceOrder(NativeEntity e) {
  var command=e.PendingOrder!;e.PendingOrder=null;e.Actions.Clear();
  // Native move/attack-move/patrol wrappers415f34/415fb8/4160c4 test the CURRENT
  // role's speed when dispatched, including orders deferred by finite deployment.
  // A mobile Slom/turret may have accepted this order before becoming immobile.
  if(command.Order is OriginalOrder.MoveOnly or OriginalOrder.MoveAttack or OriginalOrder.Patrol&&Data.Scenario.Types[e.Type].Fields[2]==0){ResetOrders(e);return;}
  if(command.Order==OriginalOrder.Stop){ResetOrders(e);return;}
  // A queued repeat can survive the transformation that made the command valid.
  // Recheck the current role before binding its special animation.
  if(command.Order==OriginalOrder.Deploy){if(CanDeploy(e))BeginDeploy(e);else ResetOrders(e);return;}
  if(command.Order==OriginalOrder.Patrol){e.Actions.Add(new(9));return;}
  if(command.Order==OriginalOrder.Inspire) {
   if(!IsCommander(e)||e.Energy<32)return;
   e.Body.Start(Data.Clips[e.Type].Special,1);e.Actions.Add(new(13){Word=50});Emit("inspire-start",e);return;
  }
  e.DestinationX=command.X256;e.DestinationZ=command.Z256;e.Target=command.Target;
  if(command.Order==OriginalOrder.Disease){BeginDisease(e);return;}
  if(command.Order==OriginalOrder.Attack){
   // 416097..4160b7: explicit orders reset immobile types to idle (412044),
   // including selected CENTs. Only movable types enter pathmode3 (414d14).
   // Keep native idle acquisition; do not invent movement speed or a movement step.
   if(Data.Scenario.Types[e.Type].Fields[2]==0)ResetOrders(e);
   else {e.Actions.Add(new(14));Plan(e,3);}
  }
  else {e.Actions.Add(new(command.Order==OriginalOrder.MoveAttack?7:2));e.Actions.Add(new(8){Mode=command.Order==OriginalOrder.MoveAttack?1:0});}
 }
 void ResetOrders(NativeEntity e) {
  if(e.PendingOrder!=null){ReplaceOrder(e);return;}
  e.Actions.Clear();e.Actions.Add(new(1){Word=-1,Health=e.Health});e.IdleCycles=0;e.LastIdleHealth=e.Health;
 }
 void Plan(NativeEntity e,int mode,bool retain=false) {
  if(mode==3&&!retain&&e.Target>=0&&Slots[e.Target] is {} target){e.DestinationX=target.X;e.DestinationZ=target.Z;}
  var destination=new Cell(Math.Clamp(e.DestinationX>>8,0,Data.Scenario.Map.Width-1),Math.Clamp(e.DestinationZ>>8,0,Data.Scenario.Map.Height-1));
  bool flying=Data.Scenario.Types[e.Type].Flying!=0;
  if(flying)destination=new(destination.X,Math.Min(destination.Z,Data.Scenario.Map.Height-3));
  else if(!Navigation.Ground(destination))destination=NearestGround(destination,false);
  e.DestinationX=destination.X*256+128;e.DestinationZ=destination.Z*256+128;
  var result=Navigation.Find(e.Cell,destination,flying:flying);
  if(!result.Found){ResetOrders(e);Emit("move-unreachable",e);return;}
  e.Actions.Add(new(6){Mode=mode,Previous=e.Cell,Route=result.Cells.Skip(1).Take(32).ToList()});
 }
 bool PathStep(NativeEntity e,NativeAction a,ref int calls) {
  if(++calls>10){Wait(e,7);return false;}
  if(e.PendingOrder!=null){ReplaceOrder(e);return true;}
  if(a.Route.Count==0) {
   Pop(e);if(e.Cell!=new Cell(e.DestinationX>>8,e.DestinationZ>>8))Plan(e,a.Mode);return false;
  }
  if(a.Rejoin.HasValue) {
   if(e.Cell==a.Rejoin.Value){a.Rejoin=null;a.Retained.Clear();}
   else Detour(e,a,a.Rejoin.Value,a.Retained);
  }
  var weapon=Weapon(e.Type,e.Owner);
  if(a.Mode==4&&Data.Weapons.GetValueOrDefault(Data.Scenario.Types[e.Type].Fields[29]) is {} alternate&&WithinDiseaseRange(e,alternate)) {Pop(e);return true;}
  if(weapon!=null&&a.Mode is 1 or 2) {
   int target=Acquire(e,weapon.Range);
   if(target!=-1){if(a.Mode==2){Pop(e);return true;}Fire(e,Slots[target]!);return false;}
  }
  if(weapon!=null&&a.Mode==3&&e.Target>=0&&Slots[e.Target] is {} explicitTarget) {
   if(!explicitTarget.Living)a.Mode=1;
   int dx=e.Cell.X-explicitTarget.Cell.X,dz=e.Cell.Z-explicitTarget.Cell.Z;
   if(dx*dx+dz*dz<weapon.Range*weapon.Range){Fire(e,explicitTarget);return false;}
  }
  var next=a.Route[0];int occupied=Occupant(next,Layer(e));
  if(occupied!=-1&&occupied!=e.Slot){Blocked(e,a,occupied);return false;}
  a.Word=0; // Progress renews the bounded obstruction wait budget.
  reservationDiagnostic=new{next,previous=a.Previous,routeBefore=a.Route.ToArray(),a.Rejoin,retained=a.Retained.ToArray(),occupantBefore=occupied};
  Layer(e)[At(next)]=e.Slot;TripReserved?.Invoke(this,e,Data.Scenario.Trips.At(next.X,next.Z));
  var previous=a.Previous;a.Previous=next;a.Route.RemoveAt(0);QueueStep(e,next.X*256+128,next.Z*256+128,previous);
  return true;
 }
 void QueueStep(NativeEntity e,int x,int z,Cell previous) {
  int angle=Data.Reference.Angle(x-e.X,z-e.Z),speed=Data.Scenario.Types[e.Type].Fields[2];
  var trig=Data.Reference.SinCos(angle*32);int distance=(trig.Cos*(x-e.X)+trig.Sin*(z-e.Z))/2048;
  movementDiagnostic=new{e.Slot,e.Incarnation,currentX=e.X,currentZ=e.Z,requestedX=x,requestedZ=z,previous,angle,speed,distance,trig.Cos,trig.Sin};
  e.Body.Start(Data.Clips[e.Type].Move,0);
  e.Actions.Add(new(5){Word=distance/speed,Vx=trig.Cos*speed/2048,Vz=trig.Sin*speed/2048,Previous=previous,Destination=new(x>>8,z>>8)});
  e.Actions.Add(new(4){Angle=angle});
  if(e.Owner!=8&&Navigation.Inside(previous)&&Layer(e)[At(previous)]==e.Slot)Layer(e)[At(previous)]=-1;
  if(e.Owner<8){Sight.MarkCurrent(e.Owner,previous);Sight.MarkCurrent(e.Owner,new(x>>8,z>>8));}
 }
 Cell NearestGround(Cell origin,bool unoccupied) {
  for(int r=0;r<256;r++)for(int x=origin.X-r;x<=origin.X+r;x++)for(int z=origin.Z-r;z<=origin.Z+r;z++) {
   //41b4d0 includes coordinate zero and the last map cell. Authored border
   //couriers are subsequently selected by their exact spawn cell by waypoint.
   var c=new Cell(x,z);
   if(Navigation.Ground(c)&&(!unoccupied||Ground[At(c)]==-1))return c;
  }
  throw new InvalidOperationException("No native free ground cell");
 }
 void Blocked(NativeEntity e,NativeAction a,int blocker) {
  int free=a.Route.FindIndex(c=>Occupant(c,Layer(e))==-1);
  if(free<0) {
   if(a.Route[^1]==e.Cell){e.Actions.Clear();e.Actions.Add(new(1));return;}
   if(e.Type is 6 or 14 && LiveVentAt(new(e.DestinationX>>8,e.DestinationZ>>8))!=null && a.Word++<120){
    if(blocker>=0&&Slots[blocker] is {} ally&&Relations[e.Owner,ally.Owner]&&ally.Type is not (47 or 48)){
     int dir=Array.IndexOf(Data.Reference.Directions,new Cell(Math.Sign(a.Route[0].X-e.Cell.X),Math.Sign(a.Route[0].Z-e.Cell.Z)));if(dir>=0&&ally.YieldDirection<0)ally.YieldDirection=dir;
    }
    Wait(e,4);return;
   }
   // Approved arrival QoL: a congested endpoint must not cause an unbounded
   // random walk. Stop nearby; a resource worker must keep the exact vent intent.
   if(a.Mode is 0 or 1){ResetOrders(e);e.YieldDirection=-1;Emit("move-blocked",e);return;}
   Pop(e);
   int Perturb(int value,int limit){int v=unchecked((ushort)(value+(Random.Next()%3-1)*256));if(v>0xf000)v=unchecked((ushort)(v+256));if(v>=limit*256)v=unchecked((ushort)(v-256));return v;}
   e.DestinationX=Perturb(e.DestinationX,Data.Scenario.Map.Width);e.DestinationZ=Perturb(e.DestinationZ,Data.Scenario.Map.Height);
   if(e.Cell!=new Cell(e.DestinationX>>8,e.DestinationZ>>8))Plan(e,a.Mode,true);return;
  }
  if(Detour(e,a,a.Route[free],a.Route.Skip(free+1).ToList()))return;
  if(a.Mode is 0 or 1 && a.Word++>=120){ResetOrders(e);e.YieldDirection=-1;Emit("move-blocked",e);return;}
  if(blocker>=0){var b=Slots[blocker]!;int dir=Array.IndexOf(Data.Reference.Directions,new Cell(Math.Sign(a.Route[0].X-e.Cell.X),Math.Sign(a.Route[0].Z-e.Cell.Z)));
   if(b.YieldDirection==-1&&Relations[b.Owner,e.Owner])b.YieldDirection=dir;}
  e.Body.Start(Data.Clips[e.Type].Stand,0);Wait(e,4);
 }
 bool Detour(NativeEntity e,NativeAction a,Cell rejoin,List<Cell> retained) {
  var route=Navigation.Find(e.Cell,rejoin,c=>Occupant(c,Layer(e))!=-1&&Occupant(c,Layer(e))!=e.Slot,true,Data.Scenario.Types[e.Type].Flying!=0);
  if(!route.Found)return false;
  a.Route=route.Cells.Skip(1).Take(32).ToList();a.Rejoin=rejoin;a.Retained=retained.ToList();
  if(a.Route.Count+retained.Count<=31)a.Route.AddRange(retained);else a.Retained.Clear();
  return true;
 }
 bool Yield(NativeEntity e,bool combat) {
  int requested=e.YieldDirection;e.YieldDirection=-1;
  int[] offsets=combat?Data.Reference.YieldCombat:Data.Reference.YieldIdle;
  int Direction(int offset)=>Data.Reference.DirectionWheel[(Data.Reference.DirectionInverse[requested]+offset)&7];
  bool Try(int dir,bool allowMoving) {
   var d=Data.Reference.Directions[dir];var c=new Cell(e.Cell.X+d.X,e.Cell.Z+d.Z);bool flying=Data.Scenario.Types[e.Type].Flying!=0;if(flying?!Navigation.Inside(c):!Navigation.Ground(c))return false;
   if(!flying&&LiveVentAt(c)!=null)return false;
   int id=Occupant(c,Layer(e));if(id!=-1&&(!allowMoving||id<0||Slots[id] is not {State:1}))return false;
   if(!flying&&d.X!=0&&d.Z!=0&&!Navigation.Ground(new(e.Cell.X+d.X,e.Cell.Z))&&!Navigation.Ground(new(e.Cell.X,e.Cell.Z+d.Z)))return false;
   e.DestinationX=c.X*256+128;e.DestinationZ=c.Z*256+128;Plan(e,combat?1:0);return true;
  }
  foreach(int offset in offsets)if(Try(Direction(offset),false))return true;
  if(!combat) {
   var order=Enumerable.Range(0,7).ToArray();
   for(int i=0;i<7;i++){int j=Random.Next()%(7-i)+i;(order[i],order[j])=(order[j],order[i]);if(Try(Direction(offsets[order[i]]),true))return true;}
  }
  return false;
 }
 void Capture(NativeEntity e) {
  if((DayCounter&3)!=0)return;
  for(int x=e.Cell.X-2;x<=e.Cell.X+2;x++)for(int z=e.Cell.Z-2;z<=e.Cell.Z+2;z++)foreach(var layer in new[]{Ground,Air}) {
   int id=Occupant(new(x,z),layer);if(id<0||Slots[id] is not {Flag:0} collector)continue;
   //41410c: flag1 belongs to player0; flag2 credits ANY eligible team and retires.
   if(e.Flag==1&&collector.Owner==0){e.Owner=0;e.Flag=0;Captures[0]++;Emit("capture",e,id);return;}
   if(e.Flag==2&&collector.Owner<8){Teams[collector.Owner].Money+=e.Health;e.Flag=0;Emit("fuel-collected",e,collector.Owner,e.Health);Retire(e);return;}
  }
 }
 public NativeEntity Spawn(int type,int owner,Cell requested,string source) {
  var cell=Data.Scenario.Types[type].Flying!=0?NearestAir(requested):NearestGround(requested,true);int slot=-1;
  for(int i=152;i<NextDynamicId;i++)if(Slots[i] is null or {State:0})slot=i;
  if(slot<0){slot=NextDynamicId++;if(NextDynamicId>=800)throw new InvalidOperationException("Native entity capacity");}
  var e=NewEntity(slot,type,owner,cell.X*256+128,cell.Z*256+128,Data.Scenario.Types[type].MaximumHealth,0,source);
  if(owner!=8)Layer(e)[At(cell)]=slot;Emit("spawn",e);return e;
 }
 Cell NearestAir(Cell origin){
  //41b4d0: air spawn searches its own layer, independent of ground/terrain.
  int limit=Math.Max(Data.Scenario.Map.Width,Data.Scenario.Map.Height);
  for(int r=0;r<limit;r++)for(int x=origin.X-r;x<=origin.X+r;x++)for(int z=origin.Z-r;z<=origin.Z+r;z++){
   var cell=new Cell(x,z);if(Navigation.Inside(cell)&&Air[At(cell)]==-1)return cell;
  }
  throw new InvalidOperationException("No native free air cell");
 }
 public void Retire(NativeEntity e) {
  ClearOccupancy(e);ArtifactContents.Remove(e.Slot);e.State=0;e.Actions.Clear();e.PendingOrder=null;Emit("retire",e);
 }
 void ClearOccupancy(NativeEntity e){foreach(var layer in new[]{Ground,Air,Other})for(int i=0;i<layer.Length;i++)if(layer[i]==e.Slot)layer[i]=-1;}
 void DeathStep(NativeEntity e) {
  if(e.DeathWord==0) {var clips=Data.Clips[e.Type];int variant=Random.Next()%clips.Die.Length;e.Body.Start(IsCommander(e)?clips.Critical:clips.Die[variant],IsCommander(e)?3:1);}
  if(IsCommander(e)){if(e.DeathWord==0)e.DeathWord=1;}else e.DeathWord++;
  if(e.DeathWord==150)Retire(e);
 }
 public void RecoveryArrived(int slot){if(Slots[slot] is {State:10} e){e.DeathWord=150;Emit("recovery-arrived",e);}}
 void ApplyInspire(NativeEntity e) {
  ResetOrders(e);e.Energy=0;int budget=Data.Scenario.Types[e.Type].Fields[26];Emit("inspire",e);
  for(int r=0;r<=10;r++)for(int offset=-20;offset<=20;offset++)for(int side=0;side<4;side++) {
   Cell c=side switch{0=>new(e.Cell.X-r,e.Cell.Z+offset),1=>new(e.Cell.X+r,e.Cell.Z+offset),2=>new(e.Cell.X+offset,e.Cell.Z-r),_=>new(e.Cell.X+offset,e.Cell.Z+r)};
   foreach(var layer in new[]{Ground,Air}) {
    int id=Occupant(c,layer);if(id<0||Slots[id] is not {} target||target.Owner!=e.Owner||IsCommander(target)||Weapon(target.Type,target.Owner)==null)continue;
    target.Inspired=(Random.Next()&15)+20;target.InspiringSlot=e.Slot;Emit("inspired-visit",target,e.Slot,target.Inspired);
    if(--budget==0)return;
   }
  }
 }
 void Emit(string kind,NativeEntity? e,int other=-1,int value=0) {
  Events.Add(new(Tick,kind,e?.Slot??-1,other,e?.X??0,e?.Z??0,value));
  if(e!=null&&kind is "launch" or "death" or "inspire-start" or "capture")AudioCue(kind,e,value);
 }
 void AudioCue(string kind,NativeEntity e,int value=0,int? height=null)=>AudioCues.Add(new(Tick,kind,e.Slot,e.Incarnation,e.Type,e.X,e.Z,height??e.Height,value));
}
