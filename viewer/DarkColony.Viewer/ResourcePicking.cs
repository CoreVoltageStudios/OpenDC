using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 string VentHelp(NativeEntity vent)=>IsHuman?(vent.Type==37?"Artifact site ? An Exploiter excavates discovered sites. Requires a Research Bay.":vent.ResourceFlow is >0?"Petra-7 vent ? Active. Move an Exploiter here to harvest.":"Petra-7 vent ? Inactive. No Petra-7 is flowing; an Exploiter cannot deploy here yet."):vent.Type==37?"Artifact site · Move a Brozaar onto the site to excavate. Requires a Neural Hive. Interruption restarts the current excavation.":vent.ResourceFlow is >0
  ? "Petra-7 vent · Active. Move a Brozaar here to harvest."
  : "Petra-7 vent · Inactive. No Petra-7 is flowing; a Brozaar cannot deploy here yet.";
 string? HarvesterStatus(NativeEntity worker){
  if(worker.Type is 77 or 78)return worker.Actions.LastOrDefault()?.Kind==13?"Retracting":"Siphoning Petra-7";
  if(worker.Type is not (6 or 14 or 47 or 48))return null;
  if(nativeWorld.ArtifactAt(worker.Cell) is {} site&&nativeWorld.ArtifactWorker(site)==worker)return $"Excavating · {Math.Clamp((450-site.Actions[0].Word)*100/450,0,100)}%";
  if(worker.Actions.LastOrDefault()?.Kind==13)return "Retracting";
  if(worker.Type is 47 or 48)return worker.Body.Mode==1?"Deploying":"Harvesting Petra-7";
  var vent=nativeWorld.Entities.FirstOrDefault(e=>e.Living&&e.Type==40&&e.Cell==worker.Cell);
  if(vent==null||worker.Actions.Any(a=>a.Kind is 4 or 5 or 6 or 8))return null;
  return vent.ResourceFlow is >0?"Preparing to deploy":"Waiting · vent inactive";
 }
 NativeEntity? ResourceAt(Vector2 point){
  if(!NativeArea.HasPoint(point)||!NativeSelected().Any(e=>e.Type is 6 or 14))return null;
  // Exact visible vent artwork/anchor beats overlapping scenery and a friendly body.
  var cell=NativeCell((point-NativeOffset)/zoom);
  return nativeWorld.Entities.Where(e=>e.Living&&e.Type is 37 or 40&&NativeVisible(e)&&
   (e.Cell==cell||UnitHitRect(e).HasPoint(point))).OrderBy(e=>NativePosition(e).DistanceSquaredTo((point-NativeOffset)/zoom)).FirstOrDefault();
 }
}
