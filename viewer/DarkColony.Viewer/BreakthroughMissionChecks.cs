using Godot;
using DarkColony.Original;
using DarkColony.Assets;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 DarkColony.Diagnostics.BreakthroughRoute? breakthroughRoute;
 async Task VerifyBreakthroughMission(){
  missionHarnessManual=true;AdvanceInterference(240);await MissionCapture("01-opening-day");
  Check(nativeData.Scenario.Header.PaletteName=="JUNGLE.GIF"&&nativeWorld.Coefficient==0,"A04 selects its own jungle/day configuration");
  var route=new DarkColony.Diagnostics.BreakthroughRoute();while(nativeWorld.Tick<1200){route.Update(nativeWorld);AdvanceInterference(1);}
  Check(mission.Blocks[16]!.Remaining==0&&nativeWorld.Events.Count(e=>e.Kind=="fuel-collected")==3,"Ordinary-command opening reaches depot, collects all tanks and returns commander");
  camera=new(100*32,(nativeData.Scenario.Map.Height-87)*32);zoom=1.5f;ClampMissionCamera();await MissionCapture("02-hive-after-return");
  var scout=nativeWorld.Entities.First(e=>e.Owner==0&&e.Type==13&&e.Living);camera=NativePosition(scout);ClampMissionCamera();await NativeMouse(MouseButton.Left,UnitHitRect(scout).GetCenter());await MissionCapture("03-ortu-selected");
  await Send(Key.M);await ClickNativeCell(MouseButton.Left,new(98,90));AdvanceInterference(70);Check(nativeSelection.Contains((scout.Slot,scout.Incarnation)),"Ortu retains selection after mouse/Move-only command");
  // Explicit isolated purchase/role fixture. Earned route above is kept separate.
  nativeWorld.Teams[0].Money=20000;nativeWorld.Teams[1].Ai=0;
  async Task Buy(int id){await UiClick("tab:"+nativeData.Scenario.Dependencies.Single(d=>d.Id==id).Kind);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);for(int page=0;!missionHits.Any(h=>h.Id=="product:"+id)&&page<8;page++){await UiClick("products-next");await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}Check(missionHits.Single(h=>h.Id=="product:"+id).Enabled,"Product available "+id);await UiClick("product:"+id);AdvanceInterference(400);}
  foreach(int id in new[]{18,19}){if(nativeWorld.DependencyState(0,id)==1)await Buy(id);for(int t=0;nativeWorld.DependencyState(0,id)!=0&&t<1600;t++)AdvanceInterference(1);Check(nativeWorld.DependencyState(0,id)==0,"Native organ construction completed before product purchase "+id);}
  camera=new(102*32,(nativeData.Scenario.Map.Height-87)*32);ClampMissionCamera();await MissionCapture("04-completed-hive");await Buy(26);await Buy(24);
  var atril=nativeWorld.Entities.First(e=>e.Living&&e.Owner==0&&e.Type==11);camera=NativePosition(atril);ClampMissionCamera();await NativeMouse(MouseButton.Left,UnitHitRect(atril).GetCenter());await MissionCapture("05-atril-selected");
  await Send(Key.A);await ClickNativeCell(MouseButton.Left,new(96,80));missionHarnessManual=false;long stop=nativeWorld.Tick+100,last=-1;
  while(nativeWorld.Tick<stop){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(nativeWorld.Tick/3!=last){last=nativeWorld.Tick/3;await MissionCapture($"movement-{nativeWorld.Tick:D5}");}}
  missionHarnessManual=true;camera=NativePosition(atril);ClampMissionCamera();
  var target=nativeWorld.Spawn(0,1,new(atril.Cell.X-8,atril.Cell.Z),"fixture/rendered-artillery-target");nativeWorld.RefreshSight();
  nativeWorld.Command(new(atril.Slot,atril.Incarnation,OriginalOrder.Attack,Target:target.Slot));
  var air=nativeWorld.Entities.First(e=>e.Living&&e.Owner==0&&e.Type==13);var airTarget=nativeWorld.Spawn(0,1,new(air.Cell.X+1,air.Cell.Z),"fixture/rendered-ortu-target");nativeWorld.RefreshSight();nativeWorld.Command(new(air.Slot,air.Incarnation,OriginalOrder.Attack,Target:airTarget.Slot));
  missionHarnessManual=false;stop=nativeWorld.Tick+180;last=-1;
  while(nativeWorld.Tick<stop){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(nativeWorld.Tick/4!=last){last=nativeWorld.Tick/4;await MissionCapture($"combat-{nativeWorld.Tick:D5}");}}
  missionHarnessManual=true;Check(nativeWorld.Events.Any(e=>e.Kind=="launch"&&e.Slot==atril.Slot)&&nativeWorld.Events.Any(e=>e.Kind=="launch"&&e.Slot==air.Slot),"New roles launch actual weapons during normal-clock rendered sequence");
  await UiClick("menu");await UiClick("save");await UiClick("save-slot:0");if(overwriteSlot>=0)await UiClick("overwrite-yes");await UiClick("close");
  var saved=mission.Snapshot();var sameCoefficient=nativeWorld.Coefficient;
  await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");Check(mainMenu,"A04 returns to main menu");await UiClick("resume-save");
  Check(mission.CampaignIndex==3&&nativeWorld.Coefficient==sameCoefficient&&System.Text.Json.Nodes.JsonNode.DeepEquals(saved,mission.Snapshot()),"Menu Continue restores complete A04 and palette without initialization replay");await MissionCapture("06-restored");
  await Send(Key.R);Check(missionBriefing&&nativeWorld.Tick==0&&nativeWorld.Teams[0].Money==0,"A04 restart resets source state");await MissionCapture("07-restart");
  // Controlled A03 outcome exercises actual unlocked campaign transition; no profile edits.
  var prior=PersistenceData("SCENARIO/ALIEN/ALIEN03.SCN");InstallMission(prior,new OriginalMission(prior,0),false);nativeWorld.Teams[1].Ai=0;mission.Blocks[3]!.Remaining=1;foreach(var c in nativeWorld.Teams[1].Cities.Take(5))if(c.Entity!=null)nativeWorld.Retire(c.Entity);AdvanceInterference(24);mission.SetClock(mission.Outcome!.Deadline+1);await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);await UiClick("continue");
  Check(mission.CampaignIndex==3&&missionBriefing&&nativeWorld.Coefficient==0,"A03 outcome Continue enters A04 with fresh day lighting");await MissionCapture("08-campaign-entry");
  await Send(Key.Enter);AdvanceInterference(240);await MissionCapture("09-campaign-opening");
 }
 async Task RunBreakthroughMission(){
  fastGame=true;breakthroughRoute=new();long captured=-1;int goal=-2;ulong started=Time.GetTicksMsec();
  while(!mission.AtOutcome&&nativeWorld.Tick<45000){
   await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(missionFault!=null)throw new InvalidOperationException(missionFault);
   var leader=nativeWorld.Entities.FirstOrDefault(e=>e.Living&&e.Owner==0&&(breakthroughRoute.Goal>=5?e.Type==11:nativeWorld.IsCommander(e)));
   if(leader!=null&&nativeWorld.Tick%16==0){camera=NativePosition(leader);ClampMissionCamera();}
   if(nativeWorld.Tick/2000!=captured||breakthroughRoute.Goal!=goal){captured=nativeWorld.Tick/2000;goal=breakthroughRoute.Goal;await MissionCapture($"run-{nativeWorld.Tick:D5}-goal{goal}");GD.Print($"BREAKTHROUGH_PROGRESS tick={nativeWorld.Tick} goal={goal} money={nativeWorld.Teams[0].Money}");}
  }
  var route=breakthroughRoute;breakthroughRoute=null;
  File.WriteAllText(Path.Combine(output,"end-to-end.json"),JsonSerializer.Serialize(new{mode="Rendered normal-clock Fast150%; ordinary authoritative orders/purchases; no state injection",elapsedMs=Time.GetTicksMsec()-started,nativeWorld.Tick,mission.Outcome,route.Actions,events=nativeWorld.Events,missionEvents=mission.Events.Where(e=>e.Kind!="normal-scan"),audio=missionAudioAudit},new JsonSerializerOptions{WriteIndented=true}));
  Check(mission.AtOutcome,"A04 complete rendered ordinary-command run reaches original outcome");
  if(missionSuite=="success")Check(mission.Outcome!.Success,"Ordinary-command victory");
  await ToSignal(GetTree().CreateTimer(16),SceneTreeTimer.SignalName.Timeout);await MissionCapture("90-rewards");
  if(mission.Outcome!.Success){await UiClick("continue");Check(missionNext,"A05 remains clearly unavailable");await MissionCapture("91-campaign-boundary");}
 }
}
