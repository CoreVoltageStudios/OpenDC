using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyBriefingInput(){
  missionHarnessManual=true;
  foreach(string scenario in new[]{"ALIEN/ALIEN04","HUMAN/HUMAN05"}){
   var d=PersistenceData("SCENARIO/"+scenario+".SCN");InstallMission(d,new OriginalMission(d,0),true);ui=1.5f;
   await UiCapture(scenario.Replace('/','-')+"-receiving");
   MissionInterfaceInput(new InputEventMouseButton{Position=Px(MissionGrid().Right).GetCenter(),ButtonIndex=MouseButton.WheelDown,Pressed=true});
   int limit=Math.Max(0,briefingTotalLines-briefingVisibleLines);
   Check(briefingScroll==Math.Min(3,limit)&&!briefingFollow,"Wheel scrolls during reveal without auto-follow "+scenario);
   if(limit>0){
    ScrollBriefing(-briefingTotalLines);
    await SessionClick("brief-down");Check(briefingScroll==Math.Min(5,limit),"Visible Down scroll control "+scenario);
    await SessionClick("brief-up");Check(briefingScroll==0,"Visible Up scroll control "+scenario);
   }else Check(!missionHits.Any(h=>h.Id is "brief-up" or "brief-down"),"No scroll controls when complete text fits");
   await SessionClick("brief-reveal");Check(missionBriefing&&!BriefingRevealing&&briefingTyping==null,"Reveal button consumes click without battle "+scenario);
   ResetBriefingPresentation();missionMixer.Stop(briefingVoice);sfxVolume=0;ClearMissionAudioBuffer();
   using var rec=new AudioEffectRecord{Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,rec);rec.SetRecordingActive(true);
   await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);
   rec.SetRecordingActive(false);using(var wave=rec.GetRecording())wave.SaveToWav(Path.Combine(output,IsHuman?"human-typing-muted.wav":"taar-typing-muted.wav"));AudioServer.RemoveBusEffect(0,AudioServer.GetBusEffectCount(0)-1);
   Check(BriefingRevealing,"Muted typing does not stop text");sfxVolume=1;StartFirstStrike();Check(briefingTyping==null,"Early battle stops typing");
  }
  // Roswell is the playable Earth briefing (RawFlag1); A15 still uses Mars.
  var earth=PersistenceData("SCENARIO/ALIEN/ALIEN05.SCN");InstallMission(earth,new OriginalMission(earth,0),true);ui=1;
  var sequence=new List<object>();
  for(int i=0;i<110;i++){
   await ToSignal(GetTree().CreateTimer(.05),SceneTreeTimer.SignalName.Timeout);QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
   var grid=MissionGrid();var crop=Px(new Rect2(grid.Left.Position,new(grid.Left.Size.X,grid.Left.Size.Y-160)));
   using var image=GetViewport().GetTexture().GetImage();using var region=image.GetRegion(new Rect2I((int)crop.Position.X,(int)crop.Position.Y,(int)crop.Size.X,(int)crop.Size.Y));region.SavePng(Path.Combine(output,$"earth-{i:000}.png"));
   sequence.Add(new{time=briefingElapsed,globe=BriefingGlobe().ToString()});
  }
  Check(BriefingGlobe()==("EARTHS",1),"Earth completes original opening and holds location");File.WriteAllText(Path.Combine(output,"earth-temporal.json"),JsonSerializer.Serialize(sequence));
  ResetFirstStrike();Check(BriefingGlobe()==("EARTHG",1),"Earth opening resets on Roswell restart");ReturnMainMenu();Check(briefingTyping==null&&interfaceTextures.Count==0,"Early exit releases briefing resources");
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));GD.Print("FIRST_STRIKE_SUITE_PASS briefing-input");
 }
 async Task VerifyRepairPolish(){
  missionHarnessManual=true;
  foreach(var (scenario,label) in new[]{("ALIEN/ALIEN01","taar-short"),("ALIEN/ALIEN04","taar-long"),("HUMAN/HUMAN01","human-short"),("HUMAN/HUMAN05","human-long"),("ALIEN/ALIEN15","taar-final")}){
   var d=PersistenceData("SCENARIO/"+scenario+".SCN");InstallMission(d,new OriginalMission(d,0),true);
   ResetBriefingPresentation();await UiCapture(label+"-start");
   Check(briefingColors.Contains((byte)1)&&briefingColors.Contains((byte)4),"Authored blue markers / green body "+label);
   if(label=="taar-short"){
    missionMixer.Stop(briefingVoice);ClearMissionAudioBuffer(); // Isolate typing in this diagnostic recording only.
    using var rec=new AudioEffectRecord{Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,rec);rec.SetRecordingActive(true);
    var rows=new List<object>();
    for(int i=0;i<110;i++){
     await ToSignal(GetTree().CreateTimer(.05),SceneTreeTimer.SignalName.Timeout);QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
     var grid=MissionGrid();var crop=Px(new Rect2(grid.Left.Position,new(grid.Left.Size.X,grid.Left.Size.Y-160)));
     using var image=GetViewport().GetTexture().GetImage();using var region=image.GetRegion(new Rect2I((int)crop.Position.X,(int)crop.Position.Y,(int)crop.Size.X,(int)crop.Size.Y));region.SavePng(Path.Combine(output,$"globe-{i:000}.png"));
     rows.Add(new{time=briefingElapsed,globe=BriefingGlobe().ToString(),briefingCharacters,typing=briefingTyping?.Playing??false});
    }
    rec.SetRecordingActive(false);using(var wave=rec.GetRecording())wave.SaveToWav(Path.Combine(output,"typing-isolated.wav"));AudioServer.RemoveBusEffect(0,AudioServer.GetBusEffectCount(0)-1);
    File.WriteAllText(Path.Combine(output,"briefing-temporal.json"),JsonSerializer.Serialize(rows));
    Check(BriefingRevealing,"Slower text remains in progress after globe sequence");RevealBriefing();
    Check(briefingTyping==null&&BriefingTransmissionComplete,"Reveal stops typing and closes transmission");
   }
   ResetBriefingPresentation();ProcessBriefing(.25);await UiCapture(label+"-revealing");
   Check(briefingCharacters>0&&BriefingRevealing,"Progressive original text "+label);
   ScrollBriefing(12);float scroll=briefingScroll;ProcessBriefing(.2);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   Check(briefingScroll==scroll,"Manual scroll disables automatic following "+label);
   await Send(Key.Space);Check(missionBriefing&&!BriefingRevealing&&briefingTyping==null,"Space reveals all without entering mission "+label);
   await UiCapture(label+"-revealed");await Send(Key.Pageup);await UiCapture(label+"-top");for(int i=0;i<12;i++)await Send(Key.Pagedown);await UiCapture(label+"-bottom");
   Check(briefingScroll==Math.Max(0,briefingTotalLines-briefingVisibleLines),"Last original line reachable "+label);
   await Send(Key.Enter);Check(!missionBriefing&&briefingTyping==null,"Enter begins after separate reveal input");
   ResetFirstStrike();Check(briefingCharacters==0&&briefingElapsed==0,"Reopening resets presentation "+label);StartFirstStrike();
  }
  var data=PersistenceData("SCENARIO/ALIEN/ALIEN02.SCN");InstallMission(data,new OriginalMission(data,0),false);AdvanceInterference(250);
  foreach(int aspect in new[]{0,16,21})foreach(float scale in new[]{.75f,1f,1.5f}){
   maximumAspect=aspect;ApplyAspectLimit();ui=scale;focusedUi="";await UiCapture($"edge-{aspect}-{scale}-hud");
   var size=GetViewportRect().Size;
   Check(MissionEdgeVelocity(new(1,size.Y/2),true,false,false,false).X<0&&MissionEdgeVelocity(new(size.X-2,size.Y/2),true,false,false,false).X>0,"Both edges accessible "+aspect+"/"+scale);
   int events=nativeWorld.Events.Count;
   foreach(var point in new[]{new Vector2(-20,size.Y/2),new Vector2(size.X+20,size.Y/2)}){
    MissionInterfaceInput(new InputEventMouseButton{Position=point,ButtonIndex=MouseButton.Right,Pressed=true});
    Check(nativeWorld.Events.Count==events&&MissionEdgeVelocity(point,true,false,false,false)==Vector2.Zero,"Side bar rejects orders and scrolling");
   }
   OpenMissionPanel("options");await UiCapture($"edge-{aspect}-{scale}-options");Check(MissionEdgeVelocity(new(1,size.Y/2),true,false,false,false)==Vector2.Zero,"Options prevent pan");CloseRelayPanel();
  }
  maximumAspect=16;ApplyAspectLimit();ui=1.5f;
  var longData=PersistenceData("SCENARIO/ALIEN/ALIEN04.SCN");InstallMission(longData,new OriginalMission(longData,0),true);
  await Send(Key.Space);await Send(Key.Pageup);await UiCapture("large-briefing-top");
  for(int i=0;i<15;i++)await Send(Key.Pagedown);await UiCapture("large-briefing-bottom");
  Check(briefingScroll==briefingTotalLines-briefingVisibleLines,"Enlarged pillarbox last line reachable");
  maximumAspect=0;ApplyAspectLimit();ui=GetViewportRect().Size.Y>1080?4/3f:1;
  foreach(var (path,label) in new[]{("human15/h01/controlled-success.json","human-next"),("human15/h05/controlled-success.json","human-unavailable"),("earth-intervenes/mission/success.json","taar-next"),("final-evacuation/mission-final/success.json","taar-final"),("earth-intervenes/mission/failure.json","defeat")}){
   var s=SaveCodec.Open(Path.Combine(root,"generated",path))["payload"]!.AsObject();var d=PersistenceData(s["scenario"]!.GetValue<string>());InstallMission(d,OriginalMission.Restore(d,s),false);
   missionOutcomeShown=true;outcomeText=ReadMissionText(mission.Outcome!.TextPath);ReleaseMissionVideo();BeginRewards();
   var current=mission;await Send(Key.Enter);Check(ReferenceEquals(mission,current),"Result entry guard rejects carried input "+label);
   await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);await UiCapture("results-"+label);
   var actions=missionHits.Where(h=>h.Id is "main-menu" or "restart" or "continue").ToArray();
   Check(actions[0].Id=="main-menu"&&actions[1].Id=="restart"&&(!resultContinueAvailable||actions[^1].Id=="continue"),"Visual order Exit Replay Continue "+label);
   Check(focusedUi==ResultDefault,"Correct default focus "+label);
   if(resultContinueAvailable){await Send(Key.Left);Check(focusedUi=="restart","Left selects Replay");await Send(Key.Right);Check(focusedUi=="continue","Right returns Continue");}
   await Send(Key.Enter);
   if(label.EndsWith("next"))Check(missionBriefing&&mission.CampaignIndex==current.CampaignIndex+1,"Default Enter continues correct faction");
   if(label=="taar-final")Check(mainMenu,"Final completion returns menu, no mission16");
   if(label is "human-unavailable" or "defeat")Check(missionBriefing||missionPanel=="abandon","Replay default when Continue absent");
  }
  Check(!godMode,"Checks did not enable session god mode");
  // Explicit focus must win over the success default, including after a movie skip.
  var result=SaveCodec.Open(Path.Combine(root,"generated/human15/h01/controlled-success.json"))["payload"]!.AsObject();
  var resultData=PersistenceData(result["scenario"]!.GetValue<string>());InstallMission(resultData,OriginalMission.Restore(resultData,result),false);
  missionOutcomeShown=true;outcomeText=ReadMissionText(mission.Outcome!.TextPath);PlayOutcomeMedia();
  await Send(Key.Enter);await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
  Check(rewardsVisible&&!missionBriefing,"Movie dismissal stays on results");
  MissionInterfaceInput(new InputEventKey{Keycode=Key.Enter,Pressed=true,Echo=true});
  Check(rewardsVisible,"Held Enter echo cannot continue");await Send(Key.Left);await Send(Key.Enter);
  Check(missionBriefing&&mission.CampaignIndex==0&&IsHuman,"Explicit Replay focus overrides Continue default");
  ReturnMainMenu();Check(interfaceTextures.Count==0&&briefingTyping==null,"Briefing textures and typing released on menu return");
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));GD.Print("FIRST_STRIKE_SUITE_PASS repair-polish");
 }
}
