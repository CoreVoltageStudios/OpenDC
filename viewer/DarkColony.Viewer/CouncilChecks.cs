using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyCouncilInterface(){
  missionHarnessManual=true;Check(!godMode,"Council session starts God OFF");
  await SessionClick("campaign");await SessionClick("edition:council");
  if(missionSuite=="council-missing"){
   Check(!councilAvailable&&!missionHits.Any(h=>h.Id.StartsWith("campaign-start:")),"Missing optional content shows no launch controls");await MenuCapture("council-unavailable");
   Directory.CreateDirectory(Path.GetDirectoryName(SavePath(0))!);File.Copy(Path.Combine(root,"generated/council-wars-first/profile3440/saves/slot-1.json"),SavePath(0),true);RefreshPersistenceCatalog();Check(saveInvalid[0]&&saveDetails[0].Contains("Council Wars"),"Missing expansion save has explicit content error");OpenMissionPanel("load");await MenuCapture("council-save-unavailable");LoadMissionSlot(0);Check(mainMenu&&missionPanel=="save-error"&&File.Exists(SavePath(0)),"Missing expansion is preserved, never loaded as base");CloseRelayPanel();OpenMissionPanel("campaign");
   await SessionClick("edition:base");await SessionClick("campaign-start:0");Check(!IsCouncil&&missionBriefing,"Base mission works without optional expansion");await MissionCapture("base-without-council");return;
  }
  foreach(int aspect in new[]{0,16})foreach(float scale in new[]{.75f,1f,1.5f}){
   maximumAspect=aspect;ApplyAspectLimit();ui=scale;await WarFrames();await MenuCapture($"council-campaign-{aspect}-{scale}");RelayLayout("Council campaign");
   Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:"))==Math.Min(campaignRows,OriginalCampaign.SupportedCount(SelectedCampaign)),"Implemented expansion missions listed");
  }
  maximumAspect=0;ApplyAspectLimit();ui=GetViewportRect().Size.Y/1080;
  int slot=0;
  foreach(string faction in new[]{"human","taar"}){
   await SessionClick("faction:"+faction);await MenuCapture("council-"+faction+"-selection");await SessionClick("campaign-start:0");
   Check(persistenceNotice.Length==0,"Prior result notice does not overlap the next briefing");Check(IsCouncil&&IsHuman==(faction=="human")&&missionBriefing,"Correct expansion/faction entry "+faction);
   Check(missionMusic!=null&&missionMusicDiagnostic==null,"Scoped original expansion music "+faction);
   await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);await MissionCapture("council-"+faction+"-briefing");
   await Send(Key.Space);Check(missionBriefing&&!BriefingRevealing&&BriefingTransmissionComplete,"Reveal all stays in Council briefing");await MissionCapture("council-"+faction+"-revealed");
   maximumAspect=16;ApplyAspectLimit();ui=1.5f;await MissionCapture("council-"+faction+"-briefing-centered-enlarged");RelayLayout("Council briefing");
   await Send(Key.Enter);AdvanceInterference(400);await MissionCapture("council-"+faction+"-opening-before-tab");Check(nativeWorld.Entities.Any(e=>e.Living&&e.Owner==0&&NativePosition(e).DistanceTo(camera)<500),"Opening camera frames local force without Tab "+faction);await Send(Key.Tab);Check(NativeSelected().Any(),"Opening commander selectable "+faction);await MissionCapture("council-"+faction+"-hud");
   OpenMissionPanel("objectives");await MissionCapture("council-"+faction+"-objectives");CloseRelayPanel();
   SaveMissionSlot(slot);Check(File.Exists(SavePath(slot)),"Expansion manual save "+faction);var saved=mission.Snapshot();ReturnMainMenu();await WarFrames();
   Check(continueSlot==slot&&saveCampaigns[slot]==OriginalCampaign.Label("cw-"+faction),"Continue identifies expansion faction");await MenuCapture("council-"+faction+"-continue");await SessionClick("resume-save");
   Check(JsonNode.DeepEquals(saved,mission.Snapshot())&&IsCouncil,"Continue restores exact expansion state");ResetFirstStrike();Check(nativeWorld.Tick==0&&missionBriefing,"Expansion restart returns to original opening");
   foreach(bool success in new[]{false,true}){
    var snap=SaveCodec.Open(Path.Combine(root,"generated/council-wars-first/checks/cw-"+faction+(success?"-success.json":"-failure.json")))["payload"]!.AsObject();
    InstallMission(nativeData,OriginalMission.Restore(nativeData,snap),false);await WarFrames();
    Check(missionVideo!=null,"Original outcome movie starts "+faction+success);await MissionCapture("council-"+faction+"-movie-"+success);
    await Send(Key.Escape);await ToSignal(GetTree().CreateTimer(.35),SceneTreeTimer.SignalName.Timeout);await MissionCapture("council-"+faction+"-result-"+success);
    Check(rewardsVisible&&resultContinueAvailable==success&&!resultFinal,"Result has no nonexistent next mission or campaign-complete claim");
    if(success){Check(!resultBatchEnd&&focusedUi=="continue","Successful continuation is default");await Send(Key.Enter);Check(missionBriefing&&mission.CampaignIndex==1,"First completion continues to expansion2");ReturnMainMenu();await SessionClick("campaign");}
    else{await Send(Key.Enter);Check(missionBriefing,"Defeat default replays mission");}
   }
   Check(CampaignProgress.Read(CampaignPath,"cw-"+faction)["completed"]!["0"]!=null,"Controlled completion recorded independently");
   slot++;
  }
  Check(!File.Exists(Path.Combine(PersistenceRoot,"campaign-v1.json"))&&!File.Exists(Path.Combine(PersistenceRoot,"human-campaign-v1.json")),"Expansion progress does not write either base campaign");
  godMode=true;await SessionClick("campaign-start:0");Check(nativeWorld.GodMode,"Temporary expansion God access");
  string before=File.ReadAllText(CampaignPath);var controlled=SaveCodec.Open(Path.Combine(root,"generated/council-wars-first/checks/cw-taar-success.json"))["payload"]!.AsObject();controlled["runId"]=Guid.NewGuid().ToString("N");InstallMission(nativeData,OriginalMission.Restore(nativeData,controlled),false);RecordCampaign();Check(File.ReadAllText(CampaignPath)==before,"God completion writes no progress");godMode=false;ReturnMainMenu();
  maximumAspect=0;ApplyAspectLimit();ui=1;await SessionClick("load");await MenuCapture("council-mixed-load");CloseRelayPanel();
  foreach(string faction in new[]{"human","taar"}){await SessionClick("campaign");await SessionClick("edition:base");await SessionClick("faction:"+faction);await SessionClick("campaign-start:0");Check(!IsCouncil&&mission.CampaignId==faction,"Base entry retained "+faction);await Send(Key.Space);await Send(Key.Enter);AdvanceInterference(64);SaveMissionSlot(2);LoadMissionSlot(2);Check(!IsCouncil,"Base save retains base content");ReturnMainMenu();}
  OpenWarSetup();await WarFrames();StartWarMatch(WarConfiguration.Default("A2PLAY01",7));Check(IsWar&&nativeData.ContentId==OriginalContent.Base&&nativeData.Scenario.Types.Length==106,"War excludes expansion definitions");AdvanceInterference(64);SaveMissionSlot(3);LoadMissionSlot(3);Check(IsWar,"War restore after expansion");ReturnMainMenu();
  Check(nativeWorld==null&&nativeTerrain.Count==0&&missionMusic==null,"Replacement clears world and media");
  HumanResult(8,true);RecordCampaign();ReleaseMissionVideo();ContinueCampaign();Check(IsHuman&&!IsCouncil&&mission.CampaignIndex==8,"Human8 to9 routing");HumanResult(9,true);RecordCampaign();ReleaseMissionVideo();ContinueCampaign();Check(IsHuman&&!IsCouncil&&mission.CampaignIndex==9,"Human9 to10 routing");ReturnMainMenu();
  await MenuCapture("consolidated-main-menu");File.WriteAllText(Path.Combine(output,"council-ui-checks.json"),JsonSerializer.Serialize(checks));
 }
}
