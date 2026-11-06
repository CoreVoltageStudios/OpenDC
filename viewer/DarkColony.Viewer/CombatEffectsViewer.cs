using Godot;
using DarkColony.Assets;
using DarkColony.Simulation;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 readonly Dictionary<int,CombatEffects> originalEffects=new();
 readonly Dictionary<int,(string key,ImageTexture texture)> effectTextures=new();readonly HashSet<int> usedEffectTextures=new();
 readonly List<(long tick,int target,int amount)> hitFeedback=new();readonly List<object> presentationEvents=new();int effectsPresented,audioPeak;
 bool effectsCapture,effectsBaseline;AudioEffectRecord? audioRecord;
 void PrepareEffects(string[] args) {
  effectsCapture=args.Contains("--effects-capture");effectsBaseline=capturing&&args.Contains("--effects-baseline");var palette=new EffectPalette(File.ReadAllText(Path.Combine(root,"private/DC/COLOUR.SET")));
  var banks=new Dictionary<string,SpriteAsset>();
  foreach(string family in unitFamilies.Values.Distinct()) {
   var fin=Import.Animation(File.ReadAllBytes(Path.Combine(root,"private/DC/ANIMATE",family+".FIN")));
   foreach(string bank in fin.Frames.SelectMany(f=>f.Cells).Where(c=>c.UnknownWords[2]==5).Select(c=>c.Bank).Distinct())if(!banks.ContainsKey(bank))banks.Add(bank,Import.Sprite(File.ReadAllBytes(Path.Combine(root,"private/DC/SPRITES",bank.ToUpperInvariant()+".SPR"))));
   var effects=new CombatEffects(fin,banks,palette,family);foreach(var pair in unitFamilies.Where(x=>x.Value==family))originalEffects.Add(pair.Key,effects);
  }
  if(skirmishMode&&(!capturing||explorationMode))showPath=false;
 }
 void ResetEffects(){hitFeedback.Clear();presentationEvents.Clear();effectsPresented=soundDispatches=audioPeak=0;foreach(var t in effectTextures.Values)t.texture.Dispose();effectTextures.Clear();}
 void PresentCombatEvent(CombatEvent e,int index) {
  if(!VisibleEvent(e))return;
  if(e.Kind=="Shot")observedShots.Add((e.Source,e.Tick));
  if(e.Kind=="Damage"){hitFeedback.Add((e.Tick,e.Target,e.Amount));if(hitFeedback.Count>32)hitFeedback.RemoveAt(0);}
  if(e.Kind is "Shot" or "Impact" or "Death"){presentationEvents.Add(new{index,e.Tick,e.Kind,e.Source,e.Target,e.ShotId});effectsPresented++;}
  if(e.Kind is "Shot" or "Death" && DisplayServer.GetName()!="headless") {int id=e.Kind=="Shot"?e.Source:e.Target;var player=combatAudio[id];player.Stop();player.Stream=e.Kind=="Shot"?combatSounds[id].shot:combatSounds[id].death;player.Play();soundDispatches++;audioPeak=Math.Max(audioPeak,combatAudio.Values.Count(p=>p.Playing));}
  hitFeedback.RemoveAll(h=>combat.Tick-h.tick>=18);observedShots.RemoveWhere(s=>combat.Tick-s.tick>60);
 }
 void DrawEffect(CombatEffectPose pose,Vector2 anchor,int slot) {
  if(effectsBaseline)return;
  var v=pose.Visual;string key=$"{v.Id}:{anchor.X}:{anchor.Y}";usedEffectTextures.Add(slot);
  if(!effectTextures.TryGetValue(slot,out var cached)||cached.key!=key){cached.texture?.Dispose();var pixels=visuals.Occlusion.ApplyGroundBody(v,(int)anchor.X,(int)anchor.Y,(int)anchor.Y+pose.BottomOffset);cached=(key,FromRaster(pixels));effectTextures[slot]=cached;}
  DrawTextureRect(cached.texture,new(anchor-new Vector2(v.OriginX,v.OriginY),new(v.LogicalWidth,v.LogicalHeight)),false);
 }
 void DrawFiringEffects(SquadUnit body,TrooperPose pose,Vector2 anchor) {if(combat.Visibility!=null&&!omniscient&&!observedShots.Contains((body.Id,combat.Unit(body.Id).LastShotTick)))return;int n=0;foreach(var effect in originalEffects[body.Id].Firing(pose.FinFrame))DrawEffect(effect,anchor,body.Id*10+n++);}
 void DrawDeliveryEffects() {
  foreach(var p in combat.Projectiles.Where(p=>VisiblePoint(p.Position))){int n=0;foreach(var effect in originalEffects[p.Source].Projectile)DrawEffect(effect,BodyAnchor(p.Position),1000+(int)p.Id*10+n++);}
  foreach(var h in hitFeedback.Where(h=>combat.Tick-h.tick<18&&VisiblePoint(combat.Unit(h.target).Body.Position))){var p=BodyAnchor(combat.Unit(h.target).Body.Position);DrawString(font,p+new Vector2(12,-33-(combat.Tick-h.tick)*.25f),$"-{h.amount}",HorizontalAlignment.Left,-1,10,new Color("ffbd9a"));}
  foreach(int key in effectTextures.Keys.Where(k=>!usedEffectTextures.Contains(k)).ToArray()){effectTextures[key].texture.Dispose();effectTextures.Remove(key);}usedEffectTextures.Clear();
 }
 void SavePresentationAudit(){File.WriteAllText(Path.Combine(output,"presentation-events.json"),JsonSerializer.Serialize(new{effectsPresented,soundDispatches,audioPeak,events=presentationEvents},new JsonSerializerOptions{WriteIndented=true}));}
 public override void _ExitTree(){importCancel?.Cancel();if(importTask is not {IsCompleted:false})contentPlan?.Dispose();CloseNetwork();foreach(var old in retiredNetworks)old.Dispose();retiredNetworks.Clear();animationTrace?.Dispose();animationTrace=null;animationProfile?.Dispose();animationProfile=null;RemoveFailureLogger();ShutdownMissionMedia();foreach(var p in combatAudio.Values){p.Stop();p.Stream=null;}foreach(var sound in combatSounds.Values.SelectMany(s=>new[]{s.shot,s.death}).Distinct())sound.Dispose();foreach(var t in effectTextures.Values)t.texture.Dispose();effectTextures.Clear();}
}
