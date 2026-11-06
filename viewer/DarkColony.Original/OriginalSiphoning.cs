namespace DarkColony.Original;
public sealed partial class OriginalWorld {
 // DC16 417974. Four edges of widening rings with a fixed +-22 offset span;
 // this is deliberately not a circular range test or a nearest-distance sort.
 public NativeEntity? SiphonCandidate(NativeEntity caster){
  for(int ring=0;ring<=11;ring++)for(int offset=-22;offset<=22;offset++)for(int edge=0;edge<4;edge++){
   var c=edge switch{0=>new Cell(caster.Cell.X-ring,caster.Cell.Z+offset),1=>new(caster.Cell.X+ring,caster.Cell.Z+offset),2=>new(caster.Cell.X+offset,caster.Cell.Z-ring),_=>new(caster.Cell.X+offset,caster.Cell.Z+ring)};
   int id=Occupant(c,Ground);if(id<0||Slots[id] is not {Living:true,Type:47 or 48} target||target.Owner==caster.Owner||!Visible(caster.Owner,target.Cell))continue;
   // Both supported deployed harvester definitions are unconcealed. Native
   // tests team sight, not the caster's own sight, and permits allied owners.
   return target;
  }return null;
 }
 void CompleteSiphonDeployment(NativeEntity e){
  bool deploy=e.Type is 4 or 12;e.Type=e.Type switch{4=>77,12=>78,77=>4,_=>12};
  e.Body.Start(Data.Clips[e.Type].Stand,0);ResetOrders(e);
  if(!deploy){Emit("siphon-retracted",e);return;}
  var target=SiphonCandidate(e);var harvest=target?.Actions.LastOrDefault();
  // 417b3c picks first candidate before testing its one-owner claim. A busy
  // first target does not cause a search for an alternative miner.
  if(target==null||harvest==null||harvest.Siphoner>=0){BeginDeploy(e);Emit("siphon-unavailable",e);return;}
  e.Actions[0].Word=target.Slot;harvest.Siphoner=e.Slot;Emit("siphon-linked",e,target.Slot);
 }
 bool SiphonIdle(NativeEntity e,NativeAction action){
  // 413bf0: do not recheck range, sight or alliance after establishing the link.
  if(action.Word<0||action.Word>=Slots.Length||Slots[action.Word] is not {Living:true,Type:47 or 48}){BeginDeploy(e);Emit("siphon-target-lost",e);}
  return false;
 }
 int SplitHarvest(NativeEntity miner,NativeAction action,int transfer){
  if(action.Siphoner<0)return transfer;
  var siphoner=Slots[action.Siphoner];
  if(siphoner is not {Living:true,Type:77 or 78}){action.Siphoner=-1;return transfer;}
  // 4137b0: floor both halves; vent debit remains the full pre-split transfer.
  // Native links are slot identities. Clear invalid/reused non-siphoner slots
  // safely instead of reproducing the original assertion/crash.
  int half=transfer/2;var team=Teams[siphoner.Owner];
  if(team.Cities[0].Health>0){team.Money+=half;EarnedResources[team.Id]+=half;Emit("siphon-income",siphoner,miner.Slot,half);}
  return half;
 }
}
