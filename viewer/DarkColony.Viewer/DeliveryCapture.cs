using Godot;
using DarkColony.Simulation;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task DeliverySuite() {
  await SaveCombat("delivery-ready");await SelectEncounter();await SquadMouse(MouseButton.Right,SquadInfo.GetCenter());Check(commandSequence==0,"UI blocks delivery world orders");
  await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(46.5,71.5)));
  int shotCaptures=0,deathCaptures=0;
  for(int i=0;i<2100;i++) {
   CombatTick();
   if(i%120==0)await SaveCombat("delivery-encounter");
   if(combat.Events.Any(e=>e.Kind=="Shot"&&e.Tick==combat.Tick)&&shotCaptures<6){await SaveCombat("delivery-launch");shotCaptures++;}
   if(combat.Events.Any(e=>e.Kind=="Death"&&e.Tick==combat.Tick)){await SaveCombat("delivery-death");deathCaptures++;}
   if(combat.Outcome==EncounterOutcome.Victory&&combat.Units.Where(u=>u.Alive&&u.Team==0).All(u=>u.Tactics!.Order==TacticalOrder.None))break;
  }
  Check(combat.Outcome==EncounterOutcome.Victory&&deathCaptures>=2,"travelling weapons complete encounter and resume");Check(combat.Events.Where(e=>e.Kind=="Impact").All(e=>combat.Events.Single(s=>s.Kind=="Shot"&&s.ShotId==e.ShotId).Tick<e.Tick),"rendered encounter damage follows launch on a later tick");
  Check(combat.Events.Any(e=>e.Kind=="Impact"&&e.Source<=4)&&combat.Events.Any(e=>e.Kind=="Impact"&&e.Source>=5),"both original weapon definitions deliver impacts");
  for(int i=0;i<90;i++)CombatTick();await SaveCombat("delivery-victory-cleanup");await Send(Key.R);Check(FreshSkirmish()&&combat.Projectiles.Length==0,"victory reset clears delivery");
  await SelectEncounter();await SquadMouse(MouseButton.Right,Screen(new(43.5,73.5)));
  for(int i=0;i<3600&&combat.Outcome==EncounterOutcome.Active;i++)CombatTick();Check(combat.Outcome==EncounterOutcome.Defeat,"passive defeat under new delivery");await SaveCombat("delivery-defeat");await Send(Key.R);Check(FreshSkirmish(),"defeat restart");
  await SelectEncounter();await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(46.5,71.5)));for(int i=0;i<300&&combat.Projectiles.Length==0;i++)CombatTick();Check(combat.Projectiles.Length>0,"pending launch reset fixture reached");await Send(Key.X);CombatTick();Check(combat.Units.Where(u=>u.Alive&&u.Team==0).All(u=>u.Target==null),"Godot stop cancels targeting while committed missiles remain independent");await Send(Key.R);Check(FreshSkirmish()&&combat.Projectiles.Length==0,"pending missile reset through Godot key");await SaveCombat("delivery-reset-ready");
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));GD.Print("DELIVERY_SUITE_PASS");GetTree().Quit();
 }
}
