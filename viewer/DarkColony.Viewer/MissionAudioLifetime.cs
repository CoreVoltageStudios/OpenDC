using Godot;
namespace DarkColony.Viewer;
public partial class Viewer {
 // Stop is asynchronous: the audio server can still mix its fade-out after the
 // player is detached. Keep BOTH playback and its generator alive until it lets go.
 readonly List<(AudioStreamGeneratorPlayback? Playback,AudioStream? Stream)> retiringMissionAudio=new();
 void RetireMissionAudio(bool removePlayer){
  AudioStream? stream=null;
  if(GodotObject.IsInstanceValid(missionAudioPlayer)){
   missionAudioPlayer!.Stop();stream=missionAudioPlayer.Stream;missionAudioPlayer.Stream=null;
   if(removePlayer){missionAudioPlayer.QueueFree();missionAudioPlayer=null;}
  }
  if(missionPlayback!=null||stream!=null)retiringMissionAudio.Add((missionPlayback,stream));
  missionPlayback=null;
 }
 void DrainRetiredMissionAudio(){
  for(int i=retiringMissionAudio.Count-1;i>=0;i--){
   var item=retiringMissionAudio[i];
   if(item.Playback!=null&&item.Playback.GetReferenceCount()>1)continue;
   item.Playback?.Dispose();item.Stream?.Dispose();retiringMissionAudio.RemoveAt(i);
  }
 }
 async Task VerifyAudioReplacement(){
  missionHarnessManual=true;
  for(int i=0;i<40;i++){
   // Briefing->battle buffer replacement, restart and load/mission replacement.
   StartFirstStrike();ClearMissionAudioBuffer();ResetFirstStrike();
   if(i%5==0){var data=PersistenceData(i%10==0?"SCENARIO/ALIEN/ALIEN04.SCN":"SCENARIO/ALIEN/ALIEN02.SCN");InstallMission(data,new(data,0),false);}
   await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  }
  ulong began=Time.GetTicksMsec();
  while(retiringMissionAudio.Count>0&&Time.GetTicksMsec()-began<2000){DrainRetiredMissionAudio();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
  Check(retiringMissionAudio.Count==0,"Forty audio replacement cycles release all retired playback and streams");
 }
}
