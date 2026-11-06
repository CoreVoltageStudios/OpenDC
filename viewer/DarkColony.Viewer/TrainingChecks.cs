using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyTrainingInterface(){
  missionHarnessManual=true;var receipts=new List<object>();
  await SessionClick("campaign");await SessionClick("edition:training");await MenuCapture("training-selection-locked");
  foreach(string faction in new[]{"human","taar"}){
   await SessionClick("faction:"+faction);Check(campaignEntries.Skip(1).All(p=>p==null),"Fresh training progress isolated "+faction);
   for(int lesson=1;lesson<=7;lesson++){
    campaignScroll=Math.Max(0,lesson-campaignRows);await WarFrames();await SessionClick("campaign-start:"+(lesson-1));
    Check(mission.CampaignId=="training-"+faction&&mission.CampaignIndex==lesson-1&&missionBriefing&&!nativeWorld.GodMode,"Normal training entry "+faction+lesson);
    Check(!missionSoundPaths.ContainsKey(1000),"Training never plays campaign briefing voice");
    if(lesson is 1 or 7){await ToSignal(GetTree().CreateTimer(.8),SceneTreeTimer.SignalName.Timeout);await MissionCapture($"{faction}{lesson}-briefing-reveal");}
    await Send(Key.Space);Check(missionBriefing&&!BriefingRevealing,"Reveal does not launch lesson");
    if(lesson is 1 or 7)await MissionCapture($"{faction}{lesson}-briefing-complete");await Send(Key.Enter);AdvanceInterference(320);
    OpenMissionPanel("objectives");await WarFrames();if(lesson==1)await MissionCapture(faction+"-instructions");await SessionClick("history");await WarFrames();if(lesson==1)await MissionCapture(faction+"-instruction-history");CloseRelayPanel();
    SaveMissionSlot(faction=="human"?0:1);var saved=mission.Snapshot();ReturnMainMenu();await SessionClick("resume-save");Check(JsonNode.DeepEquals(saved,mission.Snapshot()),"Training Continue exact "+faction+lesson);
    ResetFirstStrike();Check(missionBriefing&&nativeWorld.Tick==0,"Training retry resets instructions");await Send(Key.Space);await Send(Key.Enter);
    // Controlled original timeout, distinct from ordinary-play results.
    var data=nativeData;var failed=SaveCodec.Open(Path.Combine(root,$"generated/training-pause/checks/{faction}{lesson}-timeout.json"))["payload"]!.AsObject();InstallMission(data,OriginalMission.Restore(data,failed),false);await WarFrames();Check(rewardsVisible&&missionVideo==null&&!resultContinueAvailable,"Absent PP skips to original failure text");
    if(lesson==1)await MissionCapture(faction+"-training-defeat");await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);Check(missionBriefing&&mission.CampaignIndex==lesson-1,"Failure defaults to retry");
    var won=SaveCodec.Open(Path.Combine(root,$"generated/training-pause/checks/{faction}{lesson}-success.json"))["payload"]!.AsObject();InstallMission(data,OriginalMission.Restore(data,won),false);await WarFrames();
    if(lesson<7){Check(rewardsVisible&&missionVideo==null,"Intermediate PP skip "+lesson);if(lesson==1)await MissionCapture(faction+"-training-success");}
    else {
     Check(missionVideo is {Streaming:true},"Required final training movie streams");var movie=missionVideo!;ulong started=Time.GetTicksMsec();long peak=movie.RetainedVideoBytes;
     await MissionCapture(faction+"-training-movie");while(missionVideo!=null&&Time.GetTicksMsec()-started<120000){peak=Math.Max(peak,movie.RetainedVideoBytes);await WarFrames(1);}
     Check(rewardsVisible&&missionVideo==null&&IndexedMovieFrames.ActiveWorkers==0,"Natural final movie EOF and release");receipts.Add(new{faction,movie.Duration,peak,starvations=movie.BufferStarvations});await MissionCapture(faction+"-training-complete");
    }
    await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);
    if(lesson<7){Check(missionBriefing&&mission.CampaignIndex==lesson,"Continue advances exactly one lesson");ReturnMainMenu();OpenMissionPanel("campaign");}
    else Check(mainMenu&&missionPanel=="campaign"&&taarCampaignComplete&&studioIntro==null,"Final returns to training selection, no eighth lesson");
   }
  }
  // No training completion writes any campaign track.
  foreach(string file in new[]{"campaign-v1.json","human-campaign-v1.json","council-human-campaign-v1.json","council-taar-campaign-v1.json"})Check(!File.Exists(Path.Combine(PersistenceRoot,file)),"Training preserves "+file);
  foreach(int aspect in GetViewportRect().Size.X>2500?new[]{0,16}:new[]{0})foreach(float scale in new[]{GetViewportRect().Size.Y/1080,1.5f}){
   maximumAspect=aspect;ApplyAspectLimit();ui=scale;SelectedCampaign="training-taar";RefreshPersistenceCatalog();OpenMissionPanel("campaign");campaignScroll=0;await MenuCapture($"training-menu-{aspect}-{scale:0.##}");await SessionClick("campaign-start:3");await Send(Key.Space);await MissionCapture($"training-briefing-{aspect}-{scale:0.##}");await Send(Key.Enter);AdvanceInterference(64);
   nativeSelection.Clear();var worker=nativeWorld.Entities.First(e=>e.Type==14&&e.Owner==0);nativeSelection.Add((worker.Slot,worker.Incarnation));await Send(Key.W);Check(targetMode=="waypoints","W opens player waypoint mode");AddPlayerWaypoint(new(worker.Cell.X+2,worker.Cell.Z));await MissionCapture($"training-waypoints-{aspect}-{scale:0.##}");AddPlayerWaypoint(new(worker.Cell.X+2,worker.Cell.Z));Check(worker.PendingOrder?.Order==OriginalOrder.Patrol,"Final repeated point dispatches authoritative patrol");
   OpenMissionPanel("menu");await MissionCapture($"training-paused-{aspect}-{scale:0.##}");OpenMissionPanel("options");await MissionCapture($"training-options-{aspect}-{scale:0.##}");ReturnMainMenu();
  }
  // God mode remains session-only and cannot create a new lesson completion.
  SelectedCampaign="training-human";var original=File.ReadAllText(CampaignPath);var progress=SaveCodec.Open(CampaignPath)["payload"]!.AsObject();progress["completed"]!.AsObject().Remove("6");SaveCodec.Write(CampaignPath,progress);string before=File.ReadAllText(CampaignPath);godMode=true;
  var final=PersistenceData("SCENARIO/TEST/HTRAIN7.SCN");var outcome=SaveCodec.Open(Path.Combine(root,"generated/training-pause/checks/human7-success.json"))["payload"]!.AsObject();outcome["runId"]=Guid.NewGuid().ToString("N");InstallMission(final,OriginalMission.Restore(final,outcome),false);await WarFrames();await Send(Key.Escape);Check(rewardsVisible&&!mainMenu,"Movie skip cannot activate results");RecordCampaign();Check(File.ReadAllText(CampaignPath)==before,"God completion leaves training progress unchanged");SaveCodec.AtomicWrite(CampaignPath,original);godMode=false;ReturnMainMenu();
  Check(IndexedMovieFrames.ActiveWorkers==0&&nativeSharedArt.Count==0&&missionMusic==null,"Training replacement releases resources");File.WriteAllText(Path.Combine(output,"training-ui.json"),JsonSerializer.Serialize(receipts));GD.Print("TRAINING_UI_PASS");
 }
}
