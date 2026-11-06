using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyUnitCommands(){
  missionHarnessManual=true;AdvanceInterference(240);
  void Select(params NativeEntity[] entities){nativeSelection.Clear();foreach(var e in entities)nativeSelection.Add((e.Slot,e.Incarnation));}
  var soldier=nativeWorld.Entities.First(e=>e.Living&&e.Owner==0&&e.Type==8);
  var commander=nativeWorld.Entities.First(e=>e.Living&&e.Owner==0&&nativeWorld.IsCommander(e));
  Select();await UiCapture("commands-none");Check(!UnitEnabled("stop")&&!UnitEnabled("move")&&!UnitEnabled("attack")&&!UnitEnabled("inspire"),"No selection disables unit commands");
  Select(soldier);await UiCapture("commands-warrior");
  foreach(var pair in new[]{("stop",Key.S),("move",Key.M),("attack",Key.A)}){await UiClick(pair.Item1);if(pair.Item1=="stop")Check(soldier.PendingOrder?.Order==OriginalOrder.Stop,"Mouse stop enters authoritative order");else Check(targetMode==pair.Item1,"Mouse arms "+pair.Item1);if(pair.Item1!="stop")await Send(Key.Escape);soldier.PendingOrder=null;await Send(pair.Item2);Check(pair.Item1=="stop"?soldier.PendingOrder?.Order==OriginalOrder.Stop:targetMode==pair.Item1,"Matching shortcut "+pair.Item1);if(pair.Item1!="stop")await Send(Key.Escape);}
  var destination=new Cell(soldier.Cell.X+2,soldier.Cell.Z);
  await UiClick("move");await ClickNativeCell(MouseButton.Left,destination);Check(soldier.PendingOrder?.Order==OriginalOrder.MoveOnly,"Move target is move-only");
  await UiClick("attack");await ClickNativeCell(MouseButton.Left,destination);Check(soldier.PendingOrder?.Order==OriginalOrder.MoveAttack,"Ground attack target is attack-move");
  var enemy=nativeWorld.Spawn(0,1,new(soldier.Cell.X+3,soldier.Cell.Z),"fixture/command-target");nativeWorld.RefreshSight();
  await UiClick("attack");await NativeMouse(MouseButton.Left,UnitHitRect(enemy).GetCenter());Check(soldier.PendingOrder is {Order:OriginalOrder.Attack} o&&o.Target==enemy.Slot,"Visible hostile target is explicit attack");
  commander.Energy=0;Select(commander);await UiCapture("commands-recharging");await UiClick("inspire");await Send(Key.I);Check(commander.PendingOrder?.Order!=OriginalOrder.Inspire,"Mouse and key respect energy availability");
  commander.Energy=255;Select(commander,soldier);var previous=soldier.PendingOrder;await UiClick("inspire");Check(commander.PendingOrder?.Order==OriginalOrder.Inspire&&soldier.PendingOrder==previous,"Mixed Inspire affects commander only");commander.PendingOrder=null;await Send(Key.I);Check(commander.PendingOrder?.Order==OriginalOrder.Inspire,"Inspire keyboard parity");await UiCapture("commands-commander-group");
  if(economicMission){
   var mine=nativeWorld.Spawn(44,0,new(18,74),"fixture/command-mine");var harvester=nativeWorld.Entities.First(e=>e.Living&&e.Owner==0&&e.Type==14);Select(harvester);await ClickNativeCell(MouseButton.Right,new(4,80));AdvanceInterference(300);Check(harvester.Type==48,"Natural work path reaches retractable harvester");
   Select(harvester);await UiCapture("commands-harvester");Check(!UnitEnabled("stop")&&!UnitEnabled("move")&&UnitEnabled("retract"),"Settled harvester only exposes its supported reversal");
   Select(mine);await UiClick("deploy");Check(mine.PendingOrder?.Order==OriginalOrder.Deploy,"Original mine artwork invokes mine deployment");mine.PendingOrder=null;
   Select(commander,soldier,mine,harvester);commander.PendingOrder=null;soldier.PendingOrder=null;await UiCapture("commands-mixed");await UiClick("retract");Check(harvester.PendingOrder?.Order==OriginalOrder.Deploy&&mine.PendingOrder==null&&commander.PendingOrder==null,"Retract button does not deploy mine or inspire commander");harvester.PendingOrder=null;await Send(Key.D);Check(mine.PendingOrder?.Order==OriginalOrder.Deploy&&harvester.PendingOrder?.Order==OriginalOrder.Deploy&&commander.PendingOrder==null&&soldier.PendingOrder==null,"D applies only supported deployment/retraction roles");
   nativeWorld.Session.PreventHarvesterRetraction=true;Check(!UnitEnabled("retract"),"Retraction lock disables command");nativeWorld.Session.PreventHarvesterRetraction=false;
   Select(nativeWorld.Teams[0].Cities[0].Entity!);Check(!UnitEnabled("stop")&&!UnitEnabled("move")&&!UnitEnabled("attack"),"City selection does not advertise unit orders");
   Select(commander,soldier,mine,harvester);
  }
  foreach(float scale in new[]{1f,1.5f}){ui=scale;await UiCapture("commands-scale-"+scale);Check(missionHits.All(h=>GetViewportRect().Encloses(h.Rect)),"Command and production hitboxes stay within framebuffer at "+scale);Check(missionHits.Where(h=>h.Id.StartsWith("product:")).All(h=>h.Rect.Size.Y/ui>=25),"Economic rows remain readable beside mixed commands");}
  Select(soldier);await Send(Key.A);nativeWorld.Retire(soldier);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Check(targetMode==""&&!nativeAttackMove,"Selection death clears armed command");
  await VerifySidebarLayout();
  missionHarnessManual=false;
 }
}
