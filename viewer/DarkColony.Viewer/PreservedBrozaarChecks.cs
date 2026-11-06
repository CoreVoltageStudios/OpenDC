using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyPreservedBrozaar(){
  var snapshot=SaveCodec.Open(Path.Combine(root,"generated/brozaar-deployment/preserved/OpenDarkColony/saves/slot-1.json"))["payload"]!["mission"]!.AsObject();
  var data=PersistenceData(snapshot["scenario"]!.GetValue<string>());InstallMission(data,OriginalMission.Restore(data,snapshot),false);missionHarnessManual=true;
  var worker=nativeWorld.Slots[169]!;var vent=nativeWorld.Slots[179]!;
  Check(worker.Type==14&&worker.Cell==vent.Cell&&vent.ResourceFlow==0,"Preserved instance on inactive scripted vent");
  camera=NativePosition(vent);zoom=2;ClampMissionCamera();nativeSelection.Clear();nativeSelection.Add((worker.Slot,worker.Incarnation));
  // Resolve a legacy saved looping plume through the native idle action before
  // judging its appearance; loading must not rewrite the preserved source save.
  mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();
  Check(vent.Body.Mode==2&&vent.Body.Pose(vent.Angle)==null,"Dormant native vent has no flowing plume");
  await UiCapture("preserved-inactive");await NativeMouse(MouseButton.Right,UnitHitRect(vent).GetCenter());
  Check(worker.PendingOrder?.Order==OriginalOrder.MoveOnly,"Ordinary vent click preserves worker identity");
  void Step(){mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();}
  for(int i=0;i<2000&&worker.Type==14&&!mission.AtOutcome;i++)Step();
  Check(worker.Type==48&&vent.ResourceFlow>0,"Original instance deploys after original vent activation");
  var trace=new List<object>();
  for(int i=0;i<60;i++){
   QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
   using var image=GetViewport().GetTexture().GetImage();var center=NativePosition(worker)*zoom+NativeOffset;
   var crop=new Rect2I((int)center.X-160,(int)center.Y-160,320,320).Intersection(new Rect2I(0,0,image.GetWidth(),image.GetHeight()));
   using var region=image.GetRegion(crop);region.SavePng(Path.Combine(output,$"preserved-deploy-{i:000}.png"));
   trace.Add(new{nativeWorld.Tick,worker.Type,worker.X,worker.Z,worker.Angle,worker.Body.Frame,worker.Body.Delay,worker.Body.Mode,animation=worker.Body.Set?.Name,money=nativeWorld.Teams[0].Money});Step();
  }
  Check(worker.Body.Set?.Name=="SDPLSTAND"&&nativeWorld.Events.Any(e=>e.Kind=="harvest"&&e.Slot==169),"Rendered preserved worker reaches harvesting pose and resource transfer");
  await UiCapture("preserved-working");await UiClick("retract");
  for(int i=0;i<120&&worker.Type==48;i++)Step();Check(worker.Type==14,"Ordinary sidebar retract completes");
  await ClickNativeCell(MouseButton.Right,new(vent.Cell.X+3,vent.Cell.Z));for(int i=0;i<150;i++)Step();Check(worker.Cell!=vent.Cell,"Same instance leaves original vent");
  await NativeMouse(MouseButton.Right,UnitHitRect(vent).GetCenter());for(int i=0;i<500&&worker.Type!=48;i++)Step();Check(worker.Type==48,"Same instance redeploys through ordinary click");
  for(int i=0;i<60;i++)Step();await UiCapture("preserved-redeployed");
  File.WriteAllText(Path.Combine(output,"preserved-trace.json"),JsonSerializer.Serialize(trace,new JsonSerializerOptions{WriteIndented=true}));
 }
}
