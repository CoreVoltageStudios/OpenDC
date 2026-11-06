namespace DarkColony.Simulation;

public sealed record WeaponDefinition(string Id,int Damage,double RangeTiles,int CooldownTicks) {
 public DeliverySettings Delivery {get;init;}=new();
 public void Validate(){if(string.IsNullOrWhiteSpace(Id)||Damage<1||Damage>100000||!double.IsFinite(RangeTiles)||RangeTiles<=0||RangeTiles>12||CooldownTicks<1||CooldownTicks>3600)throw new ArgumentException("Invalid prototype weapon");Delivery.Validate();}
}
public sealed record CombatantDefinition(string Id,int MaxHealth,WeaponDefinition Weapon) {
 public double SightTiles {get;init;}=6;
 public void Validate(){if(!double.IsFinite(SightTiles)||SightTiles<=0||SightTiles>32||string.IsNullOrWhiteSpace(Id)||MaxHealth<1||MaxHealth>1000000)throw new ArgumentException("Invalid combatant");Weapon.Validate();}
}
public sealed record CombatSpawn(int Id,int Team,Point Position,CombatantDefinition Definition,bool Stationary=false);
public enum CombatOrderKind { Move,Stop,Attack,AttackMove,Hold,Guard }
public enum CombatActivity { Idle,Moving,Approaching,Firing,Blocked,Stopped,Dead,Holding,Guarding,Returning }
public enum EncounterOutcome { Active,Victory,Defeat,Draw }
public sealed record CombatCommand(long Tick,long Sequence,int[] Units,CombatOrderKind Kind,Point Destination=default,int Target=0);
public sealed record CombatReceipt(long Tick,long Sequence,string Result,int[] Units);
public sealed record CombatEvent(long Tick,string Kind,int Source,int Target,int Amount,long ShotId=0,Point? Position=null);
public sealed record CombatUnit(int Id,int Team,string Definition,int Health,int MaxHealth,bool Alive,bool Stationary,CombatActivity Activity,int? Target,long NextShotTick,long LastShotTick,long LastHitTick,long DeathTick,SquadUnit Body,TacticalUnit? Tactics=null);

// Intentional remake combat: continuous Euclidean ground-anchor range, inclusive
// boundary; configured non-occluded delivery; simultaneous impacts then summed damage.
// Terrain walkability is NOT projectile occlusion. No random numbers or sprite geometry.
public sealed partial class CombatSimulation {
 sealed class Fighter {
  public required CombatSpawn Spawn;public int Health;public CombatActivity Activity;public int? Target;
  public long NextShot,LastShot=-100000,LastHit=-100000,DeathTick=-1;public SquadUnit? DeadBody;
  public TacticalState Tactical=new();
  public bool Alive=>Health>0;
 }
 readonly SortedDictionary<int,Fighter> fighters=new();readonly List<CombatCommand> commands=new();readonly HashSet<long> sequences=new();
 readonly List<CombatReceipt> receipts=new();readonly List<CombatEvent> events=new();
 public SquadSimulation Movement {get;}
 public long Tick=>Movement.Tick;
 public int PlayerTeam {get;}
 public EncounterOutcome Outcome {get;private set;}=EncounterOutcome.Active;
 public IReadOnlyList<CombatReceipt> Receipts=>receipts;
 public IReadOnlyList<CombatEvent> Events=>events;
 public CombatUnit[] Units=>fighters.Values.Select(Snapshot).ToArray();
 public CombatUnit Unit(int id)=>Snapshot(fighters[id]);
 public CombatantDefinition Definition(int id)=>fighters[id].Spawn.Definition;
 CombatUnit Snapshot(Fighter f)=>new(f.Spawn.Id,f.Spawn.Team,f.Spawn.Definition.Id,f.Health,f.Spawn.Definition.MaxHealth,f.Alive,f.Spawn.Stationary,f.Activity,f.Target,f.NextShot,f.LastShot,f.LastHit,f.DeathTick,f.Alive?Movement.Unit(f.Spawn.Id):f.DeadBody!,engagement==null?null:f.Tactical.Snapshot);
 public CombatSimulation(NavigationGrid grid,IEnumerable<CombatSpawn> spawns,MovementSettings movement,SquadSettings? squad=null,int playerTeam=0,EngagementSettings? tactics=null,PatrolDefinition[]? patrols=null,EncounterRules? rules=null) {
  PlayerTeam=playerTeam;var input=spawns.ToArray();foreach(var s in input){s.Definition.Validate();if(s.Team<0||s.Team>7||fighters.ContainsKey(s.Id))throw new ArgumentException("Invalid team/ID");fighters.Add(s.Id,new(){Spawn=s,Health=s.Definition.MaxHealth});}
  if(!fighters.Values.Any(f=>f.Spawn.Team==playerTeam)||(tactics==null&&!fighters.Values.Any(f=>f.Spawn.Team!=playerTeam)))throw new ArgumentException("Encounter needs player units and legacy encounter both teams");
  Movement=new(grid,input.Select(s=>new UnitSpawn(s.Id,s.Position)),movement,squad);
  InitializeTactics(tactics,patrols??Array.Empty<PatrolDefinition>(),rules??new());
 }
 public void Enqueue(CombatCommand command) {
  if(command.Tick<=Tick||!sequences.Add(command.Sequence)||!Enum.IsDefined(command.Kind))throw new ArgumentException("Future tick, unique sequence and valid combat command required");
  commands.Add(command with{Units=command.Units.Distinct().Order().ToArray()});
 }
 public static bool InRange(Point a,Point b,double range){double dx=a.X-b.X,dz=a.Z-b.Z;return dx*dx+dz*dz<=range*range+1e-12;}
 bool Hostile(Fighter a,int target)=>fighters.TryGetValue(target,out var b)&&b.Alive&&b.Spawn.Team!=a.Spawn.Team;
 bool InRange(Fighter a,Fighter b)=>InRange(Movement.Unit(a.Spawn.Id).Position,Movement.Unit(b.Spawn.Id).Position,a.Spawn.Definition.Weapon.RangeTiles);
 void Cancel(Fighter f,CombatActivity state){f.Target=null;f.Activity=state;Movement.Halt(f.Spawn.Id);}
 void Command(CombatCommand c) {
  if(engagement!=null){TacticalCommand(c);return;}
  void Receipt(string result)=>receipts.Add(new(c.Tick,c.Sequence,result,c.Units));
  if(Outcome!=EncounterOutcome.Active){Receipt("EncounterFinished");return;}
  if(c.Units.Length==0){Receipt("NoUnits");return;}
  if(c.Units.Any(id=>!fighters.TryGetValue(id,out var f)||!f.Alive||f.Spawn.Team!=PlayerTeam||f.Spawn.Stationary)){Receipt("InvalidUnits");return;}
  if(c.Kind is CombatOrderKind.AttackMove or CombatOrderKind.Hold or CombatOrderKind.Guard){Receipt("UnsupportedInLegacyMode");return;}
  if(c.Kind==CombatOrderKind.Attack) {
   if(c.Units.Any(id=>!Eligible(fighters[id],c.Target))){Receipt("InvalidTarget");return;}
   int assigned=0;
   foreach(int id in c.Units){var f=fighters[id];Cancel(f,CombatActivity.Idle);f.Target=c.Target;if(InRange(f,fighters[c.Target])){f.Activity=CombatActivity.Firing;assigned++;}else if(Approach(f,c.Sequence))assigned++;}
   Receipt(assigned==c.Units.Length?"AttackAccepted":$"AttackPartial:{assigned}/{c.Units.Length}");return;
  }
  // A rejected movement order preserves the previous attack/order, just as squad movement.
  Movement.Execute(new(c.Tick,c.Sequence,c.Units,c.Kind==CombatOrderKind.Move?OrderKind.Move:OrderKind.Stop,c.Destination));
  var result=Movement.Receipts.Last().Result;
  if(result is SquadOrderResult.Accepted or SquadOrderResult.Stopped)foreach(int id in c.Units){var f=fighters[id];f.Target=null;f.Activity=c.Kind==CombatOrderKind.Move?CombatActivity.Moving:CombatActivity.Stopped;}
  Receipt(result.ToString());
 }
 bool Approach(Fighter f,long sequence) {
  var target=Movement.Unit(f.Target!.Value).Position;var start=Movement.Unit(f.Spawn.Id).Position;double range=f.Spawn.Definition.Weapon.RangeTiles;
  var candidates=new List<Point>();int r=(int)Math.Ceiling(range);var center=Movement.Grid.CellAt(target);
  for(int z=center.Z-r;z<=center.Z+r;z++)for(int x=center.X-r;x<=center.X+r;x++){var cell=new Cell(x,z);if(Movement.Grid.Walkable(cell)&&InRange(cell.Center,target,range))candidates.Add(cell.Center);}
  // Stable nearest firing cell, then Z/X. Occupied/claimed cells and static routes use
  // the squad planner. One bounded assignment attempt per attack command; no thrashing.
  foreach(var p in candidates.OrderBy(p=>start.Distance(p)).ThenBy(p=>p.Z).ThenBy(p=>p.X))if(Movement.RouteExact(f.Spawn.Id,p,sequence)){f.Activity=CombatActivity.Approaching;return true;}
  Cancel(f,CombatActivity.Blocked);return false;
 }
 public void Step() {
  long tick=Tick+1;
  foreach(var c in commands.Where(c=>c.Tick==tick).OrderBy(c=>c.Sequence).ToArray()){commands.Remove(c);Command(c);}
  if(engagement!=null)BeforeTacticalMovement();
  var before=fighters.Values.Where(f=>f.Alive).ToDictionary(f=>f.Spawn.Id,f=>Movement.Unit(f.Spawn.Id).Position);
  Movement.Step();RefreshVisibility();if(Outcome!=EncounterOutcome.Active){if(engagement!=null)AfterTacticalMovement();return;}
  if(engagement!=null)AfterTacticalMovement();
  else foreach(var f in fighters.Values.Where(f=>f.Alive)) {
   if(f.Spawn.Stationary)f.Target=fighters.Values.Where(t=>Eligible(f,t.Spawn.Id)&&InRange(f,t)).Select(t=>(int?)t.Spawn.Id).FirstOrDefault(); // lowest stable hostile ID
   if(f.Target is int id) {
    if(!Eligible(f,id)){Cancel(f,CombatActivity.Idle);continue;}
    var target=fighters[id];
    if(InRange(f,target)){Movement.Halt(f.Spawn.Id);Movement.Face(f.Spawn.Id,Movement.Unit(id).Position);f.Activity=CombatActivity.Firing;}
    else if(f.Spawn.Stationary){f.Target=null;f.Activity=CombatActivity.Idle;}
    else if(Movement.Unit(f.Spawn.Id).State is TravelState.Blocked or TravelState.Arrived){Cancel(f,CombatActivity.Blocked);} // no silent forever wait
   } else if(f.Spawn.Stationary)f.Activity=CombatActivity.Idle;
   else if(f.Activity==CombatActivity.Moving){var state=Movement.Unit(f.Spawn.Id).State;if(state==TravelState.Arrived)f.Activity=CombatActivity.Idle;else if(state==TravelState.Blocked)f.Activity=CombatActivity.Blocked;}
  }
  var shots=new List<CombatEvent>();
  foreach(var f in fighters.Values.Where(f=>f.Alive&&f.Activity==CombatActivity.Firing&&f.Target!=null&&Tick>=f.NextShot)) {
   int target=f.Target!.Value;if(!Eligible(f,target)||!InRange(f,fighters[target]))continue;
   shots.Add(Launch(f,target));f.LastShot=Tick;f.NextShot=Tick+f.Spawn.Definition.Weapon.CooldownTicks;
  }
  events.AddRange(shots);
  var impacts=AdvanceDelivery(shots,before);events.AddRange(impacts);
  foreach(var group in impacts.Where(e=>e.Kind=="Impact").GroupBy(s=>s.Target).OrderBy(g=>g.Key)) {
   var f=fighters[group.Key];int damage=Math.Min(f.Health,group.Sum(s=>s.Amount));f.Health-=damage;f.LastHit=Tick;events.Add(new(Tick,"Damage",0,group.Key,damage));
  }
  foreach(var f in fighters.Values.Where(f=>!f.Alive&&f.DeathTick<0)) {
   f.DeathTick=Tick;f.DeadBody=Movement.Unit(f.Spawn.Id) with{Moving=false,Destination=null,Route=Array.Empty<Point>(),RouteIndex=0,ReservedFrom=null,ReservedTo=null};
   f.Activity=CombatActivity.Dead;f.Target=null;Movement.Remove(f.Spawn.Id);events.Add(new(Tick,"Death",0,f.Spawn.Id,0));
   if(engagement!=null)f.Tactical=new();
  }
  foreach(var f in fighters.Values.Where(f=>f.Alive&&f.Target!=null))if(!Hostile(f,f.Target!.Value)){if(engagement!=null)EndEngagement(f,AbandonReason.TargetDead);else Cancel(f,CombatActivity.Idle);}
  RefreshVisibility();
  foreach(var f in fighters.Values.Where(f=>f.Alive&&f.Target!=null))if(!Eligible(f,f.Target!.Value)){if(engagement!=null)EndEngagement(f,AbandonReason.LostSight);else Cancel(f,CombatActivity.Idle);}
  bool friendly=fighters.Values.Any(f=>f.Alive&&f.Spawn.Team==PlayerTeam),enemy=fighters.Values.Any(f=>f.Alive&&f.Spawn.Team!=PlayerTeam);
  if(encounterRules.EliminationObjective&&(!friendly||!enemy)&&projectiles.Count==0){Outcome=!friendly&&!enemy?EncounterOutcome.Draw:enemy?EncounterOutcome.Defeat:EncounterOutcome.Victory;if(!(engagement!=null&&Outcome==EncounterOutcome.Victory&&encounterRules.ContinueAfterVictory))foreach(var f in fighters.Values.Where(f=>f.Alive))Cancel(f,CombatActivity.Idle);events.Add(new(Tick,Outcome.ToString(),0,0,0));}
 }
}
