namespace DarkColony.Simulation;
public sealed record GuardSettings(double Distance=6,int RearmTicks=180) {
 public void Validate(){if(!double.IsFinite(Distance)||Distance<=0||Distance>16||RearmTicks<1||RearmTicks>3600)throw new ArgumentException("Guard settings");}
}
public sealed partial class CombatSimulation {
 public GuardSettings Guard {get;private set;}=new();
 public void ConfigureGuard(GuardSettings settings){if(Tick!=0)throw new InvalidOperationException("Configure before ticks");settings.Validate();Guard=settings;}
 double PursuitLimit(Fighter f)=>f.Tactical.Order==TacticalOrder.Guard?Guard.Distance:engagement!.PursuitDistance;
 bool TryFinishGuardReturn(Fighter f,long tick){var t=f.Tactical;if(t.Order!=TacticalOrder.Guard||!t.Returning||Movement.Unit(f.Spawn.Id).Position.Distance(t.GuardAnchor!.Value)>Movement.Movement.ArrivalTolerance)return false;Movement.Halt(f.Spawn.Id);t.Returning=t.ResumeNeeded=false;t.ResumeFailures=t.Recoveries=0;t.ReadyAt=tick+Guard.RearmTicks;f.Activity=CombatActivity.Guarding;events.Add(new(tick,"GuardReturned",f.Spawn.Id,0,0));return true;}
 int? AcquireCandidate(Fighter f,double range,long tick) {
  var t=f.Tactical;var pos=Movement.Unit(f.Spawn.Id).Position;
  return fighters.Values.Where(e=>Eligible(f,e.Spawn.Id)&&(!t.IgnoredUntil.TryGetValue(e.Spawn.Id,out long until)||tick>=until)&&InRange(pos,Movement.Unit(e.Spawn.Id).Position,range)&&(t.GuardAnchor is not Point home||InRange(home,Movement.Unit(e.Spawn.Id).Position,Guard.Distance))).OrderBy(e=>pos.Distance(Movement.Unit(e.Spawn.Id).Position)).ThenBy(e=>e.Spawn.Id).Select(e=>(int?)e.Spawn.Id).FirstOrDefault();
 }
 void UpdateHold(Fighter f,long tick) {
  Movement.Halt(f.Spawn.Id); // current footprint remains occupied; unused route swept reservation is released
  if(f.Target is int target&&(!Eligible(f,target)||!InRange(f,fighters[target])))f.Target=null;
  if(f.Target==null&&Outcome==EncounterOutcome.Active){int? candidate=AcquireCandidate(f,f.Spawn.Definition.Weapon.RangeTiles,tick);if(candidate is int id){f.Target=id;events.Add(new(tick,"Acquired",f.Spawn.Id,id,0));}}
  if(f.Target is int id2){Movement.Face(f.Spawn.Id,Movement.Unit(id2).Position);f.Activity=CombatActivity.Firing;}else f.Activity=CombatActivity.Holding;
 }
}
