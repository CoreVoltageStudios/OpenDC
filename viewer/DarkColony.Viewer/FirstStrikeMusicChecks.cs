using Godot;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyMusicPresentation() {
  var music=missionMusic??throw new InvalidOperationException("Recovered music missing from music fixture");
  var trace=new List<object>();
  void Mark(string label)=>trace.Add(new{label,tick=nativeWorld.Tick,wall=Time.GetTicksMsec(),music.Track,music.Frame,music.Active,music.Playing,music.Starts,music.Completions,music.MixedFrames,voices=missionMixer.Voices.Where(v=>v.Playing).Select(v=>new{v.Sound,v.Copy,v.Cursor}).ToArray()});
  async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
  var recorder=new AudioEffectRecord{Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,recorder);
  async Task Capture(string name,double seconds) {
   recorder.SetRecordingActive(true);musicMixTrace=new(File.Create(Path.Combine(output,name+"-submitted.f32")));
   Mark(name+"-start");await Wait(seconds);Mark(name+"-end");musicMixTrace.Dispose();musicMixTrace=null;recorder.SetRecordingActive(false);
   using var recording=recorder.GetRecording();Check(recording.SaveToWav(Path.Combine(output,name+".wav"))==Error.Ok,"mixer capture saved "+name);
  }
  try {
   Check(music.Track==2&&music.Playing,"ordinary Enter starts original track2");
   await Capture("ordinary-opening",6);Check(music.Frame>5*44100,"ordinary real-time music advances");
   await Send(Key.Tab);await Send(Key.S);await Send(Key.I);await Wait(.4);Mark("ordinary-inputs");
   // Hold simulation only, like native audio polling before simulation advancement.
   missionHarnessManual=true;long tick=nativeWorld.Tick,frame=music.Frame;await Wait(.5);
   Check(nativeWorld.Tick==tick&&music.Frame>frame,"music continues with simulation held; no invented pause binding");
   missionMixer.StopAll();music.Seek(2,10*44100);ClearMissionAudioBuffer();await Wait(.1);
   await Capture("music-only",2);
   PlayMissionSound(12);PlayMissionSound(82);PlayMissionSound(198);
   await Capture("music-sfx-voice",3);
   Check(music.Playing&&missionMixer.Voices.Any(v=>v.Sound==82&&v.Playing),"music and native SFX coexist");
   int starts=music.Starts;
   for(int track=2;track<5;track++) {
    music.Seek(track,music.TrackFrames(track)-4410);ClearMissionAudioBuffer();await Wait(.3);
    Check(music.Track==track+1&&music.Playing,"controlled boundary continues into track "+(track+1));Mark("boundary-"+track);
   }
   Check(music.Starts==starts,"track boundaries do not create duplicate playback");
   music.Seek(5,music.TrackFrames(5)-4410);ClearMissionAudioBuffer();await Wait(.3);Mark("end-disc");
   Check(music.Completions==1,"controlled end of disc completes once");
   await Wait(5.2);Check(music.Track==2&&music.Playing&&music.Starts==starts+1,"wall poll repeats from track2 once");Mark("repeat");
   for(int n=0;n<3;n++) {
    await Send(Key.R);Check(!music.Active&&!music.Playing&&missionMixer.Voices.Single().Sound==1000,"restart stops music and leaves only briefing");
    await Wait(.2);Mark("briefing-"+n);await Send(Key.Enter);Check(music.Track==2&&music.Frame<44100,"restart Enter begins track2 from its start");
   }
   foreach(bool success in new[]{true,false}) {
    if(success)nativeWorld.Deaths[1,82]=11;else nativeWorld.Deaths[0,73]=1;
    for(int i=0;i<24;i++){mission.SetClock(mission.Milliseconds+66);mission.Step();}
    mission.SetClock(mission.Outcome!.Deadline+1);await Wait(.3);
    Check(missionVideo is {Visible:true}&&!music.Active&&!music.Playing,"injected outcome movie stops music "+success);Mark("outcome-"+success);
    await Capture(success?"outcome-success":"outcome-failure",success?13.2:5.2);Check(!music.Playing,"outcome cannot poll-restart music");
    if(success)Check(rewardsVisible,"short original outcome movie finishes naturally into rewards");
    else await Send(Key.Enter);
    Check(!music.Active&&rewardsVisible,"natural/skipped movie rewards stay music-free");
    await Send(Key.R);await Send(Key.Enter);
   }
   await Send(Key.R);missionMixer.StopAll();ClearMissionAudioBuffer();await Wait(.2);await Capture("stopped-silence",.5);
   Check(!music.Active,"final briefing has no leaked music");
   music.Dispose();missionMusic=null;PrepareMissionMusic(Path.Combine(output,"intentionally-absent-music"));
   Check(missionMusic==null&&missionMusicDiagnostic!=null,"missing music has a clear nonfatal diagnostic");
   missionHarnessManual=false;await Send(Key.Enter);await WaitMissionTick(10);Check(missionFault==null,"missing music leaves ordinary mission running");await Send(Key.R);
   File.WriteAllText(Path.Combine(output,"music-trace.json"),JsonSerializer.Serialize(new{mode="Normal-time opening and controls; explicitly held simulation, seeks and injected outcomes for focused transition coverage. Not mission victories/defeats or perceptual listening.",trace},new JsonSerializerOptions{WriteIndented=true}));
  }finally{musicMixTrace?.Dispose();musicMixTrace=null;AudioServer.RemoveBusEffect(0,AudioServer.GetBusEffectCount(0)-1);recorder.Dispose();missionHarnessManual=false;}
 }
}
