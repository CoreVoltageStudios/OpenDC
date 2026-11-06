using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyEdgeScrolling(){
  missionHarnessManual=true;bool reload=missionSuite=="edge-reload";
  Check(reload?!edgeScrolling&&edgeSpeed==1.75f:edgeScrolling&&edgeSpeed==1,"navigation defaults / fresh process persistence");
  edgeScrolling=true;edgeSpeed=1;
  string legacy=Path.Combine(output,"legacy-settings.json");File.WriteAllText(legacy,"{\"version\":1,\"worldArtwork\":\"Crisp\"}");
  Check(ArtworkPreference.LoadNavigation(legacy,out _)==(true,1f),"old artwork-only preferences enable default edge scrolling");
  ArtworkPreference.SaveNavigation(legacy,false,1.75f);ArtworkPreference.Save(legacy,ArtworkStyle.Classic);
  Check(ArtworkPreference.LoadNavigation(legacy,out _)==(false,1.75f)&&ArtworkPreference.Load(legacy,out _)==ArtworkStyle.Classic,"artwork and navigation writes preserve each other");
  File.WriteAllText(legacy,"invalid");Check(ArtworkPreference.LoadNavigation(legacy,out var diagnostic)==(true,1f)&&diagnostic!=null,"malformed settings safely default with diagnostic");
  while(nativeWorld.Tick<180){mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();}
  await Send(Key.Tab);var commander=NativeSelected().Single();var originalCamera=camera;
  var size=GetViewportRect().Size;var centre=size/2;
  var left=new Vector2(0,centre.Y);var right=new Vector2(size.X-1,centre.Y);var top=new Vector2(centre.X,0);var bottom=new Vector2(centre.X,size.Y-1);
  foreach(var p in new[]{left,right,top,bottom,new Vector2(0,0),new Vector2(size.X-1,0),new Vector2(0,size.Y-1),size-Vector2.One}){
   var v=EdgeDirection(p,size);Check(Math.Abs(v.Length()-1)<.00001,"unit speed at outer edge/corner "+p);
   Check(v.X==0||Math.Sign(v.X)==Math.Sign(p.X-centre.X),"horizontal direction follows outer edge "+p);
   Check(v.Y==0||Math.Sign(v.Y)==Math.Sign(p.Y-centre.Y),"vertical direction follows outer edge "+p);
  }
  float band=Math.Clamp(18*size.Y/1080,12,28);
  Check(EdgeDirection(new(band,centre.Y),size)==Vector2.Zero&&Math.Abs(EdgeDirection(new(band/2,centre.Y),size).Length()-.5f)<.0001,"narrow band has zero inner speed and smooth half-speed midpoint");
  Check(EdgeDirection(new(-1,0),size)==Vector2.Zero&&EdgeDirection(new(size.X,centre.Y),size)==Vector2.Zero,"outside coordinates never scroll");
  foreach(float scale in new[]{.75f,1f,1.5f})foreach(float z in new[]{1f,2f,4f}){
   ui=scale;zoom=z;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
   Check(MissionEdgeVelocity(right,true,false,false,false).X>0&&MissionEdgeVelocity(bottom,true,false,false,false).Y>0,"right/bottom rim accessible across UI/zoom "+scale+"/"+z);
   Check(MissionEdgeVelocity(new(NativeArea.End.X-1,centre.Y),true,false,false,false)==Vector2.Zero,"sidebar seam is not an edge "+scale+"/"+z);
   Check(Math.Abs(MissionEdgeVelocity(left,true,false,false,false).Length()*zoom/size.Y-720f/1080)<.0001,"screen-height speed independent of zoom and UI "+scale+"/"+z);
  }
  ui=size.Y>1080?4/3f:1;zoom=2;camera=originalCamera;await UiCapture("edge-world");
  foreach(var hit in missionHits)Check(MissionEdgeVelocity(hit.Rect.GetCenter(),true,false,false,false)==Vector2.Zero,"control hover does not pan: "+hit.Id);
  Check(MissionEdgeVelocity(MiniMapRect.GetCenter(),true,false,false,false)==Vector2.Zero,"minimap hover does not pan");
  Check(MissionEdgeVelocity(new(centre.X,size.Y-32*ui),true,false,false,false)==Vector2.Zero,"ordinary message strip does not pan");
  foreach(var state in new[]{"focus","minimized","exit","button","ui","minimap","middle","disabled","menu"}){
   uiPointerOwned=state=="ui";minimapDragging=state=="minimap";dragging=state=="middle";edgeScrolling=state!="disabled";missionPanel=state=="menu"?"menu":"";
   Check(MissionEdgeVelocity(left,state!="focus",state=="minimized",state=="exit",state=="button")==Vector2.Zero,"suppression: "+state);
  }
  CancelCameraGesture();missionPanel="";edgeScrolling=true;
  camera=new(nativeData.Scenario.Map.Width*16,nativeData.Scenario.Map.Height*16);var start=camera;
  foreach(int hz in new[]{30,60,144}){camera=start;for(int i=0;i<hz;i++)ApplyEdgeScrolling(1d/hz,right,true,false,false,false);Check(Math.Abs(camera.X-start.X-720*size.Y/1080/zoom)<.04,"one second pan at "+hz+"Hz");}
  camera=new(-100,-100);ClampMissionCamera();var corner=camera;ApplyEdgeScrolling(.1,Vector2.Zero,true,false,false,false);Check(camera==corner,"upper left map clamp");
  camera=new(100000,100000);ClampMissionCamera();corner=camera;ApplyEdgeScrolling(.1,size-Vector2.One,true,false,false,false);Check(camera==corner,"lower right map clamp");
  await Send(Key.F);corner=camera;ApplyEdgeScrolling(.1,right,true,false,false,false);Check(camera==corner,"fit-map camera cannot pan beyond map");zoom=2;await Send(Key.Tab);
  // Selection origin is fixed on the map, even after camera pan and zoom.
  var anchor=NativePosition(commander)-new Vector2(45,45);var end=NativePosition(commander)+new Vector2(45,45);
  NativeInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=anchor*zoom+NativeOffset,Pressed=true});
  var savedAnchor=nativeDragStart;ApplyEdgeScrolling(.1,left,true,false,false,true);zoom=2.5f;ClampMissionCamera();
  Check(nativeSelecting&&nativeDragStart==savedAnchor&&edgeVelocity.X<0,"selection permits edge pan with fixed world anchor");
  missionPointer=end*zoom+NativeOffset;await UiCapture("edge-selection-panned");
  NativeInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=missionPointer,Pressed=false});
  Check(NativeSelected().Contains(commander),"selection release after pan and zoom includes original world rectangle");
  foreach(var key in new[]{Key.M,Key.A}){
   await Send(key);ApplyEdgeScrolling(.1,right,true,false,false,false);var point=NativeArea.GetCenter();var expected=NativeCell((point-NativeOffset)/zoom);
   await NativeMouse(MouseButton.Left,point);Check(orderPoint==new Vector2(expected.X*32+16,(nativeData.Scenario.Map.Height-1-expected.Z)*32+16),"armed "+key+" uses camera position at click");
  }
  await Send(Key.A);await Send(Key.Escape);Check(targetMode==""&&!nativeAttackMove,"target cancellation retained");
  var before=camera;await Send(Key.Right);Check(camera.X>before.X,"arrow navigation retained");
  before=camera;var at=NativeArea.GetCenter();NativeInput(new InputEventMouseButton{Position=at,ButtonIndex=MouseButton.Middle,Pressed=true});NativeInput(new InputEventMouseMotion{Position=at+new Vector2(40,0),Relative=new(40,0)});NativeInput(new InputEventMouseButton{Position=at+new Vector2(40,0),ButtonIndex=MouseButton.Middle,Pressed=false});Check(camera.X<before.X&&!dragging,"middle drag retained");
  await NativeMouse(MouseButton.Left,MiniMapRect.GetCenter());Check(camera.DistanceTo(new(nativeData.Scenario.Map.Width*16,nativeData.Scenario.Map.Height*16))<1,"minimap navigation retained");
  nativeSelecting=dragging=minimapDragging=uiPointerOwned=true;_Notification((int)NotificationWMWindowFocusOut);Check(!nativeSelecting&&!dragging&&!minimapDragging&&!uiPointerOwned&&edgeVelocity==Vector2.Zero,"focus loss clears gestures and scrolling");
  _Notification((int)NotificationWMMouseExit);Check(edgePointerExited,"pointer exit invalidates cached position");_Notification((int)NotificationWMMouseEnter);
  await Send(Key.O);await UiCapture("edge-options");await UiClick("edge-toggle");Check(!edgeScrolling,"options toggle off");await UiClick("edge+");Check(edgeSpeed==1.25f,"options speed adjustment");await UiClick("edge-toggle");await UiClick("edge-");
  foreach(float scale in new[]{.75f,1f,1.5f}){ui=scale;await UiCapture("edge-options-"+scale);Check(missionHits.All(h=>GetViewportRect().Encloses(h.Rect)),"camera options fit viewport at UI "+scale);}
  await Send(Key.Escape);ui=size.Y>1080?4/3f:1;zoom=2;await Send(Key.Tab);
  var trace=new List<object>();foreach(var p in new[]{left,right,top,bottom,new Vector2(0,0),size-Vector2.One}){
   for(int i=0;i<30;i++){ApplyEdgeScrolling(1d/30,p,true,false,false,false);trace.Add(new{point=new[]{p.X,p.Y},camera=new[]{camera.X,camera.Y},velocity=new[]{edgeVelocity.X,edgeVelocity.Y}});await ToSignal(GetTree().CreateTimer(1d/30),SceneTreeTimer.SignalName.Timeout);}
  }
  File.WriteAllText(Path.Combine(output,"controlled-camera-trace.json"),JsonSerializer.Serialize(trace));
  await Send(Key.R);Check(MissionEdgeVelocity(left,true,false,false,false)==Vector2.Zero,"briefing suppresses scrolling after restart");
  await Send(Key.Enter);nativeWorld.Deaths[0,73]=1;for(int i=0;i<24;i++){mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();}
  mission.SetClock(mission.Outcome!.Deadline+1);Check(mission.AtOutcome&&MissionEdgeVelocity(left,true,false,false,false)==Vector2.Zero,"actual outcome state suppresses edge scrolling (diagnostic transition)");await Send(Key.R);
  edgeScrolling=reload;edgeSpeed=reload?1:1.75f;SaveEdgeScrolling();
  Check(ArtworkPreference.LoadNavigation(artworkPreferencePath,out _)==(edgeScrolling,edgeSpeed),"navigation saved for next launch");
 }
}
