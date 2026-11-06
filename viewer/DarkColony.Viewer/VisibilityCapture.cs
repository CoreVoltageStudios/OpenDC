using Godot;
using DarkColony.Simulation;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task FogSuite() {
  if(realtimeCheck){await FogRealtime();return;}
  var initial=combat.Visibility!.Snapshot(0);await SaveCombat("fog-unexplored-start");
  Check(!PresentedUnits().Any(u=>u.Team!=0)&&!DrawOrder().Any(u=>u.Id>4),"hidden enemies absent from HUD and draw order");
  Check(PickSquad(UnitScreen(5))!=5,"hidden enemy excluded from pixel and proximity picking");
  await SquadMouse(MouseButton.Left,UnitScreen(5));Check(selection.Count==0,"hidden enemy click cannot select");
  await SelectEncounter();await SquadMouse(MouseButton.Right,UnitScreen(5));CombatTick();Check(combat.Receipts.Last().Result!="AttackAccepted"&&combat.Units.Where(u=>u.Team==0).All(u=>u.Target==null),"right click hidden enemy becomes ordinary ground movement");
  await Send(Key.R);int sounds=soundDispatches,presented=effectsPresented;PresentCombatEvent(new(combat.Tick,"Shot",5,1,100,999,combat.Unit(5).Body.Position),999);PresentCombatEvent(new(combat.Tick,"Death",0,5,0),1000);
  Check(soundDispatches==sounds&&effectsPresented==presented&&observedShots.Count==0,"unobserved firing/death events produce no sound, effects or replay entries");
  await Send(Key.O);await Send(Key.N);await Send(Key.F);Check(PresentedUnits().All(u=>u.Team==0),"normal path/occupancy diagnostics retain hidden-unit filtering");await SaveCombat("fog-normal-diagnostics");await Send(Key.O);await Send(Key.N);
  await Send(Key.F8);Check(omniscient&&PresentedUnits().Length==6,"F8 explicitly exposes omniscient diagnostic view");await SaveCombat("omniscient-initial");await Send(Key.F8);Check(!omniscient&&!combat.CanSeeUnit(0,5),"diagnostic toggle does not change authoritative sight");
  await Send(Key.R);await SelectEncounter();await Send(Key.H);CombatTick();await SaveCombat("fog-hold");await Send(Key.J);CombatTick();Check(combat.Units.Where(u=>u.Team==0).All(u=>u.Target==null&&u.Tactics!.Order==TacticalOrder.Guard),"hold/guard cannot acquire hidden patrol");await SaveCombat("fog-guard");
  await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(48.5,71.5)));bool revealed=false;
  for(int i=0;i<2400;i++) {
   CombatTick();if(!revealed&&PresentedUnits().Any(u=>u.Team!=0&&u.Alive)){revealed=true;await SaveCombat("fog-first-reveal");}
   if(i%180==0)await SaveCombat("fog-encounter");
   if(combat.Events.Any(e=>e.Kind=="Death"&&e.Tick==combat.Tick))await SaveCombat("fog-visible-death");
   if(combat.Outcome==EncounterOutcome.Victory&&combat.Units.Where(u=>u.Alive&&u.Team==0).All(u=>u.Tactics!.Order==TacticalOrder.None))break;
  }
  Check(revealed&&combat.Outcome==EncounterOutcome.Victory&&combat.Units.Where(u=>u.Alive&&u.Team==0).All(u=>u.Tactics!.Order==TacticalOrder.None),"movement reveals opponents, completes encounter and resumes to arrival");
  for(int i=0;i<90;i++)CombatTick();camera=ToPixel(new(40.5,73));showRange=false;
  Check(combat.Visibility!.Snapshot(0).Contains(TerrainKnowledge.Explored)&&combat.Visibility.Snapshot(0).Contains(TerrainKnowledge.Unexplored),"actual encounter leaves explored memory and unrevealed terrain");await SaveCombat("fog-memory");await Send(Key.F8);await SaveCombat("omniscient-same-state");await Send(Key.F8);
  await Send(Key.R);Check(initial.SequenceEqual(combat.Visibility!.Snapshot(0))&&combat.Projectiles.Length==0&&observedShots.Count==0&&presentationEvents.Count==0&&!omniscient,"reset restores exact initial exploration and clears presentation");await SaveCombat("fog-reset");
  await SelectEncounter();await SquadMouse(MouseButton.Right,Screen(new(43.5,73.5)));for(int i=0;i<3600&&combat.Outcome==EncounterOutcome.Active;i++)CombatTick();Check(combat.Outcome==EncounterOutcome.Defeat,"passive exploration approach can lose");await SaveCombat("fog-defeat");
  Check(!combat.Visibility!.Snapshot(0).Contains(TerrainKnowledge.Visible)&&PresentedUnits().Length==0,"dead squad loses all live sight and unseen corpse information");
  await Send(Key.R);Check(initial.SequenceEqual(combat.Visibility!.Snapshot(0)),"defeat reset clears explored history");await SaveCombat("fog-final-ready");
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));GD.Print("FOG_SUITE_PASS");GetTree().Quit();
 }
 async Task FogRealtime() {
  ulong start=Time.GetTicksMsec();await SelectEncounter();await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(48.5,71.5)));bool pausedOnce=false,revealed=false;
  while(combat.Outcome==EncounterOutcome.Active) {
   if(Time.GetTicksMsec()-start>55000){await SaveCombat("fog-realtime-timeout");throw new TimeoutException("Fog realtime55 seconds");}
   if(!revealed&&PresentedUnits().Any(u=>u.Alive&&u.Team!=0)){revealed=true;await SaveCombat("realtime-fog-reveal");}
   if(!pausedOnce&&combat.Tick>180){await Send(Key.H);await Send(Key.Space);long tick=combat.Tick;await ToSignal(GetTree().CreateTimer(.25),SceneTreeTimer.SignalName.Timeout);Check(combat.Tick==tick,"normal Process pause freezes sight and combat");await SaveCombat("realtime-hold-pause");await Send(Key.Space);await Send(Key.J);await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);await SaveCombat("realtime-guard");await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(48.5,71.5)));pausedOnce=true;}
   await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  }
  Check(revealed&&combat.Outcome==EncounterOutcome.Victory,"normal Process fog encounter reveals enemies and reaches victory");await SaveCombat("realtime-fog-victory");await Send(Key.R);Check(combat.Tick<30&&!combat.CanSeeUnit(0,5)&&combat.Projectiles.Length==0&&combat.Units.All(u=>u.Health==u.MaxHealth&&u.Target==null),"normal Process reset restores fresh unseen patrol");await SaveCombat("realtime-fog-reset");
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));GD.Print("FOG_REALTIME_PASS");GetTree().Quit();
 }
}
