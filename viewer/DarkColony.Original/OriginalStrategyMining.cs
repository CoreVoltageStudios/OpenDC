using DarkColony.Assets;
namespace DarkColony.Original;
public sealed partial class OriginalWorld {
 // 459a30..459f14: one unassigned miner per strategy update; distinct vent zones,
 // home-distance preference, two-ring route threat and stuck/danger reassignment.
 // Threatened miners return to defense group 0 target (state+319c), not mining group 0.
 // Agent.Zone mirrors unit+11; Last/Stuck mirror cd/ce/cf. These already persist.
 void StrategyMiners(int team,NativeStrategyState s){
  var group=s.Tasks[0][0];int free=-1;
  foreach(int id in group.Members){var e=Slots[id]!;var a=s.Agents[id]!;
   if(e.Cell==a.Last)a.Stuck=unchecked((byte)(a.Stuck+1));else{a.Stuck=0;a.Last=e.Cell;}
   if(e.Type is 6 or 14){
    if(a.Stuck>10)a.Zone=0;
    else if(a.Zone!=0&&StrategyThreat(team,s,StrategyZone(e.Cell),a.Zone)!=0){StrategyOrder(e,OriginalOrder.MoveOnly,s.Zones[s.Tasks[1][0].TargetZone].Center);a.Zone=0;}
   }
   if(a.Zone==0)free=id;
  }
  if(free<0)return;var miner=Slots[free]!;int from=StrategyZone(miner.Cell),best=256,targetZone=0;NativeEntity? target=null;
  var used=group.Members.Select(id=>s.Agents[id]!.Zone).ToHashSet();
  foreach(var vent in Entities){
   if(!vent.Living||vent.Type!=40||vent.ResourceFlow==0||Occupant(vent.Cell,Ground)!=-1)continue;
   int z=StrategyZone(vent.Cell);if(used.Contains(z)||s.Zones[z].Distance>=best||StrategyThreat(team,s,from,z)!=0)continue;
   target=vent;targetZone=z;best=s.Zones[z].Distance;
  }
  if(target!=null){s.Agents[free]!.Zone=targetZone;StrategyOrder(miner,OriginalOrder.MoveOnly,target.Cell);}
 }
}
