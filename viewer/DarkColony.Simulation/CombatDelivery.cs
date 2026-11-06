namespace DarkColony.Simulation;

// Original data: straight velocity, independent missile record, speed60/256 tiles
// per original missile substep, retirement when age>35 for weapons1/15. ODC maps one
// substep to one60Hz tick provisionally. Swept square hit shape is an ODC rule.
public sealed record DeliverySettings(bool Travelling=false,double TilesPerTick=.234375,int LifetimeTicks=36,double HitHalfWidth=.18) {
 public void Validate(){if(!double.IsFinite(TilesPerTick)||TilesPerTick<=0||TilesPerTick>16||LifetimeTicks<1||LifetimeTicks>3600||!double.IsFinite(HitHalfWidth)||HitHalfWidth<=0||HitHalfWidth>.5)throw new ArgumentException("Invalid delivery settings");}
}
public sealed record Projectile(long Id,int Source,int Team,int IntendedTarget,string Weapon,int Damage,Point Position,Point Velocity,long LaunchedTick,int Age,DeliverySettings Settings);
public sealed partial class CombatSimulation {
 readonly SortedDictionary<long,Projectile> projectiles=new();long nextShotId;
 public Projectile[] Projectiles=>projectiles.Values.ToArray();
 CombatEvent Launch(Fighter f,int target) {
  long id=++nextShotId;var weapon=f.Spawn.Definition.Weapon;var from=Movement.Unit(f.Spawn.Id).Position;var to=Movement.Unit(target).Position;
  if(weapon.Delivery.Travelling){double length=from.Distance(to);if(length<=0)throw new InvalidOperationException("Overlapping launch anchors");var velocity=new Point((to.X-from.X)/length*weapon.Delivery.TilesPerTick,(to.Z-from.Z)/length*weapon.Delivery.TilesPerTick);projectiles.Add(id,new(id,f.Spawn.Id,f.Spawn.Team,target,weapon.Id,weapon.Damage,from,velocity,Tick,0,weapon.Delivery));}
  return new(Tick,"Shot",f.Spawn.Id,target,weapon.Damage,id,from);
 }
 List<CombatEvent> AdvanceDelivery(List<CombatEvent> shots,Dictionary<int,Point> before) {
  var result=new List<CombatEvent>();
  foreach(var shot in shots.Where(s=>!fighters[s.Source].Spawn.Definition.Weapon.Delivery.Travelling))result.Add(shot with{Kind="Impact",Position=Movement.Unit(shot.Target).Position});
  foreach(var p in projectiles.Values.ToArray()) {
   if(p.LaunchedTick==Tick)continue; // commit now; first movement on the next fixed tick
   var end=new Point(p.Position.X+p.Velocity.X,p.Position.Z+p.Velocity.Z);int target=0;double first=double.PositiveInfinity;
   foreach(var f in fighters.Values.Where(f=>f.Alive&&f.Spawn.Team!=p.Team)) {
    int id=f.Spawn.Id;var from=before[id];var to=Movement.Unit(id).Position;
    // Relative swept segment checks the whole movement of BOTH bodies this tick.
    double? t=Entry(new(p.Position.X-from.X,p.Position.Z-from.Z),new(end.X-to.X,end.Z-to.Z),p.Settings.HitHalfWidth);
    if(t is double time&&(time<first-1e-12||(Math.Abs(time-first)<=1e-12&&id<target))){first=time;target=id;}
   }
   if(target!=0){var at=new Point(p.Position.X+p.Velocity.X*first,p.Position.Z+p.Velocity.Z*first);result.Add(new(Tick,"Impact",p.Source,target,p.Damage,p.Id,at));projectiles.Remove(p.Id);}
   else if(p.Age+1>=p.Settings.LifetimeTicks||!Movement.Grid.InBounds(end,0)){result.Add(new(Tick,"Expired",p.Source,0,0,p.Id,end));projectiles.Remove(p.Id);}
   else projectiles[p.Id]=p with{Position=end,Age=p.Age+1};
  }
  return result.OrderBy(e=>e.ShotId).ToList();
 }
 static double? Entry(Point a,Point b,double half) {
  double lo=0,hi=1;
  bool Clip(double p,double d){if(Math.Abs(d)<1e-15)return p>=-half&&p<=half;double x=(-half-p)/d,y=(half-p)/d;if(x>y)(x,y)=(y,x);lo=Math.Max(lo,x);hi=Math.Min(hi,y);return lo<=hi;}
  return Clip(a.X,b.X-a.X)&&Clip(a.Z,b.Z-a.Z)?lo:null;
 }
}
