namespace DarkColony.Simulation;
public sealed record EngagementSettings(double AcquisitionRange=6,double PursuitDistance=12,int AttackMoveTicks=600,int ExplicitAttackTicks=1200,int ReplanInterval=60,int MaxReplans=8,int MaxPathFailures=3,int ReacquireDelay=180,int ResumeAttempts=3) {
 public void Validate(){if(!double.IsFinite(AcquisitionRange)||AcquisitionRange<=0||AcquisitionRange>16||!double.IsFinite(PursuitDistance)||PursuitDistance<AcquisitionRange||PursuitDistance>32||AttackMoveTicks<1||ExplicitAttackTicks<1||ExplicitAttackTicks>3600||AttackMoveTicks>3600||ReplanInterval<1||MaxReplans<1||MaxReplans>32||MaxPathFailures<1||MaxPathFailures>8||ReacquireDelay<1||ResumeAttempts<1||ResumeAttempts>8)throw new ArgumentException("Unsupported pursuit settings");}
}
public sealed record PatrolDefinition(int UnitId,Point[] Waypoints,bool Engage=true,int DwellTicks=30);
public sealed record EncounterRules(bool EliminationObjective=true,bool ContinueAfterVictory=false);
public enum TacticalOrder { None,Move,Stop,Attack,AttackMove,Patrol,Hold,Guard }
public enum AbandonReason { TargetDead,Leash,Timeout,NoPath,ReplanBudget,LostSight }
public sealed record TacticalUnit(TacticalOrder Order,Point? Destination,long EngagementStart,int Replans,int PathFailures,int ResumeFailures,bool ResumeBlocked,int PatrolWaypoint,int Recoveries,Point? GuardAnchor=null,bool Returning=false,long ReadyAt=0);
public sealed partial class CombatSimulation {
 sealed class TacticalState {
  public TacticalOrder Order;public Point? Destination;public Point Origin;public long Start,NextPlan,NextResume;public int Plans,Failures,ResumeFailures,PatrolWaypoint,Recoveries;public bool ResumeNeeded,ResumeBlocked;
  public Dictionary<int,long> IgnoredUntil=new();
  public Point? GuardAnchor;public bool Returning;public long ReadyAt;
  public TacticalUnit Snapshot=>new(Order,Destination,Start,Plans,Failures,ResumeFailures,ResumeBlocked,PatrolWaypoint,Recoveries,GuardAnchor,Returning,ReadyAt);
 }
 EngagementSettings? engagement;EncounterRules encounterRules=new();readonly Dictionary<int,PatrolDefinition> patrols=new();long routeSequence;
 void InitializeTactics(EngagementSettings? settings,PatrolDefinition[] definitions,EncounterRules rules) {
  engagement=settings;encounterRules=rules;if(settings==null){if(definitions.Length!=0)throw new ArgumentException("Patrol needs engagement settings");return;}settings.Validate();
  foreach(var p in definitions.OrderBy(p=>p.UnitId)) {
   if(!fighters.TryGetValue(p.UnitId,out var f)||p.Waypoints.Length<1||p.Waypoints.Length>16||p.DwellTicks<0||p.DwellTicks>600||p.Waypoints.Any(w=>!Movement.Grid.Clear(w,w,Movement.Movement.Radius))||!patrols.TryAdd(p.UnitId,p with{Waypoints=(Point[])p.Waypoints.Clone()}))throw new ArgumentException("Invalid patrol definition");
   f.Tactical.Order=TacticalOrder.Patrol;f.Tactical.Destination=p.Waypoints[0];f.Tactical.ResumeNeeded=true;
  }
 }
 void TacticalCommand(CombatCommand c) {
  void Receipt(string result)=>receipts.Add(new(c.Tick,c.Sequence,result,c.Units));
  if(Outcome!=EncounterOutcome.Active){Receipt("EncounterFinished");return;}
  if(c.Units.Length==0){Receipt("NoUnits");return;}
  if(c.Units.Any(id=>!fighters.TryGetValue(id,out var f)||!f.Alive||f.Spawn.Team!=PlayerTeam||f.Spawn.Stationary)){Receipt("InvalidUnits");return;}
  if(c.Kind is CombatOrderKind.Hold or CombatOrderKind.Guard){foreach(int id in c.Units){var f=fighters[id];Movement.Halt(id);f.Target=null;var anchor=Movement.Unit(id).Position;f.Tactical=new(){Order=c.Kind==CombatOrderKind.Hold?TacticalOrder.Hold:TacticalOrder.Guard,GuardAnchor=c.Kind==CombatOrderKind.Guard?anchor:null,Destination=c.Kind==CombatOrderKind.Guard?anchor:null};f.Activity=c.Kind==CombatOrderKind.Hold?CombatActivity.Holding:CombatActivity.Guarding;}Receipt(c.Kind+"Accepted");return;}
  if(c.Kind==CombatOrderKind.Attack) {
   if(c.Units.Any(id=>!Eligible(fighters[id],c.Target))){Receipt("InvalidTarget");return;}
   foreach(int id in c.Units){var f=fighters[id];Movement.Halt(id);f.Tactical=new(){Order=TacticalOrder.Attack};BeginEngagement(f,c.Target,c.Tick);}
   Receipt("AttackAccepted");return;
  }
  Movement.Execute(new(c.Tick,++routeSequence,c.Units,c.Kind==CombatOrderKind.Stop?OrderKind.Stop:OrderKind.Move,c.Destination));
  var result=Movement.Receipts.Last();
  if(result.Result is SquadOrderResult.Accepted or SquadOrderResult.Stopped)foreach(int id in c.Units) {
   var f=fighters[id];f.Target=null;f.Tactical=new(){Order=c.Kind switch{CombatOrderKind.Stop=>TacticalOrder.Stop,CombatOrderKind.AttackMove=>TacticalOrder.AttackMove,_=>TacticalOrder.Move},Destination=result.Assignments.FirstOrDefault(a=>a.Id==id)?.Destination};f.Activity=c.Kind==CombatOrderKind.Stop?CombatActivity.Stopped:CombatActivity.Moving;
  }
  Receipt(result.Result==SquadOrderResult.Accepted&&c.Kind==CombatOrderKind.AttackMove?"AttackMoveAccepted":result.Result.ToString());
 }
 void BeginEngagement(Fighter f,int target,long tick) {
  var t=f.Tactical;Movement.Halt(f.Spawn.Id);f.Target=target;t.Start=tick;t.Origin=t.GuardAnchor??Movement.Unit(f.Spawn.Id).Position;t.Plans=t.Failures=0;t.NextPlan=tick;t.ResumeNeeded=false;f.Activity=CombatActivity.Approaching;
  events.Add(new(tick,"Acquired",f.Spawn.Id,target,0));
 }
 void EndEngagement(Fighter f,AbandonReason reason,long? atTick=null) {
  long stamp=atTick??Tick;
  int target=f.Target??0;var t=f.Tactical;Movement.Halt(f.Spawn.Id);f.Target=null;
  if(reason!=AbandonReason.TargetDead&&target!=0)t.IgnoredUntil[target]=stamp+engagement!.ReacquireDelay;
  events.Add(new(stamp,reason==AbandonReason.TargetDead?"TargetEnded":"Abandoned",f.Spawn.Id,target,(int)reason));
  if(t.Order==TacticalOrder.Attack){t.Order=TacticalOrder.None;t.Destination=null;f.Activity=reason==AbandonReason.TargetDead?CombatActivity.Idle:CombatActivity.Blocked;}
  else {t.ResumeNeeded=t.Destination!=null;t.NextResume=stamp+1;t.Returning=t.Order==TacticalOrder.Guard;f.Activity=t.Returning?CombatActivity.Returning:CombatActivity.Idle;}
 }
 bool Automatic(Fighter f)=>!f.Tactical.ResumeBlocked&&(f.Tactical.Order==TacticalOrder.AttackMove||(f.Tactical.Order==TacticalOrder.Guard&&!f.Tactical.Returning&&Tick+1>=f.Tactical.ReadyAt)||(f.Tactical.Order==TacticalOrder.Patrol&&patrols[f.Spawn.Id].Engage));
 void BeforeTacticalMovement() {
  if(Outcome!=EncounterOutcome.Active&&!(Outcome==EncounterOutcome.Victory&&encounterRules.ContinueAfterVictory))return;
  long tick=Tick+1;var settings=engagement!;
  foreach(var f in fighters.Values.Where(f=>f.Alive)) {
   var t=f.Tactical;var body=Movement.Unit(f.Spawn.Id);
   if(TryFinishGuardReturn(f,tick))continue;
   if(t.Order==TacticalOrder.Hold){UpdateHold(f,tick);continue;}
   if(f.Target is int target) {
    if(!Hostile(f,target))EndEngagement(f,AbandonReason.TargetDead,tick);
    else if(!Eligible(f,target))EndEngagement(f,AbandonReason.LostSight,tick);
    else if(tick-t.Start>=(t.Order==TacticalOrder.Attack?settings.ExplicitAttackTicks:settings.AttackMoveTicks))EndEngagement(f,AbandonReason.Timeout,tick);
    else if(!InRange(t.Origin,Movement.Unit(target).Position,PursuitLimit(f))||!InRange(t.Origin,body.Position,PursuitLimit(f)))EndEngagement(f,AbandonReason.Leash,tick);
   }
   if(f.Target==null&&Outcome==EncounterOutcome.Active&&Automatic(f)) {
    int? candidateId=AcquireCandidate(f,settings.AcquisitionRange,tick);
    if(candidateId is int id)BeginEngagement(f,id,tick);
   }
   if(f.Target is int id2) {
    var enemy=fighters[id2];
    if(InRange(f,enemy)){Movement.Halt(f.Spawn.Id);Movement.Face(f.Spawn.Id,Movement.Unit(id2).Position);f.Activity=CombatActivity.Firing;continue;}
    f.Activity=CombatActivity.Approaching;body=Movement.Unit(f.Spawn.Id);
    // Keep an adequate route; retarget only at a segment boundary and no more often
    // than the configured interval. The independent budget survives route resets.
    bool adequate=body.RouteIndex<body.Route.Length&&body.WaitTicks<settings.ReplanInterval&&body.Destination is Point dest&&InRange(dest,Movement.Unit(id2).Position,f.Spawn.Definition.Weapon.RangeTiles);
    if(!adequate&&body.ReservedTo==null&&tick>=t.NextPlan) {
     if(t.Plans>=settings.MaxReplans){EndEngagement(f,AbandonReason.ReplanBudget,tick);continue;}
     t.Plans++;t.NextPlan=tick+settings.ReplanInterval;
     if(!TacticalFiringRoute(f)){t.Failures++;if(t.Failures>=settings.MaxPathFailures)EndEngagement(f,AbandonReason.NoPath,tick);}
    }
    continue;
   }
   if(t.ResumeNeeded&&!t.ResumeBlocked&&t.Destination is Point destination&&tick>=t.NextResume) {
    if(Movement.RouteExact(f.Spawn.Id,destination,++routeSequence,true,t.GuardAnchor,Guard.Distance)){t.ResumeNeeded=false;t.ResumeFailures=0;f.Activity=t.Returning?CombatActivity.Returning:CombatActivity.Moving;events.Add(new(tick,"Resumed",f.Spawn.Id,0,0));}
    else {t.ResumeFailures++;t.NextResume=tick+settings.ReplanInterval;if(t.ResumeFailures>=settings.ResumeAttempts){t.ResumeBlocked=true;t.ResumeNeeded=false;f.Activity=CombatActivity.Blocked;events.Add(new(tick,"ResumeBlocked",f.Spawn.Id,0,0));}}
   }
  }
 }
 bool TacticalFiringRoute(Fighter f) {
  var target=Movement.Unit(f.Target!.Value).Position;var position=Movement.Unit(f.Spawn.Id).Position;double range=f.Spawn.Definition.Weapon.RangeTiles;var center=Movement.Grid.CellAt(target);int r=(int)Math.Ceiling(range);var candidates=new List<Point>();
  if(f.Spawn.Stationary)return false;
  for(int z=center.Z-r;z<=center.Z+r;z++)for(int x=center.X-r;x<=center.X+r;x++){var c=new Cell(x,z);if(Movement.Grid.Walkable(c)&&InRange(c.Center,target,range)&&InRange(f.Tactical.Origin,c.Center,PursuitLimit(f)))candidates.Add(c.Center);}
  foreach(var p in candidates.OrderBy(p=>position.Distance(p)).ThenBy(p=>p.Z).ThenBy(p=>p.X))if(Movement.RouteExact(f.Spawn.Id,p,++routeSequence,true,f.Tactical.GuardAnchor,Guard.Distance)){events.Add(new(Tick+1,"PursuitPlan",f.Spawn.Id,f.Target.Value,f.Tactical.Plans));return true;}
  return false;
 }
 void AfterTacticalMovement() {
  foreach(var f in fighters.Values.Where(f=>f.Alive)) {
   var t=f.Tactical;var body=Movement.Unit(f.Spawn.Id);
   if(t.Order==TacticalOrder.Hold){UpdateHold(f,Tick);continue;}
   if(t.Order==TacticalOrder.Guard&&t.Returning&&body.Position.Distance(t.GuardAnchor!.Value)<=Movement.Movement.ArrivalTolerance){Movement.Halt(f.Spawn.Id);t.Returning=t.ResumeNeeded=false;t.ResumeFailures=t.Recoveries=0;t.ReadyAt=Tick+Guard.RearmTicks;f.Activity=CombatActivity.Guarding;events.Add(new(Tick,"GuardReturned",f.Spawn.Id,0,0));continue;}
   if(f.Target is int hidden&&!Eligible(f,hidden)){EndEngagement(f,Hostile(f,hidden)?AbandonReason.LostSight:AbandonReason.TargetDead);continue;}
   if(f.Target is int id) {
    // Target may have moved later in the same movement pass. Damage eligibility is
    // tested here AND again when gathering authoritative shot intents.
    if(Hostile(f,id)&&InRange(f,fighters[id])){Movement.Halt(f.Spawn.Id);Movement.Face(f.Spawn.Id,Movement.Unit(id).Position);f.Activity=CombatActivity.Firing;}
    else f.Activity=CombatActivity.Approaching;
   } else if(!t.ResumeNeeded&&!t.ResumeBlocked&&body.State==TravelState.Arrived) {
    if(t.Order==TacticalOrder.Patrol){var patrol=patrols[f.Spawn.Id];t.PatrolWaypoint=(t.PatrolWaypoint+1)%patrol.Waypoints.Length;t.Destination=patrol.Waypoints[t.PatrolWaypoint];t.ResumeNeeded=true;t.NextResume=Tick+patrol.DwellTicks;f.Activity=CombatActivity.Idle;}
    else if(t.Order is TacticalOrder.Move or TacticalOrder.AttackMove){events.Add(new(Tick,"DestinationReached",f.Spawn.Id,0,0));t.Order=TacticalOrder.None;t.Destination=null;f.Activity=CombatActivity.Idle;}
   } else if(body.State==TravelState.Blocked&&!t.ResumeBlocked) {
    if(t.Order is TacticalOrder.AttackMove or TacticalOrder.Patrol or TacticalOrder.Guard&&t.Recoveries<engagement!.ResumeAttempts){t.Recoveries++;t.ResumeNeeded=true;t.NextResume=Tick+engagement.ReplanInterval;Movement.Halt(f.Spawn.Id);events.Add(new(Tick,"RouteRecovery",f.Spawn.Id,0,t.Recoveries));}
    else {t.ResumeBlocked=true;f.Activity=CombatActivity.Blocked;}
   }
  }
 }
}
