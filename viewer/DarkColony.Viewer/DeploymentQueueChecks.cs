using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyDeploymentQueue(){
  // Explicit focused fixture, not an earned production route or mission completion.
  missionHarnessManual=true;foreach(var team in nativeWorld.Teams)team.Ai=0;
  var mine=nativeWorld.Spawn(44,0,new(97,18),"fixture/reported-slom");mine.X=24973;mine.Z=4706;
  var warrior=nativeWorld.Spawn(8,0,new(98,18),"fixture/mixed-mobile");
  nativeWorld.RefreshSight();camera=NativePosition(mine);zoom=2;ClampMissionCamera();
  await NativeMouse(MouseButton.Left,UnitHitRect(mine).GetCenter());await UiClick("deploy");
  missionHarnessManual=false;
  while(mine.Actions.LastOrDefault()?.Kind!=13)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  await MissionCapture("01-deploying");
  // Group destination arrives while the Slom's finite action still owns execution.
  nativeSelection.Add((warrior.Slot,warrior.Incarnation));await ClickNativeCell(MouseButton.Right,new(97,12));
  Check(mine.PendingOrder?.Order==OriginalOrder.MoveOnly,"Frontend queues move while Slom is deploying");
  await MissionCapture("02-pending-move");long end=nativeWorld.Tick+160;
  while(nativeWorld.Tick<end&&missionFault==null)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  Check(missionFault==null&&mine.Type==46&&mine.X==24973&&mine.Z==4706,"Queued move completes as stationary mine without mission fault");
  Check(warrior.Cell.Z<18,"Mobile member obeys the same group move");
  Check(nativeWorld.Other.Count(id=>id==mine.Slot)==1&&!nativeWorld.Ground.Contains(mine.Slot),"Mine occupancy remains singular");
  missionHarnessManual=true;nativeSelection.Clear();nativeSelection.Add((mine.Slot,mine.Incarnation));await MissionCapture("03-mine-after-group-move");
  Check(!UnitEnabled("move"),"Stationary mine no longer offers movement");
  await Send(Key.R);Check(nativeWorld.Tick==0&&missionBriefing,"Restart clears fixture and pending commands");await MissionCapture("04-restarted");missionHarnessManual=false;
 }
}
