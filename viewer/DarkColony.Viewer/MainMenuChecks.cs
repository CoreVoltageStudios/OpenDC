using Godot;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task MenuCapture(string name){if(!mainMenu){await UiCapture(name);return;}QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(output,name+".png"));File.WriteAllText(Path.Combine(output,name+"-ui.json"),JsonSerializer.Serialize(new{mainMenu,ui,missionPanel,continueSlot,hits=missionHits}));}
 public async void MainMenuSuite(){try{
  missionHarnessManual=true;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  Check(mainMenu&&mission==null&&nativeWorld==null,"Normal main menu owns no active session");await MenuCapture("main-menu");
  if(missionSuite=="camera-overscroll"){await VerifyCameraOverscroll();}
  else if(missionSuite=="scenery-occlusion"){await VerifySceneryOcclusion();}
  else if(missionSuite=="network-names"){await VerifyNetworkNames();}
  else if(missionSuite=="enet-departure"){await VerifyDepartureUi();}
  else if(missionSuite=="landfall-replay"){await VerifyLandfallReplay();}
  else if(missionSuite=="motion-preference"){await VerifyMotionPreference();}
  else if(missionSuite=="landfall-results"){await VerifyLandfallResults();}
  else if(missionSuite=="landfall-performance"){await VerifyLandfallPerformance();}
  else if(missionSuite=="landfall"){await VerifyLandfall();}
  else if(missionSuite is "imported-runtime" or "imported-content-return"){await VerifyImportedRuntime();}
  else if(missionSuite.StartsWith("menu-map")){await CaptureMenuMap();}
  else if(missionSuite=="training-final"){await VerifyTrainingFinal();}
  else if(missionSuite=="training"){await VerifyTrainingInterface();}
  else if(missionSuite=="menu-pause"){await VerifyMenuPause();}
  else if(missionSuite=="council-introduction"){await VerifyCouncilIntroduction();}
  else if(missionSuite is "council-final" or "council-final-reset" or "council-final-god"){await VerifyCouncilFinal();}
  else if(missionSuite=="council-middle"){await VerifyCouncilMiddle();}
  else if(missionSuite=="council-next"){await VerifyCouncilNext();}
  else if(missionSuite is "council" or "council-missing"){await VerifyCouncilInterface();}
  else if(missionSuite is "council-final-performance" or "council-performance" or "council-next-performance" or "council-middle-performance"){await VerifyCouncilPerformance();}
  else if(missionSuite=="network-performance"){await VerifyNetworkPerformance();}
  else if(missionSuite=="network-rendering"){await VerifyNetworkTextRendering();}
  else if(missionSuite=="network-errors"){await VerifyNetworkErrors();}
  else if(missionSuite=="network"){await VerifyNetworkInterface();}
  else if(missionSuite=="war"){await VerifyWarInterface();}
  else if(missionSuite=="war-ordinary"){await VerifyWarOrdinary();}
  else if(missionSuite=="war-performance"){await VerifyWarPerformance();}
  else if(missionSuite is "human-late-campaign" or "human-late-reset"){await VerifyLateHumanCampaign();}
  else if(missionSuite is "human-campaign" or "human-reset"){await VerifyHumanCampaign();}
  else if(missionSuite=="completion-restart"){await VerifyCompletedRestart();}
  else if(missionSuite=="late-menu"){await VerifyLateTaarMenu();}
  else if(missionSuite=="buried-menu"){await VerifyBuriedMenu();}
  else if(missionSuite=="screamer-menu"){await VerifyScreamerMenu();}
  else if(missionSuite=="civil-war-menu"){await VerifyCivilWarMenu();}
  else if(missionSuite=="puppet-menu"){await VerifyPuppetMenu();}
  else if(missionSuite=="session-camera"){await VerifyCameraMotion();}
  else if(missionSuite is "session-polish" or "session-reload"){await VerifySessionPolish();}
  else if(missionSuite.StartsWith("installer")){await VerifyInstaller();}
  else if(missionSuite.StartsWith("ui-polish")){await VerifyUiPolish();}
  else if(missionSuite=="relay-polish"){
   Check(surfaceScene is { } && surfaceScene.GetWidth()==1450 && surfaceScene.GetHeight()==470,"Restricted cached original-art Surface still");
   foreach(float scale in new[]{GetViewportRect().Size.Y/1080,1.5f}){ui=scale;await MenuCapture("surface-polish-"+scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));foreach(string panel in new[]{"campaign","load","options"}){OpenMissionPanel(panel);await MenuCapture("surface-"+panel+"-"+scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));RelayLayout(panel+scale);await Send(Key.Escape);}}
  }
  else if(missionSuite=="relay-errors"){await VerifyRelayStorageErrors();}
  else if(missionSuite=="aspect-repair"){await VerifyAspectRepair();}
  else if(missionSuite=="aspect-window"){await VerifyAspectWindow();}
  else if(missionSuite=="playtest-repair"){await VerifyPlaytestRepair();}
  else if(missionSuite=="aspect-reload"){Check(maximumAspect==16,"Persisted aspect preference loaded");await MenuCapture("aspect-reloaded");}
  else if(missionSuite=="settings-malformed"){await VerifyMalformedSettings();}
  else if(missionSuite=="settings-shortcuts"){await VerifyMalformedSettingShortcuts();}
  else if(missionSuite=="settings-reload"){await VerifyRecoveredSettingsReload();}
  else if(missionSuite=="campaign-catalog"){await VerifyCampaignCatalog();}
  else if(missionSuite=="relay-resume"){await VerifyRelayResume();}
  else if(missionSuite=="relay"){await VerifyRelaySurface();}
  else if(missionSuite.StartsWith("release-")){await VerifyReleasePackage(missionSuite=="release-resume");}
  else if(missionSuite=="menu-roswell-continue"){
   Check(continueSlot==0&&completedBreakthrough!=null,"Fresh process finds A05 save and A04 completion");await UiClick("resume-save");Check(mission!.CampaignIndex==4&&!economicMission&&nativeData.Scenario.Header.PaletteName=="HTRAIN.GIF","Fresh Continue restores A05 terrain and cityless state");Check(!fastGame&&nativeSampling==0&&!enhancedVideo,"Fresh process preferences persist");await MenuCapture("continued-roswell");
   long tick=nativeWorld!.Tick;missionHarnessManual=false;await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);missionHarnessManual=true;Check(nativeWorld!.Tick>tick,"Restored A05 advances");await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");await UiClick("campaign");await MenuCapture("campaign-five");await UiClick("campaign-start:4");Check(missionBriefing&&mission!.CampaignIndex==4,"Persisted campaign unlock enters A05");await MenuCapture("roswell-briefing");
  }
  else if(missionSuite=="menu-breakthrough-continue"){
   Check(continueSlot==0&&completedLegacy!=null,"Fresh process finds A04 save and earned A03 completion");await UiClick("resume-save");Check(mission!.CampaignIndex==3&&nativeData.Scenario.Header.PaletteName=="JUNGLE.GIF","Fresh-process Continue restores A04 palette/state");await MenuCapture("continued-breakthrough");
   missionHarnessManual=false;await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);missionHarnessManual=true;Check(nativeWorld!.Tick>240,"Restored A04 advances");await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");await UiClick("campaign");await MenuCapture("campaign-four-missions");await UiClick("campaign-start:3");Check(missionBriefing&&mission!.CampaignIndex==3,"Unlocked normal campaign menu starts A04");await MenuCapture("breakthrough-briefing");
  }
  else if(missionSuite=="menu-legacy-continue"){
   Check(continueSlot==0&&completedInterference!=null,"Fresh process finds A03 save and persisted A02 completion");await UiClick("resume-save");Check(mission!.CampaignIndex==2&&nativeData.Scenario.Header.PaletteName=="JUNGLE.GIF","Fresh-process Continue restores A03 jungle");await MenuCapture("continued-legacy");
   missionHarnessManual=false;await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);missionHarnessManual=true;Check(nativeWorld!.Tick>240,"Restored A03 advances");await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");await UiClick("campaign");await MenuCapture("campaign-three-missions");await UiClick("campaign-start:2");Check(missionBriefing&&mission!.CampaignIndex==2,"Unlocked campaign menu starts A03");await MenuCapture("legacy-briefing");
  }
  else if(missionSuite=="menu-continue"){
   Check(continueSlot==0,"Fresh process finds newest valid slot despite newer broken slot");await UiClick("resume-save");
   Check(!mainMenu&&!missionBriefing&&nativeWorld!.Tick>=300,"Continue restores saved active Interference");
   long tick=nativeWorld!.Tick;missionHarnessManual=false;await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);missionHarnessManual=true;
   Check(nativeWorld!.Tick>tick,"Restored session advances normally");await MenuCapture("continued-mission");
   await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");await UiClick("campaign");
   Check(availableInterference?.Rank==1,"Campaign progress and earned profile survive process restart");await MenuCapture("campaign-unlocked");
   await UiClick("campaign-start:1");Check(missionBriefing&&economicMission&&campaignEntry?.Rank==1,"Campaign starts unlocked Interference with earned profile");await MenuCapture("interference-briefing-clean");await Send(Key.Escape);
   await UiClick("load");await MenuCapture("load-slots");await UiClick("load-slot:0");Check(!mainMenu&&!missionBriefing,"Main menu Load Game enters mission");
  }else{
   Check(continueSlot<0,"Continue disabled with no valid saves");await UiClick("campaign");Check(availableInterference==null,"New campaign locks Interference");await MenuCapture("campaign-new");
   await UiClick("campaign-start:0");Check(missionBriefing&&!economicMission,"Campaign opens First Strike briefing");await MenuCapture("first-strike-briefing-clean");
   Check(!missionHits.Any(h=>h.Id is "load" or "campaign"),"Briefing has no save/campaign overlays competing with scroll controls");
   await Send(Key.Enter);AdvanceInterference(310);await Send(Key.Escape);await UiClick("save");await UiClick("save-slot:0");await MenuCapture("main-menu-created-save");await UiClick("close");await Send(Key.Escape);await UiClick("main-menu");
   Check(missionPanel=="abandon"&&!mainMenu,"Returning from play requires unsaved-play confirmation");await MenuCapture("unsaved-warning");await UiClick("abandon-cancel");Check(!mainMenu,"Cancel retains live session");await UiClick("main-menu");await UiClick("abandon-confirm");
   Check(mainMenu&&mission==null&&nativeWorld==null&&missionAudioPlayer==null&&missionPlayback==null&&missionMusic==null&&nativeTerrain.Count==0,"Main menu releases simulation, terrain and media");
   Check(continueSlot==0,"Continue available after saving");await UiClick("options");await MenuCapture("main-options");await UiClick("close");await UiClick("resume-save");Check(!mainMenu&&nativeWorld!.Tick==310,"Continue resumes exact saved tick");
  }
  Check(failureLogger.FirstError==null&&missionFaultCount==0,"No engine or simulation faults");
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));GD.Print("MAIN_MENU_SUITE_PASS "+missionSuite);await FinishFirstStrike(0);
 }catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());GD.PushError(e.ToString());await FinishFirstStrike(1);}}
}
