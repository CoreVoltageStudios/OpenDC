using DarkColony.Assets;
using DarkColony.Original;
namespace DarkColony.Diagnostics;
// Isolated verification player: only the same authoritative orders as the UI.
public sealed class RoswellRoute {
 public int Goal {get;private set;}
 public readonly List<object> Actions=new();
 public readonly Cell[] Route={new(26,15),new(17,34),new(12,69),new(50,29),new(67,33),new(102,25),new(94,35),new(96,39),new(95,40),new(95,41),new(96,42),new(95,43),new(94,44),new(94,46),new(91,78),new(63,73),new(56,69),new(56,56),new(58,51),new(59,49),new(60,48),new(61,47),new(62,46),new(65,45),new(69,47),new(80,48)};
 long last=-1,lastOrder=-1000;
 public void Update(OriginalWorld w){
  if(w.Tick==last||w.Tick%8!=0)return;last=w.Tick;
  var c=w.Entities.FirstOrDefault(e=>e.Owner==0&&e.Living&&w.IsCommander(e));if(c==null)return;
  if(Goal<Route.Length-1&&Math.Abs(c.Cell.X-Route[Goal].X)+Math.Abs(c.Cell.Z-Route[Goal].Z)<=1){
   Goal++;lastOrder=-1000;
  }
  if(w.Tick-lastOrder<180)return;lastOrder=w.Tick;
  var dest=Route[Goal];if(w.Command(new(c.Slot,c.Incarnation,OriginalOrder.MoveOnly,dest.X*256+128,dest.Z*256+128)))Actions.Add(new{w.Tick,c.Identity,order="MoveOnly",dest});
 }
}
