using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 static string LateFolder(int map)=>map switch{10=>"animal-instinct",11=>"spider-canyon",12=>"then-there-was-one",13=>"earth-intervenes",14=>"the-answer",15=>"final-evacuation",_=>throw new ArgumentOutOfRangeException(nameof(map))};
 void LateState(int map,string name){var data=PersistenceData($"SCENARIO/ALIEN/ALIEN{map:00}.SCN");var snap=SaveCodec.Open(Path.Combine(root,"generated",LateFolder(map),map==15?"mission-final":"mission",name+".json"))["payload"]!.AsObject();InstallMission(data,OriginalMission.Restore(data,snap),false);}
 async Task VerifyLateTaarMission(){
  int map=campaignMission.Index+1;missionHarnessManual=true;Check(economicMission==(map!=14)&&!godMode,"Direct launch production eligibility and God OFF");AdvanceInterference(map==10?1000:700);ResetMissionCamera();
  await UiCapture("01-opening");await Send(Key.J);await UiCapture("02-objectives");await Send(Key.Escape);
  SaveMissionSlot(0);CloseRelayPanel();long saved=nativeWorld.Tick;ReturnMainMenu();await SessionClick("resume-save");Check(mission.CampaignIndex==map-1&&nativeWorld.Tick==saved,"Continue restores exact mission opening");
  if(map==10){var data=nativeData;var s=SaveCodec.Open(Path.Combine(root,"generated/animal-instinct/shared/activation-animation-save.json"))["payload"]!.AsObject();InstallMission(data,OriginalMission.Restore(data,s),false);var artifact=nativeWorld.Entities.Single(e=>e.Type==67);nativeSelection.Clear();nativeSelection.Add((artifact.Slot,artifact.Incarnation));camera=NativePosition(artifact);nativeWorld.RefreshSight();ClampMissionCamera();await UiCapture("03-tektaara-animation");AdvanceInterference(180);var wildlife=nativeWorld.Entities.First(e=>e.Owner==0&&e.Type is 23 or 36);nativeSelection.Clear();nativeSelection.Add((wildlife.Slot,wildlife.Incarnation));camera=NativePosition(wildlife);ClampMissionCamera();await UiCapture("04-controlled-wildlife");}
  if(map==11){
   var s=SaveCodec.Open(Path.Combine(root,"generated/spider-canyon/shared/deploying-save.json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,s),false);
   var gorrem=nativeWorld.Entities.Single(e=>e.SourceIdentity=="controlled/gorrem");nativeSelection.Clear();nativeSelection.Add((gorrem.Slot,gorrem.Incarnation));camera=NativePosition(gorrem);ClampMissionCamera();
   for(int f=0;f<12;f++){await UiCapture("03-siphon-deploy-"+f.ToString("00"));AdvanceInterference(4);}
   AdvanceInterference(100);Check(gorrem.Type==78,"Rendered deployed siphoner role");int earned=nativeWorld.EarnedResources[0];AdvanceInterference(32);Check(nativeWorld.EarnedResources[0]>earned,"Deployed visual corresponds to real income");await UiCapture("04-siphon-income");
   await SessionClick("retract");AdvanceInterference(150);Check(gorrem.Type==12,"Sidebar Retract dispatched to siphoner");await SessionClick("siphon");AdvanceInterference(150);Check(gorrem.Type==78,"Sidebar Steal Petra-7 redeploys");
  }
  if(map==12){var s=SaveCodec.Open(Path.Combine(root,"generated/then-there-was-one/artifacts/tektaara-animation-save.json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,s),false);var tek=nativeWorld.Entities.First(e=>e.Type==67&&e.Owner==0);nativeSelection.Clear();nativeSelection.Add((tek.Slot,tek.Incarnation));camera=NativePosition(tek);ClampMissionCamera();await UiCapture("03-excavated-tektaara");AdvanceInterference(180);var animal=nativeWorld.Entities.First(e=>e.Owner==0&&e.Type is 23 or 36);nativeSelection.Clear();nativeSelection.Add((animal.Slot,animal.Incarnation));camera=NativePosition(animal);ClampMissionCamera();await UiCapture("04-wildlife-ownership");}
  LateState(map,map==10?"hive-established-save":"arrival-save");
  var commander=nativeWorld.Entities.FirstOrDefault(e=>e.Owner==0&&nativeWorld.IsCommander(e));if(commander!=null){nativeSelection.Add((commander.Slot,commander.Incarnation));camera=NativePosition(commander);ClampMissionCamera();}
  foreach(float scale in new[]{.75f,1f,1.5f})foreach(int aspect in new[]{0,16}){ui=scale;maximumAspect=aspect;ApplyAspectLimit();await UiCapture($"05-hud-{scale}-{aspect}");RelayLayout("A"+map+" HUD");focusedUi="inspire";relayPointerHelp=false;await UiCapture($"06-tooltip-{scale}-{aspect}");if(relayTooltip is {Visible:true})Check(GetViewportRect().Encloses(relayTooltip.GetGlobalRect()),"Actual tooltip bounds inside active viewport");focusedUi="";OpenMissionPanel("menu");await UiCapture($"07-menu-{scale}-{aspect}");RelayLayout("A"+map+" menu");CloseRelayPanel();}
  ui=GetViewportRect().Size.Y/1080;maximumAspect=0;ApplyAspectLimit();
  if(map==15){var s=SaveCodec.Open(Path.Combine(root,"generated/final-evacuation/portalis-final/projectile-flight-save.json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,s),false);var portalis=nativeWorld.Entities.Single(e=>e.Type==105);nativeSelection.Add((portalis.Slot,portalis.Incarnation));camera=NativePosition(portalis);ClampMissionCamera();for(int f=0;f<12;f++){await UiCapture("portalis-fire-"+f.ToString("00"));AdvanceInterference(2);}Check(nativeWorld.Events.Any(e=>e.Kind=="hit"),"Rendered Portalis flight has actual impact");}
  foreach(string result in new[]{"success","failure"}){LateState(map,result);await ToSignal(GetTree().CreateTimer(.8),SceneTreeTimer.SignalName.Timeout);Check(missionVideo is {Frame:>0,Enhanced:false},"Original-source outcome movie "+result);await UiCapture("08-movie-"+result);ReleaseMissionVideo();BeginRewards();await UiCapture("09-result-"+result);}
  ResetFirstStrike();Check(nativeWorld.Tick==0&&mission.CampaignIndex==map-1,"Restart restores original mission");await UiCapture("10-restart");
 }
 async Task VerifyFinalEnding(){
  missionHarnessManual=true;LateState(15,"success");
  await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
  Check(missionVideo is {Streaming:true,Enhanced:false},"Final outcome chooses original indexed ending");
  Check(missionVideo!.RetainedVideoBytes<2000000,"Ending RGB buffers bounded below2MB");
  foreach(int aspect in new[]{0,16,21}){maximumAspect=aspect;ApplyAspectLimit();await UiCapture("ending-aspect-"+aspect);Check(GetViewportRect().Encloses(missionVideo.GetGlobalRect()),"Movie stays inside active viewport");}
  await Send(Key.Escape);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  Check(missionVideo==null&&rewardsVisible&&IndexedMovieFrames.ActiveWorkers==0,"Skip closes decoder and enters original rewards");
  await UiCapture("ending-skipped-results");ContinueCampaign();
  Check(mainMenu&&mission==null&&studioIntro==null&&!missionNext,"Final continuation returns to menu without mission16 or startup replay");
  Check(CampaignProgress.Read(CampaignPath)["completed"]!["14"]!=null,"Earned final completion persisted");await MenuCapture("campaign-complete");
 }
 async Task VerifyCompletedRestart(){
  Check(mainMenu&&!godMode&&studioIntro==null&&CampaignProgress.Read(CampaignPath)["completed"]!["14"]!=null,"Fresh process retains final campaign completion, God OFF");
  OpenMissionPanel("campaign");campaignScroll=14;await MenuCapture("campaign-complete-restart");
  Check(missionHits.Any(h=>h.Id=="campaign-start:14"&&h.Enabled)&&!missionHits.Any(h=>h.Id=="campaign-start:15"),"Completed final mission replay remains accessible; no mission16");
 }
 async Task VerifyLateTaarMenu(){
  int map=OriginalCampaign.SupportedTaarMissions;Check(!godMode,"Fresh session God OFF");
  OpenMissionPanel("campaign");campaignScroll=map-1;await MenuCapture("late-locked");Check(missionHits.Any(h=>h.Id=="campaign-start:"+(map-1)&&!h.Enabled),"New mission locked before earned progress");CloseRelayPanel();
  await SessionClick("options");await SessionClick("playtesting");await SessionClick("god-mode");CloseRelayPanel();CloseRelayPanel();
  foreach(float scale in new[]{1f,1.5f}){ui=scale;OpenMissionPanel("campaign");campaignScroll=0;await MenuCapture("late-campaign-top-"+scale);RelayLayout("Campaign top");await Send(Key.Pagedown);await Send(Key.Pagedown);await MenuCapture("late-campaign-bottom-"+scale);RelayLayout("Campaign bottom");Check(missionHits.Any(h=>h.Id=="campaign-start:"+(map-1)&&h.Enabled),"Paged temporary mission access at scale"+scale);CloseRelayPanel();}
  OpenMissionPanel("campaign");await MenuCapture("late-campaign-select");await SessionClick("campaign-start:"+(map-1));await MenuCapture("late-briefing-top");await Send(Key.Pagedown);await MenuCapture("late-briefing-middle");for(int i=0;i<8;i++)await Send(Key.Pagedown);await MenuCapture("late-briefing-bottom");Check(briefingScroll==Math.Max(0,briefingTotalLines-briefingVisibleLines),"Complete briefing text reachable");await Send(Key.Enter);Check(nativeWorld.GodMode,"Temporary access applies session God mode");
  LateState(map,"success");godMode=true;ApplySessionPlaytesting();RecordCampaign();Check(CampaignProgress.Entry(CampaignPath,map)==null,"God completion writes no permanent progress");ReturnMainMenu();godMode=false;
  int previous=map-1;string previousFolder=previous==9?"buried-treasure/mission-verified":LateFolder(previous)+"/mission";var data=PersistenceData($"SCENARIO/ALIEN/ALIEN{previous:00}.SCN");var snap=SaveCodec.Open(Path.Combine(root,"generated",previousFolder,"success.json"))["payload"]!.AsObject();InstallMission(data,OriginalMission.Restore(data,snap),false);RecordCampaign();ContinueCampaign();Check(mission.CampaignIndex==map-1&&missionBriefing&&!nativeWorld.GodMode,"Earned prior outcome enters next mission normally");await MenuCapture("late-earned-entry");
  if(map==12){LateState(map,"success");ReleaseMissionVideo();ContinueCampaign();Check(missionNext&&mission.CampaignIndex==11&&nextMissionNotice.Contains("not available"),"A12 result retained without launching unavailable A13");await MenuCapture("late-end-of-build");ReturnMainMenu();OpenMissionPanel("campaign");campaignScroll=11;await MenuCapture("late-consolidated");Check(OriginalCampaign.Taar(nativeData).Count(m=>m.Implemented)==12&&!missionHits.Any(h=>h.Id=="campaign-start:12"),"Consolidated twelve-mission catalog excludes13-15");}
 }
}
