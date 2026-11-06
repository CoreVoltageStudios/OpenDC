using DarkColony.Assets;
using DarkColony.Original;
namespace DarkColony.Diagnostics;

// Verification player, never used outside explicit diagnostic suites. No state
// injection: earned resources, authoritative purchases and ordinary player orders.
public sealed class InterferenceRoute {
 public int Goal {get;private set;}=-1;
 public readonly List<object> Actions=new();
 readonly HashSet<string> assembled=new();
 readonly List<string> guards=new();
 readonly Cell[] route={new(25,68),new(43,65),new(60,55),new(82,55),new(90,53),new(79,42),new(63,31),new(41,32),new(30,30),new(28,20),new(24,18),new(32,15)};
 long last=-1,lastOrder=-1000;int bought;bool minerSent,secondVent;
 public void Update(OriginalWorld w){
  if(w.Tick==last||w.Tick<180||w.Tick%16!=0)return;last=w.Tick;
  var own=w.Entities.Where(e=>e.Living&&e.Owner==0).ToArray();
  void Order(NativeEntity e,OriginalOrder order,Cell c,int target=-1){if(w.Command(new(e.Slot,e.Incarnation,order,c.X*256+128,c.Z*256+128,target)))Actions.Add(new{w.Tick,e.Identity,order,c,target});}
  var miner=own.FirstOrDefault(e=>e.Type==14);
  if(!minerSent&&miner!=null){Order(miner,OriginalOrder.MoveOnly,new(4,80));minerSent=true;}
  if(minerSent&&!secondVent&&miner!=null&&w.Tick>5000&&!w.Entities.Any(e=>e.Type==40&&e.Living&&e.Cell==new Cell(4,80))){Order(miner,OriginalOrder.MoveOnly,new(13,51));secondVent=true;}
  // Build the two available organs, then original first-tier warrior upgrades.
  foreach(int id in new[]{16,17,30,32})if(w.DependencyState(0,id)==1){var r=w.RequestProduction(0,id);if(r.Accepted)Actions.Add(new{w.Tick,product=id});break;}
  int reserve=w.Teams[0].WeaponTiers[8]>0&&w.Teams[0].ArmorTiers[8]>0?0:2000;
  if(w.Teams[0].Production[0].Types.Count<2&&bought<22&&w.Teams[0].Money>=350+reserve){var r=w.RequestProduction(0,23);if(r.Accepted){bought++;Actions.Add(new{w.Tick,product=23});}}
  foreach(var e in own.Where(e=>e.Type is 8 or 10||w.IsCommander(e)))if(assembled.Add(e.Identity)){
   if(e.Type==8&&guards.Count<4)guards.Add(e.Identity);
   Order(e,OriginalOrder.MoveAttack,guards.Contains(e.Identity)?new(12,76):Goal<0?new(16,73):route[Math.Min(Goal,route.Length-1)]);
  }
  var army=own.Where(e=>(e.Type is 8 or 10||w.IsCommander(e))&&!guards.Contains(e.Identity)).ToArray();
  foreach(var c in army.Where(w.IsCommander))if(c.Energy>=32)Order(c,OriginalOrder.Inspire,c.Cell);
  if(Goal<0&&army.Length>=24&&w.Teams[0].WeaponTiers[8]>0&&w.Teams[0].ArmorTiers[8]>0){Goal=0;lastOrder=-1000;}
  if(Goal<0)return;
  var dest=route[Math.Min(Goal,route.Length-1)];
  if(Goal is 3 or 4&&w.Deaths[1,86]>=3){Goal=5;dest=route[Goal];lastOrder=-1000;}
  if(Goal<route.Length-1&&army.Any(e=>Math.Abs(e.Cell.X-dest.X)+Math.Abs(e.Cell.Z-dest.Z)<7)){
   // At the objective clusters, stay until the visible relays are destroyed.
   bool relay=own.Length>0&&w.Entities.Any(e=>e.Living&&e.Type==86&&w.Visible(0,e.Cell)&&Math.Abs(e.Cell.X-dest.X)+Math.Abs(e.Cell.Z-dest.Z)<10);
   if(!relay){dest=route[++Goal];lastOrder=-1000;}
  }
  var target=w.Entities.Where(e=>e.Living&&e.Owner<8&&!w.Relations[0,e.Owner]&&w.Visible(0,e.Cell))
   .Where(e=>army.Any(a=>Math.Abs(a.Cell.X-e.Cell.X)+Math.Abs(a.Cell.Z-e.Cell.Z)<9))
   .OrderBy(e=>e.Type==86?1:0).ThenBy(e=>army.Min(a=>Math.Abs(a.Cell.X-e.Cell.X)+Math.Abs(a.Cell.Z-e.Cell.Z))).FirstOrDefault();
  if(w.Tick-lastOrder<160)return;lastOrder=w.Tick;
  foreach(var e in army)if(target!=null)Order(e,OriginalOrder.Attack,target.Cell,target.Slot);else Order(e,OriginalOrder.MoveAttack,dest);
 }
}
