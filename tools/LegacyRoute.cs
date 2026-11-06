using DarkColony.Assets;
using DarkColony.Original;
namespace DarkColony.Diagnostics;

// Explicit verification player only. Purchases and orders use normal authoritative
// operations. No resources, entities, visibility, triggers or outcomes are injected.
public sealed class LegacyRoute {
 public int Goal {get;private set;}=-1;
 public readonly List<object> Actions=new();
 readonly HashSet<string> assigned=new(); readonly List<string> guards=new(); readonly List<string> baseGuards=new();
 readonly Cell[] route={new(51,28),new(48,43),new(45,58),new(32,76),new(36,88),new(49,88),new(53,82)};
 long last=-1,lastOrder=-1000;int bought;bool minerSent,secondMiner;
 public void Update(OriginalWorld w){
  if(w.Tick==last||w.Tick<16||w.Tick%16!=0)return;last=w.Tick;
  var own=w.Entities.Where(e=>e.Living&&e.Owner==0).ToArray();
  void Order(NativeEntity e,OriginalOrder order,Cell c,int target=-1){if(w.Command(new(e.Slot,e.Incarnation,order,c.X*256+128,c.Z*256+128,target)))Actions.Add(new{w.Tick,e.Identity,order,c,target});}
  var miners=own.Where(e=>e.Type==14).ToArray();
  if(!minerSent&&miners.Length>0){Order(miners[0],OriginalOrder.MoveOnly,new(64,11));minerSent=true;}
  if(minerSent&&!secondMiner&&miners.Length>0&&w.Tick>800){Order(miners[0],OriginalOrder.MoveOnly,new(86,11));secondMiner=true;}
  if(w.Tick>9000&&w.Tick%320==0)foreach(var m in miners){var vent=w.Entities.Where(v=>v.Living&&v.Type==40&&v.ResourceFlow>0&&w.Visible(0,v.Cell)&&w.Ground[v.Cell.Z*w.Data.Scenario.Map.Width+v.Cell.X]<0).OrderBy(v=>Math.Abs(v.Cell.X-m.Cell.X)+Math.Abs(v.Cell.Z-m.Cell.Z)).FirstOrDefault();if(vent!=null)Order(m,OriginalOrder.MoveOnly,vent.Cell);}
  if(!secondMiner&&w.Tick>300&&w.Live[0,14]+w.Live[0,48]<2&&w.Teams[0].Production.All(q=>!q.Types.Contains(14))){var r=w.RequestProduction(0,21);if(r.Accepted)Actions.Add(new{w.Tick,product=21});}
  foreach(int id in new[]{16,17,30,32,41,43})if(w.DependencyState(0,id)==1){var r=w.RequestProduction(0,id);if(r.Accepted)Actions.Add(new{w.Tick,product=id});break;}
  int reserve=new[]{16,17,30,32,41,43}.Where(id=>w.DependencyState(0,id)==1).Select(id=>w.Data.Scenario.Dependencies.First(d=>d.Id==id).Cost).FirstOrDefault();
  if(w.DependencyState(0,25)==1&&w.Teams[0].Production[1].Types.Count<2&&w.Teams[0].Money>=600+reserve&&w.Live[0,10]<25){var r=w.RequestProduction(0,25);if(r.Accepted)Actions.Add(new{w.Tick,product=25});}
  if(w.Teams[0].Production[0].Types.Count<2&&bought<65&&w.Teams[0].Money>=350+reserve&&(w.DependencyState(0,25)!=1||w.Live[0,8]<20||w.Live[0,10]>=18)){var r=w.RequestProduction(0,23);if(r.Accepted){bought++;Actions.Add(new{w.Tick,product=23});}}
  foreach(var e in own.Where(e=>e.Type is 8 or 9 or 10||w.IsCommander(e)))if(assigned.Add(e.Identity)){
   if(e.Type==8&&guards.Count(id=>own.Any(e=>e.Identity==id))<8)guards.Add(e.Identity);
   else if(e.Type==8&&baseGuards.Count(id=>own.Any(e=>e.Identity==id))<6)baseGuards.Add(e.Identity);
   Order(e,OriginalOrder.MoveAttack,guards.Contains(e.Identity)||e.Type==9?new(92,12):baseGuards.Contains(e.Identity)?new(54,20):Goal<0?new(54,25):route[Math.Min(Goal,route.Length-1)]);
  }
  foreach(var x in own.Where(e=>e.Type==9&&Math.Abs(e.Cell.X-92)+Math.Abs(e.Cell.Z-12)<4))Order(x,OriginalOrder.Deploy,x.Cell);
  var army=own.Where(e=>(e.Type is 8 or 10||w.IsCommander(e))&&!guards.Contains(e.Identity)&&!baseGuards.Contains(e.Identity)).ToArray();
  foreach(var c in own.Where(w.IsCommander))if(c.Energy>=32)Order(c,OriginalOrder.Inspire,c.Cell);
  if(Goal<0&&army.Length>=20&&w.Teams[0].WeaponTiers[8]>0&&w.Teams[0].ArmorTiers[8]>0){Goal=0;lastOrder=-1000;}
  if(Goal<0)return;
  var dest=route[Math.Min(Goal,route.Length-1)];
  if(Goal<route.Length-1&&army.Count(e=>Math.Abs(e.Cell.X-dest.X)+Math.Abs(e.Cell.Z-dest.Z)<8)>army.Length/2){dest=route[++Goal];lastOrder=-1000;}
  if(w.Tick-lastOrder<180)return;lastOrder=w.Tick;
  foreach(var e in army){
   var target=w.Entities.Where(t=>t.Living&&t.Owner<8&&!w.Relations[0,t.Owner]&&w.Visible(0,t.Cell))
    .Where(t=>Math.Abs(t.Cell.X-e.Cell.X)+Math.Abs(t.Cell.Z-e.Cell.Z)<9)
    .OrderBy(t=>Math.Abs(t.Cell.X-e.Cell.X)+Math.Abs(t.Cell.Z-e.Cell.Z)).FirstOrDefault();
   if(target!=null)Order(e,OriginalOrder.Attack,target.Cell,target.Slot);else Order(e,OriginalOrder.MoveAttack,dest);
  }
 }
}
