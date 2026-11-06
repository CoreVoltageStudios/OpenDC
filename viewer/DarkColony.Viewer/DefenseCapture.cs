using Godot;
using DarkColony.Simulation;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task GuardSuite() {
  await SelectEncounter();await Send(Key.V);await Send(Key.H);CombatTick();
  var homes=combat.Units.Where(u=>u.Team==0).ToDictionary(u=>u.Id,u=>u.Body.Position);
  for(int i=0;i<90;i++)CombatTick();
  Check(showRange&&combat.Units.Where(u=>u.Team==0).All(u=>u.Tactics!.Order==TacticalOrder.Hold&&u.Body.Position==homes[u.Id]),"H holds exact positions; V selects one actual Euclidean range");await SaveCombat("hold-stationary-outside-range");
  await Send(Key.R);await SelectEncounter();await SquadMouse(MouseButton.Right,Screen(new(38.5,73.5)));for(int i=0;i<100;i++)CombatTick();
  homes=combat.Units.Where(u=>u.Team==0).ToDictionary(u=>u.Id,u=>u.Body.Position);await Send(Key.H);CombatTick();
  for(int i=0;i<300;i++){CombatTick();if(i%75==0)await SaveCombat("hold-stationary-engagement");}
  Check(combat.Units.Where(u=>u.Team==0&&u.Alive).All(u=>u.Body.Position==homes[u.Id])&&combat.Events.Any(e=>e.Kind=="Shot"&&e.Source<=4),"hold interrupts mid-cell movement and fires without pursuit");
  // Separate initialized demonstration fixture: one low-health non-retaliating Gray.
  // No live body repositioning; the ordinary encounter is restored for attack-move.
  var normal=encounter;encounter=encounter with {Spawns=encounter.Spawns.Select(s=>s.Id==5?s with {Position=new Point(39.5,73.5)}:s).ToArray(),Definitions=encounter.Definitions.Select(d=>d.Id=="martian.gray.base"?d with{MaxHealth=100}:d).ToArray(),Patrols=Array.Empty<PatrolDefinition>()};
  ResetCombat();await SquadMouse(MouseButton.Left,UnitScreen(1));await Send(Key.J);CombatTick();await SaveCombat("guard-controlled-home-and-leash");
  bool departed=false;for(int i=0;i<500;i++){CombatTick();departed|=combat.Unit(1).Body.Position.Distance(encounter.Spawns[0].Position)>.1;if(i%30==0)await SaveCombat("guard-controlled-engage-return");if(combat.Events.Any(e=>e.Kind=="GuardReturned"))break;}
  Check(departed&&combat.Events.Any(e=>e.Kind=="GuardReturned")&&combat.Unit(1).Body.Position==encounter.Spawns[0].Position,"guard visibly pursues and returns to recorded home");await SaveCombat("guard-returned");encounter=normal;
  await Send(Key.R);await SelectEncounter();await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(46.5,71.5)));
  for(int i=0;i<2100;i++){CombatTick();if(i%180==0)await SaveCombat("attack-move-engage-continue");if(combat.Outcome==EncounterOutcome.Victory&&combat.Units.Where(u=>u.Alive&&u.Team==0).All(u=>u.Tactics!.Order==TacticalOrder.None))break;}
  Check(combat.Outcome==EncounterOutcome.Victory&&combat.Events.Any(e=>e.Kind=="Resumed"&&e.Source<=4),"attack-move continues to destination instead of guard home");await SaveCombat("attack-move-arrived");
  await Send(Key.R);Check(combat.Tick==0&&combat.Events.Count==0&&combat.Projectiles.Length==0&&combat.Units.All(u=>u.Tactics!.GuardAnchor==null&&u.Target==null)&&squad.Units.All(u=>u.ReservedTo==null),"reset clears defensive orders, anchors, targets and reservations");await SaveCombat("defense-reset");
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));GD.Print("DEFENSE_SUITE_PASS");GetTree().Quit();
 }
}
