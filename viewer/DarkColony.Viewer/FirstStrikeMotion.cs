using Godot;
using DarkColony.Assets;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 readonly Dictionary<int,PresentationPose> nativePrevious=new();long nativePreviousTick=-2;
 bool nativeInterpolation=true; // Only disabled by the labelled before/after capture fixture.
 PresentationPose Pose(NativeEntity e)=>new(e.Incarnation,e.Type,e.Owner,e.State,e.Flag,e.X,e.Z,e.Height,NativeVisible(e),e.Body.Mode!=2);
 void RememberNativePositions(){nativePrevious.Clear();foreach(var e in nativeWorld.Entities)nativePrevious[e.Slot]=Pose(e);nativePreviousTick=nativeWorld.Tick;}
 (Vector2 Body,float Height) NativeRenderPosition(NativeEntity e) {
  // 439c0b/439d0a/439d26 subtract logical city-slot offset before FIN placement.
  // All city artwork is authored against the shared city base, not each logical anchor.
  if(e.Slot<120&&e.Slot%15<6){var team=nativeWorld.Teams[e.Slot/15];var point=OriginalCoordinates.Project(team.Base[0]*256,team.Base[1]*256,e.Height,nativeData.Scenario.Map.Height);return(new(point.X,point.Y),e.Height>>3);}
  if((!firstStrike&&!interferenceDev)||(missionHarnessManual&&!renderingMotion)||(!interferenceDev&&(missionBriefing||mission.AtOutcome))||!nativeInterpolation||nativePreviousTick!=nativeWorld.Tick-1)return(NativePosition(e),e.Height>>3);
  var p=PresentationMotion.Sample(nativePrevious.GetValueOrDefault(e.Slot),Pose(e),nativeAccumulator/.066);
  return(new((float)(p.X/8),(float)((nativeData.Scenario.Map.Height*256-p.Z-1-p.Height)/8)),(float)(p.Height/8));
 }
 void DisposeNativeTexture(Texture2D texture){if(nativeSharedTextureIds.Contains(texture.GetInstanceId()))return;nativeSamplingLayout.Remove(texture.GetInstanceId());animationTextureProof.Remove(texture.GetInstanceId());if(texture is CanvasTexture canvas){var source=canvas.DiffuseTexture;canvas.DiffuseTexture=null;if(source!=null){animationTextureProof.Remove(source.GetInstanceId());source.Dispose();}}texture.Dispose();}
 Texture2D NativeShadowTexture(Raster pixels)=>new CanvasTexture{DiffuseTexture=OriginalTexture(pixels),TextureFilter=firstStrike&&nativeSampling==0?TextureFilterEnum.Nearest:TextureFilterEnum.Linear};
}
