using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyTrainingFinal(){
  var receipts=new List<object>();missionHarnessManual=true;
  Engine.MaxFps=60;DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);
  foreach(string faction in new[]{"human","taar"}){
   var data=PersistenceData($"SCENARIO/TEST/{(faction=="human"?"H":"A")}TRAIN5.SCN");
   var saved=SaveCodec.Open(Path.Combine(root,$"generated/training-pause/checks/ordinary-{faction}5-stage3.json"))["payload"]!.AsObject();
   foreach(bool fast in new[]{false,true}){
    InstallMission(data,OriginalMission.Restore(data,saved),false);fastGame=fast;maximumAspect=16;ApplyAspectLimit();ui=1.5f;
    camera=NativePosition(nativeWorld.Entities.First(e=>e.Owner==0&&e.Type==(faction=="human"?3:11)));ClampMissionCamera();missionPointer=new(-100,-100);
    missionHarnessManual=false;await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);
    await MissionCapture($"developed-{faction}-{fast}-start");perfFrames.Clear();perfLastSample=0;perfStage="training";perfActive=true;ulong start=Time.GetTicksMsec();long tick=nativeWorld.Tick;
    await ToSignal(GetTree().CreateTimer(12),SceneTreeTimer.SignalName.Timeout);perfActive=false;missionHarnessManual=true;
    var frames=perfFrames.Select(f=>f.FrameMs).Order().ToArray();double P(double p)=>frames[(int)((frames.Length-1)*p)];
    receipts.Add(new{faction,fast,tick,final=nativeWorld.Tick,seconds=(Time.GetTicksMsec()-start)/1000d,frames=frames.Length,median=P(.5),p95=P(.95),p99=P(.99),worst=frames[^1],over33=frames.Count(f=>f>33.3),allocated=perfFrames[^1].Allocated-perfFrames[0].Allocated,gen0=perfFrames[^1].Gen0-perfFrames[0].Gen0,artBytes=nativeSharedBytes,units=nativeWorld.Entities.Count()});
    await MissionCapture($"developed-{faction}-{fast}-end");OpenMissionPanel("menu");await MissionCapture($"developed-{faction}-paused");ReturnMainMenu();
    Check(nativeSharedArt.Count==0&&missionMusic==null,"Training developed replacement releases artwork/music");
   }
   var finalData=PersistenceData($"SCENARIO/TEST/{(faction=="human"?"H":"A")}TRAIN7.SCN");var won=SaveCodec.Open(Path.Combine(root,$"generated/training-pause/checks/{faction}7-success.json"))["payload"]!.AsObject();InstallMission(finalData,OriginalMission.Restore(finalData,won),false);
   await ToSignal(GetTree().CreateTimer(3),SceneTreeTimer.SignalName.Timeout);await MissionCapture(faction+"-movie-visible-3s");await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);await MissionCapture(faction+"-movie-visible-5s");await Send(Key.Escape);Check(rewardsVisible&&!mainMenu,"Movie skip consumes input");await MissionCapture(faction+"-final-training-result");ReturnMainMenu();
  }
  Check(IndexedMovieFrames.ActiveWorkers==0,"Skipped training movies release workers");
  var direct=PersistenceData("SCENARIO/TEST/HTRAIN1.SCN");InstallMission(direct,new OriginalMission(direct,7),false);missionHarnessManual=false;
  foreach(var key in new[]{Key.Escape,Key.O,Key.J}){
   await Send(key);Check(SinglePlayerPaused&&missionPanel.Length>0,"Direct shortcut owns pause "+key);var state=mission.Snapshot();
   await ToSignal(GetTree().CreateTimer(.6),SceneTreeTimer.SignalName.Timeout);Check(JsonNode.DeepEquals(state,mission.Snapshot()),"Direct panel freezes state "+key);CloseRelayPanel();
  }
  OpenMissionPanel("menu");ActivateMissionUi("restart");Check(missionPanel=="abandon"&&SinglePlayerPaused,"Restart confirmation stays paused");await Send(Key.Escape);Check(missionPanel=="menu"&&SinglePlayerPaused,"Cancel returns to paused parent");CloseRelayPanel();ReturnMainMenu();

  File.WriteAllText(Path.Combine(output,"training-performance.json"),JsonSerializer.Serialize(new{viewport=GetViewportRect().Size.ToString(),ui,maximumAspect,artwork=nativeSampling,cap=60,kind="Optimized source; ordinary saved artillery states; 12s/speed/faction; not a soak or external frame-present measurement",receipts},new JsonSerializerOptions{WriteIndented=true}));
  GD.Print("TRAINING_FINAL_PASS");
 }
}
