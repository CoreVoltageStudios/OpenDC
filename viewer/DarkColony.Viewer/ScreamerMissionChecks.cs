using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyScreamerTemporal(){
  missionHarnessManual=true;var snap=SaveCodec.Open(Path.Combine(root,"generated/screamer/ordinary-opening/route-2400-save.json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,snap),false);
  var commander=nativeWorld.Entities.First(e=>e.Owner==0&&e.Living&&nativeWorld.IsCommander(e));camera=NativePosition(commander);zoom=2;ClampMissionCamera();nativeSelection.Add((commander.Slot,commander.Incarnation));
  Check(!nativeWorld.GodMode&&nativeWorld.Entities.Any(e=>e.Type==45&&NativeVisible(e)),"Ordinary pursuit reveals minefield through real local sight");
  await UiCapture("minefield-ordinary");missionHarnessManual=false;var frames=new List<object>();
  for(int i=0;i<100;i++){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);frames.Add(new{nativeWorld.Tick,units=nativeWorld.Entities.Where(e=>e.Type is 45 or 46||nativeWorld.IsCommander(e)).Select(e=>new{e.Slot,e.Type,e.X,e.Z,e.Health,e.State,body=e.Body.Set?.Name,e.Body.Frame}).ToArray()});if(i%5==0){using var frame=GetViewport().GetTexture().GetImage();frame.SavePng(Path.Combine(output,$"mine-frame-{i:D3}.png"));}}
  missionHarnessManual=true;File.WriteAllText(Path.Combine(output,"mine-sequence.json"),System.Text.Json.JsonSerializer.Serialize(frames));
 }
 async Task VerifyScreamerMission(){
  missionHarnessManual=true;AdvanceInterference(180);ResetMissionCamera();
  Check(mission.CampaignIndex==7&&economicMission&&!godMode,"Direct Screamer opening with God OFF");
  await MissionCapture("01-opening");await Send(Key.J);await MissionCapture("02-objectives");await Send(Key.Escape);await Send(Key.Tab);await MissionCapture("03-commander");
  SaveMissionSlot(0);CloseRelayPanel();long saved=nativeWorld.Tick;ReturnMainMenu();await SessionClick("resume-save");Check(mission.CampaignIndex==7&&nativeWorld.Tick==saved,"Continue restores Screamer opening");
  var data=nativeData;
  foreach(string state in new[]{"minefield","betrayal","reinforced","extraction","returned"}){
   var snap=SaveCodec.Open(Path.Combine(root,"generated/screamer/mission",state+"-save.json"))["payload"]!.AsObject();InstallMission(data,OriginalMission.Restore(data,snap),false);
   var commander=nativeWorld.Entities.FirstOrDefault(e=>e.Owner==0&&e.Living&&nativeWorld.IsCommander(e));
   camera=state is "minefield" or "betrayal"?new(49*32,(nativeData.Scenario.Map.Height-5)*32):commander==null?camera:NativePosition(commander);ClampMissionCamera();
   if(commander!=null)nativeSelection.Add((commander.Slot,commander.Incarnation));
   await UiCapture("04-"+state);
  }
  foreach(float scale in new[]{.75f,1f,1.5f})foreach(int aspect in new[]{0,16}){
   ui=scale;maximumAspect=aspect;ApplyAspectLimit();developmentTab=1;
   await UiCapture($"05-hud-{scale}-{aspect}");RelayLayout("A08 HUD");
   focusedUi="product:84";relayPointerHelp=false;await UiCapture($"06-tooltip-{scale}-{aspect}");
   if(relayTooltip is {Visible:true})Check(GetViewportRect().Encloses(relayTooltip.GetGlobalRect()),"Screamer tooltip actual bounds inside viewport");focusedUi="";
   OpenMissionPanel("menu");await UiCapture($"07-menu-{scale}-{aspect}");RelayLayout("A08 menu");CloseRelayPanel();
  }
  ui=GetViewportRect().Size.Y/1080;maximumAspect=0;ApplyAspectLimit();
  SaveMissionSlot(1);CloseRelayPanel();saved=nativeWorld.Tick;LoadMissionSlot(1);CloseRelayPanel();Check(nativeWorld.Tick==saved&&nativeWorld.Teams[6].CommanderRecoveryDisabled==1,"Save/load retains transition/recovery flag");
  foreach(string result in new[]{"success","failure"}){
   var snap=SaveCodec.Open(Path.Combine(root,"generated/screamer/mission",result+".json"))["payload"]!.AsObject();InstallMission(data,OriginalMission.Restore(data,snap),false);await ToSignal(GetTree().CreateTimer(.8),SceneTreeTimer.SignalName.Timeout);
   Check(missionVideo is {Frame:>0,Enhanced:false},"Original Screamer outcome movie "+result);await UiCapture("08-movie-"+result);ReleaseMissionVideo();BeginRewards();await UiCapture("09-result-"+result);
  }
  ResetFirstStrike();Check(nativeWorld.Tick==0&&nativeWorld.Teams[0].Money==0&&nativeWorld.Teams[0].Allies[6]!=0,"Restart restores original pursuit start");await MissionCapture("10-restart");
 }
 async Task VerifyScreamerMenu(){
  Check(!godMode,"Fresh session God OFF");OpenMissionPanel("campaign");await MenuCapture("a08-locked");Check(missionHits.Any(h=>h.Id=="campaign-start:7"&&!h.Enabled),"Screamer locked without Civil War completion");CloseRelayPanel();
  await SessionClick("options");await SessionClick("playtesting");await SessionClick("god-mode");CloseRelayPanel();CloseRelayPanel();
  foreach(float scale in new[]{1f,1.5f}){ui=scale;OpenMissionPanel("campaign");await MenuCapture("a08-campaign-"+scale);RelayLayout("Campaign A08");Check(missionHits.Any(h=>h.Id=="campaign-start:7"&&h.Enabled),"Temporary Screamer access");CloseRelayPanel();}
  OpenMissionPanel("campaign");await SessionClick("campaign-start:7");await MenuCapture("a08-briefing-top");if(briefingTotalLines>briefingVisibleLines){await Send(Key.Pagedown);await MenuCapture("a08-briefing-middle");await Send(Key.Pagedown);await Send(Key.Pagedown);await MenuCapture("a08-briefing-bottom");Check(briefingScroll==briefingTotalLines-briefingVisibleLines,"Full briefing reachable");}
  await Send(Key.Enter);AdvanceInterference(180);Check(nativeWorld.GodMode,"Campaign session God mode applied");ReturnMainMenu();godMode=false;
  var previous=PersistenceData("SCENARIO/ALIEN/ALIEN07.SCN");InstallMission(previous,new OriginalMission(previous,0),false);foreach(int team in new[]{1,2,7})foreach(var c in nativeWorld.Teams[team].Cities.Take(5))if(c.Entity!=null)nativeWorld.Retire(c.Entity);
  AdvanceInterference(250);RecordCampaign();ContinueCampaign();Check(mission.CampaignIndex==7&&missionBriefing&&campaignEntry?.CampaignIndex==7&&!nativeWorld.GodMode,"Earned Civil War result enters Screamer");await MenuCapture("a08-earned-entry");
 }
}
