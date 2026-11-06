using Godot;
using DarkColony.Simulation;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task TacticalSuite() {
  if(realtimeCheck) {
   ulong start=Time.GetTicksMsec();async Task WaitUntil(Func<bool> done){while(!done()){if(Time.GetTicksMsec()-start>40000){await SaveCombat("realtime-timeout");throw new TimeoutException($"Tactical realtime40 second bound at tick{combat.Tick}, {combat.Outcome}");}await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
   await SelectEncounter();await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(46.5,71.5)));await WaitUntil(()=>combat.Events.Any(e=>e.Kind=="Shot"));Check(combat.Units.Any(u=>u.Id>4&&u.Body.Position!=encounter.Spawns.Single(s=>s.Id==u.Id).Position),"real-time opponents move through simulation");await SaveCombat("realtime-engagement");
   await Send(Key.Space);long tick=combat.Tick;await ToSignal(GetTree().CreateTimer(.25),SceneTreeTimer.SignalName.Timeout);Check(combat.Tick==tick,"real-time tactical pause");await Send(Key.Space);
   await WaitUntil(()=>combat.Outcome==EncounterOutcome.Victory&&combat.Units.Where(u=>u.Team==0&&u.Alive).All(u=>u.Tactics!.Order==TacticalOrder.None));Check(combat.Events.Any(e=>e.Kind=="Resumed"&&e.Source<=4),"real-time victory and resumed destination arrival");await SaveCombat("realtime-resumed-arrival");await Send(Key.R);Check(combat.Tick<30&&combat.Events.All(e=>e.Kind=="Resumed")&&combat.Units.All(u=>u.Health==u.MaxHealth)&&combat.Units.All(u=>u.Target==null),"real-time tactical reset");await SaveCombat("realtime-reset");
  } else {
   await SaveCombat("initial-patrols");await Send(Key.A);Check(!attackMoveArmed,"attack-move requires selection");await SelectEncounter();
   await Send(Key.A);Check(attackMoveArmed,"A arms attack-move without panning");long sequence=commandSequence;await SquadMouse(MouseButton.Right,new(30,30));Check(commandSequence==sequence&&attackMoveArmed,"armed UI right-click cannot issue order");await Send(Key.Escape);Check(!attackMoveArmed&&selection.Count==4,"Escape disarms without losing selection");
   await SquadMouse(MouseButton.Left,buttons.Single(b=>b.Action=="attack-move").Rect.GetCenter());await SaveCombat("attack-move-armed");await SquadMouse(MouseButton.Left,Screen(new(46.5,71.5)));CombatTick();Check(!attackMoveArmed&&combat.Receipts.Last().Result=="AttackMoveAccepted"&&combat.Units.Where(u=>u.Team==0).All(u=>u.Tactics!.Destination!=null),"button then ground click issues retained group destinations");await CombatAdvance(36,"approach-input-test",12);
   await SquadMouse(MouseButton.Left,UnitScreen(1));await Send(Key.X);var p=combat.Unit(1).Body.Position;CombatTick();Check(combat.Unit(1).Body.Position==p&&combat.Unit(1).Target==null&&combat.Unit(1).Tactics!.Order==TacticalOrder.Stop,"input stop clears tactical destination and pursuit");await SaveCombat("stopped-subset");
   await SquadMouse(MouseButton.Right,Screen(new(36.5,73.5)));CombatTick();Check(combat.Unit(1).Tactics!.Order==TacticalOrder.Move,"ordinary ground move retained");
   await SquadMouse(MouseButton.Right,UnitScreen(5));CombatTick();Check(combat.Unit(1).Target==5&&combat.Unit(1).Tactics!.Order==TacticalOrder.Attack,"explicit designated attack retained");await SaveCombat("explicit-pursuit");
   await Send(Key.R);await SelectEncounter();await SaveCombat("demo-selected");await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(46.5,71.5)));
   for(int i=0;i<1800;i+=12){await CombatAdvance(12,"demo-engage-resume",12);if(combat.Outcome==EncounterOutcome.Victory&&combat.Units.Where(u=>u.Team==0&&u.Alive).All(u=>u.Tactics!.Order==TacticalOrder.None))break;}
   Check(combat.Outcome==EncounterOutcome.Victory,"two moving opponents defeated through attack-move input");Check(combat.Units.Where(u=>u.Team==0&&u.Alive).All(u=>u.Tactics!.Order==TacticalOrder.None),"survivors resume and reach retained destinations");Check(combat.Events.Where(e=>e.Kind=="Acquired"&&e.Source<=4).Select(e=>e.Target).Distinct().Count()==2,"rendered sequence acquires and reacquires both enemies");Check(combat.Events.Any(e=>e.Kind=="Resumed"&&e.Source<=4),"rendered resumption recorded");await SaveCombat("demo-arrived-after-victory");await CombatAdvance(90,"death-cleanup",30);
   await Send(Key.R);Check(combat.Tick==0&&combat.Events.Count==0&&combat.Units.All(u=>u.Health==u.MaxHealth)&&combat.Movement.Units.All(u=>u.ReservedTo==null),"reset removes stale targets/events/reservations");await SaveCombat("reset");
  }
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));GD.Print("TACTICAL_SUITE_PASS");GetTree().Quit();
 }
}
