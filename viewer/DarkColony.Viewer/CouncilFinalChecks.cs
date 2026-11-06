using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyCouncilFinal(){
  missionHarnessManual=true;var movies=new List<object>();
  if(missionSuite=="council-final-reset"){
   foreach(string faction in new[]{"human","taar"}){SelectedCampaign="cw-"+faction;RefreshPersistenceCatalog();Check(taarCampaignComplete&&campaignEntries[7]!=null,"Final completion retained across process "+faction);}
   Check(!godMode&&studioIntro==null,"Session God resets; startup not replayed by return flow");GD.Print("COUNCIL_FINAL_RESET_PASS");return;
  }
  if(missionSuite=="council-final-god"){await VerifyFinalGod();GD.Print("COUNCIL_FINAL_GOD_PASS");return;}
  Check(!godMode,"Fresh God OFF");await SessionClick("campaign");await SessionClick("edition:council");await MenuCapture("final-campaign-locked");
  foreach(string faction in new[]{"human","taar"}){
   string id="cw-"+faction;SelectedCampaign=id;
   // Labelled previous-result fixture; actual campaign click and continuation follow.
   var previous=PersistenceData("SCENARIO/"+(faction=="human"?"AEROGEN/AERO":"COUNCIL/COUN")+"07.SCN");
   InstallMission(previous,OriginalMission.Restore(previous,SaveCodec.Open(Path.Combine(root,"generated/council-wars-middle/checks/"+id+"-7-success.json"))["payload"]!.AsObject()),false);RecordCampaign();ReleaseMissionVideo();ReturnMainMenu();await SessionClick("campaign");await SessionClick("edition:council");await SessionClick("faction:"+faction);campaignScroll=7;await MenuCapture(id+"-8-menu");await SessionClick("campaign-start:7");
   Check(mission.CampaignId==id&&mission.CampaignIndex==7&&missionBriefing,"Normal menu enters final "+id);await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);await MissionCapture(id+"-8-briefing-reveal");await Send(Key.Space);await MissionCapture(id+"-8-briefing-complete");await Send(Key.Enter);AdvanceInterference(480);await Send(Key.Tab);await MissionCapture(id+"-8-opening");OpenMissionPanel("objectives");await MissionCapture(id+"-8-objectives");CloseRelayPanel();
   SaveMissionSlot(faction=="human"?0:1);var saved=mission.Snapshot();ReturnMainMenu();await WarFrames();await SessionClick("resume-save");Check(JsonNode.DeepEquals(saved,mission.Snapshot()),"Exact final Continue "+id);ResetFirstStrike();Check(nativeWorld.Tick==0&&missionBriefing,"Final restart "+id);
   foreach(bool success in new[]{false,true}){
    var state=SaveCodec.Open(Path.Combine(root,"generated/council-wars-final/checks/"+id+"-8-"+(success?"success":"failure")+".json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,state),false);await WarFrames();Check(missionVideo!=null,"Original final outcome starts "+id+success);
    if(success){var movie=missionVideo!;Check(movie.Streaming,"Final movie uses bounded streaming "+id);long peak=movie.RetainedVideoBytes;int starvations=0;ulong began=Time.GetTicksMsec();bool middle=false;
     await MissionCapture(id+"-ending-start");while(missionVideo!=null&&Time.GetTicksMsec()-began<120000){peak=Math.Max(peak,movie.RetainedVideoBytes);starvations=Math.Max(starvations,movie.BufferStarvations);if(!middle&&movie.PositionSeconds>movie.Duration/2){middle=true;await MissionCapture(id+"-ending-middle");}await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
     Check(missionVideo==null&&rewardsVisible,"Natural ending EOF "+id);movies.Add(new{id,duration=movie.Duration,seconds=(Time.GetTicksMsec()-began)/1000d,peakVideoBytes=peak,starvations,workers=IndexedMovieFrames.ActiveWorkers});Check(IndexedMovieFrames.ActiveWorkers==0,"Ending worker disposed "+id);
    }else {await MissionCapture(id+"-defeat-movie");await Send(Key.Escape);}
    await ToSignal(GetTree().CreateTimer(.35),SceneTreeTimer.SignalName.Timeout);await MissionCapture(id+"-result-"+success);Check(resultFinal==success&&resultContinueAvailable==success&&!resultBatchEnd,"Final result buttons "+id+success);
    await Send(Key.Enter);if(success){Check(mainMenu&&studioIntro==null&&taarCampaignComplete,"Finish returns without mission9 or intro "+id);}else Check(missionBriefing&&mission.CampaignIndex==7,"Defeat Replay "+id);
   }
  }
  // God completion cannot write progress, including final campaign completion.
  await VerifyFinalGod();
  foreach(int aspect in new[]{0,16})foreach(float scale in new[]{GetViewportRect().Size.Y/1080,1.5f}){
   maximumAspect=aspect;ApplyAspectLimit();ui=scale;await SessionClick("campaign");await SessionClick("edition:council");campaignScroll=7;await MenuCapture($"final-menu-{aspect}-{scale:0.##}");await SessionClick("campaign-start:7");await Send(Key.Space);await MissionCapture($"final-briefing-{aspect}-{scale:0.##}");RelayLayout("Final briefing");await Send(Key.Enter);AdvanceInterference(32);await MissionCapture($"final-hud-{aspect}-{scale:0.##}");ReturnMainMenu();
  }
  // Original new-unit artwork/action frames; controlled visible scout only.
  var frames=new List<object>();foreach(var spec in new[]{("cw-human",107), ("cw-human",112),("cw-human",108),("cw-taar",106),("cw-taar",117)}){
   SelectedCampaign=spec.Item1;var data=PersistenceData("SCENARIO/"+(spec.Item1=="cw-human"?"AEROGEN/AERO":"COUNCIL/COUN")+"08.SCN");InstallMission(data,new OriginalMission(data,7),false);missionBriefing=false;AdvanceInterference(32);var e=nativeWorld.Entities.FirstOrDefault(e=>e.Living&&e.Type==spec.Item2)??nativeWorld.Spawn(spec.Item2,0,new(35,35),"controlled-art-review");nativeWorld.Spawn(IsHuman?0:8,0,new(e.Cell.X+2,e.Cell.Z),"controlled-art-scout");nativeWorld.RefreshSight();camera=NativePosition(e);zoom=1.5f;ClampMissionCamera();if(spec.Item2 is 106 or 107)nativeWorld.Command(new(e.Slot,e.Incarnation,OriginalOrder.MoveOnly,e.X+8*256,e.Z));
   for(int i=0;i<12;i++){AdvanceInterference(2);await MissionCapture($"unit-{spec.Item2}-{i:00}");frames.Add(new{e.Type,i,nativeWorld.Tick,e.X,e.Z,e.Height,e.Body.Frame,e.Body.Mode});}
   ReturnMainMenu();
  }
  await WarFrames();DrainRetiredMissionAudio();Check(nativeWorld==null&&nativeTerrain.Count==0&&nativeSharedArt.Count==0&&missionMusic==null&&IndexedMovieFrames.ActiveWorkers==0,"Finale replacement releases mission/art/audio/movie workers");
  Check(!File.Exists(Path.Combine(PersistenceRoot,"campaign-v1.json"))&&!File.Exists(Path.Combine(PersistenceRoot,"human-campaign-v1.json")),"Finals do not write base progress");File.WriteAllText(Path.Combine(output,"final-ui.json"),JsonSerializer.Serialize(new{checks,movies,frames},new JsonSerializerOptions{WriteIndented=true}));GD.Print("COUNCIL_FINAL_UI_PASS");
 }
 async Task VerifyFinalGod(){
  SelectedCampaign="cw-taar";await SessionClick("campaign");await SessionClick("edition:council");godMode=true;campaignScroll=7;await WarFrames();await SessionClick("campaign-start:7");
  string? retained=File.Exists(CampaignPath)?File.ReadAllText(CampaignPath):null;
  // Controlled isolated-profile fixture removes only its final completion. An
  // already-completed record would make even a broken God guard look harmless.
  if(retained!=null){var payload=SaveCodec.Open(CampaignPath)["payload"]!.AsObject();payload["completed"]!.AsObject().Remove("7");SaveCodec.Write(CampaignPath,payload);}
  string? before=File.Exists(CampaignPath)?File.ReadAllText(CampaignPath):null;
  var state=SaveCodec.Open(Path.Combine(root,"generated/council-wars-final/checks/cw-taar-8-success.json"))["payload"]!.AsObject();state["runId"]=Guid.NewGuid().ToString("N");InstallMission(nativeData,OriginalMission.Restore(nativeData,state),false);await WarFrames();Check(nativeWorld.GodMode&&missionVideo!=null,"God finale movie starts");await Send(Key.Escape);Check(rewardsVisible&&!mainMenu,"Ending skip cannot activate result");RecordCampaign();Check((File.Exists(CampaignPath)?File.ReadAllText(CampaignPath):null)==before,"God cannot record previously uncompleted finale");
  if(retained!=null)SaveCodec.AtomicWrite(CampaignPath,retained);godMode=false;ReturnMainMenu();
 }

}
