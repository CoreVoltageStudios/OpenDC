using DarkColony.Assets;
using DarkColony.Original;
namespace DarkColony.Diagnostics;
// Verification player. Only shared authoritative commands/purchases; no state injection.
public sealed class BreakthroughRoute {
 public int Goal {get;private set;}
 public readonly List<object> Actions=new();
 readonly Cell[] infiltration={new(67,69),new(47,83),new(26,88),new(8,93)};
 readonly Cell[] assault={new(82,95),new(58,95),new(29,88),new(12,80),new(7,72),new(6,49),new(6,30),new(7,24),new(6,14),new(6,5)};
 long last=-1,lastOrder=-1000;int leg,minersOrdered;bool returned;readonly HashSet<string> assigned=new(),guards=new();
 public void Update(OriginalWorld w){
  if(w.Tick==last||w.Tick<240||w.Tick%16!=0)return;last=w.Tick;
  void Order(NativeEntity e,OriginalOrder order,Cell c,int target=-1){if(w.Command(new(e.Slot,e.Incarnation,order,c.X*256+128,c.Z*256+128,target)))Actions.Add(new{w.Tick,e.Identity,order,c,target});}
  var own=w.Entities.Where(e=>e.Living&&e.Owner==0).ToArray();var commander=own.FirstOrDefault(w.IsCommander);
  bool depot=w.Session.ScriptBlocks[16]!.Remaining==0;
  if(!depot){
   if(commander==null)return;
   if(Math.Abs(commander.Cell.X-infiltration[leg].X)+Math.Abs(commander.Cell.Z-infiltration[leg].Z)<4&&leg<infiltration.Length-1){leg++;lastOrder=-1000;}
   Goal=leg;
   if(w.Tick-lastOrder>180){lastOrder=w.Tick;Order(commander,OriginalOrder.MoveOnly,infiltration[leg]);}
   return;
  }
  if(!returned&&commander!=null&&commander.Cell.X>85){returned=true;Goal=4;leg=0;lastOrder=-1000;}
  if(!returned)return;
  var miners=own.Where(e=>e.Type==14).ToArray();
  foreach(var miner in miners)if(assigned.Add(miner.Identity)){Order(miner,OriginalOrder.MoveOnly,own.Any(e=>e.Type==48&&e.Cell.X==86)?new(58,95):new(86,94));minersOrdered++;}
  if(w.Live[0,14]+w.Live[0,48]<(Goal>=6?2:1)&&w.Teams[0].Production.All(q=>!q.Types.Contains(14))){var r=w.RequestProduction(0,21);if(r.Accepted)Actions.Add(new{w.Tick,product=21});return;}
  foreach(int id in new[]{14,15,16,17,18,19,41,43})if(w.DependencyState(0,id)==1){var r=w.RequestProduction(0,id);if(r.Accepted)Actions.Add(new{w.Tick,product=id});return;}
  int product=own.Count(e=>e.Type==10)<16?25:own.Count(e=>e.Type==11)<8?26:25;
  var d=w.Data.Scenario.Dependencies.First(d=>d.Id==product);if(w.DependencyState(0,product)==1&&!w.Teams[0].Production.Any(q=>q.Types.Contains(d.Subject))){var r=w.RequestProduction(0,product);if(r.Accepted)Actions.Add(new{w.Tick,product});}
  foreach(var e in own.Where(e=>e.Type is 8 or 10))if(e.Type==8||guards.Count(id=>own.Any(u=>u.Identity==id&&u.Type==10))<6)guards.Add(e.Identity);
  var army=own.Where(e=>(e.Type is 8 or 10 or 11 or 13||w.IsCommander(e))&&!guards.Contains(e.Identity)).ToArray();
  if(Goal==4&&army.Count(e=>e.Type==10)>=9&&army.Count(e=>e.Type==11)>=6){Goal=5;leg=0;lastOrder=-1000;}
  var dest=Goal==4?new Cell(97,83):assault[leg];
  if(Goal>4&&leg<assault.Length-1&&army.Count(e=>Math.Abs(e.Cell.X-dest.X)+Math.Abs(e.Cell.Z-dest.Z)<12)>=7){dest=assault[++leg];Goal=5+leg;lastOrder=-1000;}
  if(w.Tick-lastOrder<200)return;lastOrder=w.Tick;
  foreach(var e in own.Where(e=>guards.Contains(e.Identity)))Order(e,OriginalOrder.MoveAttack,new(98+e.Slot%4,80+e.Slot%3));
  foreach(var e in army){
   if(w.IsCommander(e)&&e.Energy>=32){Order(e,OriginalOrder.Inspire,e.Cell);continue;}

   var weapon=w.Weapon(e.Type,e.Owner)!;
   var target=w.Entities.Where(t=>t.Living&&t.Owner<8&&!w.Relations[0,t.Owner]&&w.Visible(0,t.Cell)&&w.Data.DamageFactors[weapon.Class][w.Data.Scenario.Types[t.Type].Fields[10]]!=0&&Math.Abs(t.Cell.X-e.Cell.X)+Math.Abs(t.Cell.Z-e.Cell.Z)<(e.Type==11?15:7)).OrderBy(t=>Math.Abs(t.Cell.X-e.Cell.X)+Math.Abs(t.Cell.Z-e.Cell.Z)).FirstOrDefault();
   if(Goal>=12){var city=w.Teams[1].Cities.Take(5).Select(c=>c.Entity).Where(c=>c is {Living:true}&&w.Visible(0,c.Cell)).OrderBy(c=>Math.Abs(c!.Cell.X-e.Cell.X)+Math.Abs(c.Cell.Z-e.Cell.Z)).FirstOrDefault();if(city!=null&&(e.Type==11||target==null))target=city;}
   if(target!=null)Order(e,OriginalOrder.Attack,target.Cell,target.Slot);else Order(e,OriginalOrder.MoveAttack,new(Math.Clamp(dest.X+e.Slot%5-2,1,w.Data.Scenario.Map.Width-2),Math.Clamp(dest.Z+(e.Slot/5)%5-2,1,w.Data.Scenario.Map.Height-3)));
  }
 }
}
