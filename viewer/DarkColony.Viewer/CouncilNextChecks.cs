using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyCouncilNext(){
  missionHarnessManual=true;Check(!godMode,"New session God OFF");
  await SessionClick("campaign");await SessionClick("edition:council");await MenuCapture("next-campaign-locked");
  Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:"))==Math.Min(campaignRows,OriginalCampaign.SupportedCount(SelectedCampaign)),"Implemented CW missions are paginated");
  foreach(string faction in new[]{"human","taar"}){
   // Controlled completion of mission1 establishes normal campaign2 entry.
   SelectedCampaign="cw-"+faction;var first=PersistenceData("SCENARIO/"+(faction=="human"?"AEROGEN/AERO":"COUNCIL/COUN")+"01.SCN");
   var complete=SaveCodec.Open(Path.Combine(root,"generated/council-wars-first/checks/cw-"+faction+"-success.json"))["payload"]!.AsObject();InstallMission(first,OriginalMission.Restore(first,complete),false);RecordCampaign();ReleaseMissionVideo();ReturnMainMenu();
   await SessionClick("campaign");await SessionClick("edition:council");await SessionClick("faction:"+faction);
   foreach(int index in new[]{1,2}){
    string name="next-"+faction+"-"+(index+1);Check(campaignEntries[index]!=null,"Earned expansion progression "+name);await SessionClick("campaign-start:"+index);
    Check(mission.CampaignIndex==index&&mission.CampaignId=="cw-"+faction&&missionBriefing,"Menu enters exact new mission "+name);
    await ToSignal(GetTree().CreateTimer(1.2),SceneTreeTimer.SignalName.Timeout);await MissionCapture(name+"-briefing-reveal");await Send(Key.Space);await MissionCapture(name+"-briefing-complete");await Send(Key.Enter);AdvanceInterference(480);
    Check(missionMusic!=null&&missionMusicDiagnostic==null,"New mission music "+name);await Send(Key.Tab);await MissionCapture(name+"-opening");
    OpenMissionPanel("objectives");await MissionCapture(name+"-objectives");CloseRelayPanel();
    int slot=index-1+(faction=="taar"?2:0);SaveMissionSlot(slot);var saved=mission.Snapshot();ReturnMainMenu();await WarFrames();Check(continueSlot==slot&&saveCampaigns[slot]==OriginalCampaign.Label("cw-"+faction),"Continue expansion identity "+name);
    await SessionClick("resume-save");Check(JsonNode.DeepEquals(saved,mission.Snapshot()),"Exact menu save/Continue "+name);ResetFirstStrike();Check(nativeWorld.Tick==0&&missionBriefing,"New mission restart "+name);
    foreach(bool success in new[]{false,true}){
     var snap=SaveCodec.Open(Path.Combine(root,"generated/council-wars-next/checks/cw-"+faction+"-"+(index+1)+(success?"-success.json":"-failure.json")))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,snap),false);await WarFrames();Check(missionVideo!=null,"Original outcome movie "+name+success);await MissionCapture(name+"-movie-"+success);await Send(Key.Escape);await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await MissionCapture(name+"-result-"+success);
     Check(rewardsVisible&&!resultFinal&&resultContinueAvailable==success,"Expansion result availability "+name+success);
     if(success){bool last=index+1==OriginalCampaign.SupportedCount(mission.CampaignId);Check(resultBatchEnd==last&&focusedUi=="continue","Correct next/batch-end default "+name);await Send(Key.Enter);if(!last){Check(missionBriefing&&mission.CampaignIndex==index+1,"Continues to next implemented mission");ReturnMainMenu();await SessionClick("campaign");}else Check(mainMenu&&missionPanel=="campaign"&&!taarCampaignComplete,"Last implemented mission returns without completion claim");}
     else {await Send(Key.Enter);Check(missionBriefing&&mission.CampaignIndex==index,"Defeat replays exact expansion mission");}
    }
   }
  }
  Check(!File.Exists(Path.Combine(PersistenceRoot,"campaign-v1.json"))&&!File.Exists(Path.Combine(PersistenceRoot,"human-campaign-v1.json")),"New CW results do not write base progress");
  godMode=true;await SessionClick("campaign-start:2");string prior=File.ReadAllText(CampaignPath);var god=SaveCodec.Open(Path.Combine(root,"generated/council-wars-next/checks/cw-taar-3-success.json"))["payload"]!.AsObject();god["runId"]=Guid.NewGuid().ToString("N");InstallMission(nativeData,OriginalMission.Restore(nativeData,god),false);Check(nativeWorld.GodMode,"Temporary God access includes third mission");RecordCampaign();Check(prior==File.ReadAllText(CampaignPath),"God completion does not persist");godMode=false;ReturnMainMenu();
  foreach(int aspect in new[]{0,16})foreach(float scale in new[]{GetViewportRect().Size.Y/1080,1.5f}){
   maximumAspect=aspect;ApplyAspectLimit();ui=scale;await SessionClick("campaign");await SessionClick("edition:council");await MenuCapture($"next-campaign-{aspect}-{scale:0.##}");RelayLayout("New CW campaign");await SessionClick("campaign-start:2");await Send(Key.Space);await MissionCapture($"next-long-briefing-{aspect}-{scale:0.##}");RelayLayout("New CW briefing");await Send(Key.Enter);AdvanceInterference(64);await MissionCapture($"next-hud-{aspect}-{scale:0.##}");ReturnMainMenu();
  }
  maximumAspect=0;ApplyAspectLimit();ui=GetViewportRect().Size.Y/1080;
  // Visible isolated droid animation fixture: original SCID poses, authoritative
  // command/animation clock; visibility supplied by a nearby player scout.
  SelectedCampaign="cw-taar";await SessionClick("campaign");await SessionClick("campaign-start:2");await Send(Key.Space);await Send(Key.Enter);AdvanceInterference(32);var robot=nativeWorld.Entities.First(e=>e.Type==109);nativeWorld.Spawn(8,0,new(robot.Cell.X+2,robot.Cell.Z),"controlled-render-scout");nativeWorld.RefreshSight();camera=NativePosition(robot);zoom=1.5f;ClampMissionCamera();
  for(int i=0;i<24;i++){AdvanceInterference(2);await MissionCapture($"droid-motion-{i:00}");}
  ReturnMainMenu();await WarFrames();DrainRetiredMissionAudio();Check(nativeWorld==null&&nativeTerrain.Count==0&&nativePoseFrames.Count==0&&missionMusic==null,"New mission replacement releases simulation/render/audio resources");
  File.WriteAllText(Path.Combine(output,"council-next-ui-checks.json"),JsonSerializer.Serialize(checks));GD.Print("COUNCIL_NEXT_UI_PASS");
 }
}
