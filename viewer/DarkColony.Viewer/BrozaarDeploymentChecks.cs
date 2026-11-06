using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyBrozaarDeployment(){
  missionHarnessManual=true;
  foreach(int map in new[]{2,4}){
   var data=PersistenceData($"SCENARIO/ALIEN/ALIEN{map:00}.SCN");InstallMission(data,new OriginalMission(data,0),false);missionHarnessManual=true;
   foreach(var t in nativeWorld.Teams)t.Ai=0;
   foreach(var e in nativeWorld.Entities.Where(e=>e.Type!=40&&e.Slot>=120).ToArray())nativeWorld.Retire(e);
   var vent=nativeWorld.Entities.First(e=>e.Type==40&&e.ResourceFlow>0);
   for(int settle=0;settle<240;settle++)nativeWorld.Step(); // Let ordinary city construction carriers depart before the close-up.
   var worker=nativeWorld.Spawn(14,0,new(vent.Cell.X+3,vent.Cell.Z),"fixture/render-worker");
   nativeWorld.Step();nativeWorld.RefreshSight();camera=NativePosition(vent);zoom=2;ClampMissionCamera();nativeSelection.Clear();nativeSelection.Add((worker.Slot,worker.Incarnation));
   await UiCapture($"brozaar-{map}-approach");await NativeMouse(MouseButton.Right,UnitHitRect(vent).GetCenter());
   Check(worker.PendingOrder?.Order==OriginalOrder.MoveOnly,"Rendered vent right click dispatches ordinary Move only");
   for(int t=0;t<1000&&worker.Type!=48;t++)nativeWorld.Step();Check(worker.Type==48,"Rendered worker arrives and begins deployment");
   var trace=new List<object>();
   for(int t=0;t<60;t++){
    trace.Add(new{nativeWorld.Tick,worker.Type,worker.X,worker.Z,worker.Angle,worker.Body.Frame,worker.Body.Delay,worker.Body.Mode,animation=worker.Body.Set?.Name,harvest=nativeWorld.Events.Count(e=>e.Kind=="harvest"&&e.Slot==worker.Slot)});
    await UiCapture($"brozaar-{map}-deploy-{t:000}");nativeWorld.Step();
   }
   Check(worker.Body.Set?.Name=="SDPLSTAND"&&nativeWorld.Events.Any(e=>e.Kind=="harvest"&&e.Slot==worker.Slot),"Deployment reaches original working loop and transfers Petra-7");
   nativeWorld.Command(new(worker.Slot,worker.Incarnation,OriginalOrder.Deploy));
   for(int t=0;t<60;t++){await UiCapture($"brozaar-{map}-retract-{t:000}");nativeWorld.Step();if(worker.Type==14)break;}
   Check(worker.Type==14,"Explicit retract completes original finite animation");
   await ClickNativeCell(MouseButton.Right,new(vent.Cell.X+3,vent.Cell.Z));for(int t=0;t<160;t++)nativeWorld.Step();Check(worker.Cell!=vent.Cell,"Retracted worker moves away");
   await UiCapture($"brozaar-{map}-departed");File.WriteAllText(Path.Combine(output,$"brozaar-{map}-trace.json"),JsonSerializer.Serialize(trace));
  }
 }
}
