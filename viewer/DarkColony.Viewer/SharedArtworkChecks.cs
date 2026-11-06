using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifySharedArtwork(){
  missionHarnessManual=true;var route=new DarkColony.Diagnostics.BreakthroughRoute();
  while(nativeWorld.Tick<9016&&!mission.AtOutcome){route.Update(nativeWorld);mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();}
  var results=new List<object>();missionPointer=new(-100,-100);nativeSelection.Clear();int sample=0;
  foreach(int sampling in new[]{0,1})foreach(var center in new[]{new Vector2(102*32,(98-87)*32),new Vector2(7*32,(98-20)*32),new Vector2(102*32+600,(98-87)*32-100)}){
   SetNativeSampling(sampling);camera=center;zoom=1.5f;ClampMissionCamera();renderingMotion=true;
   for(int t=0;t<3;t++){
    RememberNativePositions();mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();nativeAccumulator=.033;
    byte[]? baseline=null;var before=mission.Snapshot();
    foreach(bool share in new[]{false,true}){
     nativeShareUnmasked=share;ClearNativeArtBodies();QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
     using var screen=GetViewport().GetTexture().GetImage();using var world=screen.GetRegion(new Rect2I((Vector2I)NativeArea.Position,(Vector2I)NativeArea.Size));
     var bytes=world.GetData();if(!share)baseline=bytes;else Check(baseline!.SequenceEqual(bytes),$"Exact original battlefield pixels with shared unmasked art sample{sample}");
     if(t==0)screen.SavePng(Path.Combine(output,$"shared-art-{sample}-{share}.png"));
    }
    Check(JsonNode.DeepEquals(before,mission.Snapshot()),"Shared art does not mutate authoritative animation or state");results.Add(new{sample,sampling,t,hits=sharedArtHits,misses=sharedArtMisses,bytes=nativeSharedBytes});sample++;
   }
  }
  nativeShareUnmasked=true;renderingMotion=false;nativeAccumulator=0;
  File.WriteAllText(Path.Combine(output,"shared-art.json"),JsonSerializer.Serialize(results));
 }
}
