using Godot;
using System.Text.Json;
namespace DarkColony.Viewer;

public partial class Viewer {
 bool edgeScrolling=true,edgePointerExited,edgeTrace;
 float edgeSpeed=1;double edgeTraceClock;
 Vector2 edgeVelocity;
 void PrepareEdgeScrolling(string[] args){
  var preference=ArtworkPreference.LoadNavigation(artworkPreferencePath,out var diagnostic);
  edgeScrolling=preference.Enabled;edgeSpeed=preference.Speed;
  if(diagnostic!=null){artworkPreferenceDiagnostic=diagnostic;GD.Print(diagnostic);}
  edgeTrace=args.Contains("--camera-trace");
 }
 void SaveEdgeScrolling(){
  SaveSettings(p=>ArtworkPreference.SaveNavigation(p,edgeScrolling,edgeSpeed),"Camera settings changed, but could not be saved.");
 }
 // Outer client-area rim only: never the internal battlefield/sidebar seam.
 // Height-based physical band/speed remain independent of interface scaling.
 static Vector2 EdgeDirection(Vector2 point,Vector2 size){
  if(!new Rect2(Vector2.Zero,size).HasPoint(point))return Vector2.Zero;
  float band=Math.Clamp(18*size.Y/1080,12,28);
  float Ramp(float distance){float t=Math.Clamp(1-distance/band,0,1);return t*t*(3-2*t);}
  var direction=new Vector2(Ramp(size.X-1-point.X)-Ramp(point.X),Ramp(size.Y-1-point.Y)-Ramp(point.Y));
  float strength=Math.Max(Math.Abs(direction.X),Math.Abs(direction.Y));
  return strength>0?direction.Normalized()*strength:Vector2.Zero;
 }
 Vector2 MissionEdgeVelocity(Vector2 point,bool focused,bool minimized,bool exited,bool buttonHeld){
  if(!edgeScrolling||focusedUi.Length>0||!focused||minimized||exited||MissionModal||uiPointerOwned||minimapDragging||dragging||
     (buttonHeld&&!nativeSelecting)||MiniMapRect.Grow(4*ui).HasPoint(point)||
     missionHits.Any(h=>h.Rect.Grow(4*ui).HasPoint(point)))return Vector2.Zero;
  return EdgeDirection(point,GetViewportRect().Size)*(720*GetViewportRect().Size.Y/1080*edgeSpeed/zoom);
 }
 void ApplyEdgeScrolling(double delta,Vector2 point,bool focused,bool minimized,bool exited,bool buttonHeld){
  edgeVelocity=MissionEdgeVelocity(point,focused,minimized,exited,buttonHeld);
  // Ignore exceptional stalled/focus-transition time; ordinary frame rates integrate seconds.
  camera+=edgeVelocity*(float)Math.Clamp(delta,0,.1);ClampMissionCamera();
 }
 void ProcessEdgeScrolling(double delta){
  UpdatePointerBoundary();
  // Scripted capture suites own their pointer; real OS input is verified separately.
  if(capturing){edgeVelocity=Vector2.Zero;return;}
  var window=GetWindow();
  // Use the same Godot-transformed client coordinates as selection/minimap/UI.
  // Global OS minus window position also includes non-client/DPI assumptions and
  // can disagree with input after a window/monitor/aspect transition.
  var point=GetViewport().GetMousePosition();
  bool focused=window.HasFocus(),minimized=window.Mode==Window.ModeEnum.Minimized;
  bool inside=GetViewportRect().HasPoint(point)&&!edgePointerExited;
  if(!focused||minimized||!inside)CancelCameraGesture();
  if(focused)missionPointer=point; // Also clear hover/custom cursor when entering a side bar.
  ApplyEdgeScrolling(delta,point,focused,minimized,!inside,Input.IsMouseButtonPressed(MouseButton.Left)||Input.IsMouseButtonPressed(MouseButton.Right)||Input.IsMouseButtonPressed(MouseButton.Middle));
  if(edgeTrace&&(edgeTraceClock+=delta)>=.05){edgeTraceClock=0;var local=(Vector2)(DisplayServer.MouseGetPosition()-DisplayServer.WindowGetPosition(window.GetWindowId()));File.AppendAllText(Path.Combine(output,"camera-trace.jsonl"),JsonSerializer.Serialize(new{ms=Time.GetTicksMsec(),nativeWorld.Tick,local=new[]{local.X,local.Y},viewportMouse=new[]{point.X,point.Y},final=GetViewport().GetFinalTransform().ToString(),screen=GetViewport().GetScreenTransform().ToString(),focused,minimized,inside,point=new[]{point.X,point.Y},camera=new[]{camera.X,camera.Y},velocity=new[]{edgeVelocity.X,edgeVelocity.Y},zoom,ui,edgeScrolling,edgeSpeed,missionPanel,missionBriefing,minimapDragging,uiPointerOwned,nativeSelecting,dragging,targetMode,selection=NativeSelected().Select(e=>e.Slot).ToArray()})+System.Environment.NewLine);}
 }
 void CancelCameraGesture(){edgeVelocity=Vector2.Zero;pressedUi="";uiPointerOwned=minimapDragging=nativeSelecting=dragging=false;}
}

