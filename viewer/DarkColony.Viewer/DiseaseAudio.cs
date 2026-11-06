using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 readonly Dictionary<long,OriginalAudioMixer.Voice> diseaseAudio=new();
 readonly List<long> staleDiseaseAudio=new();
 void SyncDiseaseAudio(){
  staleDiseaseAudio.Clear();
  foreach(var pair in diseaseAudio)if(!nativeWorld.Missiles.Any(m=>m.Id==pair.Key&&m.State==3))staleDiseaseAudio.Add(pair.Key);
  foreach(long id in staleDiseaseAudio){missionMixer.Stop(diseaseAudio[id]);diseaseAudio.Remove(id);}
  var camera=SoundCamera;
  foreach(var m in nativeWorld.Missiles){
   if(m.State!=3)continue;var cell=new Cell(m.X>>8,m.Z>>8);if(!nativeWorld.Navigation.Inside(cell))continue;
   var spatial=NativeSpatialSound.Transport(camera.X,camera.Z,m.X,m.Z,0,true);
   bool audible=spatial.Audible&&nativeWorld.Sight.Explored[0][cell.Z*nativeData.Scenario.Map.Width+cell.X];
   if(diseaseAudio.TryGetValue(m.Id,out var voice)&&voice.Playing){missionMixer.Set(voice,audible?spatial.Volume:-10000,spatial.Pan);continue;}
   //442576: native sound63; loops are derived anew after loading a snapshot.
   if(audible&&PlayMissionSound(63,m.Shooter,spatial) is {} started)diseaseAudio[m.Id]=started;
  }
 }
}
