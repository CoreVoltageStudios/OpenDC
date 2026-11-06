using Godot;
using DarkColony.Simulation;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 object CombatState(string label)=>new{label,diagnosticExport="Omniscient verification state; not the player HUD",omniscient,presentedIds=PresentedUnits().Select(u=>u.Id).ToArray(),visibility=combat.Visibility?.Snapshot(combat.PlayerTeam),combat.Tick,combat.Outcome,reset=combatResetCount,selection=selection.Order().ToArray(),paused,zoom,ui,camera=new[]{camera.X,camera.Y},viewport=new[]{GetViewportRect().Size.X,GetViewportRect().Size.Y},area=new[]{Area.Position.X,Area.Position.Y,Area.Size.X,Area.Size.Y},feedback,showRange,showPath,occupancy,soundDispatches,drawOrder=DrawOrder().Select(u=>u.Id).ToArray(),units=combat.Units.Select(u=>new{state=u,anchor=new[]{BodyAnchor(u.Body.Position).X,BodyAnchor(u.Body.Position).Y},pose=CombatPose(u.Body).Sequence,frame=CombatPose(u.Body).FinFrame,cell=CombatPose(u.Body).SpriteCell,opacity=CombatOpacity(u.Id)}),projectiles=combat.Projectiles,events=combat.Events,receipts=combat.Receipts};
 async Task SaveCombat(string label) {
  string name=$"combat-{combatCaptureIndex++:D4}";QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
  using var im=GetViewport().GetTexture().GetImage();if(im.SavePng(Path.Combine(output,name+".png"))!=Error.Ok)throw new IOException("Combat capture failed");
  File.WriteAllText(Path.Combine(output,name+".json"),JsonSerializer.Serialize(CombatState(label),new JsonSerializerOptions{WriteIndented=true}));
 }
 async void CaptureCombat(string label){try{await SaveCombat(label);}catch(Exception e){GD.PushError(e.ToString());}}
 async Task CombatAdvance(int ticks,string label,int stride=6){for(int i=0;i<ticks;i++){CombatTick();if((i+1)%stride==0)await SaveCombat(label);}}
 async Task SelectEncounter(){await SquadBox(Screen(new(32.8,75.5)),Screen(new(35.2,73)));Check(selection.SetEquals(new[]{1,2,3,4}),"combat box selects only friendly squad");}
 async Task CombatRealtime() {
  ulong start=Time.GetTicksMsec();async Task WaitTicks(int ticks){long end=combat.Tick+ticks;while(combat.Tick<end){if(Time.GetTicksMsec()-start>30000)throw new TimeoutException("Combat real-time bounded30 seconds");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
  await SelectEncounter();await SquadMouse(MouseButton.Right,UnitScreen(5));await WaitTicks(240);Check(combat.Events.Any(e=>e.Kind=="Shot"),"normal Process produces combat shots from Godot input");await SaveCombat("realtime-fire");
  await Send(Key.Space);long tick=combat.Tick;await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);Check(combat.Tick==tick,"real-time pause freezes damage/cooldown clock");await SaveCombat("realtime-pause");await Send(Key.Space);
  while(combat.Outcome==EncounterOutcome.Active)await WaitTicks(10);Check(combat.Outcome==EncounterOutcome.Victory,"normal Process encounter victory");await WaitTicks(90);await SaveCombat("realtime-victory");await Send(Key.R);Check(combat.Outcome==EncounterOutcome.Active&&combat.Units.All(u=>u.Health==800)&&selection.Count==0,"real-time reset restores encounter");await SaveCombat("realtime-reset");
 }
 async Task CombatSuite() {
  if(fogCapture){await FogSuite();return;}
  if(guardCapture){await GuardSuite();return;}
  if(effectsCapture){await EffectsSuite();return;}
  if(deliveryCapture){await DeliverySuite();return;}
  if(skirmishMode&&!realtimeCheck){await SkirmishSuite();return;}
  if(tacticalMode){await TacticalSuite();return;}
  if(realtimeCheck)await CombatRealtime();
  else {
   await SaveCombat("initial");await SquadMouse(MouseButton.Right,UnitScreen(5));Check(commandSequence==0,"combat attack requires selection");
   await SquadMouse(MouseButton.Left,UnitScreen(5));Check(selection.Count==0,"enemy cannot be player-selected");await SelectEncounter();await SaveCombat("selected");
   await SquadMouse(MouseButton.Right,new(30,30));await SquadMouse(MouseButton.Left,SquadInfo.GetCenter());Check(selection.Count==4&&commandSequence==0,"combat UI isolates selection and orders");
   await SquadMouse(MouseButton.Right,UnitScreen(5));CombatTick();Check(combat.Units.Where(u=>u.Team==0).All(u=>u.Target==5)&&combat.Receipts.Last().Result=="AttackAccepted","enemy right click dispatches attack with distinct firing positions");await SaveCombat("attack-order");
   await CombatAdvance(30,"approach-test");await SquadMouse(MouseButton.Left,UnitScreen(1));var position=combat.Unit(1).Body.Position;await Send(Key.X);CombatTick();Check(combat.Unit(1).Body.Position==position&&combat.Unit(1).Target==null&&combat.Unit(2).Target==5,"input subset stop cancels only selected attack");await SaveCombat("subset-stop");
   await SquadMouse(MouseButton.Right,Screen(new(36.5,73.5)));CombatTick();Check(combat.Unit(1).Activity==CombatActivity.Moving&&combat.Unit(1).Target==null,"ground right click remains a movement order");await SaveCombat("ground-replacement");
   await SquadMouse(MouseButton.Left,buttons.Single(b=>b.Action=="reset").Rect.GetCenter());Check(combat.Tick==0&&combat.Units.All(u=>u.Health==800)&&selection.Count==0,"restart button resets all encounter state");await SaveCombat("demo-start");
   await SelectEncounter();await SaveCombat("demo-selected");await SquadMouse(MouseButton.Right,UnitScreen(5));
   for(int i=0;combat.Outcome==EncounterOutcome.Active&&i<1000;i+=6)await CombatAdvance(6,"demo-encounter");
   Check(combat.Outcome==EncounterOutcome.Victory,"complete encounter reaches victory through attack input");Check(combat.Events.Count(e=>e.Kind=="Death"&&e.Target==5)==1&&!squad.Units.Any(u=>u.Id==5),"enemy death once releases physical occupancy");Check(combat.Units.Where(u=>u.Team==0).All(u=>u.Target==null),"surviving attackers clear dead target");
   await SaveCombat("demo-victory-collapse");await CombatAdvance(90,"demo-death-presentation");Check(soundDispatches>0,"original WAVs loaded and playback dispatched");await SaveCombat("demo-victory");
   await Send(Key.R);Check(combat.Tick==0&&combat.Events.Count==0&&combat.Outcome==EncounterOutcome.Active&&combat.Units.All(u=>u.Health==800),"R resets victory fixture deterministically");await SaveCombat("reset-ready");
   await SelectEncounter();await SquadMouse(MouseButton.Right,Screen(new(41.5,73.5)));CombatTick();Check(combat.Units.Where(u=>u.Team==0).All(u=>u.Target==null),"passive squad ground approach does not silently attack");await SaveCombat("defeat-start");
   for(int i=0;combat.Outcome==EncounterOutcome.Active&&i<2600;i+=30)await CombatAdvance(30,"defeat-encounter",30);
   Check(combat.Outcome==EncounterOutcome.Defeat&&selection.Count==0&&squad.Units.Length==1,"enemy eliminates passive squad; dead selection and occupancy cleared");await CombatAdvance(90,"defeat-collapse",15);await SaveCombat("defeat-outcome");
   await Send(Key.F);await SquadMouse(MouseButton.Left,buttons.Single(b=>b.Action=="reset").Rect.GetCenter());Check(combat.Tick==0&&combat.Outcome==EncounterOutcome.Active&&combat.Units.All(u=>u.Alive&&u.Health==800),"defeat focus is safe and restart button restores all five bodies");await SaveCombat("defeat-reset");
  }
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));GD.Print("COMBAT_SUITE_PASS");GetTree().Quit();
 }
}
