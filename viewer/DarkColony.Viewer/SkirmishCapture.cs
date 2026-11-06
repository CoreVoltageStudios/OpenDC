using Godot;
using DarkColony.Simulation;
using DarkColony.Assets;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 TrooperPose? grayCapturePose;
 bool FreshSkirmish()=>combat.Tick==0&&combat.Outcome==EncounterOutcome.Active&&combat.Events.Count==0&&combat.Receipts.Count==0&&selection.Count==0&&combat.Units.Length==6&&combat.Units.All(u=>u.Alive&&u.Health==u.MaxHealth&&u.Target==null&&u.LastShotTick<0)&&squad.Units.All(u=>u.ReservedTo==null);
 async Task SkirmishSuite() {
  // Explicit presentation-only overlap fixture, not NPC movement or gameplay state.
  // Start a separate simulation at a permitted canopy cell; never teleport a unit.
  var overlap=encounter with{Spawns=encounter.Spawns.Select(s=>s.Id==5?s with{Position=new(36.5,72.5)}:s).ToArray(),Patrols=Array.Empty<PatrolDefinition>()};combat=overlap.Create(navigation,movementSettings);squad=combat.Movement;
  for(int i=0;i<8;i++){grayCapturePose=combatClips[5].Locomotion.Resolve(7,true,i);await SaveCombat("gray-foreground-pose-fixture");}grayCapturePose=null;ResetCombat();
  await SaveCombat("original-gray-patrol");Check(unitFamilies[5]=="GRAY"&&CombatPose(combat.Unit(5).Body).Sequence.StartsWith("GRAY"),"original Martian adapter active; no TRSC stand-in");
  await SquadMouse(MouseButton.Right,UnitScreen(5));Check(commandSequence==0,"enemy attack requires player selection");await SquadMouse(MouseButton.Left,UnitScreen(5));Check(selection.Count==0,"Martian cannot be player-selected");await SelectEncounter();
  await SquadMouse(MouseButton.Right,SquadInfo.GetCenter());Check(selection.Count==4&&commandSequence==0,"skirmish UI cannot issue world command");await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(46.5,71.5)));await CombatAdvance(120,"input-approach",15);
  await SquadMouse(MouseButton.Left,UnitScreen(1));await Send(Key.X);var p=combat.Unit(1).Body.Position;CombatTick();Check(combat.Unit(1).Body.Position==p&&combat.Unit(1).Target==null&&combat.Unit(1).Tactics!.Order==TacticalOrder.Stop,"stop interrupts skirmish pursuit via Godot input");await SaveCombat("input-stop");
  await SquadMouse(MouseButton.Right,UnitScreen(5));CombatTick();Check(combat.Unit(1).Target==5&&combat.Unit(1).Tactics!.Order==TacticalOrder.Attack,"right-clicking original Martian issues designated attack");await SaveCombat("input-explicit-attack");
  await Send(Key.R);Check(FreshSkirmish(),"restart interrupted attack creates six clean bodies");await SelectEncounter();await SaveCombat("demo-selected");await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(46.5,71.5)));
  for(int i=0;i<1800;i+=8){await CombatAdvance(8,"demo-engage-resume",8);if(combat.Outcome==EncounterOutcome.Victory&&combat.Units.Where(u=>u.Team==0&&u.Alive).All(u=>u.Tactics!.Order==TacticalOrder.None))break;}
  Check(combat.Outcome==EncounterOutcome.Victory&&combat.Units.Where(u=>u.Team==7).All(u=>!u.Alive),"rendered complete encounter defeats original Gray patrol");Check(combat.Units.Where(u=>u.Team==0&&u.Alive).All(u=>u.Tactics!.Order==TacticalOrder.None),"surviving squad resumes and arrives");await CombatAdvance(90,"demo-cleanup",15);Check(DrawOrder().All(u=>combat.Unit(u.Id).Alive)&&combat.Units.Where(u=>u.Alive).All(u=>u.Target==null),"victory removes dead art, occupancy and target references");await SaveCombat("demo-victory");
  await Send(Key.R);Check(FreshSkirmish(),"R restart after victory");await SaveCombat("victory-reset");await SelectEncounter();await SquadMouse(MouseButton.Right,Screen(new(43.5,73.5)));CombatTick();
  for(int i=0;combat.Outcome==EncounterOutcome.Active&&i<3600;i+=30)await CombatAdvance(30,"defeat-encounter",30);
  Check(combat.Outcome==EncounterOutcome.Defeat&&selection.Count==0&&combat.Events.All(e=>e.Kind!="Shot"||e.Source>4),"ordinary move does not auto-attack; Martian patrol defeats passive squad");await CombatAdvance(90,"defeat-cleanup",15);Check(DrawOrder().All(u=>u.Id>4)&&squad.Units.All(u=>u.Id>4),"defeat clears friendly bodies and reservations");await SaveCombat("defeat-outcome");
  await SquadMouse(MouseButton.Left,buttons.Single(b=>b.Action=="reset").Rect.GetCenter());Check(FreshSkirmish(),"restart button after defeat");await SaveCombat("defeat-reset");
  for(int i=0;i<4;i++){await CombatAdvance(20,"restart-patrol",20);await Send(Key.R);Check(FreshSkirmish(),"repeated restart "+(i+1)+" has no stale units/events/reservations");}await SaveCombat("final-ready");
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));GD.Print("SKIRMISH_SUITE_PASS");GetTree().Quit();
 }
}
