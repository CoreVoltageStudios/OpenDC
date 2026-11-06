using Godot;
using DarkColony.Assets;
namespace DarkColony.Viewer;
public partial class Viewer {
 Task<PcmWave[]>? colonyAmbienceTask;AudioStreamWav[] colonyAmbience=Array.Empty<AudioStreamWav>();AudioStreamPlayer? colonyAmbiencePlayer;
 string colonyAmbienceRoot="";double colonyAmbienceClock;int colonyAmbienceNext;
 void ProcessColonyAmbience(double delta){
  bool active=ready&&mainMenu&&contentSetup==null&&menuColony!=null;
  if(!active){colonyAmbiencePlayer?.Stop();colonyAmbienceClock=0;return;}
  if(colonyAmbienceRoot!=root){
   colonyAmbiencePlayer?.Stop();if(colonyAmbiencePlayer!=null)colonyAmbiencePlayer.Stream=null;
   foreach(var sample in colonyAmbience)sample.Dispose();colonyAmbience=Array.Empty<AudioStreamWav>();colonyAmbienceRoot=root;
   string data=Path.Combine(root,"private/DC/SOUND");colonyAmbienceTask=Task.Run(()=>new[]{"DCHIRP2","BUG","CHIGGERS"}.Select(n=>PcmWave.Decode(File.ReadAllBytes(Path.Combine(data,n+".WAV")))).ToArray());
  }
  if(colonyAmbienceTask is {IsCompleted:true}){try{colonyAmbience=colonyAmbienceTask.GetAwaiter().GetResult().Select(s=>new AudioStreamWav{Data=s.SignedSamples,Format=s.Bits==8?AudioStreamWav.FormatEnum.Format8Bits:AudioStreamWav.FormatEnum.Format16Bits,MixRate=s.SampleRate,Stereo=s.Channels==2}).ToArray();}catch(Exception e){GD.Print("MENU_AMBIENCE_UNAVAILABLE "+e.Message);}colonyAmbienceTask=null;}
  colonyAmbienceClock+=delta;if(colonyAmbienceClock<12||colonyAmbience.Length==0||colonyAmbiencePlayer?.Playing==true)return;
  colonyAmbienceClock=0;colonyAmbiencePlayer??=new AudioStreamPlayer();if(colonyAmbiencePlayer.GetParent()==null)AddChild(colonyAmbiencePlayer);
  colonyAmbiencePlayer.Stream=colonyAmbience[colonyAmbienceNext++%colonyAmbience.Length];colonyAmbiencePlayer.VolumeDb=-29+Mathf.LinearToDb(Math.Max(.0001f,sfxVolume));colonyAmbiencePlayer.Play();
 }
}
