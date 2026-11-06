using Godot;
using DarkColony.Assets;
using DarkColony.Simulation;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 readonly Dictionary<int,TrooperPose> effectPoseFixture=new();
 async Task EffectsSuite() {
  if(realtimeCheck){await EffectsRealtime();return;}
  // Explicit presentation fixture: original FIN poses, fixed legal anchors under canopy.
  var fixture=encounter with{Spawns=encounter.Spawns.Select(s=>s.Id==1?s with{Position=new(35.5,72.5)}:s.Id==5?s with{Position=new(36.5,72.5)}:s).ToArray(),Patrols=Array.Empty<PatrolDefinition>()};combat=fixture.Create(navigation,movementSettings);squad=combat.Movement;
  for(int i=0;i<8;i++){effectPoseFixture[1]=combatClips[1].Fire(2,i);effectPoseFixture[5]=combatClips[5].Fire(2,i);await SaveCombat("original-fire-composition-"+i);}if(effectsBaseline){GD.Print("EFFECTS_BASELINE_PASS");GetTree().Quit();return;}effectPoseFixture.Clear();ResetCombat();
  await SelectEncounter();await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(46.5,71.5)));int detail=0;
  for(int i=0;i<2100;i++) {CombatTick();if(i%120==0)await SaveCombat("effects-encounter");
   if(combat.Projectiles.Any(p=>p.Source>=5&&p.Age>=7)&&detail<5){await SaveCombat("gray-bolt-travel");detail++;}
   if(combat.Events.Any(e=>e.Kind=="Death"&&e.Tick==combat.Tick)){await SaveCombat("effects-death");for(int j=0;j<30;j++)CombatTick();await SaveCombat("effects-collapse");}
   if(combat.Outcome==EncounterOutcome.Victory&&combat.Units.Where(u=>u.Alive&&u.Team==0).All(u=>u.Tactics!.Order==TacticalOrder.None))break;
  }
  Check(combat.Outcome==EncounterOutcome.Victory,"effects encounter victory and resumed orders");
  Check(effectsPresented==combat.Events.Count(e=>e.Kind is "Shot" or "Impact" or "Death"),"each launch/impact/death presented once");Check(soundDispatches==combat.Events.Count(e=>e.Kind is "Shot" or "Death")&&audioPeak<=6,"original sound dispatch once per firing/death; at most six voices");SavePresentationAudit();
  for(int i=0;i<90;i++)CombatTick();await SaveCombat("effects-cleanup");Check(hitFeedback.All(h=>combat.Tick-h.tick>=18)&&DrawOrder().All(u=>combat.Unit(u.Id).Alive)&&combat.Projectiles.Length==0,"impact feedback expired; death visuals and projectiles cleaned up");
  await Send(Key.R);Check(FreshSkirmish()&&effectsPresented==0&&hitFeedback.Count==0&&effectTextures.Count==0&&combatAudio.Values.All(p=>!p.Playing),"reset clears active effects, pending presentation and sound");await SaveCombat("effects-victory-reset");
  await SelectEncounter();await SquadMouse(MouseButton.Right,Screen(new(43.5,73.5)));for(int i=0;i<3600&&combat.Outcome==EncounterOutcome.Active;i++)CombatTick();Check(combat.Outcome==EncounterOutcome.Defeat,"effects passive encounter defeat");await SaveCombat("effects-defeat");
  await SquadMouse(MouseButton.Left,buttons.Single(b=>b.Action=="reset").Rect.GetCenter());Check(FreshSkirmish()&&combat.Projectiles.Length==0&&hitFeedback.Count==0,"defeat restart clears authoritative and presentation state");
  for(int r=0;r<3;r++){await SelectEncounter();await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(46.5,71.5)));for(int i=0;i<140;i++)CombatTick();Check(combat.Projectiles.Length>0||soundDispatches>0,"restart stress has launched events");await Send(Key.R);Check(FreshSkirmish()&&presentationEvents.Count==0&&hitFeedback.Count==0&&combat.Projectiles.Length==0,"repeated reset clears pending effects "+r);}
  await SaveCombat("effects-final-ready");File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));GD.Print("EFFECTS_SUITE_PASS");GetTree().Quit();
 }
 async Task EffectsRealtime() {
  if(showPath)await Send(Key.O);
  audioRecord=new AudioEffectRecord{Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,audioRecord);audioRecord.SetRecordingActive(true);
  ulong start=Time.GetTicksMsec();await SelectEncounter();await Send(Key.A);await SquadMouse(MouseButton.Left,Screen(new(46.5,71.5)));bool pausedOnce=false;int next=120;
  while(combat.Outcome==EncounterOutcome.Active){if(Time.GetTicksMsec()-start>45000)throw new TimeoutException("Bounded effects real-time run");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   if(combat.Tick>=next){await SaveCombat("realtime-effects");next+=120;}
   if(!pausedOnce&&combat.Tick>=140){await Send(Key.Space);long tick=combat.Tick;await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);Check(combat.Tick==tick,"normal Process pause freezes projectiles and damage");await Send(Key.Space);pausedOnce=true;}
  }
  Check(combat.Outcome==EncounterOutcome.Victory&&effectsPresented>0,"normal Process moving encounter victory with presentation events");SavePresentationAudit();await SaveCombat("realtime-effects-victory");audioRecord.SetRecordingActive(false);var recording=audioRecord.GetRecording();Check(recording.Data.Length>0,"Godot mixer capture returned PCM");if(recording.SaveToWav(Path.Combine(output,"encounter-audio.wav"))!=Error.Ok)throw new IOException("Audio capture save");
  int resets=combatResetCount;await Send(Key.R);await SaveCombat("realtime-effects-reset");Check(combatResetCount==resets+1&&combat.Tick<30&&combat.Outcome==EncounterOutcome.Active&&combat.Units.All(u=>u.Alive&&u.Health==u.MaxHealth&&u.Target==null)&&combat.Projectiles.Length==0&&hitFeedback.Count==0&&presentationEvents.Count==0,"real-time restart removes pending effects while normal ticks resume");AudioServer.RemoveBusEffect(0,0);audioRecord.Dispose();audioRecord=null;File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));GD.Print("EFFECTS_REALTIME_PASS");GetTree().Quit();
 }
 void HeadlessEffectsCheck(){combat.Enqueue(new(1,1,new[]{1,2,3,4},CombatOrderKind.AttackMove,new(46.5,71.5)));for(int i=0;i<1500;i++)CombatTick();Check(combat.Outcome==EncounterOutcome.Victory&&effectsPresented>0&&soundDispatches==0,"headless encounter completes with audio playback explicitly suppressed");SavePresentationAudit();GD.Print("HEADLESS_EFFECTS_PASS");GetTree().Quit();}
}
