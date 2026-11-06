using Godot;
using DarkColony.Original;
using DarkColony.Assets;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool nativeViewportCull=true; // Diagnostic comparison only; no player visibility rule.
 bool NativeIntersectsViewport(NativeEntity e,Vector2 at,float height){
  if(e.Body.Pose(e.Angle)==null)return false;
  var offset=NativeOffset;var area=NativeArea;
  bool Hits(float x,float y,float w,float h)=>new Rect2((at+new Vector2(x,y))*zoom+offset,new Vector2(w,h)*zoom).Grow(2*zoom).Intersects(area,true);
  bool Channel(NativeAnimation animation){
   var frame=animation.Pose(e.Angle);if(frame==null)return false;
   foreach(var c in frame.Cells){int mode=c.UnknownWords[2];if(mode==3)continue;
    var sprite=nativeData.Sprite(c.Bank).Cells[c.CellId];
    if(nativeShadows&&mode is 1 or 2){var r=OriginalShadow.Bounds(sprite,c);if(Hits(r.X,r.Y+height,r.Width,r.Height))return true;}
    if(mode is 0 or 1 or 5){var r=OriginalShadow.BodyPlacement(c,sprite);if(Hits(r.X,r.Y,r.Width,r.Height))return true;}
   }return false;
  }
  return Channel(e.Body)||Channel(e.Blood)||Channel(e.Overlay);
 }
 async Task VerifyNativeViewportCulling(){
  missionHarnessManual=true;var route=new DarkColony.Diagnostics.BreakthroughRoute();
  while(nativeWorld.Tick<9016){route.Update(nativeWorld);mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();}
  nativeAccumulator=0;nativeSelection.Clear();missionPointer=new(-100,-100);zoom=1.5f;
  var result=new List<object>();int sample=0;
  foreach(var center in new[]{new Vector2(102*32,(98-87)*32),new Vector2(7*32,(98-20)*32),new Vector2(102*32+600,(98-87)*32-100)}){
   camera=center;ClampMissionCamera();byte[]? before=null;var snapshot=mission.Snapshot();int all=0,shown=0;
   foreach(bool cull in new[]{false,true}){
    nativeViewportCull=cull;ClearNativeArtBodies();QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
    using var screen=GetViewport().GetTexture().GetImage();using var world=screen.GetRegion(new Rect2I((Vector2I)NativeArea.Position,(Vector2I)NativeArea.Size));var bytes=world.GetData();
    if(!cull){before=bytes;all=nativeDrawn.Count;}else{shown=nativeDrawn.Count;Check(before!.SequenceEqual(bytes),"Viewport culling keeps exact battlefield pixels, sample"+sample);}
    screen.SavePng(Path.Combine(output,$"cull-{sample}-{cull}.png"));
   }
   Check(System.Text.Json.Nodes.JsonNode.DeepEquals(snapshot,mission.Snapshot()),"Viewport lookup leaves animation/state unchanged");result.Add(new{sample,all,shown});sample++;
  }
  // Continue identical snapshots for30 simulation ticks/90 presented subframes.
  // Moving offscreen units invalidate their old raster keys; PNG exports are excluded.
  camera=new(7*32,(98-20)*32);ClampMissionCamera();var start=camera;var metrics=new List<object>();
  var checkpoint=mission.Snapshot();System.Text.Json.Nodes.JsonObject? firstEnd=null;renderingMotion=true;
  foreach(bool cull in new[]{false,true}){
   mission=OriginalMission.Restore(nativeData,checkpoint);nativeWorld=mission.World;
   nativeViewportCull=cull;ClearNativeBodies();camera=start;animationStage="viewport-cull-"+cull;
   for(int i=0;i<90;i++){
    if(i%3==0){RememberNativePositions();mission.SetClock(mission.Milliseconds+66);mission.Step();}
    nativeAccumulator=(i%3+1)*.022;camera=start+new Vector2(i*.65f,0);QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
   }
   if(!cull)firstEnd=mission.Snapshot();else Check(System.Text.Json.Nodes.JsonNode.DeepEquals(firstEnd,mission.Snapshot()),"Culled/unculled moving battle continues identically");
   // Read every draw from frame-times.csv. Sampling the last draw after the GPU
   // signal can miss a preceding expensive redraw and select only a cached one.
   metrics.Add(new{cull,timingSource="frame-times.csv",stage=animationStage,ticks=30});
  }
  renderingMotion=false;nativeAccumulator=0;nativeViewportCull=true;
  animationStage="viewport-cull-normal";missionHarnessManual=false;long target=nativeWorld.Tick+180;
  while(nativeWorld.Tick<target){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(missionFault!=null)throw new InvalidOperationException(missionFault);}
  missionHarnessManual=true;
  File.WriteAllText(Path.Combine(output,"viewport-culling.json"),JsonSerializer.Serialize(new{mode="Ordinary-command generated battle at tick9016; exact frozen pixels; matched30-tick motion continuation; final180 ticks run at normal Fast150% clock without PNG exports",result,metrics}));
 }
}
