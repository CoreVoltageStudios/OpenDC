using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class WarNetworkLab {
 // Explicit, identical controlled initial fixture on both processes. Thereafter
 // every ability/production order travels through real ENet and the host queue.
 void PrepareMechanics(){
  if(scenario!="mechanics")return;var w=session.Match!.Mission.World;
  foreach(var t in w.Teams){t.Ai=0;t.Money=30000;}
  for(int slot=0;slot<2;slot++){
   int owner=Array.IndexOf(session.Match.Mission.War!.Starts,slot);var center=w.Entities.First(e=>e.Owner==owner&&e.Slot>=152).Cell;
   foreach(int type in slot==0?new[]{43,1,5,49}:new[]{12,44,13,50,63}){
    if(!w.Data.Clips.ContainsKey(type))throw new InvalidOperationException("Fixture requires prepared War type "+type);var e=w.Spawn(type,owner,new(center.X+6+type%4,center.Z+6),"network-fixture/ability");e.Energy=255;
   }
   if(slot==1)w.Teams[owner].WeaponTiers[12]=2;
  }
  w.RefreshSight();
 }
 void MechanicsOrders(SynchronizedWar match){
  var w=match.Mission.World;long tick=w.Tick;int owner=w.Session.LocalTeam;
  if(lastOrder==0||tick>60)return;lastOrder=0;
  foreach(var e in w.Entities.Where(e=>e.Owner==owner&&e.Living&&e.SourceIdentity=="network-fixture/ability")){
   if(e.Type is 43 or 44 or 1 or 63)session.Submit(new("units",owner,new[]{new WarUnit(e.Slot,e.Incarnation)},OriginalOrder.Deploy));
   if(e.Type==12)session.Submit(new("units",owner,new[]{new WarUnit(e.Slot,e.Incarnation)},OriginalOrder.Disease,e.X+256,e.Z+256));
   if(e.Type is 5 or 13)session.Submit(new("group",owner,new[]{new WarUnit(e.Slot,e.Incarnation)},OriginalOrder.MoveOnly,e.X+768,e.Z));
  }
 }
}
