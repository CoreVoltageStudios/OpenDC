using Godot;
using DarkColony.Simulation;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task RealtimeSuite() {
  var started=Time.GetTicksMsec();
  async Task WaitTicks(long count){long end=simulation.Tick+count;while(simulation.Tick<end){if(Time.GetTicksMsec()-started>20000)throw new TimeoutException("Real-time smoke bounded at20 seconds");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
  await MousePlay(MouseButton.Left,Screen(simulation.Unit.Position));Check(selected,"real-time selection input");
  await MousePlay(MouseButton.Right,Screen(new(40.5,73.5)));await WaitTicks(80);await SavePlay("realtime-moving");
  Check(simulation.Unit.Moving&&simulation.Unit.Position.X>35.5,"normal process advances fixed-step simulation");
  await Send(Key.Space);long tick=simulation.Tick;var before=simulation.Unit.Position;
  await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);Check(simulation.Tick==tick&&simulation.Unit.Position==before,"real-time pause freezes simulation");await SavePlay("realtime-paused");
  await Send(Key.X);await Send(Key.Space);await WaitTicks(2);Check(!simulation.Unit.Moving&&simulation.Unit.Position==before,"paused stop processes on resume without drift");await SavePlay("realtime-stopped");
  await MousePlay(MouseButton.Right,Screen(new(38.5,73.5)));await WaitTicks(180);Check(!simulation.Unit.Moving&&simulation.Unit.Position==new Point(38.5,73.5),"real-time move arrives");await SavePlay("realtime-arrived");
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));GD.Print("MOVEMENT_REALTIME_PASS");GetTree().Quit();
 }
 async Task MoveAndArrive(Point target,string label,int limit=600) {
  await MousePlay(MouseButton.Right,Screen(target));PlayTick();await SavePlay(label+"-order");
  int ticks=0;while(simulation.Unit.Moving&&ticks<limit){await AdvancePlay(5,label);ticks+=5;}
  Check(simulation.Unit.Position==navigation.CellAt(target).Center&&!simulation.Unit.Moving,label+" arrived");await SavePlay(label+"-idle");
 }
 async Task NavigationSuite() {
  await MousePlay(MouseButton.Right,Screen(new(40.5,73.5)));Check(commandSequence==0&&!simulation.Unit.Moving,"unselected move ignored");
  await MousePlay(MouseButton.Left,Screen(simulation.Unit.Position));Check(selected,"left click selects");await SavePlay("demo-selected");
  await MousePlay(MouseButton.Right,new(30,30));Check(commandSequence==0&&selected,"UI right click consumed");
  await MousePlay(MouseButton.Left,PlayInfo.GetCenter());Check(selected&&commandSequence==0,"UI left click retains selection");
  await MoveAndArrive(new(40.5,73.5),"demo-open");
  await MoveAndArrive(new(40.5,71.5),"demo-approach");
  await MousePlay(MouseButton.Right,Screen(new(34.5,71.5)));PlayTick();
  Check(simulation.Unit.Route.Any(p=>p.Z!=71.5),"original PTH plant footprint forces detour");await SavePlay("demo-obstacle-order");
  for(int ticks=0;simulation.Unit.Moving&&ticks<600;ticks+=5)await AdvancePlay(5,"demo-obstacle");
  Check(simulation.Unit.Position==new Point(34.5,71.5),"obstacle detour arrived");await SavePlay("demo-obstacle-idle");
  await MousePlay(MouseButton.Right,Screen(new(40.5,73.5)));await AdvancePlay(60,"demo-first-order");
  await MousePlay(MouseButton.Right,Screen(new(34.5,74.5)));await AdvancePlay(45,"demo-replacement");
  Check(simulation.Unit.Destination==new Point(34.5,74.5),"moving route replaced through right click");
  var before=simulation.Unit.Position;await Send(Key.X);PlayTick();Check(simulation.Unit.Position==before&&!simulation.Unit.Moving,"X stops without teleport");await SavePlay("demo-stopped");
  await MoveAndArrive(new(34.5,73.5),"demo-final");
  File.WriteAllText(Path.Combine(output,"demo-end.json"),JsonSerializer.Serialize(new{lastFrame=playCaptureIndex-1,simulation.Tick}));

  before=simulation.Unit.Position;await MousePlay(MouseButton.Right,Screen(new(36.5,71.5)));PlayTick();
  Check(simulation.Receipts.Last().Result==OrderResult.Blocked&&simulation.Unit.Position==before,"original PTH blocked destination rejected");await SavePlay("blocked-feedback");
  await Send(Key.N);await SavePlay("static-blocking-overlay");await Send(Key.N);
  await MousePlay(MouseButton.Left,Screen(new(32.5,73.5)));Check(!selected,"empty ground deselects");
  await MousePlay(MouseButton.Right,Screen(new(40.5,73.5)));Check(!simulation.Unit.Moving,"unselected right click after deselection ignored");
  await MousePlay(MouseButton.Left,Screen(simulation.Unit.Position));Check(selected,"reselects");
  float originalZoom=zoom,originalUi=ui;var originalCamera=camera;
  await Send(Key.D);Check(camera.X>originalCamera.X,"camera pan via Godot keyboard input");
  await MousePlay(MouseButton.WheelUp,Screen(simulation.Unit.Position));Check(zoom>originalZoom,"wheel world zoom input");
  var uiButton=buttons.Single(b=>b.Action=="ui-");await MousePlay(MouseButton.Left,uiButton.Rect.GetCenter());
  Check(ui<originalUi&&zoom>originalZoom,"UI scale independent of world zoom");
  await MousePlay(MouseButton.Left,Screen(simulation.Unit.Position));Check(selected,"selection after pan zoom and UI scaling");
  await MousePlay(MouseButton.Right,Screen(new(35.5,73.5)));await Send(Key.Space);
  Check(paused,"pause input");long tick=simulation.Tick;ProcessPlay(.2);Check(simulation.Tick==tick,"paused host does not advance");
  await Send(Key.Space);Check(!paused,"resume input");await AdvancePlay(40,"transform-input");
  Check(simulation.Unit.Position==new Point(35.5,73.5),"pointer inverse transform target after pan zoom UI scaling");await SavePlay("transform-arrived");
  await MoveAndArrive(new(35.5,74.5),"north-direction");
  await MoveAndArrive(new(36.5,73.5),"southeast-direction");
  await Send(Key.Escape);Check(!selected,"Escape deselection input");
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
  File.WriteAllText(Path.Combine(output,"final-play-state.json"),JsonSerializer.Serialize(PlayState("final")));
  GD.Print("MOVEMENT_NAVIGATION_PASS");GetTree().Quit();
 }
}
