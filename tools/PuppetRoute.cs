using DarkColony.Assets;
using DarkColony.Original;
namespace DarkColony.Diagnostics;
// Diagnostic player: only ordinary unit commands and production requests. No
// economy, visibility, health, objectives, AI or mission-state injection.
public sealed class PuppetRoute {
 public int Goal {get;private set;}
 public readonly List<object> Actions=new();
 readonly Dictionary<string,Cell> miners=new();
 readonly HashSet<string> staged=new();
 long last=-1,lastOrder=-1000;int leg;
 readonly Cell[] route={new(28,100),new(46,99),new(64,97),new(81,91),new(95,86),new(96,76),new(94,69)};
 public void Update(OriginalWorld w){
  if(w.Tick==last||w.Tick<240||w.Tick%16!=0)return;last=w.Tick;
  void Order(NativeEntity e,OriginalOrder order,Cell c,int target=-1){if(w.Command(new(e.Slot,e.Incarnation,order,c.X*256+128,c.Z*256+128,target)))Actions.Add(new{w.Tick,e.Identity,order,c,target});}
  var own=w.Entities.Where(e=>e.Living&&e.Owner==0).ToArray();
  foreach(var worker in own.Where(e=>e.Type==14)){
   if(miners.TryGetValue(worker.Identity,out var cell)){
    if(w.Tick%240==0&&worker.Cell!=cell&&worker.Actions.Count<=2)Order(worker,OriginalOrder.MoveOnly,cell);
    if(!w.Entities.Any(e=>e.Living&&e.Type==40&&e.Cell==cell&&e.ResourceFlow>0)){miners.Remove(worker.Identity);}else continue;
   }
   var vent=w.Entities.Where(e=>e.Living&&e.Type==40&&e.ResourceFlow>0&&w.Visible(0,e.Cell)&&!miners.Values.Contains(e.Cell)&&!own.Any(u=>u.Type==48&&u.Cell==e.Cell))
    .OrderBy(e=>Math.Abs(e.Cell.X-worker.Cell.X)+Math.Abs(e.Cell.Z-worker.Cell.Z)).FirstOrDefault();
   if(vent!=null){miners[worker.Identity]=vent.Cell;Order(worker,OriginalOrder.MoveOnly,vent.Cell);}
  }
  foreach(var name in miners.Keys.Where(id=>!own.Any(e=>e.Identity==id)).ToArray())miners.Remove(name);
  int count=own.Count(e=>e.Type is 14 or 48);
  if(w.Tick>1000&&count<2&&!w.Teams[0].Production.Any(q=>q.Types.Contains(14)))Buy(21);
  // Native roster prerequisites; no altered costs or build rates.
  foreach(int id in new[]{14,15,16,17,18,19,20,41,43})if(w.DependencyState(0,id)==1&&Buy(id))break;
  bool Buy(int id){var r=w.RequestProduction(0,id);if(r.Accepted)Actions.Add(new{w.Tick,product=id});return r.Accepted;}
  foreach(int id in new[]{25,26,23}){
   int desired=id==25?18:id==26?10:8;var d=w.Data.Scenario.Dependencies.First(d=>d.Id==id);
   if(own.Count(e=>e.Type==d.Subject)<desired&&!w.Teams[0].Production.Any(q=>q.Types.Contains(d.Subject)))Buy(id);
  }
  var commander=own.FirstOrDefault(w.IsCommander);
  // Preserve the original northern reinforcement party during base development.
  foreach(var e in own.Where(e=>e.Type is 8 or 65||w.IsCommander(e)))if(staged.Add(e.Identity))Order(e,OriginalOrder.MoveOnly,new(16,96));
  foreach(var scout in own.Where(e=>e.Type==13))if(w.Tick%480==0)Order(scout,OriginalOrder.MoveOnly,new(42,102));
  var army=own.Where(e=>e.Type is 8 or 10 or 11||w.IsCommander(e)).ToArray();
  if(Goal==0&&army.Count(e=>e.Type==10)>=12&&army.Count(e=>e.Type==11)>=6){Goal=1;lastOrder=-1000;}
  var dest=Goal==0?new Cell(18,96):route[leg];
  if(Goal>0&&leg<route.Length-1&&army.Count(e=>Math.Abs(e.Cell.X-dest.X)+Math.Abs(e.Cell.Z-dest.Z)<12)>=12){dest=route[++leg];Goal=1+leg;lastOrder=-1000;}
  if(w.Tick-lastOrder<240)return;lastOrder=w.Tick;
  foreach(var e in army){
   if(w.IsCommander(e)&&e.Energy>=32){Order(e,OriginalOrder.Inspire,e.Cell);continue;}
   var weapon=w.Weapon(e.Type,e.Owner);if(weapon==null)continue;
   var target=w.Entities.Where(t=>t.Living&&t.Owner<8&&!w.Relations[0,t.Owner]&&w.Visible(0,t.Cell)&&w.Data.DamageFactors[weapon.Class][w.Data.Scenario.Types[t.Type].Fields[10]]!=0&&Math.Abs(t.Cell.X-e.Cell.X)+Math.Abs(t.Cell.Z-e.Cell.Z)<(e.Type==11?17:7)).OrderBy(t=>Math.Abs(t.Cell.X-e.Cell.X)+Math.Abs(t.Cell.Z-e.Cell.Z)).FirstOrDefault();
   if(leg==route.Length-1){var city=w.Teams[2].Cities.Take(5).Select(c=>c.Entity).Where(c=>c is {Living:true}&&w.Visible(0,c.Cell)).OrderBy(c=>Math.Abs(c!.Cell.X-e.Cell.X)+Math.Abs(c.Cell.Z-e.Cell.Z)).FirstOrDefault();if(city!=null&&(e.Type==11||target==null))target=city;}
   if(target!=null)Order(e,OriginalOrder.Attack,target.Cell,target.Slot);
   else Order(e,Goal==0?OriginalOrder.MoveOnly:OriginalOrder.MoveAttack,new(dest.X+e.Slot%5-2,dest.Z+e.Slot/5%5-2));
  }
  if(Goal>0)foreach(var e in own.Where(e=>e.Type==65)){
   var enemies=w.Entities.Where(t=>t.Living&&t.Owner==2&&t.Slot>=120&&w.Visible(0,t.Cell)).OrderBy(t=>Math.Abs(t.Cell.X-e.Cell.X)+Math.Abs(t.Cell.Z-e.Cell.Z)).ToArray();
   if(enemies.Length==0)continue;var t=enemies[0];
   if(enemies.Count(t=>Math.Abs(t.Cell.X-e.Cell.X)<=5&&Math.Abs(t.Cell.Z-e.Cell.Z)<=5)>=3)Order(e,OriginalOrder.Deploy,e.Cell);
   else Order(e,OriginalOrder.MoveOnly,t.Cell);
  }
 }
}
