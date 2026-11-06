using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyReleasePackage(bool resume){
  if(IsolatedPlayerData==null)throw new InvalidOperationException("Release checks require ODC_USER_DATA isolation");
  string videoOnly=Path.Combine(output,"video-only-preference.json");ArtworkPreference.SaveEnhancedVideo(videoOnly,false);
  Check(ArtworkPreference.Load(videoOnly,out var missingArtwork)==ArtworkStyle.Classic&&missingArtwork==null&&!ArtworkPreference.LoadEnhancedVideo(videoOnly),"Video-only preferences preserve a valid default artwork setting without a warning");
  missionHarnessManual=true;
  async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
  async Task Mix(string name){var r=new AudioEffectRecord{Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,r);r.SetRecordingActive(true);await Wait(2);r.SetRecordingActive(false);using var w=r.GetRecording();Check(w.SaveToWav(Path.Combine(output,name+".wav"))==Error.Ok,"Captured package mixer "+name);AudioServer.RemoveBusEffect(0,AudioServer.GetBusEffectCount(0)-1);r.Dispose();}
  if(resume){
   Check(continueSlot==1&&availableInterference?.Rank==1&&completedInterference!=null,"Fresh package process retains campaign and latest save");
   Check(nativeSampling==0&&!fastGame&&!enhancedVideo,"Artwork and speed persist across package processes");
   await UiClick("resume-save");Check(economicMission&&!missionBriefing&&nativeWorld.Tick>=540,"Fresh Continue restores active Interference");await UiCapture("continued-interference");
   await Send(Key.Tab);await ClickNativeCell(MouseButton.Right,new(17,74));missionHarnessManual=false;await Wait(2);missionHarnessManual=true;
   await UiCapture("continued-movement");await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");await UiClick("load");await UiClick("load-slot:0");Check(!economicMission&&!missionBriefing,"Load Game restores First Strike in fresh process");await UiCapture("loaded-first-strike");
   await Send(Key.R);Check(missionBriefing,"Packaged restart returns briefing");await Send(Key.Escape);Check(mainMenu&&nativeWorld==null,"Briefing return releases package session");await MenuCapture("returned-main-menu");return;
  }
  Check(!enhancedVideo,"Fresh profile uses original-source video");
  Check(continueSlot<0&&availableInterference==null,"Fresh isolated profile starts locked with no Continue");await UiClick("campaign");await MenuCapture("fresh-campaign");await UiClick("campaign-start:0");await MenuCapture("first-strike-briefing");await Mix("briefing");
  await Send(Key.Enter);AdvanceInterference(310);await Send(Key.Tab);await ClickNativeCell(MouseButton.Right,new(27,63));missionHarnessManual=false;await Wait(2);missionHarnessManual=true;await Send(Key.S);await UiCapture("first-strike-play");
  await Send(Key.Escape);await UiClick("save");await UiClick("save-slot:0");Check(File.Exists(SavePath(0)),"First Strike saved through packaged UI");await UiClick("close");
  // Explicit controlled progress fixture, isolated profile only. Native result rules retained.
  var d=PersistenceData("SCENARIO/ALIEN/ALIEN01.SCN");var win=new OriginalMission(d,0);win.World.Deaths[1,82]=11;win.World.NearbyCommanderKills[0]=10;for(int i=0;i<24;i++){win.SetClock((win.World.Tick+1)*66);win.Step();}win.SetClock(win.Outcome!.Deadline+1);CampaignProgress.Complete(CampaignPath,win);
  await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");await UiClick("campaign");Check(availableInterference?.Rank==1,"Controlled persisted victory unlocks Interference");await UiClick("campaign-start:1");await MenuCapture("interference-briefing");await Send(Key.Enter);AdvanceInterference(240);
  var miner=nativeWorld.Entities.Single(e=>e.Living&&e.Owner==0&&e.Type==14);await NativeMouse(MouseButton.Left,UnitHitRect(miner).GetCenter());await ClickNativeCell(MouseButton.Right,new(4,80));AdvanceInterference(300);Check(miner.Type==48&&nativeWorld.Teams[0].Money>0,"Packaged harvester earns original resource");
  nativeWorld.Teams[0].Money=12000;await UiClick("tab:1");await UiCapture("production-funded-fixture");var product=missionHits.First(h=>h.Enabled&&h.Id.StartsWith("product:"));await UiClick(product.Id);Check(nativeWorld.Teams[0].Money<12000,"Packaged production purchase commits resources");
  await Send(Key.Tab);var soldier=nativeWorld.Entities.First(e=>e.Living&&e.Owner==0&&e.Type==8);var enemy=nativeWorld.Spawn(0,1,new(soldier.Cell.X+2,soldier.Cell.Z),"fixture/package-combat");nativeWorld.RefreshSight();nativeSelection.Clear();nativeSelection.Add((soldier.Slot,soldier.Incarnation));await UiClick("attack");await NativeMouse(MouseButton.Left,UnitHitRect(enemy).GetCenter());AdvanceInterference(60);await UiCapture("interference-combat");await Mix("music-voices-effects");Check(missionMusic!.Frame>0,"Packaged music advances");
  var cameraBefore=camera;await NativeMouse(MouseButton.Left,MiniMapRect.GetCenter());Check(camera!=cameraBefore,"Packaged minimap navigates");await Send(Key.Tab);await Send(Key.O);await UiClick("art-crisp");await UiClick("speed-classic");await UiCapture("package-options");await UiClick("close");await Send(Key.Escape);await UiClick("save");await UiClick("save-slot:1");await UiCapture("saved-interference");
  foreach(bool success in new[]{true,false}){
   LoadMissionSlot(1);await Send(Key.O);await UiCapture(success?"options-enhanced":"options-original");await UiClick("close");if(success)nativeWorld.Deaths[1,86]=6;else foreach(int slot in new[]{0,1,2,3,4}){var c=nativeWorld.Teams[0].Cities[slot].Entity;if(c!=null)nativeWorld.Retire(c);}
   AdvanceInterference(24);Check(mission.Outcome!=null,"Controlled package outcome scheduled");mission.SetClock(mission.Outcome!.Deadline+1);await Wait(.4);Check(missionVideo is {Visible:true}&&!missionVideo.Enhanced,"Packaged outcome media opens selected Original/Enhanced mode");if(success)Check(!campaignWritePending&&CampaignProgress.Entry(CampaignPath,2)!=null,"Second campaign completion persists alongside First Strike");await Wait(1);await UiCapture(success?"outcome-success":"outcome-failure");await Send(Key.Enter);Check(rewardsVisible,"Packaged movie reaches rewards by skip");
  }
  LoadMissionSlot(1);await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");await MenuCapture("saved-main-menu");
  File.WriteAllText(Path.Combine(PlayerDataRoot,"package-start.json"),JsonSerializer.Serialize(new{fixture=true,version="0.1.0-test.7",save=1,campaignRank=availableInterference?.Rank}));
 }
}
