using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task SessionClick(string id){
  QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
  var point=NativeWindowPoint(missionHits.Single(h=>h.Id==id).Rect.GetCenter());
  Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=point,Pressed=true});Input.FlushBufferedEvents();
  if(pressedUi!=id)throw new InvalidOperationException($"Fixture press {id} reached {pressedUi} in {missionPanel}");
  Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=point,Pressed=false});Input.FlushBufferedEvents();
  await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
 }
 async Task VerifySessionPolish(){
  Check(!godMode,"God mode starts off on every process");
  Check(!ArtworkPreference.LoadEnhancedVideo(artworkPreferencePath),"Legacy video setting is ignored");
  if(missionSuite=="session-reload"){OpenMissionPanel("campaign");await MenuCapture("restart-campaign");Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:")&&h.Enabled)==1,"Temporary unlocks did not persist");return;}
  foreach(int fps in new[]{5,30,60,144}){
   var sum=Vector2.Zero;for(int i=0;i<fps;i++)sum+=KeyboardPan(Vector2.Right,1d/fps,2);
   Check(Math.Abs(sum.X-300)<.01,"Frame-rate independent camera "+fps);
  }
  Check(Math.Abs(KeyboardPan(Vector2.One,.02,2).Length()-KeyboardPan(Vector2.Right,.02,2).Length())<.001,"No diagonal boost");
  Check(KeyboardPan(Vector2.Zero,.02,2)==Vector2.Zero,"No release inertia");
  await SessionClick("options");Check(!missionHits.Any(h=>h.Id.StartsWith("video-")),"Video selector removed");await SessionClick("playtesting");await SessionClick("god-mode");Check(godMode,"Options toggles god mode");await MenuCapture("god-options");CloseRelayPanel();CloseRelayPanel();
  foreach(int n in Enumerable.Range(0,OriginalCampaign.SupportedTaarMissions)){
   OpenMissionPanel("campaign");await MenuCapture("god-campaign-"+n);Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:")&&h.Enabled)==OriginalCampaign.SupportedTaarMissions,"All implemented missions temporarily available");
   await SessionClick("campaign-start:"+n);Check(nativeWorld.GodMode&&mission.CampaignIndex==n,"God mode crosses mission change "+n);ReturnMainMenu();
  }
  var data=PersistenceData("SCENARIO/ALIEN/ALIEN02.SCN");InstallMission(data,new OriginalMission(data,0),false);AdvanceInterference(250);
  // Directly exercise the shared hit path on a unit, a building and a foreign unit.
  var hit=typeof(OriginalWorld).GetMethod("ApplyDamage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
  var shooter=nativeWorld.Entities.First(e=>e.Owner==1);
  foreach(var target in new[]{nativeWorld.Entities.First(e=>e.Owner==0&&e.Type==8),nativeWorld.Teams[0].Cities[0].Entity!}){int hp=target.Health;hit.Invoke(nativeWorld,new object[]{shooter,target,100});Check(target.Health==hp,"Player damage prevented "+target.Type);}
  int foreign=shooter.Health;hit.Invoke(nativeWorld,new object[]{nativeWorld.Teams[0].Cities[0].Entity!,shooter,1});Check(shooter.Health==foreign-1,"Foreign units remain vulnerable");
  SaveMissionSlot(0);CloseRelayPanel();godMode=false;ApplySessionPlaytesting();LoadMissionSlot(0);Check(!nativeWorld.GodMode,"Save does not persist invulnerability");
  var unit=nativeWorld.Entities.First(e=>e.Owner==0&&e.Type==8);int health=unit.Health;hit.Invoke(nativeWorld,new object[]{nativeWorld.Entities.First(e=>e.Owner==1),unit,1});Check(unit.Health==health-1,"Off immediately restores damage");
  nativeWorld.Deaths[1,86]=6;AdvanceInterference(24);mission.SetClock(mission.Outcome!.Deadline+1);Check(mission.AtOutcome&&mission.ResultProfile!=null,"Controlled completion has a real result to persist");RecordCampaign();Check(!File.Exists(CampaignPath),"Completion after disabling god mode does not write campaign progress");missionOutcomeShown=true;PlayOutcomeMedia();await ToSignal(GetTree().CreateTimer(.8),SceneTreeTimer.SignalName.Timeout);Check(missionVideo is {Enhanced:false,Frame:>0},"Actual outcome route ignores stale enhanced files");ReleaseMissionVideo();
  ReturnMainMenu();OpenMissionPanel("campaign");await MenuCapture("normal-campaign");Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:")&&h.Enabled)==1,"Off restores normal mission access");
  InstallMission(data,new OriginalMission(data,0),false);AdvanceInterference(250);
  foreach(int aspect in new[]{0,16,21})foreach(float scale in new[]{.75f,1f,1.5f}){
   maximumAspect=aspect;ApplyAspectLimit();ui=scale;
   foreach(string panel in new[]{"menu","options","playtesting","save","load"}){OpenMissionPanel(panel);await UiCapture($"polish-{aspect}-{scale}-{panel}");RelayLayout(panel);CloseRelayPanel();missionPanel="";}
  }
  await VerifyCameraMotion();
  foreach(string name in new[]{"avhd4","scrub"}){
   var movie=new OriginalMovie();movie.Load(Path.Combine(root,"generated/original-video/v1",name+".odcv"));AddChild(movie);movie.Size=GetViewportRect().Size;movie.Play();await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);Check(movie.Frame>0&&!movie.Enhanced,"Original movie advances "+name);await UiCapture("original-"+name);RemoveChild(movie);movie.Free();
  }
  godMode=true;ApplySessionPlaytesting();ReturnMainMenu();await MenuCapture("god-before-exit");
 }
 async Task VerifyCameraMotion(){
  if(mainMenu){var data=PersistenceData("SCENARIO/ALIEN/ALIEN02.SCN");InstallMission(data,new OriginalMission(data,0),false);AdvanceInterference(250);}
  ui=1;maximumAspect=16;ApplyAspectLimit();ResetMissionCamera();camera=new(nativeData.Scenario.Map.Width*16,nativeData.Scenario.Map.Height*16);ClampMissionCamera();focusedUi="";missionPanel="";
  Input.ParseInputEvent(new InputEventKey{Keycode=Key.Right,Pressed=true});
  var motion=new List<object>();var before=camera;
  for(int i=0;i<30;i++){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);motion.Add(new{ms=Time.GetTicksUsec(),camera.X,camera.Y,key=Input.IsKeyPressed(Key.Right),MissionModal,focusedUi,dragging,minimapDragging,uiPointerOwned});}
  Input.ParseInputEvent(new InputEventKey{Keycode=Key.Right,Pressed=false});Input.FlushBufferedEvents();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);var stopped=camera;await ToSignal(GetTree().CreateTimer(.15),SceneTreeTimer.SignalName.Timeout);
  File.WriteAllText(Path.Combine(output,"pan-result.json"),JsonSerializer.Serialize(new{before=new[]{before.X,before.Y},stopped=new[]{stopped.X,stopped.Y},camera=new[]{camera.X,camera.Y},focus=GetWindow().HasFocus()}));
  File.WriteAllText(Path.Combine(output,"camera-frames.json"),JsonSerializer.Serialize(motion));
  Check(camera==stopped&&stopped.X>before.X,"Held Godot key pans continuously and release stops");
  File.WriteAllText(Path.Combine(output,"camera-frames.json"),JsonSerializer.Serialize(motion));
  OpenMissionPanel("menu");before=camera;Input.ParseInputEvent(new InputEventKey{Keycode=Key.Right,Pressed=true});await ToSignal(GetTree().CreateTimer(.15),SceneTreeTimer.SignalName.Timeout);Input.ParseInputEvent(new InputEventKey{Keycode=Key.Right,Pressed=false});Check(camera==before,"Menu focus blocks arrow camera");CloseRelayPanel();
  var field=new LineEdit();AddChild(field);field.GrabFocus();before=camera;Input.ParseInputEvent(new InputEventKey{Keycode=Key.Left,Pressed=true});await ToSignal(GetTree().CreateTimer(.1),SceneTreeTimer.SignalName.Timeout);Input.ParseInputEvent(new InputEventKey{Keycode=Key.Left,Pressed=false});Input.FlushBufferedEvents();Check(camera==before,"Text entry owns arrows");RemoveChild(field);field.Free();
  float oldZoom=zoom;await Send(Key.Plus);Check(zoom>oldZoom,"Zoom preserved");await Send(Key.Minus);
  before=camera;var at=NativeArea.GetCenter();NativeInput(new InputEventMouseButton{Position=at,ButtonIndex=MouseButton.Middle,Pressed=true});NativeInput(new InputEventMouseMotion{Position=at+new Vector2(30,0),Relative=new(30,0)});NativeInput(new InputEventMouseButton{Position=at,ButtonIndex=MouseButton.Middle,Pressed=false});Check(camera.X<before.X&&!dragging,"Middle drag preserved");
  camera=new(-10000,10000);ClampMissionCamera();Check(camera.X>=0&&camera.Y<=nativeData.Scenario.Map.Height*32,"Map boundary clamp preserved");ResetMissionCamera();

 }
}
