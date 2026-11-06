using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyRewardPresentation() {
  // Explicit state-injected frontend coverage. This is NOT a completed gameplay mission.
  missionHarnessManual=true;
  nativeWorld.Deaths[1,82]=11;nativeWorld.NearbyCommanderKills[0]=10;nativeWorld.Kills[0,73]=80;
  for(int i=0;i<24;i++){mission.SetClock(mission.Milliseconds+66);mission.Step();}
  mission.SetClock(mission.Outcome!.Deadline+1);
  await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);await MissionCapture("reward-fixture-movie");
  await Send(Key.Enter);await ToSignal(GetTree().CreateTimer(.15),SceneTreeTimer.SignalName.Timeout);await MissionCapture("reward-fixture-early");
  await ToSignal(GetTree().CreateTimer(3),SceneTreeTimer.SignalName.Timeout);await MissionCapture("reward-fixture-complete");
  Check(string.Join(',',mission.Outcome.Medals)=="6,7,0,9,-1,-1","native else-if awards unchanged at80 kills");
  Check(missionRewards!.GrayRanks[0]=="XIMAL."&&missionRewards.GrayRanks[1]=="IDRAC.","native rank labels");
  await Send(Key.Enter);Check(missionBriefing&&!rewardsVisible,"success Replay returns to First Strike briefing");await MissionCapture("reward-fixture-replay");
  GD.Print("AV_STAGE rewards before reset");await Send(Key.R);GD.Print("AV_STAGE rewards after reset");await Send(Key.Enter);nativeWorld.Deaths[0,73]=1;for(int i=0;i<24;i++){mission.SetClock(mission.Milliseconds+66);mission.Step();}mission.SetClock(mission.Outcome!.Deadline+1);
  await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);await MissionCapture("reward-fixture-failure");
  await Send(Key.Enter);Check(nativeWorld.Tick==0&&missionBriefing&&!rewardsVisible,"failure Enter retries and clears rewards");
  for(int cycle=0;cycle<2;cycle++) {
   await Send(Key.Enter);nativeWorld.Deaths[0,73]=1;for(int i=0;i<24;i++){mission.SetClock(mission.Milliseconds+66);mission.Step();}mission.SetClock(mission.Outcome!.Deadline+1);
   await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);Check(missionVideo is {Visible:true},"outcome media active before restart");
   await Send(Key.R);Check(missionVideo==null&&nativeWorld.Tick==0&&missionBriefing,"R releases active outcome video/audio");
  }
  missionHarnessManual=false;
 }
 async Task VerifyAudioPresentation() {
  // Controlled mixer exercise, not hidden-state-equivalence or audible inspection.
  missionHarnessManual=true;missionMusic?.Stop();missionMixer.StopAll();ClearMissionAudioBuffer();
  long draws=nativeWorld.Random.Draws;int index=nativeWorld.Random.Index;
  var recorder=new AudioEffectRecord{Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,recorder);
  async Task Segment(string name,NativeSpatialSound spatial) {
   missionMixer.StopAll();ClearMissionAudioBuffer();var voice=PlayMissionSound(82,spatial:spatial)!;
   await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);recorder.SetRecordingActive(true);
   await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);recorder.SetRecordingActive(false);
   using(var recording=recorder.GetRecording())recording.SaveToWav(Path.Combine(output,name+".wav"));
   Check(voice.Playing,"native transport loop persists "+name);
  }
  await Segment("spatial-center",new(0,0));await Segment("spatial-left",new(-600,-1200));await Segment("spatial-right",new(-600,1200));
  missionMixer.StopAll();ClearMissionAudioBuffer();recorder.SetRecordingActive(true);
  for(int i=0;i<5;i++)PlayMissionSound(12);PlayMissionSound(45);PlayMissionSound(82);PlayMissionSound(187);PlayMissionSound(198);
  Check(missionMixer.Dropped>=4,"busy one-copy weapon drops overlapping requests");
  await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);recorder.SetRecordingActive(false);using(var recording=recorder.GetRecording())recording.SaveToWav(Path.Combine(output,"concurrent.wav"));
  Check(nativeWorld.Random.Draws==draws&&nativeWorld.Random.Index==index,"presentation pool/spatial mixing consumes no gameplay RNG");
  await Send(Key.R);Check(missionMixer.Voices.Count()==1&&missionMixer.Voices.Single().Sound==1000,"restart leaves only fresh briefing voice");
  await Send(Key.Enter);missionHarnessManual=false;await WaitMissionTick(25);
  var transport=transportAudio.Values.Single();int before=transport.Volume;await Send(Key.Right);await WaitMissionTick(nativeWorld.Tick+2);
  Check(transport.Volume!=before,"moving camera updates vertical transport at next native dispatch");
  await Send(Key.R);Check(transportAudio.Count==0,"restart clears active transport handles");
  AudioServer.RemoveBusEffect(0,0);recorder.Dispose();
  File.WriteAllText(Path.Combine(output,"audio-mixer-proof.json"),JsonSerializer.Serialize(new{mode="Godot rendered and mixer-captured fixtures; not listening",checks},new JsonSerializerOptions{WriteIndented=true}));
 }
}
