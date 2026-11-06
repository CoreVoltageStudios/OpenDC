using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyAspectWindow(){
  Check(!capturing,"Uses the ordinary Windows rendering path");
  var window=GetWindow();window.Mode=Window.ModeEnum.Windowed;
  foreach(var size in new[]{new Vector2I(1920,1080),new Vector2I(1280,720),new Vector2I(960,540)}){
   window.Size=size;ui=1.5f;
   foreach(int aspect in new[]{0,16,21}){
    maximumAspect=aspect;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);ApplyAspectLimit();
    await MenuCapture($"window-{size.X}-{aspect}-menu");OpenMissionPanel("options");await MenuCapture($"window-{size.X}-{aspect}-options");RelayLayout("Resized Options");CloseRelayPanel();
    Check(GetViewportRect().Size.X==Math.Min(window.Size.X,aspect==0?window.Size.X:(int)Math.Floor(window.Size.Y*aspect/9d)),"Resize updates complete active area");
   }
  }
  window.Mode=Window.ModeEnum.Fullscreen;maximumAspect=16;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);ApplyAspectLimit();await MenuCapture("window-fullscreen-restored");
 }
 async Task VerifyAspectRepair(){
  foreach(int aspect in new[]{0,16,21})foreach(float scale in new[]{.75f,1f,1.5f}){
   maximumAspect=aspect;ApplyAspectLimit();ui=scale;await MenuCapture($"aspect-{aspect}-{scale}-menu");
   Check(GetViewportRect().Size.X<=aspectBasis.X,"Aspect never expands width");
   if(aspect!=0)Check(GetViewportRect().Size.X/GetViewportRect().Size.Y<=aspect/9f+.001f,"Maximum ratio enforced");
   foreach(string panel in new[]{"options","load","campaign"}){OpenMissionPanel(panel);await MenuCapture($"aspect-{aspect}-{scale}-{panel}");RelayLayout(panel);CloseRelayPanel();}
  }
  var data=PersistenceData("SCENARIO/ALIEN/ALIEN02.SCN");InstallMission(data,new OriginalMission(data,0),false);AdvanceInterference(250);SaveMissionSlot(0);CloseRelayPanel();
  foreach(int aspect in new[]{0,16,21})foreach(float scale in new[]{.75f,1.5f}){
   maximumAspect=aspect;ApplyAspectLimit();ui=scale;ResetMissionCamera();await UiCapture($"aspect-{aspect}-{scale}-hud");RelayLayout("HUD");
   focusedUi=missionHits.First(h=>h.Id.StartsWith("product:")).Id;relayPointerHelp=false;await UiCapture($"aspect-{aspect}-{scale}-tooltip");Check(GetViewportRect().Encloses(relayTooltipBounds),"Tooltip stays inside active game area");focusedUi="";
   await Send(Key.Tab);Check(NativeSelected().Any(),"Commander selected");
   var center=NativeArea.GetCenter();await NativeMouse(MouseButton.Right,center);Check(nativeWorld.Events.Any(e=>e.Kind=="order"),"Transformed world input issues order");
   await NativeMouse(MouseButton.Left,MiniMapRect.GetCenter());
   foreach(string panel in new[]{"options","save","load"}){OpenMissionPanel(panel);await UiCapture($"aspect-{aspect}-{scale}-mission-{panel}");RelayLayout(panel);CloseRelayPanel();}
   int count=nativeWorld.Events.Count;float z=zoom;
   Input.ParseInputEvent(new InputEventMouseButton{Position=NativeWindowPoint(new(-20,40)),ButtonIndex=MouseButton.Right,Pressed=true});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   Check(nativeWorld.Events.Count==count&&zoom==z,"Black bar rejects world order");
   Check(!NativeArea.HasPoint(missionPointer),"Black bar clears world hover and custom cursor");
   Check(MissionEdgeVelocity(new(-1,40),true,false,false,false)==Vector2.Zero,"Black bar rejects edge scrolling");
  }
  foreach(int aspect in new[]{0,16,21}){
   maximumAspect=aspect;ApplyAspectLimit();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
   var movie=new OriginalMovie();movie.Load(Path.Combine(root,"generated/original-video/v1/avhd4.odcv"));AddChild(movie);movie.Size=GetViewportRect().Size;movie.Play();await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);await UiCapture("aspect-movie-"+aspect);Check(movie.Frame>0,"Movie advances in limited viewport");RemoveChild(movie);movie.Free();
  }
  maximumAspect=16;ApplyAspectLimit();SaveSettings(p=>ArtworkPreference.SaveAspect(p,16),"save");
 }
 async Task VerifyPlaytestRepair(){
  var markerData=PersistenceData("SCENARIO/ALIEN/ALIEN04.SCN");InstallMission(markerData,new OriginalMission(markerData,0),false);
  foreach(var t in nativeWorld.Teams)t.Ai=0;
  var marker=nativeWorld.Entities.First(e=>e.Type==94&&e.Owner==5);
  foreach(var e in nativeWorld.Entities.Where(e=>e!=marker).ToArray())nativeWorld.Retire(e);
  var scout=nativeWorld.Spawn(8,0,new(marker.Cell.X+1,marker.Cell.Z),"fixture/marker-scout");nativeWorld.Step();nativeWorld.RefreshSight();
  nativeSelection.Clear();nativeSelection.Add((scout.Slot,scout.Incarnation));camera=NativePosition(marker);zoom=2;ClampMissionCamera();await UiCapture("marker-before-click");
  var markerPoint=UnitHitRect(marker).GetCenter();Check(UiHover(markerPoint)?.Type!=94,"Invisible script marker cannot be hovered");
  await NativeMouse(MouseButton.Right,markerPoint);Check(scout.PendingOrder?.Order==OriginalOrder.MoveOnly,"Right-click marker scenery is movement, never attack");await UiCapture("marker-after-click");
  foreach(int n in new[]{2,4}){
   var data=PersistenceData($"SCENARIO/ALIEN/ALIEN{n:00}.SCN");InstallMission(data,new OriginalMission(data,0),false);
   // Isolated terrain/roster fixture. Mission scripts disabled by stepping World directly.
   foreach(var t in nativeWorld.Teams)t.Ai=0;
   var vent=nativeWorld.Entities.First(e=>e.Type==40&&e.ResourceFlow>0);
   foreach(var e in nativeWorld.Entities.Where(e=>e.Type!=40).ToArray())nativeWorld.Retire(e);
   var miner=nativeWorld.Spawn(14,0,new(vent.Cell.X+3,vent.Cell.Z),"fixture/miner");
   var group=Enumerable.Range(0,16).Select(i=>nativeWorld.Spawn(8,0,new(vent.Cell.X+3,vent.Cell.Z),"fixture/group/"+i)).Prepend(miner).ToArray();
   nativeWorld.Step();nativeWorld.RefreshSight();nativeSelection.Clear();foreach(var e in group)nativeSelection.Add((e.Slot,e.Incarnation));
   camera=NativePosition(vent);zoom=2;ClampMissionCamera();await UiCapture($"repair-{n}-before");
   var at=UnitHitRect(vent).GetCenter();Check(ResourceAt(at)==vent,"Visible vent artwork resolves exact resource anchor");
   foreach(var key in new[]{Key.M,Key.A}){
    await Send(key);await NativeMouse(MouseButton.Left,at);
    Check(miner.PendingOrder is {} pending&&pending.X256==vent.Cell.X*256+128&&pending.Z256==vent.Cell.Z*256+128,"Move control targets exact vent anchor");
   }
   await NativeMouse(MouseButton.Right,at);
   var samples=new List<object>();
   for(int tick=0;tick<600;tick++){
    nativeWorld.Step();
    samples.Add(new{nativeWorld.Tick,units=group.Select(e=>new{e.Slot,e.Type,e.X,e.Z}).ToArray()});
    // Every frame of a 3-second sequence at the normal 15 simulation ticks/sec.
    if(tick<45)await UiCapture($"repair-{n}-frame-{tick:000}");
    else if(tick%100==0)await UiCapture($"repair-{n}-tick-{tick}");
   }
   Check(miner.Type==48&&miner.Cell==vent.Cell,"Mixed group harvest deploys at vent");
   var positions=group.Select(e=>(e.X,e.Z)).ToArray();for(int t=0;t<150;t++)nativeWorld.Step();
   Check(group.Select(e=>(e.X,e.Z)).SequenceEqual(positions),"Arrived group settled for ten simulation seconds");
   File.WriteAllText(Path.Combine(output,$"repair-{n}-motion.json"),JsonSerializer.Serialize(samples));await UiCapture($"repair-{n}-settled");
  }
 }
}
