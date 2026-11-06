using Godot;
using DarkColony.Original;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 void BuriedState(string name){var data=PersistenceData("SCENARIO/ALIEN/ALIEN09.SCN");var snap=SaveCodec.Open(Path.Combine(root,"generated/buried-treasure/mission-verified",name+".json"))["payload"]!.AsObject();InstallMission(data,OriginalMission.Restore(data,snap),false);}
 async Task VerifyBuriedMission(){
  missionHarnessManual=true;Check(mission.CampaignIndex==8&&!economicMission&&!godMode,"Direct Underground cityless start with God OFF");AdvanceInterference(30);ResetMissionCamera();
  await MissionCapture("01-opening");await Send(Key.J);await MissionCapture("02-objectives");await Send(Key.Escape);
  foreach(var g in nativeWorld.Entities.Where(e=>e.Type==12&&e.Owner==0).ToArray()){nativeSelection.Clear();nativeSelection.Add((g.Slot,g.Incarnation));camera=NativePosition(g);ClampMissionCamera();await UiCapture("03-split-gorrem-"+g.Slot);Check(missionHits.Any(h=>h.Id=="disease"&&!h.Enabled),"Uncharged Gorrem shows original Disease control disabled");}
  SaveMissionSlot(0);CloseRelayPanel();long saved=nativeWorld.Tick;ReturnMainMenu();await SessionClick("resume-save");Check(mission.CampaignIndex==8&&nativeWorld.Tick==saved&&!economicMission,"Continue restores cityless split start");
  var caster=nativeWorld.Entities.First(e=>e.Type==12&&e.Owner==0);var commander=nativeWorld.Entities.First(e=>e.Owner==0&&nativeWorld.IsCommander(e));
  // Controlled presentation fixture: remove threats and charge one real Gorrem;
  // firing/field lifecycle still uses Godot targeting and shared simulation.
  foreach(var e in nativeWorld.Entities.Where(e=>e.Owner==1).ToArray())nativeWorld.Retire(e);caster.Energy=255;nativeSelection.Clear();nativeSelection.Add((caster.Slot,caster.Incarnation));nativeSelection.Add((commander.Slot,commander.Incarnation));camera=NativePosition(caster);zoom=2;ClampMissionCamera();nativeWorld.RefreshSight();
  await UiCapture("04-mixed-abilities");Check(missionHits.Any(h=>h.Id=="disease"&&h.Enabled)&&missionHits.Any(h=>h.Id=="inspire"&&h.Enabled),"Mixed commander/Gorrem retains both abilities");SaveMissionSlot(2);CloseRelayPanel();
  int orders=nativeWorld.Events.Count(e=>e.Kind=="order");await UiClick("disease");Check(targetMode=="disease"&&nativeWorld.Events.Count(e=>e.Kind=="order")==orders,"Disease button captures click and arms ground target");
  var target=new Cell(caster.Cell.X-3,caster.Cell.Z);var point=new Vector2(target.X*32+16,(nativeData.Scenario.Map.Height-1-target.Z)*32+16)*zoom+NativeOffset;
  await NativeMouse(MouseButton.Left,point);Check(caster.PendingOrder?.Order==OriginalOrder.Disease&&commander.PendingOrder==null,"Target dispatches only to eligible Gorrem in mixed selection");
  for(int i=0;i<150&&!nativeWorld.Missiles.Any(m=>m.State==3);i++)AdvanceInterference(1);AdvanceInterference(4);Check(nativeWorld.Missiles.Any(m=>m.State==3),"Visible original spore field from actual firing path");
  await UiCapture("05-disease-active");SyncDiseaseAudio();Check(diseaseAudio.Values.Any(v=>v.Playing&&v.Sound==63),"Original Disease sound63 loop active");
  SaveCodec.Write(Path.Combine(output,"disease-view-save.json"),mission.Snapshot());SaveMissionSlot(1);CloseRelayPanel();saved=nativeWorld.Tick;LoadMissionSlot(1);CloseRelayPanel();Check(nativeWorld.Tick==saved&&nativeWorld.Missiles.Any(m=>m.State==3),"In-game save/load preserves active field");
  foreach(float scale in new[]{.75f,1f,1.5f})foreach(int aspect in new[]{0,16}){ui=scale;maximumAspect=aspect;ApplyAspectLimit();camera=NativePosition(nativeWorld.Slots[caster.Slot]!);ClampMissionCamera();await UiCapture($"06-hud-{scale}-{aspect}");RelayLayout("A09 HUD");Check(!missionHits.Any(h=>h.Id.StartsWith("product:")),"No production invented in Underground");focusedUi="disease";relayPointerHelp=false;await UiCapture($"07-tooltip-{scale}-{aspect}");if(relayTooltip is {Visible:true})Check(GetViewportRect().Encloses(relayTooltip.GetGlobalRect()),"Disease tooltip actual bounds inside active viewport");focusedUi="";OpenMissionPanel("menu");await UiCapture($"08-menu-{scale}-{aspect}");RelayLayout("A09 menu");CloseRelayPanel();}
  AdvanceInterference(230);SyncDiseaseAudio();Check(diseaseAudio.Count==0&&!nativeWorld.Ground.Contains(OriginalWorld.DiseaseGround),"Field expiry releases occupancy and presentation loop");
  ui=GetViewportRect().Size.Y/1080;maximumAspect=0;ApplyAspectLimit();
  foreach(string state in new[]{"scanning-save","exit-armed-save"}){BuriedState(state);await Send(Key.Tab);await UiCapture("09-"+state);await Send(Key.J);await UiCapture("09-objectives-"+state);await Send(Key.Escape);}
  foreach(string result in new[]{"success","failure"}){BuriedState(result);await ToSignal(GetTree().CreateTimer(.8),SceneTreeTimer.SignalName.Timeout);Check(missionVideo is {Frame:>0,Enhanced:false},"Original Underground outcome movie "+result);await UiCapture("10-movie-"+result);ReleaseMissionVideo();BeginRewards();await UiCapture("11-result-"+result);}
  ResetFirstStrike();Check(nativeWorld.Tick==0&&nativeWorld.Entities.Count(e=>e.Owner==0)==12&&diseaseAudio.Count==0,"Restart restores split roster and clears effects");await MissionCapture("12-restart");
 }
 async Task VerifyBuriedTemporal(){
  missionHarnessManual=true;var data=PersistenceData("SCENARIO/ALIEN/ALIEN09.SCN");var snap=SaveCodec.Open(Path.Combine(root,"generated/buried-treasure/render-1080/disease-view-save.json"))["payload"]!.AsObject();InstallMission(data,OriginalMission.Restore(data,snap),false);var field=nativeWorld.Missiles.First(m=>m.State==3);camera=new(field.X/8f,(data.Scenario.Map.Height*256-field.Z)/8f);zoom=2;ClampMissionCamera();
  await UiCapture("disease-sequence-start");missionHarnessManual=false;var frames=new List<object>();
  for(int i=0;i<100;i++){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);frames.Add(new{nativeWorld.Tick,field.State,field.FlightRemaining,field.Animation.Frame,field.Animation.Delay});if(i%5==0){using var im=GetViewport().GetTexture().GetImage();im.SavePng(Path.Combine(output,$"disease-frame-{i:D3}.png"));}}
  missionHarnessManual=true;File.WriteAllText(Path.Combine(output,"disease-sequence.json"),System.Text.Json.JsonSerializer.Serialize(frames));AdvanceInterference(230);SyncDiseaseAudio();await UiCapture("disease-sequence-expired");Check(diseaseAudio.Count==0,"Temporal field retires audio");
 }
 async Task VerifyBuriedMenu(){
  Check(!godMode,"Fresh session God OFF");OpenMissionPanel("campaign");await MenuCapture("a09-locked");Check(missionHits.Any(h=>h.Id=="campaign-start:8"&&!h.Enabled),"Buried Treasure locked without Screamer completion");CloseRelayPanel();
  await SessionClick("options");await SessionClick("playtesting");await SessionClick("god-mode");CloseRelayPanel();CloseRelayPanel();
  foreach(float scale in new[]{1f,1.5f}){ui=scale;OpenMissionPanel("campaign");await MenuCapture("a09-campaign-"+scale);RelayLayout("Nine mission campaign");Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:"))==9&&missionHits.Any(h=>h.Id=="campaign-start:8"&&h.Enabled),"Nine temporary mission routes only");CloseRelayPanel();}
  OpenMissionPanel("campaign");await SessionClick("campaign-start:8");await MenuCapture("a09-briefing-top");await Send(Key.Pagedown);await MenuCapture("a09-briefing-middle");await Send(Key.Pagedown);await Send(Key.Pagedown);await MenuCapture("a09-briefing-bottom");Check(briefingScroll==briefingTotalLines-briefingVisibleLines,"Full long Underground briefing reachable");await Send(Key.Enter);Check(nativeWorld.GodMode,"Session God access applies to A09");
  BuriedState("success");godMode=true;ApplySessionPlaytesting();RecordCampaign();Check(CampaignProgress.Entry(CampaignPath,9)==null,"God-mode result writes no permanent progress");ReturnMainMenu();godMode=false;
  var previous=PersistenceData("SCENARIO/ALIEN/ALIEN08.SCN");var s=SaveCodec.Open(Path.Combine(root,"generated/screamer/mission/success.json"))["payload"]!.AsObject();InstallMission(previous,OriginalMission.Restore(previous,s),false);RecordCampaign();ContinueCampaign();Check(mission.CampaignIndex==8&&missionBriefing&&campaignEntry?.CampaignIndex==8&&!nativeWorld.GodMode,"Earned Screamer outcome enters Buried Treasure");await MenuCapture("a09-earned-entry");
 }
}
