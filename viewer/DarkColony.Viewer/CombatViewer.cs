using Godot;
using DarkColony.Assets;
using DarkColony.Simulation;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool deliveryCapture,combatMode,tacticalMode,skirmishMode,attackMoveArmed;CombatSimulation combat=null!;CombatFixture encounter=null!;readonly Dictionary<int,TrooperCombatClips> combatClips=new();
 readonly Dictionary<int,AudioStreamPlayer> combatAudio=new();readonly Dictionary<int,(AudioStreamWav shot,AudioStreamWav death)> combatSounds=new();readonly Dictionary<int,string> unitFamilies=new();int combatReceipt,combatEvent,combatCaptureIndex,combatResetCount,soundDispatches;
 void PrepareCombat(string[] args) {
  explorationMode=args.Contains("--exploration");fogCapture=args.Contains("--fog-capture");
  guardCapture=args.Contains("--guard-capture");deliveryCapture=args.Contains("--delivery-capture");combatMode=true;skirmishMode=args.Contains("--skirmish");tacticalMode=skirmishMode||args.Contains("--attack-move");string source=File.ReadAllText(Path.Combine(root,explorationMode?"fixtures/exploration-skirmish.json":skirmishMode?"fixtures/martian-skirmish.json":tacticalMode?"fixtures/attack-move-encounter.json":"fixtures/combat-encounter.json"));encounter=CombatFixture.Parse(source);
  using var document=JsonDocument.Parse(source);
  AudioStreamWav Sound(string name){var pcm=PcmWave.Decode(File.ReadAllBytes(Path.Combine(root,"private/DC/SOUND",name+".WAV")));File.WriteAllBytes(Path.Combine(output,name+".signed-pcm"),pcm.SignedSamples);return new(){Data=pcm.SignedSamples,Format=pcm.Bits==8?AudioStreamWav.FormatEnum.Format8Bits:AudioStreamWav.FormatEnum.Format16Bits,MixRate=pcm.SampleRate,Stereo=pcm.Channels==2};}
  var sounds=new Dictionary<string,AudioStreamWav>();AudioStreamWav Cached(string name){if(!sounds.TryGetValue(name,out var sound)){sound=Sound(name);sounds.Add(name,sound);}return sound;}
  foreach(var spawn in encounter.Spawns){string family="TRSC",shot="TRPWEA",death="TROPDEA3";if(skirmishMode){var binding=document.RootElement.GetProperty("presentation").GetProperty(spawn.Definition);family=binding.GetProperty("family").GetString()!;shot=binding.GetProperty("shotSound").GetString()!;death=binding.GetProperty("deathSound").GetString()!;}
   if(family is not ("TRSC" or "GRAY"))throw new NotSupportedException("Unverified scenario art family: "+family);
   var fin=family=="TRSC"?visuals.Animation:Import.Animation(File.ReadAllBytes(Path.Combine(root,"private/DC/ANIMATE",family+".FIN")));var spr=family=="TRSC"?visuals.Sprites:Import.Sprite(File.ReadAllBytes(Path.Combine(root,"private/DC/SPRITES",family+".SPR")));
   combatClips.Add(spawn.Id,new(fin,spr,visuals.Palette,spawn.Team,family));unitFamilies.Add(spawn.Id,family);combatSounds.Add(spawn.Id,(Cached(shot),Cached(death)));var player=new AudioStreamPlayer{VolumeDb=-16,MaxPolyphony=2};AddChild(player);combatAudio.Add(spawn.Id,player);
  }
  File.WriteAllText(Path.Combine(output,"combat-presentation.json"),JsonSerializer.Serialize(new{units=encounter.Spawns.Select(s=>new{s.Id,s.Definition,s.Team,family=unitFamilies[s.Id],limitations=combatClips[s.Id].Limitations}),sounds=sounds.ToDictionary(s=>s.Key,s=>s.Value.GetLength()),soundSelection="Fixed first SLIST variant: weapon1 GUN91/type0 DEA28; weapon15 GUN12/type8 DEA35. Original random/state selection unresolved."},new JsonSerializerOptions{WriteIndented=true}));
  PrepareEffects(args);ResetCombat();GetWindow().Title=skirmishMode?"OpenDC | Human versus Martian":tacticalMode?"OpenDC | Attack-Move":"OpenDC | First Encounter";
 }
 void ResetCombat() {
  combat=encounter.Create(navigation,movementSettings);squad=combat.Movement;combatReceipt=combatEvent=0;commandSequence=0;selection.Clear();boxActive=attackMoveArmed=false;paused=false;simClock=new(movementSettings.TickRate);combatResetCount++;
  omniscient=false;observedShots.Clear();camera=ToPixel(encounter.Camera);zoom=2.5f;foreach(var audio in combatAudio.Values)audio.Stop();ResetEffects();
  feedback=tacticalMode?$"{encounter.Objective}. Select squad; A then ground click. R restarts.":"Select the squad. Right-click the blue trooper to attack; ground to move. R restarts.";
 }
 void CombatCommand(CombatOrderKind kind,Point destination,int target=0) {
  attackMoveArmed=false;
  if(selection.Count==0){feedback="Select at least one friendly trooper first.";SquadLog("combat-order-without-selection");return;}
  combat.Enqueue(new(combat.Tick+1,++commandSequence,selection.Order().ToArray(),kind,destination,target));SquadLog("combat:"+kind);
 }
 void CombatTick() {
  combat.Step();selection.RemoveWhere(id=>!combat.Unit(id).Alive);
  while(combatReceipt<combat.Receipts.Count){var r=combat.Receipts[combatReceipt++];feedback=r.Result switch{"HoldAccepted"=>"Hold: stationary fire in range; no pursuit.","GuardAccepted"=>"Guard: defend this position, return after engagement.","AttackAccepted"=>"Attack ordered. Units approach, stop and fire in range.","Accepted"=>"Move ordered; previous attacks cancelled for this subset.","Stopped"=>"Selected units stopped; attacks cancelled.","EncounterFinished"=>"Encounter complete. R restarts.",_=>"Order: "+r.Result};}
  while(combatEvent<combat.Events.Count){int index=combatEvent++;PresentCombatEvent(combat.Events[index],index);}
  hitFeedback.RemoveAll(h=>combat.Tick-h.tick>=18);
  foreach(var pair in combatAudio)if(!VisibleUnit(combat.Unit(pair.Key)))pair.Value.Stop();
  if(combat.Units.Any(u=>u.Alive&&u.Team==combat.PlayerTeam&&u.Activity==CombatActivity.Blocked))feedback="An attacker is blocked. Select it and issue another move or attack order.";
  if(combat.Outcome!=EncounterOutcome.Active)feedback=$"{combat.Outcome.ToString().ToUpperInvariant()} - "+(tacticalMode&&combat.Outcome==EncounterOutcome.Victory?"remaining attack-move orders continue. R restarts.":"press R or Restart to repeat the encounter.");
 }
 TrooperPose CombatPose(SquadUnit body) {
  var u=combat.Unit(body.Id);var set=combatClips[u.Id];if(capturing&&effectPoseFixture.TryGetValue(body.Id,out var fixturePose))return fixturePose;if(capturing&&body.Id==5&&grayCapturePose!=null)return grayCapturePose;
  if(!u.Alive)return set.Death((combat.Tick-u.DeathTick)*animationFps/movementSettings.TickRate);
  if(u.Activity==CombatActivity.Firing&&combat.Tick-u.LastShotTick<60)return set.Fire((int)body.Facing,(combat.Tick-u.LastShotTick)*animationFps/movementSettings.TickRate);
  return set.Locomotion.Resolve((int)body.Facing,body.Moving,body.MovingTicks*animationFps/movementSettings.TickRate);
 }
 float CombatOpacity(int id){var u=combat.Unit(id);return u.Alive?1:Math.Clamp((90-(combat.Tick-u.DeathTick))/30f,0,1);}
 void DrawCombatUnitFeedback(SquadUnit body,TrooperPose pose,Vector2 anchor) {
  var u=combat.Unit(body.Id);if(!u.Alive)return;
  var color=u.Team==combat.PlayerTeam?accent:new Color("74b9ff");var p=anchor+new Vector2(-13,-pose.Visual.OriginY-7);var r=new Rect2(p,new Vector2(26,3));
  DrawRect(r,new Color("18211b"));DrawRect(new(p,new Vector2(26*u.Health/(float)u.MaxHealth,3)),color);DrawRect(r,combat.Tick-u.LastHitTick<12?new Color("ff7f71"):new Color("0b110d"),false,1/zoom);
  DrawString(font,p+new Vector2(10,-3),u.Id.ToString(),HorizontalAlignment.Left,-1,Math.Max(7,(int)(16/zoom)),color);
  if(u.Target is int id&&combat.Unit(id).Alive&&VisibleUnit(combat.Unit(id))&&selection.Contains(u.Id)) {var target=ToPixel(combat.Unit(id).Body.Position);DrawArc(target,14,0,Mathf.Tau,36,new Color("ee917c"),1.5f/zoom);if(showPath)DrawLine(ToPixel(body.Position),target,new Color(1,.6f,.4f,.22f),1/zoom);}
  if(u.Tactics?.Order==TacticalOrder.AttackMove&&u.Tactics.Destination is Point destination&&selection.Contains(u.Id))DrawRect(new Rect2(ToPixel(destination)-new Vector2(5,5),new Vector2(10,10)),new Color("e6bc84"),false,1.5f/zoom);
 }
 void DrawCombatHud(Vector2 size) {
  buttons.Clear();DrawRect(new(0,0,size.X,140*ui),panel);Text("OpenDC",new(24,34),26,accent);Text(explorationMode?(omniscient?"OMNISCIENT DIAGNOSTIC [F8]":"EXPLORATION / TEAM SIGHT [F8: diagnostic]"):skirmishMode?"HUMAN04 / MARTIAN SKIRMISH":tacticalMode?"HUMAN04 / ATTACK-MOVE":"HUMAN04 / FIRST ENCOUNTER",new(244,32),18,muted);
  Text($"{selection.Count} SELECTED   {(paused?"PAUSED":combat.Outcome.ToString().ToUpperInvariant())}   WORLD {zoom:0.00}x   UI {ui:0.00}x",new(24,64),18,ink);
  float x=24;void Button(string action,string label,float width){var r=new Rect2(new Vector2(x,82)*ui,new Vector2(width,36)*ui);buttons.Add((r,action,label));DrawRect(r,new Color("263b32"));Text(label,new(x+10,106),16,ink);x+=width+8;}
  if(tacticalMode){Button("attack-move",attackMoveArmed?"Click ground":"Attack-move [A]",160);Button("hold","Hold [H]",100);Button("guard","Guard [J]",108);Button("range",showRange?"Range ON [V]":"Range [V]",140);}Button("stop","Stop [X]",104);Button("pause",paused?"Resume [Space]":"Pause [Space]",152);Button("reset","Restart [R]",132);Button("focus","Focus [F]",108);Button("path","Paths [O]",116);Button("nav","Occupancy [N]",164);Button("grid","Grid [G]",100);Button("ui-","UI -",62);Button("ui+","UI +",62);
  DrawRect(SquadInfo,new Color(.05f,.09f,.08f,.96f));float right=size.X/ui-316;Text("ENCOUNTER",new(right+14,185),18,accent);
  int row=0;foreach(var u in PresentedUnits()){float y=216+row++*48;Text($"{(selection.Contains(u.Id)?">":" ")} {u.Id} {(u.Team==0?"SQUAD":skirmishMode?unitFamilies[u.Id]:"ENEMY")}  {u.Health}/{u.MaxHealth}",new(right+14,y),16,u.Team==0?ink:new Color("74b9ff"));Text(DefenseStatus(u)+PresentedTarget(u),new(right+24,y+20),14,muted);}
  float infoY=235+PresentedUnits().Length*48;Text(explorationMode?"Explore east; defeat the patrol":tacticalMode?$"Objective: {combat.Units.Count(u=>u.Team!=0&&u.Alive)} enemies remain":"Range 4 tiles / 100 damage",new(right+14,infoY),15,muted);Text(explorationMode?"Black: unseen / dim: explored":"1s cooldown is provisional",new(right+14,infoY+22),15,new Color("e6bc84"));Text("R: deterministic restart",new(right+14,infoY+44),15,muted);
  float bottom=size.Y/ui-76;DrawRect(new(0,bottom*ui,size.X,76*ui),panel);Text(attackMoveArmed?"ATTACK-MOVE: click ground to set destination. Escape cancels.":feedback,new(24,bottom+24),18,ink);Text(tacticalMode?"A + click: attack-move   H: hold   J: guard   X: stop   V: primary range (cyan fire / amber guard)   Right-click: move/attack   R: restart":"Right-click enemy: attack   Ground: move   X: stop   R: restart   Space: pause   Wheel: zoom   Q: quit   Delivery configured per weapon; no terrain occlusion",new(24,bottom+51),15,new Color("e6bc84"));
 }
}
