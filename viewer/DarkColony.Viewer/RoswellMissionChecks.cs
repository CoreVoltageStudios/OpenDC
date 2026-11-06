using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 DarkColony.Diagnostics.RoswellRoute? roswellRoute;
 async Task VerifyRoswellMission(){
  missionHarnessManual=true;AdvanceInterference(250);await Send(Key.Tab);await MissionCapture("01-opening");
  Check(!economicMission&&nativeData.Scenario.Header.PaletteName=="HTRAIN.GIF","A05 cityless interface and own terrain palette");
  Check(nativeWorld!.VisionBits[0]==225,"Original opening enables three direct vision links");
  await Send(Key.M);await ClickNativeCell(MouseButton.Left,new(12,8));
  missionHarnessManual=false;await ToSignal(GetTree().CreateTimer(4),SceneTreeTimer.SignalName.Timeout);missionHarnessManual=true;
  await MissionCapture("02-moving");
  await Send(Key.J);await MissionCapture("03-objectives");await Send(Key.Escape);
  long before=nativeWorld!.Events.Count(e=>e.Kind=="order");await NativeMouse(MouseButton.Left,MiniMapRect.GetCenter());Check(nativeWorld!.Events.Count(e=>e.Kind=="order")==before,"Minimap consumes input without world orders");await Send(Key.Tab);
  await Send(Key.O);await UiClick("art-crisp");await UiClick("speed-classic");await MissionCapture("04-options");await Send(Key.Escape);
  ui=GetViewportRect().Size.X>2000?1.5f:1.25f;await MissionCapture("05-scaled-commander");
  await Send(Key.Escape);await UiClick("save");await UiClick("save-slot:0");if(missionPanel=="overwrite")await UiClick("overwrite-yes");long savedTick=nativeWorld!.Tick;await MissionCapture("06-save");await UiClick("close");await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");
  Check(mainMenu&&mission==null&&nativeWorld==null,"Leaving A05 releases its active session");await UiClick("resume-save");Check(nativeWorld!.Tick==savedTick&&!economicMission&&nativeData.Scenario.Header.PaletteName=="HTRAIN.GIF","Continue preserves cityless state and terrain");Check(!fastGame&&nativeSampling==0&&!enhancedVideo,"Loading preserves current global preferences");await MissionCapture("07-restored");
  await Send(Key.R);Check(missionBriefing&&nativeWorld!.Tick==0&&nativeWorld!.VisionBits[0]==1&&mission!.Blocks[17]!.Remaining==1,"Restart resets sight links and mission events");await MissionCapture("08-earth-briefing");
  // Controlled A04 outcome fixture verifies the actual Continue transition only.
  var a04=PersistenceData("SCENARIO/ALIEN/ALIEN04.SCN");InstallMission(a04,new OriginalMission(a04,0),false);nativeWorld!.Teams[1].Ai=0;foreach(var c in nativeWorld!.Teams[1].Cities.Take(5))if(c.Entity!=null)nativeWorld!.Retire(c.Entity);foreach(var e in nativeWorld!.Entities.Where(e=>e.Owner==2).ToArray())nativeWorld!.Retire(e);AdvanceInterference(100);mission!.SetClock(mission!.Outcome!.Deadline+1);await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);await UiClick("continue");
  Check(missionBriefing&&mission!.CampaignIndex==4&&!economicMission&&nativeData.Scenario.Header.PaletteName=="HTRAIN.GIF"&&nativeWorld!.Tick==0,"A04 outcome Continue enters fresh A05 with its palette");await MissionCapture("09-campaign-entry");
  await Send(Key.Escape);await UiClick("campaign");await MenuCapture("10-campaign-five-missions");Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:"))==OriginalCampaign.SupportedTaarMissions&&missionHits.Any(h=>h.Id=="campaign-start:4"&&h.Enabled),"Implemented catalog; A05 unlocked");await UiClick("campaign-start:4");await Send(Key.Enter);AdvanceInterference(250);await Send(Key.Tab);await MissionCapture("11-campaign-opening");
 }
 async Task RunRoswellMission(){
  fastGame=true;roswellRoute=new();long sequenceTick=-1;var sequence=new List<object>();var recording=new AudioEffectRecord{Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,recording);recording.SetRecordingActive(true);long captured=-1;int goal=-1;ulong started=Time.GetTicksMsec();
  while(!mission!.AtOutcome&&nativeWorld!.Tick<14000){
   await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(missionFault!=null)throw new InvalidOperationException(missionFault);
   var leader=nativeWorld!.Entities.FirstOrDefault(e=>e.Living&&e.Owner==0&&nativeWorld!.IsCommander(e));
   if(leader!=null){camera=NativePosition(leader);ClampMissionCamera();}
   if(nativeWorld!.Tick!=sequenceTick&&(nativeWorld!.Tick is >=1800 and <=1825 or >=3610 and <=3650 or >=3678 and <=3710)){
    sequenceTick=nativeWorld!.Tick;animationCaptureActive=true;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var frame=GetViewport().GetTexture().GetImage();string name=$"sequence-{sequence.Count:D4}.png";frame.SavePng(Path.Combine(output,name));sequence.Add(new{name,nativeWorld!.Tick,wall=Time.GetTicksMsec()-started,poses=nativeWorld!.Entities.Where(e=>nativeWorld!.IsCommander(e)||e.Type is 92 or 93).Select(e=>new{e.Identity,e.X,e.Z,e.Height,e.State,e.Body.Frame,e.Body.Delay,e.Body.Mode})});animationCaptureActive=false;
   }
   if(nativeWorld!.Tick/1000!=captured||roswellRoute.Goal!=goal){captured=nativeWorld!.Tick/1000;goal=roswellRoute.Goal;await MissionCapture($"run-{nativeWorld!.Tick:D5}-goal{goal}");}
  }
  var route=roswellRoute;roswellRoute=null;File.WriteAllText(Path.Combine(output,"sequence.json"),JsonSerializer.Serialize(sequence,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"final-state.json"),mission!.Snapshot().ToJsonString());
  File.WriteAllText(Path.Combine(output,"end-to-end.json"),JsonSerializer.Serialize(new{mode="Rendered normal-clock Fast150%; ordinary authoritative commands; no state injection",elapsedMs=Time.GetTicksMsec()-started,nativeWorld!.Tick,mission!.Outcome,route.Actions,events=nativeWorld!.Events,missionEvents=mission!.Events.Where(e=>e.Kind!="normal-scan"),audio=missionAudioAudit},new JsonSerializerOptions{WriteIndented=true}));
  Check(mission!.AtOutcome&&mission!.Outcome!.Success,"A05 complete ordinary-command rendered victory");
  Check(nativeWorld!.Events.Count(e=>e.Kind=="extracted")==2,"Both commanders actually picked up before outcome");
  await ToSignal(GetTree().CreateTimer(49),SceneTreeTimer.SignalName.Timeout);await MissionCapture("90-rewards");recording.SetRecordingActive(false);recording.GetRecording().SaveToWav(Path.Combine(output,"run-mixer.wav"));
  await UiClick("continue");Check(missionNext,"Puppet Masters boundary is unavailable, earned handoff retained");await MissionCapture("91-campaign-boundary");
 }
}
