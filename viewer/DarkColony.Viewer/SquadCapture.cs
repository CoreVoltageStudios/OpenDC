using Godot;
using DarkColony.Simulation;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 object SquadState(string label)=>new{label,squad.Tick,selection=selection.Order().ToArray(),paused,zoom,ui,camera=new[]{camera.X,camera.Y},viewport=new[]{GetViewportRect().Size.X,GetViewportRect().Size.Y},area=new[]{Area.Position.X,Area.Position.Y,Area.Size.X,Area.Size.Y},feedback,showPath,occupancy,drawOrder=DrawOrder().Select(u=>u.Id).ToArray(),units=squad.Units.Select(u=>new{state=u,anchor=new[]{BodyAnchor(u.Position).X,BodyAnchor(u.Position).Y},pose=SquadPose(u).Sequence,frame=SquadPose(u).FinFrame,cell=SquadPose(u).SpriteCell}),receipts=squad.Receipts};
 async Task SaveSquad(string label) {
  string name=$"squad-{squadCaptureIndex++:D4}";QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
  using var im=GetViewport().GetTexture().GetImage();if(im.SavePng(Path.Combine(output,name+".png"))!=Error.Ok)throw new IOException("Squad capture failed");
  File.WriteAllText(Path.Combine(output,name+".json"),JsonSerializer.Serialize(SquadState(label),new JsonSerializerOptions{WriteIndented=true}));
 }
 async void CaptureSquad(string label){try{await SaveSquad(label);}catch(Exception e){GD.PushError(e.ToString());}}
 Vector2 NativePoint(Vector2 p){var size=(Vector2)GetWindow().Size;var viewport=GetViewportRect().Size;float scale=Math.Min(size.X/viewport.X,size.Y/viewport.Y);return(size-viewport*scale)/2+p*scale;}
 async Task SquadMouse(MouseButton button,Vector2 position,bool shift=false) {
  position=NativePoint(position);Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=button,Position=position,Pressed=true,ShiftPressed=shift});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=button,Position=position,Pressed=false,ShiftPressed=shift});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 }
 async Task SquadBox(Vector2 a,Vector2 b,bool shift=false) {
  a=NativePoint(a);b=NativePoint(b);Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=a,Pressed=true,ShiftPressed=shift});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  Input.ParseInputEvent(new InputEventMouseMotion{Position=b,Relative=b-a,ButtonMask=MouseButtonMask.Left,ShiftPressed=shift});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  if(combatMode)await SaveCombat("selection-box");else await SaveSquad("selection-box");Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=b,Pressed=false,ShiftPressed=shift});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 }
 Vector2 UnitScreen(int id) {
  if(!skirmishMode||unitFamilies[id]!="GRAY")return Screen(squad.Unit(id).Position)-new Vector2(0,24*zoom);
  // Test aim point must be on actual visible original art, not TRSC's fixed torso
  // offset. Input still travels through Godot's mouse dispatch and normal hit test.
  var u=squad.Unit(id);var pose=SquadPose(u);var v=pose.Visual;var anchor=BodyAnchor(u.Position);var raster=visuals.Occlusion.ApplyGroundBody(v,(int)anchor.X,(int)anchor.Y,(int)anchor.Y+pose.BottomOffset);
  var candidates=Enumerable.Range(0,raster.Width*raster.Height).Where(i=>raster.Rgba[i*4+3]!=0).OrderBy(i=>Math.Pow(i%raster.Width-v.OriginX,2)+Math.Pow(i/raster.Width-v.OriginY+20,2));
  int pixel=candidates.First();var world=anchor-new Vector2(v.OriginX,v.OriginY)+new Vector2((pixel%raster.Width+.5f)*v.LogicalWidth/raster.Width,(pixel/raster.Width+.5f)*v.LogicalHeight/raster.Height);return Screen(ToLogical(world));
 }
 async Task SquadAdvance(int ticks,string label){for(int i=0;i<ticks;i++){SquadTick();if((i+1)%10==0)await SaveSquad(label);}}
 async Task SquadFinish(string label,int limit=1400){for(int i=0;squad.Units.Any(u=>u.State is TravelState.Moving or TravelState.Waiting)&&i<limit;i+=10)await SquadAdvance(10,label);Check(!squad.Units.Any(u=>u.State is TravelState.Moving or TravelState.Waiting),label+" bounded termination");await SaveSquad(label+"-end");}
 async Task SquadRealtime() {
  ulong start=Time.GetTicksMsec();async Task WaitTicks(int ticks){long end=squad.Tick+ticks;while(squad.Tick<end){if(Time.GetTicksMsec()-start>20000)throw new TimeoutException("Squad real-time timeout");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
  await SquadMouse(MouseButton.Left,UnitScreen(1));await SquadMouse(MouseButton.Right,Screen(new(41.5,69.5)));await WaitTicks(70);Check(squad.Unit(1).Moving,"real-time squad input movement");await SaveSquad("realtime-moving");
  await Send(Key.Space);long tick=squad.Tick;await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);Check(squad.Tick==tick,"real-time squad pause");
  await Send(Key.X);await Send(Key.Space);await WaitTicks(2);Check(squad.Unit(1).State==TravelState.Stopped,"real-time squad stop");await SaveSquad("realtime-stopped");
 }
 async Task SquadSuite() {
  if(realtimeCheck){await SquadRealtime();}
  else {
   await SquadMouse(MouseButton.Right,Screen(new(41.5,69.5)));Check(commandSequence==0,"squad move requires selection");
   await SquadMouse(MouseButton.Left,UnitScreen(1));Check(selection.SetEquals(new[]{1}),"single squad selection");
   await SquadMouse(MouseButton.Left,UnitScreen(2),true);Check(selection.SetEquals(new[]{1,2}),"Shift adds individual");
   await SquadMouse(MouseButton.Left,UnitScreen(1),true);Check(selection.SetEquals(new[]{2}),"Shift toggles individual off");
   await SquadBox(Screen(new(33,68.5)),Screen(new(35,67)));Check(selection.SetEquals(new[]{1,2}),"box selects two");
   await SquadBox(Screen(new(31,68.5)),Screen(new(33,67)),true);Check(selection.Count==4,"Shift box adds remaining units");
   await SquadMouse(MouseButton.Right,new(30,30));await SquadMouse(MouseButton.Left,SquadInfo.GetCenter());Check(selection.Count==4&&commandSequence==0,"squad UI consumes both mouse buttons");await SaveSquad("four-selected");
   await SquadMouse(MouseButton.Right,Screen(new(41.5,69.5)));SquadTick();Check(squad.Receipts.Last().Assignments.Select(a=>a.Destination).Distinct().Count()==4,"group assigned distinct destinations");
   Check(squad.Units.Count(u=>u.Route.Contains(new Point(37.5,67.5)))>=3,"three original narrow-cell routes; remaining unit may take valid detour");await SaveSquad("crossing-order");
   await SquadAdvance(80,"crossing-queue");await Send(Key.N);await SaveSquad("occupancy-reservations");await Send(Key.N);await SquadFinish("crossing");
   Check(squad.Units.All(u=>u.State==TravelState.Arrived),"original crossing group arrives");
   var stationary=squad.Units.Where(u=>u.Id>=3).Select(u=>u.Position).ToArray();
   await SquadMouse(MouseButton.Left,UnitScreen(1));await SquadMouse(MouseButton.Left,UnitScreen(2),true);await SquadMouse(MouseButton.Right,Screen(new(36.5,73.5)));await SquadAdvance(70,"subset-move");
   await SquadMouse(MouseButton.Left,UnitScreen(1));var before=squad.Unit(1).Position;var other=squad.Unit(2).Destination;await Send(Key.X);SquadTick();
   Check(squad.Unit(1).Position==before&&squad.Unit(1).State==TravelState.Stopped&&squad.Unit(2).Destination==other,"selected stop leaves unselected order intact");await SaveSquad("subset-stopped");
   await SquadMouse(MouseButton.Left,UnitScreen(2));await SquadMouse(MouseButton.Right,Screen(new(40.5,73.5)));await SquadAdvance(30,"replacement-order");Check(squad.Unit(2).Destination==new Point(40.5,73.5),"moving subset replacement");await SquadFinish("subset-finish");
   Check(squad.Units.Where(u=>u.Id>=3).Select(u=>u.Position).SequenceEqual(stationary),"unselected stationary units remain unchanged");
   await SquadMouse(MouseButton.Right,Screen(new(36.5,71.5)));SquadTick();Check(squad.Receipts.Last().Result==SquadOrderResult.BlockedTerrain,"squad blocked target feedback");await SaveSquad("blocked-feedback");
   await Send(Key.D);await SquadMouse(MouseButton.WheelUp,UnitScreen(2));await SquadMouse(MouseButton.Left,buttons.Single(b=>b.Action=="ui-").Rect.GetCenter());await SquadMouse(MouseButton.Left,UnitScreen(2));Check(selection.SetEquals(new[]{2}),"squad picking after camera zoom/UI changes");await SaveSquad("transformed-selection");
   await Send(Key.Escape);Check(selection.Count==0,"squad deselection");
  }
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));GD.Print("SQUAD_SUITE_PASS");GetTree().Quit();
 }
}
