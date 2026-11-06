using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyCouncilMiddle(){
  missionHarnessManual=true;Check(!godMode,"Fresh session God OFF");
  await SessionClick("campaign");await SessionClick("edition:council");await MenuCapture("middle-campaign-locked");
  Check(OriginalCampaign.SupportedCount("cw-human")>=7&&OriginalCampaign.SupportedCount("cw-taar")>=7,"Earlier seven missions remain implemented");
  foreach(string faction in new[]{"human","taar"}){
   // Labelled prior-result fixture unlocks4; the subsequent4->7 routing is normal.
   SelectedCampaign="cw-"+faction;var prior=PersistenceData("SCENARIO/"+(faction=="human"?"AEROGEN/AERO":"COUNCIL/COUN")+"03.SCN");
   var complete=SaveCodec.Open(Path.Combine(root,"generated/council-wars-next/checks/cw-"+faction+"-3-success.json"))["payload"]!.AsObject();InstallMission(prior,OriginalMission.Restore(prior,complete),false);RecordCampaign();ReleaseMissionVideo();ReturnMainMenu();
   await SessionClick("campaign");await SessionClick("edition:council");await SessionClick("faction:"+faction);
   foreach(int index in new[]{3,4,5,6}){
    string name="middle-"+faction+"-"+(index+1);campaignScroll=index;await MenuCapture(name+"-entry");Check(campaignEntries[index]!=null,"Earned expansion entry "+name);await SessionClick("campaign-start:"+index);
    Check(mission.CampaignIndex==index&&mission.CampaignId=="cw-"+faction&&missionBriefing,"Menu enters exact mission "+name);
    await ToSignal(GetTree().CreateTimer(.8),SceneTreeTimer.SignalName.Timeout);await MissionCapture(name+"-briefing-reveal");await Send(Key.Space);await MissionCapture(name+"-briefing-complete");await Send(Key.Enter);AdvanceInterference(480);
    Check(missionMusic!=null&&missionMusicDiagnostic==null,"Music initialized "+name);await Send(Key.Tab);await MissionCapture(name+"-opening");OpenMissionPanel("objectives");await MissionCapture(name+"-objectives");CloseRelayPanel();
    int slot=index-3;SaveMissionSlot(slot);var saved=mission.Snapshot();ReturnMainMenu();await WarFrames();Check(continueSlot==slot&&saveCampaigns[slot]==OriginalCampaign.Label("cw-"+faction),"Continue identity "+name);await SessionClick("resume-save");Check(JsonNode.DeepEquals(saved,mission.Snapshot()),"Exact menu save/Continue "+name);ResetFirstStrike();Check(nativeWorld.Tick==0&&missionBriefing,"Restart "+name);
    foreach(string result in faction=="taar"&&index>=4?new[]{"failure","council-failure","success"}:new[]{"failure","success"}){
     bool success=result=="success";var snap=SaveCodec.Open(Path.Combine(root,"generated/council-wars-middle/checks/cw-"+faction+"-"+(index+1)+"-"+result+".json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,snap),false);await WarFrames();Check(missionVideo!=null,"Original outcome playback "+name+result);await MissionCapture(name+"-movie-"+result);await Send(Key.Escape);await ToSignal(GetTree().CreateTimer(.35),SceneTreeTimer.SignalName.Timeout);await MissionCapture(name+"-result-"+result);
     Check(rewardsVisible&&!resultFinal&&resultContinueAvailable==success,"Result availability "+name+result);if(result=="council-failure")Check(outcomeText==ReadMissionText(mission.Outcome!.TextPath)&&mission.Outcome.TextPath.EndsWith(".003"),"Original Council-loss text rendered");
     if(success){Check(resultBatchEnd==(index+1==OriginalCampaign.SupportedCount(mission.CampaignId))&&focusedUi=="continue","Continue default and batch boundary "+name);await Send(Key.Enter);if(index+1<OriginalCampaign.SupportedCount("cw-"+faction)){Check(missionBriefing&&mission.CampaignIndex==index+1,"Normal next expansion mission");ReturnMainMenu();await SessionClick("campaign");}else Check(mainMenu&&missionPanel=="campaign"&&!taarCampaignComplete,"Seventh returns without launching final or claiming completion");}
     else {await Send(Key.Enter);Check(missionBriefing&&mission.CampaignIndex==index,"Defeat replay exact mission");}
    }
   }
  }
  Check(!File.Exists(Path.Combine(PersistenceRoot,"campaign-v1.json"))&&!File.Exists(Path.Combine(PersistenceRoot,"human-campaign-v1.json")),"Expansion progress does not write base campaigns");
  godMode=true;campaignScroll=6;await WarFrames();await SessionClick("campaign-start:6");string before=File.ReadAllText(CampaignPath);var god=SaveCodec.Open(Path.Combine(root,"generated/council-wars-middle/checks/cw-taar-7-success.json"))["payload"]!.AsObject();god["runId"]=Guid.NewGuid().ToString("N");InstallMission(nativeData,OriginalMission.Restore(nativeData,god),false);Check(nativeWorld.GodMode,"Temporary God access mission7");RecordCampaign();Check(before==File.ReadAllText(CampaignPath),"God completion does not persist");godMode=false;ReturnMainMenu();
  foreach(int aspect in new[]{0,16})foreach(float scale in new[]{GetViewportRect().Size.Y/1080,1.5f}){
   maximumAspect=aspect;ApplyAspectLimit();ui=scale;await SessionClick("campaign");await SessionClick("edition:council");campaignScroll=6;await MenuCapture($"middle-campaign-{aspect}-{scale:0.##}");RelayLayout("Expanded campaign");await SessionClick("campaign-start:6");await Send(Key.Space);await MissionCapture($"middle-long-briefing-{aspect}-{scale:0.##}");RelayLayout("Middle briefing");await Send(Key.Enter);AdvanceInterference(64);await MissionCapture($"middle-hud-{aspect}-{scale:0.##}");ReturnMainMenu();
  }
  maximumAspect=0;ApplyAspectLimit();ui=GetViewportRect().Size.Y/1080;
  // Original-art temporal fixture; isolated visible snake/prototype commands.
  SelectedCampaign="cw-taar";await SessionClick("campaign");campaignScroll=3;await WarFrames();await SessionClick("campaign-start:3");await Send(Key.Space);await Send(Key.Enter);AdvanceInterference(32);
  var snake=nativeWorld.Entities.First(e=>e.Type==110);var prototype=nativeWorld.Entities.First(e=>e.Type==113);var frames=new List<object>();
  foreach(var e in new[]{snake,prototype}){
   nativeWorld.Spawn(8,0,new(e.Cell.X+2,e.Cell.Z),"controlled-render-scout");nativeWorld.RefreshSight();camera=NativePosition(e);zoom=1.5f;ClampMissionCamera();
   for(int i=0;i<16;i++){AdvanceInterference(2);await MissionCapture($"unit-{e.Type}-motion-{i:00}");frames.Add(new{e.Type,i,nativeWorld.Tick,e.X,e.Z,e.Height,e.Body.Frame,e.Body.Mode});}
  }
  File.WriteAllText(Path.Combine(output,"unit-frames.json"),JsonSerializer.Serialize(frames));ReturnMainMenu();await SessionClick("campaign");await SessionClick("faction:human");campaignScroll=3;await WarFrames();await SessionClick("campaign-start:3");await Send(Key.Space);await Send(Key.Enter);AdvanceInterference(32);var horn=nativeWorld.Entities.First(e=>e.Type==111);nativeWorld.Spawn(0,0,new(horn.Cell.X+2,horn.Cell.Z),"controlled-horn-scout");nativeWorld.RefreshSight();camera=NativePosition(horn);zoom=1.5f;ClampMissionCamera();await MissionCapture("unit-111-scenery");
  ReturnMainMenu();await WarFrames();DrainRetiredMissionAudio();Check(nativeWorld==null&&nativeTerrain.Count==0&&nativePoseFrames.Count==0&&missionMusic==null,"Replacement releases world/render/audio");
  File.WriteAllText(Path.Combine(output,"council-middle-ui-checks.json"),JsonSerializer.Serialize(checks));GD.Print("COUNCIL_MIDDLE_UI_PASS");
 }
}
