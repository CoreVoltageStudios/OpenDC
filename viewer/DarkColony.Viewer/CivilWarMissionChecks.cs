using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyCivilWarTemporal(){
  // Controlled shared human-healer rendering, separate from ordinary gameplay.
  missionHarnessManual=true;foreach(var b in mission.Blocks)if(b!=null)b.Remaining=0;foreach(var t in nativeWorld.Teams)t.Ai=0;AdvanceInterference(180);
  var healer=nativeWorld.Spawn(49,0,new(38,81),"controlled/Medi-craft");var patient=nativeWorld.Spawn(0,0,new(39,81),"controlled/human-patient");patient.Health-=100;
  nativeSelection.Clear();nativeSelection.Add((healer.Slot,healer.Incarnation));camera=NativePosition(healer);zoom=2;ClampMissionCamera();
  var sequence=new List<object>();missionHarnessManual=false;
  for(int i=0;i<100;i++){
   await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
   sequence.Add(new{nativeWorld.Tick,healer.Energy,body=healer.Body.Set?.Name,healer.Body.Frame,healer.Body.Mode,patient.Health,effect=patient.Overlay.Set?.Name,effectFrame=patient.Overlay.Frame,effectMode=patient.Overlay.Mode});
   if(i%4==0){using var frame=GetViewport().GetTexture().GetImage();frame.SavePng(Path.Combine(output,$"medicraft-frame-{i:D3}.png"));}
  }
  missionHarnessManual=true;File.WriteAllText(Path.Combine(output,"medicraft-sequence.json"),JsonSerializer.Serialize(sequence));
  Check(nativeWorld.HealedHealth[0]>=36&&nativeWorld.AudioCues.Any(e=>e.Kind=="heal"&&e.Type==49),"Medi-craft renders shared healing and original DPY cue");
  var snapshot=SaveCodec.Open(Path.Combine(root,"generated/civil-war/mission/hostiles-first-success.json"))["payload"]!.AsObject();
  InstallMission(nativeData,OriginalMission.Restore(nativeData,snapshot),false);await ToSignal(GetTree().CreateTimer(8),SceneTreeTimer.SignalName.Timeout);
  Check(rewardsVisible&&missionVideo==null,"Original victory movie finishes into rewards");
  await ToSignal(GetTree().CreateTimer(4),SceneTreeTimer.SignalName.Timeout);await UiCapture("rewards-animation");
 }
 async Task VerifyCivilWarMission(){
  missionHarnessManual=true;AdvanceInterference(180);ResetMissionCamera();
  Check(economicMission&&mission.CampaignIndex==6&&!godMode,"Direct Civil War entry is ordinary economy gameplay");
  Check((camera-NativePosition(nativeWorld.Teams[0].Cities[0].Entity!)).Length()<200,"Opening view frames imported player base");
  Check(nativeWorld.ArtifactContents.Values.Sum(c=>c.Count)==2&&!nativeWorld.Entities.Any(e=>e.Type==65),"A07 buried artifacts remain distinct");
  await MissionCapture("01-opening");await Send(Key.J);await MissionCapture("02-objectives");await Send(Key.Escape);
  var commander=nativeWorld.Entities.First(e=>e.Owner==0&&nativeWorld.IsCommander(e));
  nativeSelection.Add((commander.Slot,commander.Incarnation));await MissionCapture("03-commander");
  var savedCamera=camera;float savedZoom=zoom;SaveMissionSlot(0);CloseRelayPanel();long tick=nativeWorld.Tick;ReturnMainMenu();await SessionClick("resume-save");
  Check(mission.CampaignIndex==6&&nativeWorld.Tick==tick,"Continue restores A07 save");
  Check(camera==savedCamera&&zoom==savedZoom,"Continue preserves camera position and zoom");
  // Controlled visual fixture: native production, original costs/animations; only
  // budget, damage and inactive AI/scripts are setup. Not an ordinary victory.
  foreach(var b in mission.Blocks)if(b!=null)b.Remaining=0;foreach(var t in nativeWorld.Teams)t.Ai=0;
  nativeWorld.Teams[0].Money=20000;
  foreach(int product in new[]{16,17,18,19,20}){
   Check(nativeWorld.RequestProduction(0,product).Accepted,"A07 original prerequisite "+product);
   for(int i=0;i<1500&&nativeWorld.Teams[0].Cities.Any(c=>c.InitializationPending);i++)AdvanceInterference(1);
  }
  developmentTab=1;await UiCapture("04-zisp-ready");await SessionClick("product:84");AdvanceInterference(12);await UiCapture("05-zisp-producing");
  Check(relayProducts[84].Active&&relayProducts[84].Progress is >0 and <1,"Production strip advances with native relative FIN frame");
  for(int i=0;i<500&&!nativeWorld.Entities.Any(e=>e.Owner==0&&e.Type==50);i++)AdvanceInterference(1);
  var healer=nativeWorld.Entities.Single(e=>e.Owner==0&&e.Type==50);nativeSelection.Clear();nativeSelection.Add((healer.Slot,healer.Incarnation));
  camera=NativePosition(healer);zoom=2;ClampMissionCamera();
  var patient=nativeWorld.Spawn(8,0,healer.Cell,"controlled/healing-patient");patient.Health-=100;
  nativeWorld.Command(new(healer.Slot,healer.Incarnation,OriginalOrder.Stop));
  await UiCapture("06-healer");
  var sequence=new List<object>();missionHarnessManual=false;
  for(int i=0;i<100;i++){
   await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
   sequence.Add(new{nativeWorld.Tick,healer.Energy,healer.X,healer.Z,body=healer.Body.Set?.Name,healer.Body.Frame,healer.Body.Mode,patient.Health,effect=patient.Overlay.Set?.Name,effectFrame=patient.Overlay.Frame,effectMode=patient.Overlay.Mode});
   if(i%3==0){using var frame=GetViewport().GetTexture().GetImage();frame.SavePng(Path.Combine(output,$"healing-frame-{i:D3}.png"));}
  }
  missionHarnessManual=true;File.WriteAllText(Path.Combine(output,"healing-sequence.json"),JsonSerializer.Serialize(sequence));
  Check(nativeWorld.HealedHealth[0]>0&&nativeWorld.AudioCues.Any(e=>e.Kind=="heal"),"Rendered healing restores health with original cue");
  foreach(float scale in new[]{.75f,1f,1.5f})foreach(int aspect in new[]{0,16}){
   ui=scale;maximumAspect=aspect;ApplyAspectLimit();camera=NativePosition(healer);ClampMissionCamera();developmentTab=1;
   await UiCapture($"07-hud-{scale}-{aspect}");RelayLayout("A07 HUD "+scale+"/"+aspect);
   Check(missionHits.Any(h=>h.Id=="product:84"),"Zisp visible in four-column category");
   focusedUi="product:84";relayPointerHelp=false;await UiCapture($"08-tooltip-{scale}-{aspect}");
   Check(relayTooltip is {Visible:true}&&GetViewportRect().Encloses(relayTooltip.GetGlobalRect()),"Actual Zisp tooltip bounds fit viewport");focusedUi="";
   OpenMissionPanel("menu");await UiCapture($"09-menu-{scale}-{aspect}");RelayLayout("A07 menu");CloseRelayPanel();
  }
  ui=GetViewportRect().Size.Y/1080;maximumAspect=0;ApplyAspectLimit();
  SaveMissionSlot(1);CloseRelayPanel();long saved=nativeWorld.Tick;LoadMissionSlot(1);CloseRelayPanel();Check(nativeWorld.Tick==saved&&nativeWorld.HealedHealth[0]>0,"UI save/load preserves healing state");
  var oldData=nativeData;
  foreach(var pair in new[]{("hostiles-first-success","jtalk2"),("failure","bodys")}){
   var snapshot=SaveCodec.Open(Path.Combine(root,"generated/civil-war/mission",pair.Item1+".json"))["payload"]!.AsObject();
   InstallMission(oldData,OriginalMission.Restore(oldData,snapshot),false);await ToSignal(GetTree().CreateTimer(.8),SceneTreeTimer.SignalName.Timeout);
   Check(missionVideo is {Frame:>0,Enhanced:false},"A07 original outcome movie "+pair.Item2);await UiCapture("10-movie-"+pair.Item2);
   ReleaseMissionVideo();BeginRewards();await UiCapture("11-result-"+pair.Item2);
   if(pair.Item2=="jtalk2"){ContinueCampaign();Check(mission.CampaignIndex==7&&missionBriefing,"Continue enters implemented A08");await UiCapture("12-end-of-build");}
  }
  ResetFirstStrike();Check(missionBriefing&&nativeWorld.Tick==0&&nativeWorld.HealedHealth.All(v=>v==0)&&nativeWorld.Teams[0].Allies[1]!=0,"Restart clears healing and restores native alliance");await MissionCapture("13-restart");
 }
 async Task VerifyCivilWarMenu(){
  Check(!godMode,"Fresh process god mode is off");OpenMissionPanel("campaign");await MenuCapture("a07-locked");
  Check(missionHits.Any(h=>h.Id=="campaign-start:6"&&!h.Enabled),"Civil War locked without Puppet Masters completion");CloseRelayPanel();
  await SessionClick("options");await SessionClick("playtesting");await SessionClick("god-mode");CloseRelayPanel();CloseRelayPanel();
  foreach(float scale in new[]{.75f,1f,1.5f}){ui=scale;OpenMissionPanel("campaign");await MenuCapture("a07-campaign-"+scale);RelayLayout("Seven mission campaign "+scale);Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:")&&h.Enabled)==OriginalCampaign.SupportedTaarMissions,"God mode opens implemented missions only");CloseRelayPanel();}
  ui=GetViewportRect().Size.Y/1080;OpenMissionPanel("campaign");await SessionClick("campaign-start:6");
  Check(missionBriefing&&mission.CampaignIndex==6&&nativeWorld.GodMode,"God-mode campaign launches Civil War");await MenuCapture("a07-briefing-default");
  ui=1.5f;await MenuCapture("a07-briefing-top");
  if(briefingTotalLines>briefingVisibleLines){
   await Send(Key.Pagedown);await MenuCapture("a07-briefing-middle");Check(briefingScroll>0,"Page Down reaches remaining briefing text");
   await Send(Key.Pagedown);await MenuCapture("a07-briefing-bottom");Check(briefingScroll==briefingTotalLines-briefingVisibleLines,"Briefing reaches final original line");await Send(Key.Pageup);
  }else Check(briefingScroll==0,"Short briefing entirely visible without unnecessary scrolling");
  ui=GetViewportRect().Size.Y/1080;
  await Send(Key.Enter);AdvanceInterference(180);SaveMissionSlot(2);CloseRelayPanel();ReturnMainMenu();godMode=false;await SessionClick("resume-save");
  Check(mission.CampaignIndex==6&&!nativeWorld.GodMode,"Loading does not restore session god mode");
  foreach(int team in new[]{1,2,7})foreach(var c in nativeWorld.Teams[team].Cities.Take(5))if(c.Entity!=null)nativeWorld.Retire(c.Entity);
  AdvanceInterference(250);RecordCampaign();Check(!File.Exists(CampaignPath),"God-mode run cannot write permanent Civil War completion after toggling off");
  ReturnMainMenu();OpenMissionPanel("campaign");await MenuCapture("a07-relocked");Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:")&&h.Enabled)==1,"Temporary access leaves no progress");CloseRelayPanel();
  // Controlled A06 completion exercises the normal catalog/profile handoff.
  var puppet=PersistenceData("SCENARIO/ALIEN/ALIEN06.SCN");InstallMission(puppet,new OriginalMission(puppet,0),false);foreach(var c in nativeWorld.Teams[2].Cities.Take(5))if(c.Entity!=null)nativeWorld.Retire(c.Entity);
  AdvanceInterference(250);mission.SetClock(Math.Max(mission.Milliseconds,mission.Outcome!.Deadline+1));RecordCampaign();ContinueCampaign();
  Check(mission.CampaignIndex==6&&missionBriefing&&campaignEntry?.CampaignIndex==6&&!nativeWorld.GodMode,"Puppet Masters result enters Civil War with earned profile");await MenuCapture("a07-normal-progression");
  ReturnMainMenu();OpenMissionPanel("campaign");await MenuCapture("a07-earned-unlock");Check(missionHits.Any(h=>h.Id=="campaign-start:6"&&h.Enabled),"Earned Puppet Masters progress unlocks Civil War");
 }
}
