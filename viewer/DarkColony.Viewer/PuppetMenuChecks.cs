using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool desktopReview;int desktopReviewCapture;
 async void CaptureDesktopReview(){
  await MenuCapture("windows-input-"+(++desktopReviewCapture).ToString("00"));
  GD.Print($"WINDOWS_REVIEW_CAPTURE {desktopReviewCapture} mainMenu={mainMenu} panel={missionPanel} mission={campaignMission?.Index} briefing={missionBriefing}");
 }
 // Source-only manual input fixture. Some desktop capture paths return black for
 // exclusive/fullscreen OpenGL windows; this fits a wide window on the same monitor.
 // No simulated input, game-state changes or normal-launch window preference.
 async void PrepareDesktopReview(){
  if(IsolatedPlayerData==null)throw new InvalidOperationException("Desktop review requires an isolated profile");
  desktopReview=true;
  GetWindow().Mode=Window.ModeEnum.Windowed;GetWindow().Size=new(2560,1080);
  GetWindow().Position=new(200,100);ApplyAspectLimit();QueueRedraw();
  await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);
  await MenuCapture("desktop-internal");
 }
 async Task VerifyPuppetMenu(){
  Check(!godMode,"Fresh process starts with god mode off");
  OpenMissionPanel("campaign");await MenuCapture("a06-normal-campaign");
  Check(missionHits.Any(h=>h.Id=="campaign-start:5"&&!h.Enabled),"Normal A06 entry requires Roswell completion");CloseRelayPanel();
  await SessionClick("options");await SessionClick("playtesting");await SessionClick("god-mode");CloseRelayPanel();CloseRelayPanel();
  foreach(float scale in new[]{.75f,1f,1.5f}){ui=scale;OpenMissionPanel("campaign");await MenuCapture("a06-god-campaign-"+scale);RelayLayout("Campaign "+scale);Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:")&&h.Enabled )==OriginalCampaign.SupportedTaarMissions,"Session temporarily opens all implemented missions");CloseRelayPanel();}
  ui=GetViewportRect().Size.Y/1080;OpenMissionPanel("campaign");await SessionClick("campaign-start:5");Check(godMode&&nativeWorld.GodMode&&mission.CampaignIndex==5&&missionBriefing,"God-mode campaign enters Puppet Masters");await MenuCapture("a06-god-briefing");
  await Send(Key.Enter);AdvanceInterference(250);await MenuCapture("a06-god-hud");SaveMissionSlot(1);CloseRelayPanel();ReturnMainMenu();godMode=false;
  await SessionClick("resume-save");Check(mission.CampaignIndex==5&&!nativeWorld.GodMode,"Continue does not restore god mode from a save");
  // Controlled outcome, after use and then disabling god mode. This is a storage
  // check only; it is not counted as an ordinary-command victory.
  foreach(var c in nativeWorld.Teams[2].Cities.Take(5))if(c.Entity!=null)nativeWorld.Retire(c.Entity);
  AdvanceInterference(220);Check(mission.Outcome is {Success:true},"Controlled Citadel completion reaches success");mission.SetClock(Math.Max(mission.Milliseconds,mission.Outcome!.Deadline+1));RecordCampaign();
  Check(!File.Exists(CampaignPath),"A06 god-mode run writes no permanent campaign progress after toggling off");
  await ToSignal(GetTree().CreateTimer(.7),SceneTreeTimer.SignalName.Timeout);Check(missionVideo is {Frame:>0,Enhanced:false},"Actual A06 victory route plays original movie");await MenuCapture("a06-victory-movie");ReleaseMissionVideo();BeginRewards();await MenuCapture("a06-victory-result");
  var puppetData=nativeData;ContinueCampaign();Check(mission.CampaignIndex==6&&missionBriefing,"Continue enters Civil War after Puppet Masters");await MenuCapture("a06-end-of-build");
  var failed=SaveCodec.Open(Path.Combine(root,"generated/puppet-masters/outcomes/controlled-failure.json"))["payload"]!.AsObject();InstallMission(puppetData,OriginalMission.Restore(puppetData,failed),false);await ToSignal(GetTree().CreateTimer(.7),SceneTreeTimer.SignalName.Timeout);Check(missionVideo is {Frame:>0,Enhanced:false},"Actual A06 failure route plays original movie");await MenuCapture("a06-failure-movie");ReleaseMissionVideo();BeginRewards();await MenuCapture("a06-failure-result");
  ReturnMainMenu();OpenMissionPanel("campaign");await MenuCapture("a06-relocked");Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:")&&h.Enabled)==1,"Temporary access leaves no persistent unlocks");
 }
}
