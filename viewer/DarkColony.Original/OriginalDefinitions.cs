using DarkColony.Assets;
namespace DarkColony.Original;

public sealed record OriginalWeapon(int Id,string Family,int Class,int Sound,int Rate,int Damage,int Speed,int Range,int Shots,int Reload,int Boom,int[] Raw) {
 public int Lifetime=>((Range*256+1024)*2+1)/(Speed*2)+1;
 public int ProjectileKind=>Raw[10];
}
public sealed record OriginalBoom(int Id,int Size,string[] Effects,int[] Damage,int[] Scatter);
public sealed record OriginalClipSet(string Name,AnimationAsset Asset,AnimationDef[] Directions) {
 public AnimationDef Direction(int angle)=>Directions[(((angle+8)&255)>>4)*2];
 public (int X,int Z,int Delay)[] Anchors(int angle) {
  var d=Direction(angle);var anchors=new List<(int,int,int)>();int delay=0;
  for(int i=d.First;i<=d.Last;i++) {
   var f=Asset.Frames[i];var p=f.Parts[7];
   if(p.Name is not ("" or "NONAME"))anchors.Add((p.X*8,-p.Y*8,delay));
   delay+=NativeAnimation.DurationTicks(f.Duration);
  }
  return anchors.Count==0?new[]{(0,0,0)}:anchors.ToArray();
 }
}
public sealed record OriginalClips(OriginalClipSet Stand,OriginalClipSet Move,OriginalClipSet[] Fire,
 OriginalClipSet[] Die,OriginalClipSet Critical,OriginalClipSet Special,OriginalClipSet[] Blood) {
 public OriginalClipSet? Build {get;init;}
 public OriginalClipSet? Scratched {get;init;}
 public OriginalClipSet? Burnt {get;init;}
}

public sealed class OriginalDefinitions {
 public OriginalScenario Scenario {get;}
 public NativeReference Reference {get;}
 public IReadOnlyDictionary<int,OriginalWeapon> Weapons {get;}
 public int[][] DamageFactors {get;}
 public Dictionary<int,OriginalClips> Clips {get;}=new();
 public Dictionary<int,OriginalClipSet?> MissileImpacts {get;}=new();
 public Dictionary<int,OriginalClipSet?> MissileBodies {get;}=new();
 public Dictionary<int,OriginalClipSet[]> BlastEffects {get;}=new();
 public Dictionary<int,OriginalBoom> Booms {get;}=new();
 public Dictionary<string,AnimationAsset> Animations {get;}=new(StringComparer.OrdinalIgnoreCase);
 public Dictionary<string,SpriteAsset> Sprites {get;}=new(StringComparer.OrdinalIgnoreCase);
 public List<ScenarioSource> Sources {get;}=new();
 public string DataRoot {get;}
 public string ContentId=>OriginalContent.ForScenario(Scenario.Scenario);
 public OriginalDefinitions(string root,OriginalScenario scenario) {
  DataRoot=root;Scenario=scenario;Reference=new(File.ReadAllBytes(Path.Combine(root,File.Exists(Path.Combine(root,"native-tables.json"))?"native-tables.json":"DC16.EXE")));
  if(ContentId==OriginalContent.Council)OriginalContent.ValidateCouncil(root);
  if(Reference.Hash!=(ContentId==OriginalContent.Council?NativeReference.CouncilHash:NativeReference.ExpectedHash)||Scenario.Types.Length!=(ContentId==OriginalContent.Council?118:106))throw new InvalidDataException("Content/ruleset definitions do not match the scenario");
  var weaponLines=OriginalScenarioImport.Lines(scenario.Sources.Single(s=>s.Path=="GAMESTAT/WEAPSTAT.TXT").Bytes,"WEAPSTAT");
  Weapons=weaponLines.Skip(1).Select(l=> {
   var w=l.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
   if(w.Length!=13)throw new InvalidDataException("Unsupported WEAPSTAT shape");
   var n=w.Where((_,i)=>i!=1).Select(int.Parse).ToArray();
   // 43baec..43bb2d: +1c BOOM, +20 shots, +24 reload; final two are byte flags.
   return new OriginalWeapon(n[0],w[1],n[1],n[2],n[3],n[4],n[5],n[6],n[8],n[9],n[7],n);
  }).ToDictionary(w=>w.Id);
  var classLines=OriginalScenarioImport.Lines(scenario.Sources.Single(s=>s.Path=="GAMESTAT/MBULLET.TXT").Bytes,"MBULLET");
  DamageFactors=classLines.Skip(2).Select(l=>l.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(s=>int.Parse(s)*256/100).ToArray()).ToArray();
  var boomLines=OriginalScenarioImport.Lines(scenario.Sources.Single(s=>s.Path=="GAMESTAT/BOOMSTAT.TXT").Bytes,"BOOMSTAT");int bi=1;
  while(bi<boomLines.Length){var h=boomLines[bi++].Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();var effects=new List<string>();while(boomLines[bi].Text!="NONE")effects.Add(boomLines[bi++].Text);bi++;
   int[] Rows(int count){var values=new List<int>();for(int row=0;row<count;row++)values.AddRange(boomLines[bi++].Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(s=>int.Parse(s)*256/100));return values.ToArray();}
   Booms.Add(h[0],new(h[0],h[1],effects.ToArray(),Rows(h[1]),Rows(3)));
  }
  LoadAnimationIndex();
  foreach(int id in scenario.Entities.Select(e=>e.Type).Concat(scenario.ArtifactSites.SelectMany(a=>a.Contents)).Concat(new[]{0,8,69,73,82,89,92,93}).Distinct())LoadClips(id);
  // Global FIN join: selected weapons1/16 have no weaponsBULLET/EXPLODE;
  // commander5/62 use SMOKEXPLODE0 in TURR.FIN.43bb9f..43bc48 permits null.
  foreach(int id in new[]{1,5,16,62})MissileImpacts[id]=Set(Weapons[id].Family,"EXPLODE");
  if(scenario.SpawnSources.Length>0||scenario.Entities.Any(e=>e.Kind=="city"))PrepareEconomyAssets();
  PrepareScenarioAssets();
 }
 public byte[] Read(string relative) {
  var bytes=File.ReadAllBytes(Path.Combine(DataRoot,relative));
  if(!Sources.Any(s=>s.Path==relative))Sources.Add(new(relative,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),bytes));
  return bytes;
 }
 public void PrepareEconomyAssets() {
  // Preload production and role transformations before entering the simulation.
  var types=Scenario.Entities.Select(e=>e.Type).Concat(Scenario.SpawnSources.Select(s=>s.Type))
   .Concat(Scenario.Dependencies.Where(d=>d.Kind==1&&Scenario.Teams.Any(t=>t.Base.Any(v=>v!=0)&&!t.DisabledDependencies[d.Id]&&Scenario.Types[d.Subject].Race==t.Race)).Select(d=>d.Subject))
   .Concat(Scenario.Dependencies.Where(d=>d.Kind==0).Select(d=>OriginalScenarioImport.CityType(d.RaceOrTier,d.Level,d.Subject)))
   .Concat(new[]{1,9,6,14,28,29,30,32,41,42,43,44,45,46,47,48,69,70,71,72,73,74,75,76,94});
  foreach(int id in types.Distinct())if(!Clips.ContainsKey(id))LoadClips(id);
  PrepareWeaponAssets();
 }
 public void PrepareWarAssets(){
  foreach(int id in new[]{37,40,63,64,65,66,67,85,90})if(!Clips.ContainsKey(id))LoadClips(id);
  PrepareWeaponAssets();
 }
 void PrepareScenarioAssets() {
  // Scenario, TRO arrivals and role transitions form one closure, regardless of
  // city/source presence. Native loads global definitions; ODC binds the used set.
  var types=new HashSet<int>(Clips.Keys);
  foreach(var action in OriginalMission.Parse(Scenario.Sources.Single(s=>s.Path.EndsWith(".TRO")).Bytes,true).SelectMany(b=>b.Actions))
   if(action.Name=="funkytower"&&ContentId==OriginalContent.Council)types.Add(112);
   else if(action.Name=="newtype")types.Add(unchecked((byte)int.Parse(action.SourceText.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)[3])));
   else if(action.Name is "reinforce" or "reinforce2")for(int i=0;i<5;i++)if(action.Arguments[4+2*i]>0&&action.Arguments[3+2*i]<Scenario.Types.Length)types.Add(action.Arguments[3+2*i]);
  foreach(int id in types.ToArray()) {
   if(id is >=69 and <=76)foreach(int rank in Enumerable.Range(id<73?69:73,4))types.Add(rank);
   foreach(int role in id switch{1=>new[]{41},9=>new[]{42},4 or 77=>new[]{4,77},12 or 78=>new[]{12,78},6 or 47=>new[]{6,47},14 or 48=>new[]{14,48},43=>new[]{45},44=>new[]{46},_=>Array.Empty<int>()})types.Add(role);
  }
  foreach(int id in types.Order())if(!Clips.ContainsKey(id))LoadClips(id);
  PrepareWeaponAssets();
 }
 void PrepareWeaponAssets() {
  var weapons=Clips.Keys.SelectMany(t=>Scenario.Types[t].Fields.Skip(5).Take(3)).Concat(Clips.ContainsKey(12)?new[]{Scenario.Types[12].Fields[29]}:Array.Empty<int>()).Where(id=>id>=0).Distinct().ToArray();
  foreach(int id in weapons)
  {MissileImpacts[id]=Set(Weapons[id].Family,"EXPLODE")??Set(Weapons[id].Family,"EXPL");MissileBodies[id]=Set(Weapons[id].Family,"BULLET");}
  foreach(int boom in weapons.Select(id=>Weapons[id].Boom).Where(b=>b>0).Distinct())
   BlastEffects[boom]=Booms[boom].Effects.Select(name=>{var a=animationIndex[name];return new OriginalClipSet(name,a.Asset,Enumerable.Repeat(a.Definition,32).ToArray());}).ToArray(); // 43b694: exact name, same direction32 times.
  if(MissileBodies.Keys.Any(id=>Weapons[id].ProjectileKind==2))MissileImpacts[43]=Set(Weapons[43].Family,"EXPLODE"); // 442870: Firestorm trail.
 }
 public AnimationAsset Animation(string family) {
  string file=family.ToUpperInvariant();
  if(!Animations.TryGetValue(file,out var fin))Animations[file]=fin=Import.Animation(Read("ANIMATE/"+file+".FIN"));
  return fin;
 }
 readonly Dictionary<string,(AnimationAsset Asset,AnimationDef Definition,string File)> animationIndex=new(StringComparer.OrdinalIgnoreCase);
 public IReadOnlyDictionary<string,(AnimationAsset Asset,AnimationDef Definition,string File)> AnimationIndex=>animationIndex;
 void LoadAnimationIndex() {
  // 404e8c/4256a4 load ANIM.DAT in order; 4261bf joins names globally, not by unit family.
  // Retained test.5 content is a deliberately closed A01 bundle without ANIM.DAT.
  // Its explicit bank closure remains supported; new source installs use the original manifest.
  var files=File.Exists(Path.Combine(DataRoot,"ANIM.DAT"))
   ?OriginalScenarioImport.Lines(Read("ANIM.DAT"),"ANIM.DAT").Select(l=>l.Text.ToUpperInvariant()).ToArray()
   :new[]{"TRSC.FIN","GRAY.FIN","SALA.FIN","LEFT.FIN","CENT.FIN","DROP.FIN","SAWS.FIN","TURR.FIN"}.Where(f=>File.Exists(Path.Combine(DataRoot,"ANIMATE",f))).ToArray();
  foreach(var file in files) {
   if(Path.GetFileName(file)!=file||!file.EndsWith(".FIN"))throw new InvalidDataException("Invalid ANIM.DAT filename");
   var asset=Animation(Path.GetFileNameWithoutExtension(file));
   foreach(var definition in asset.Animations)if(!animationIndex.TryAdd(definition.Name,(asset,definition,file)))throw new InvalidDataException("Ambiguous global animation: "+definition.Name);
  }
 }
 public SpriteAsset Sprite(string name) {
  if(!Sprites.TryGetValue(name,out var spr))Sprites[name]=spr=Import.Sprite(Read("SPRITES/"+name.ToUpperInvariant()+".SPR"));
  return spr;
 }
 public OriginalClipSet? Set(string family,string action) {
  // 4261bf..4261ef builds the native bearing table from FIN suffix (12-i)&15.
  // Native angle0 points +X; FIN suffix0 depicts screen-down, suffix12 right.
  // Apply this before fallback search, to body poses AND firing-anchor selection.
  var bindings=Enumerable.Range(0,16).Select(i=>animationIndex.GetValueOrDefault(family+action+((12-i)&15))).ToArray();
  var named=bindings.Select(b=>b.Definition).ToArray();
  if(named.All(a=>a==null))return null;
  var assets=bindings.Where(b=>b.Definition!=null).Select(b=>b.Asset).Distinct().ToArray();
  if(assets.Length!=1)throw new NotSupportedException("Directional set spans FIN banks: "+family+action);
  var fin=assets[0];
  var directions=new AnimationDef[32];
  for(int i=0;i<32;i++)directions[i]=Reference.AnimationFallback.Select(offset=>named[((offset+i+32)&31)/2]).First(a=>a!=null)!;
  return new(family+action,fin,directions);
 }
 public void LoadClips(int type) {
  string family=Scenario.Types[type].Sprite.ToUpperInvariant();
  var stand=Set(family,"STAND")??throw new InvalidDataException("Missing original STAND: "+family);
  var fire=new List<OriginalClipSet>();var basic=Set(family,"FIRE");if(basic!=null)fire.Add(basic);
  var a=Set(family,"FIREA");if(a!=null){fire.Clear();fire.Add(a);}
  foreach(string suffix in new[]{"FIREB","FIREC"})if(Set(family,suffix) is {} v)fire.Add(v);
  if(fire.Count==0)fire.Add(stand);
  var death=new List<OriginalClipSet>();var die=Set(family,"DIE");
  if(die!=null)death.Add(die);else foreach(string suffix in new[]{"DIEA","DIEB","DIEC"})if(Set(family,suffix) is {} v)death.Add(v);
  if(death.Count==0)death.Add(stand);
  var blood=Enumerable.Range(0,7).Select(i=>Set(family,"BLOOD"+(char)('A'+i))).Where(s=>s!=null).Cast<OriginalClipSet>().ToArray();
  Clips[type]=new(stand,Set(family,"MOVE")??stand,fire.ToArray(),death.ToArray(),Set(family,"FUNK")??stand,Set(family,"DEPLOY")??stand,blood){
   Build=Set(family,"BUILDSTAND")??Set(family,"BUILD"),Scratched=Set(family,"SCRCH"),Burnt=Set(family,"BURN")};
 }
}

// 42633c/4264f8; frame and delay have native byte wrap, independent of render rate.
public sealed class NativeAnimation {
 // 425b51..425b9b converts the signed FIN word at load, before counters/anchors.
 // Keep imported bytes raw; presentation adapters and diagnostic readers use them.
 public static int DurationTicks(ushort raw)=>((raw==0?15:unchecked((short)raw))+3)*15/100;
 public OriginalClipSet? Set {get;private set;}
 public int Frame {get;private set;}
 public int Delay {get;private set;}
 public int Mode {get;private set;}=2;
 internal void Restore(OriginalClipSet? set,int frame,int delay,int mode){Set=set;Frame=frame;Delay=delay;Mode=mode;}
 public void Start(OriginalClipSet set,int mode) {
  if(ReferenceEquals(Set,set)&&Mode==mode)return;
  Set=set;Mode=mode;Frame=Delay=0;
 }
 public void Tick(int angle) {
  if(Set==null||Mode==2)return;
  var d=Set.Direction(angle);bool next=Delay==0;if(next)Frame=(Frame+1)&255;
  if(Frame>d.Last-d.First) {
   Frame=0;next=true;
   if(Mode==1){Mode=2;return;}
   if(Mode==3){Frame=d.Last-d.First;return;}
  }
  if(next)Delay=DurationTicks(Set.Asset.Frames[d.First+Frame].Duration)&255;
  Delay=(Delay-1)&255;
 }
 // 426364 is also called by 439934 before drawing. A turn can select a shorter
 // direction between Tick and draw: wrap/terminate/hold, never clamp a loop pose.
 public bool Validate(int angle) {
  if(Set==null||Mode==2)return false;
  var d=Set.Direction(angle);
  if(Frame>d.Last-d.First) {
   Frame=0;
   if(Mode==1){Mode=2;return false;}
   if(Mode==3){Frame=d.Last-d.First;return true;}
   Delay=DurationTicks(Set.Asset.Frames[d.First].Duration)&255;
  }
  return true;
 }
 // Rendering and save preflight must never consume or rewind authoritative state.
 public AnimationFrame? Pose(int angle) {
  if(Set==null||Mode==2)return null;
  var d=Set.Direction(angle);int frame=Frame;
  if(frame>d.Last-d.First){if(Mode==1)return null;frame=Mode==3?d.Last-d.First:0;}
  return Set.Asset.Frames[d.First+frame];
 }
}
