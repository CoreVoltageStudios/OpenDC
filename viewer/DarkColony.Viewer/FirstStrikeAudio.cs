using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;

public partial class Viewer {
 OriginalMissionMusic? missionMusic;string? missionMusicDiagnostic;
 BinaryWriter? musicMixTrace;
 AudioEffectRecord? ordinaryMusicRecorder;StreamWriter? ordinaryMusicTrace;double ordinaryMusicTraceTime;
 void PrepareMissionMusic(string? directory=null) {
  try{if(invalidImportedMusic.Contains(IsCouncil?OriginalContent.Council:OriginalContent.Base))throw new InvalidDataException("Imported music needs validation/repair");missionMusic=new(directory??Path.Combine(root,IsCouncil?"generated/council-wars-first/music":"generated/original-music"),IsCouncil?OriginalContent.Council:OriginalContent.Base);missionMusicDiagnostic=null;GD.Print("MISSION_MUSIC_READY "+(IsCouncil?"Council Wars":"DCUK")+" CDDA tracks 2..5");}
  catch(Exception error) when(error is IOException or UnauthorizedAccessException) {
   UnavailableMissionMusic(error);
  }
 }
 void UnavailableMissionMusic(Exception error) {
   missionMusic?.Dispose();missionMusic=null;
   missionMusicDiagnostic="Original mission music unavailable";
   nativePresentationGaps.Add(missionMusicDiagnostic+": "+error.Message);
   GD.Print("MISSION_MUSIC_UNAVAILABLE "+error.Message+"; use Options > Game content to add a supported CD-audio image. Voices and effects remain available.");
 }
 void BeginOrdinaryMusicTrace() {
  ordinaryMusicRecorder=new(){Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,ordinaryMusicRecorder);ordinaryMusicRecorder.SetRecordingActive(true);
  ordinaryMusicTrace=new(Path.Combine(output,"ordinary-music.jsonl"));musicMixTrace=new(File.Create(Path.Combine(output,"ordinary-submitted.f32")));
 }
 void TraceOrdinaryMusic(double delta) {
  if(ordinaryMusicTrace==null)return;ordinaryMusicTraceTime+=delta;if(ordinaryMusicTraceTime<.5)return;ordinaryMusicTraceTime=0;
  ordinaryMusicTrace.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{wall=Time.GetTicksMsec(),tick=nativeWorld.Tick,missionBriefing,outcome=mission.AtOutcome,inputEvents,music=missionMusic==null?null:new{missionMusic.Track,missionMusic.Frame,missionMusic.Playing,missionMusic.Active,missionMusic.Starts,missionMusic.MixedFrames},voices=missionMixer.Voices.Where(v=>v.Playing).Select(v=>new{v.Sound,v.Copy,v.Cursor})}));ordinaryMusicTrace.Flush();
 }
 void EndOrdinaryMusicTrace() {
  if(ordinaryMusicRecorder==null)return;
  ordinaryMusicRecorder.SetRecordingActive(false);using(var recording=ordinaryMusicRecorder.GetRecording())recording.SaveToWav(Path.Combine(output,"ordinary-mixer.wav"));
  AudioServer.RemoveBusEffect(0,AudioServer.GetBusEffectCount(0)-1);ordinaryMusicRecorder.Dispose();ordinaryMusicRecorder=null;
  ordinaryMusicTrace?.Dispose();ordinaryMusicTrace=null;musicMixTrace?.Dispose();musicMixTrace=null;
 }
 int messageSoundCursor;readonly float[] missionMixBuffer=new float[512];readonly Vector2[] missionStereoBuffer=new Vector2[256];
 void ResetMissionAudio() {
  missionMusic?.Stop();
  RetireMissionAudio(true);
  missionMixer=new(missionSoundPaths,nativeData.Read);
  missionAudioPlayer=new(){Stream=new AudioStreamGenerator{MixRate=44100,BufferLength=.06f}};
  AddChild(missionAudioPlayer);missionAudioPlayer.Play();missionPlayback=(AudioStreamGeneratorPlayback)missionAudioPlayer.GetStreamPlayback();
 }
 void ClearMissionAudioBuffer() {
  if(!GodotObject.IsInstanceValid(missionAudioPlayer))return;
  // Godot forbids ClearBuffer on an active generator. Replace the playback at discontinuities.
  RetireMissionAudio(false);
  missionAudioPlayer!.Stream=new AudioStreamGenerator{MixRate=44100,BufferLength=.06f};
  missionAudioPlayer.Play();missionPlayback=(AudioStreamGeneratorPlayback)missionAudioPlayer.GetStreamPlayback();
 }
 void PumpMissionAudio() {
  if(missionPlayback==null||missionAudioPlayer is not {Playing:true})return;
  while(missionPlayback.GetFramesAvailable()>=missionStereoBuffer.Length) {
   missionMixer.Mix(missionMixBuffer);
   if(sfxVolume!=1)for(int n=0;n<missionMixBuffer.Length;n++)missionMixBuffer[n]*=sfxVolume;
   try{missionMusic?.AddTo(missionMixBuffer,.25f*musicVolume);}catch(IOException error){UnavailableMissionMusic(error);}
   if(musicMixTrace!=null)foreach(float sample in missionMixBuffer)musicMixTrace.Write(sample);
   for(int i=0;i<missionStereoBuffer.Length;i++)missionStereoBuffer[i]=new(missionMixBuffer[2*i],missionMixBuffer[2*i+1]);
   if(!missionPlayback.PushBuffer(missionStereoBuffer))throw new InvalidOperationException("Mission audio buffer rejected available frames");
  }
 }
 void DispatchMessageSound() {
  // 433f15: queued-message advance notification. Opening insertion is not this branch.
  if(mission.MessageIndex>messageSoundCursor){messageSoundCursor=mission.MessageIndex;PlayMissionSound(187);}
  // 433f6c..433f9f repeatedly requests MSG.WAV at native presentation updates in [200,1000).
  // Pool size1 suppresses overlapping requests. This is an alert, not spoken mission dialogue.
  if(mission.CurrentMessage is {Expired:false} m&&(m.Flags&1)!=0) {
   long age=mission.Milliseconds-m.Timestamp;if(age>=200&&age<1000)PlayMissionSound(187);
  }
 }
}
