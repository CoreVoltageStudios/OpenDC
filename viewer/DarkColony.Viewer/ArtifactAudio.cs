using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 readonly Dictionary<(int Slot,int Incarnation),OriginalAudioMixer.Voice> artifactAudio=new();
 readonly List<(int Slot,int Incarnation)> staleArtifactAudio=new();
 void SyncArtifactAudio(){
  // Derived presentation only. Restore/restart reconstructs loops from the worker
  // and site, never from an audio cursor or a saved presentation object.
  staleArtifactAudio.Clear();
  foreach(var pair in artifactAudio){
   var site=nativeWorld.Slots[pair.Key.Slot];
   if(site==null||site.Incarnation!=pair.Key.Incarnation||nativeWorld.ArtifactWorker(site)==null||!site.Living)staleArtifactAudio.Add(pair.Key);
  }
  foreach(var key in staleArtifactAudio){missionMixer.Stop(artifactAudio[key]);artifactAudio.Remove(key);}
  var camera=SoundCamera;
  foreach(int slot in nativeWorld.ArtifactContents.Keys){
   var site=nativeWorld.Slots[slot]!;if(nativeWorld.ArtifactWorker(site)==null)continue;
   var key=(site.Slot,site.Incarnation);var spatial=NativeSpatialSound.Immediate(camera.X,camera.Z,site.X,site.Z);
   bool audible=spatial.Audible&&nativeWorld.Sight.Explored[0][site.Cell.Z*nativeData.Scenario.Map.Width+site.Cell.X];
   if(artifactAudio.TryGetValue(key,out var voice)&&voice.Playing){missionMixer.Set(voice,audible?spatial.Volume:-10000,spatial.Pan);continue;}
   if(audible&&PlayMissionSound(95,site.Slot,spatial) is {} started)artifactAudio[key]=started;
  }
 }
}
