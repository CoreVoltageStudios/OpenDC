using DarkColony.Assets;
using DarkColony.Original;
namespace DarkColony.Diagnostics;
// Bounded diagnostic player. All changes use player commands/purchases, God OFF.
// No health/resources/AI/vision/script writes. Fixed map waypoints are strategy.
public sealed class CivilWarRoute {
 public int Goal {get;private set;}
 public readonly List<object> Actions=new();
 readonly Dictionary<string,Cell> miners=new();
 readonly HashSet<string> staging=new();
 long last=-1,lastOrder=-1000;int leg;
 readonly Cell[][] routes={new[]{new Cell(36,73),new Cell(30,55),new Cell(26,39),new Cell(19,29)},new[]{new Cell(35,24),new Cell(54,24),new Cell(77,25),new Cell(99,21)},new[]{new Cell(100,39),new Cell(100,60),new Cell(100,75)}};
 public void Update(OriginalWorld w){
  if(w.GodMode)throw new InvalidOperationException("Ordinary route requires God mode OFF");
  if(w.Tick==last||w.Tick<16||w.Tick%16!=0)return;last=w.Tick;
  var own=w.Entities.Where(e=>e.Living&&e.Owner==0).ToArray();
  void Order(NativeEntity e,OriginalOrder order,Cell c,int target=-1){if(w.Command(new(e.Slot,e.Incarnation,order,c.X*256+128,c.Z*256+128,target)))Actions.Add(new{w.Tick,e.Identity,order,c,target});}
  bool Buy(int id){var result=w.RequestProduction(0,id);if(result.Accepted)Actions.Add(new{w.Tick,product=id});return result.Accepted;}
  foreach(var miner in own.Where(e=>e.Type==14)){
   if(miners.TryGetValue(miner.Identity,out var cell)){
    if(w.Tick%240==0&&miner.Cell!=cell&&miner.Actions.Count<=2)Order(miner,OriginalOrder.MoveOnly,cell);
    if(w.Entities.Any(e=>e.Living&&e.Type==40&&e.Cell==cell&&e.ResourceFlow>0))continue;
    miners.Remove(miner.Identity);
   }
   var vent=w.Entities.Where(e=>e.Living&&e.Type==40&&e.ResourceFlow>0&&w.Visible(0,e.Cell)&&!miners.Values.Contains(e.Cell)&&!own.Any(u=>u.Type==48&&u.Cell==e.Cell))
    .OrderBy(e=>Math.Abs(e.Cell.X-miner.Cell.X)+Math.Abs(e.Cell.Z-miner.Cell.Z)).FirstOrDefault();
   if(vent!=null){miners[miner.Identity]=vent.Cell;Order(miner,OriginalOrder.MoveOnly,vent.Cell);}
  }
  foreach(string id in miners.Keys.Where(id=>!own.Any(e=>e.Identity==id)).ToArray())miners.Remove(id);
  foreach(int id in new[]{14,15,16,17,18,19,20})if(w.DependencyState(0,id)==1&&Buy(id))break;
  if(own.Count(e=>e.Type is 14 or 48)<3&&w.Tick>3500&&!w.Teams[0].Production.Any(q=>q.Types.Contains(14)))Buy(21);
  int armySize=own.Count(e=>e.Type is 8 or 10 or 11);
  if(armySize>=12)foreach(int id in new[]{30,32,41,43,49,51,31,33})if(w.DependencyState(0,id)==1&&w.Teams[0].Money>2500&&Buy(id))break;
  foreach(var (id,type,count) in new[]{(25,10,18),(26,11,14),(23,8,28),(84,50,5)})
   if(own.Count(e=>e.Type==type)<count&&!w.Teams[0].Production.Any(q=>q.Types.Contains(type)))Buy(id);
  foreach(var scout in own.Where(e=>e.Type==13))if(w.Tick%600==0)Order(scout,OriginalOrder.MoveOnly,new(57,48));
  if(Goal==0&&own.Count(e=>e.Type==10)>=12&&own.Count(e=>e.Type==11)>=7){Goal=1;leg=0;lastOrder=-1000;}
  if(Goal>0){
   int targetTeam=Goal==1?2:Goal==2?7:1;
   if(w.Teams[targetTeam].Cities.Take(5).All(c=>c.Health==0)){Goal++;leg=0;lastOrder=-1000;}
  }
  if(Goal>3)return;
  var army=own.Where(e=>e.Type is 8 or 10 or 11 or 50||w.IsCommander(e)).ToArray();
  var destination=Goal==0?new Cell(38,81):routes[Goal-1][leg];
  if(Goal>0&&leg<routes[Goal-1].Length-1&&army.Count(e=>Math.Abs(e.Cell.X-destination.X)+Math.Abs(e.Cell.Z-destination.Z)<12)>=Math.Min(15,army.Length/2)){
   destination=routes[Goal-1][++leg];lastOrder=-1000;
  }
  // Newly produced units reinforce the current group immediately without
  // replacing every existing unit's order on each production check.
  foreach(var e in army)if(staging.Add(e.Identity))Order(e,OriginalOrder.MoveOnly,destination);
  if(w.Tick-lastOrder<180)return;lastOrder=w.Tick;
  foreach(var e in army){
   if(w.IsCommander(e)&&e.Energy>=32){Order(e,OriginalOrder.Inspire,e.Cell);continue;}
   var weapon=w.Weapon(e.Type,e.Owner);
   var target=weapon==null?null:w.Entities.Where(t=>t.Living&&t.Owner<8&&!w.Relations[0,t.Owner]&&w.Visible(0,t.Cell)&&w.Data.DamageFactors[weapon.Class][w.Data.Scenario.Types[t.Type].Fields[10]]!=0&&Math.Abs(t.Cell.X-e.Cell.X)+Math.Abs(t.Cell.Z-e.Cell.Z)<(e.Type==11?18:8))
    .OrderBy(t=>Math.Abs(t.Cell.X-e.Cell.X)+Math.Abs(t.Cell.Z-e.Cell.Z)).FirstOrDefault();
   if(Goal>0&&leg==routes[Goal-1].Length-1){
    int team=Goal==1?2:Goal==2?7:1;var city=w.Teams[team].Cities.Take(5).Select(c=>c.Entity).Where(c=>c is {Living:true}&&w.Visible(0,c.Cell)).OrderBy(c=>Math.Abs(c!.Cell.X-e.Cell.X)+Math.Abs(c.Cell.Z-e.Cell.Z)).FirstOrDefault();
    if(city!=null&&(target==null||e.Type==11))target=city;
   }
   if(target!=null)Order(e,OriginalOrder.Attack,target.Cell,target.Slot);
   else Order(e,Goal==0||weapon==null?OriginalOrder.MoveOnly:OriginalOrder.MoveAttack,new(destination.X+e.Slot%5-2,destination.Z+e.Slot/5%5-2));
  }
 }
}
