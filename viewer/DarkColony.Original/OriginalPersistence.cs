using System.Reflection;
using DarkColony.Assets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
namespace DarkColony.Original;

// ODC-owned format. Explicit domain inventory; never engine objects or arbitrary type names.
public sealed class SaveCodec {
 public const int Version=1;public const string Rules="odc-a01-a02-persistence-1";
 public readonly JsonSerializerOptions Options=new(){IncludeFields=true,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,MaxDepth=64};
 public SaveCodec(OriginalDefinitions data){MissionSaveSchema.VerifyInventory();Options.Converters.Add(new AnimationConverter(data));}
 public JsonNode? Node<T>(T value)=>JsonSerializer.SerializeToNode(value,Options);
 public T Read<T>(JsonNode? node)=>node==null?default!:node.Deserialize<T>(Options)!;
 public JsonObject Fields(object value){var result=new JsonObject();foreach(var f in value.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))result[f.Name]=JsonSerializer.SerializeToNode(f.GetValue(value),f.FieldType,Options);return result;}
 public void FieldsInto(JsonNode node,object target){var fields=target.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance);if(node.AsObject().Count!=fields.Length)throw new InvalidDataException("Incomplete state fields");foreach(var f in fields){if(!node.AsObject().ContainsKey(f.Name))throw new InvalidDataException("Missing state field "+f.Name);var restored=node[f.Name]?.Deserialize(f.FieldType,Options);if(f.GetValue(target) is Array oldArray&&restored is Array newArray&&oldArray.Length!=newArray.Length)throw new InvalidDataException("Invalid state array "+f.Name);f.SetValue(target,restored);}}
 sealed class AnimationConverter:JsonConverter<NativeAnimation>{
  readonly Dictionary<string,OriginalClipSet> sets=new();readonly Dictionary<OriginalClipSet,string> keys=new(ReferenceEqualityComparer.Instance);
  public AnimationConverter(OriginalDefinitions data){
   void Add(string key,OriginalClipSet? set){if(set!=null){sets.Add(key,set);keys.TryAdd(set,key);}}
   foreach(var pair in data.Clips.OrderBy(p=>p.Key))foreach(var property in typeof(OriginalClips).GetProperties()){
    var value=property.GetValue(pair.Value);if(value is OriginalClipSet set)Add($"unit/{pair.Key}/{property.Name}",set);
    if(value is OriginalClipSet[] array)for(int i=0;i<array.Length;i++)Add($"unit/{pair.Key}/{property.Name}/{i}",array[i]);
   }
   foreach(var p in data.MissileBodies)Add("missile/"+p.Key,p.Value);foreach(var p in data.MissileImpacts)Add("impact/"+p.Key,p.Value);
   foreach(var p in data.BlastEffects)for(int i=0;i<p.Value.Length;i++)Add($"blast/{p.Key}/{i}",p.Value[i]);
  }
  public override NativeAnimation Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options){var n=JsonNode.Parse(ref reader)!;if(n.AsObject().Count!=4)throw new InvalidDataException("Incomplete animation");string? key=n["binding"]?.GetValue<string>();int f=n["frame"]!.GetValue<int>(),d=n["delay"]!.GetValue<int>(),m=n["mode"]!.GetValue<int>();if(f<0||f>255||d<0||d>255||m<0||m>3)throw new InvalidDataException("Invalid animation counters");var a=new NativeAnimation();a.Restore(key==null?null:sets[key],f,d,m);return a;}
  public override void Write(Utf8JsonWriter writer,NativeAnimation a,JsonSerializerOptions options){writer.WriteStartObject();writer.WriteString("binding",a.Set==null?null:keys[a.Set]);writer.WriteNumber("frame",a.Frame);writer.WriteNumber("delay",a.Delay);writer.WriteNumber("mode",a.Mode);writer.WriteEndObject();}
 }
 // KNOBE is the reward frontend only; viewing it must never change a mission's
 // authoritative identity or poison saves made after Replay. Keep all simulation
 // FINs in the original fingerprint and recognize only this known older variant.
 static string Fingerprint(IEnumerable<ScenarioSource> sources,string nativeHash)=>Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",sources.GroupBy(s=>s.Path).Select(g=>g.First()).OrderBy(s=>s.Path,StringComparer.Ordinal).Select(s=>s.Path+":"+s.Sha256))+nativeHash)));
 static IEnumerable<ScenarioSource> ContentSources(OriginalDefinitions data)=>data.Scenario.Sources.Concat(data.Sources.Where(s=>(s.Path.StartsWith("ANIMATE/",StringComparison.OrdinalIgnoreCase)&&!s.Path.Equals("ANIMATE/KNOBE.FIN",StringComparison.OrdinalIgnoreCase))||s.Path=="ANIM.DAT"));
 public static string Content(OriginalDefinitions data)=>Fingerprint(ContentSources(data),data.Reference.Hash);
 public static string? LegacyRewardContent(OriginalDefinitions data){
  const string path="ANIMATE/KNOBE.FIN";if(!File.Exists(Path.Combine(data.DataRoot,path)))return null;
  var bytes=data.Read(path);return Fingerprint(ContentSources(data).Append(new ScenarioSource(path,Convert.ToHexString(SHA256.HashData(bytes)),bytes)),data.Reference.Hash);
 }

 public static void AtomicWrite(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=System.Text.Encoding.UTF8.GetBytes(text);stream.Write(bytes);stream.Flush(true);}if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public static JsonObject Open(string path){if(new FileInfo(path).Length>32*1024*1024)throw new InvalidDataException("Save file is too large");var n=JsonNode.Parse(File.ReadAllText(path),documentOptions:new(){MaxDepth=64})!.AsObject();if(n["version"]?.GetValue<int>()!=Version||n["rules"]?.GetValue<string>()!=Rules)throw new InvalidDataException("Unsupported ODC save version/rules; this build supports version 1");var payload=n["payload"]!.ToJsonString();if(n["sha256"]?.GetValue<string>()!=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))))throw new InvalidDataException("Save is incomplete or damaged (checksum mismatch)");return n;}
 public static void Write(string path,JsonObject payload){var text=payload.ToJsonString();AtomicWrite(path,new JsonObject{["version"]=Version,["rules"]=Rules,["sha256"]=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))),["payload"]=payload.DeepClone()}.ToJsonString());}
}
public sealed partial class OriginalWorld {
 internal JsonObject SaveState(SaveCodec c){
  JsonNode? N<T>(T v)=>c.Node(v);
  var teams=new JsonArray();foreach(var t in Teams){var cities=new JsonArray();foreach(var city in t.Cities)cities.Add(new JsonObject{["level"]=city.Level,["pending"]=city.InitializationPending,["entity"]=city.Entity?.Slot});var queues=new JsonArray();foreach(var q in t.Production)queues.Add(new JsonObject{["fields"]=c.Fields(q),["types"]=N(q.Types)});teams.Add(new JsonObject{["fields"]=c.Fields(t),["cities"]=cities,["queues"]=queues});}
  var state=new JsonObject{["healedHealth"]=N(HealedHealth),["artifacts"]=N(ArtifactContents),["commanderRanks"]=N(CommanderRanks),["combatResources"]=new JsonObject{["earned"]=N(EarnedResources),["bursts"]=N(ShotCounters)},["tick"]=Tick,["phase"]=Phase,["day"]=DayCounter,["coefficient"]=Coefficient,["nextDynamic"]=NextDynamicId,["nextMissile"]=nextMissile,["aiCursor"]=aiCursor,["armyLimit"]=ArmyLimit,["rng"]=N(new long[]{Random.Index,Random.Draws}),["navigation"]=Navigation.Generation,
   ["slots"]=N(Slots),["ground"]=N(Ground),["air"]=N(Air),["other"]=N(Other),["relations"]=N(Relations.Cast<bool>().ToArray()),["deaths"]=N(Deaths.Cast<int>().ToArray()),["live"]=N(Live.Cast<int>().ToArray()),["kills"]=N(Kills.Cast<int>().ToArray()),["nearbyKills"]=N(NearbyCommanderKills),["captures"]=N(Captures),["visionBits"]=N(VisionBits),["missiles"]=N(Missiles),["transports"]=N(Transports),["reservations"]=N(TransportReservations.Cast<bool>().ToArray()),
   ["visible"]=N(Sight.Visible),["explored"]=N(Sight.Explored),["teams"]=teams,["maximumArmy"]=Session.MaximumArmy,["preventRetraction"]=Session.PreventHarvesterRetraction,["scriptValues"]=N(Session.ScriptValues.Cast<int>().ToArray()),["scriptRemaining"]=N(Session.ScriptBlocks.Select(b=>b?.Remaining).ToArray()),["sources"]=N(Session.Sources.Select(s=>new {s.Initialized,s.Members}).ToArray())};
  if(Data.ContentId==OriginalContent.Council)state["councilPalaces"]=N(CouncilPalaces);
  return state;
 }
 internal void RestoreState(JsonNode n,SaveCodec c){
  T R<T>(string key)=>c.Read<T>(n[key]??throw new InvalidDataException("Missing authoritative state: "+key));void Copy<T>(string key,T[] into){var a=R<T[]>(key);if(a.Length!=into.Length)throw new InvalidDataException("Invalid "+key+" length");a.CopyTo(into,0);}
  void Matrix<T>(string key,T[,] into){var a=R<T[]>(key);if(a.Length!=into.Length)throw new InvalidDataException("Invalid matrix "+key);for(int i=0;i<a.Length;i++)into[i/into.GetLength(1),i%into.GetLength(1)]=a[i];}
  Tick=R<long>("tick");Phase=R<int>("phase");DayCounter=R<int>("day");Coefficient=R<int>("coefficient");NextDynamicId=R<int>("nextDynamic");nextMissile=R<long>("nextMissile");aiCursor=R<int>("aiCursor");ArmyLimit=R<int>("armyLimit");var rng=R<long[]>("rng");if(rng.Length!=2||rng[0]<0||rng[0]>255||rng[1]<0||Tick<0||Phase<0||Phase>1)throw new InvalidDataException("Invalid simulation clock/random state");Random.Restore((int)rng[0],rng[1]);Navigation.Restore(R<long>("navigation"));
  if(Data.ContentId==OriginalContent.Council)Copy("councilPalaces",CouncilPalaces);
  Copy("slots",Slots);for(int i=0;i<Slots.Length;i++)if(Slots[i] is {} e&&(e.Slot!=i||e.Incarnation<1||e.Owner<0||e.Owner>9||!Data.Clips.ContainsKey(e.Type)||e.Actions==null))throw new InvalidDataException("Invalid entity slot");
  foreach(var e in Entities)foreach(var a in e.Actions)if(a.Siphoner is < -1 or >=800)throw new InvalidDataException("Invalid siphon claim slot");
  foreach(var pair in R<Dictionary<int,List<int>>>("artifacts")){
   var site=Data.Scenario.ArtifactSites.SingleOrDefault(s=>s.Slot==pair.Key);
   bool war=WarCatalog.IsWar(Data.Scenario.Scenario);
   if(site==null||Slots[pair.Key] is not {Living:true,Type:37}||(war?pair.Value.Count>128||pair.Value.Any(t=>(t is <63 or >67)&&!site.Contents.Contains(t)):pair.Value.Count>site.Contents.Length||!pair.Value.SequenceEqual(site.Contents.Skip(site.Contents.Length-pair.Value.Count))))throw new InvalidDataException("Invalid artifact inventory");
   ArtifactContents.Add(pair.Key,pair.Value);
  }
  if(Entities.Any(e=>e.Type==37&&e.Living&&!ArtifactContents.ContainsKey(e.Slot))||Entities.Any(e=>e.Berserk is <0 or >56))throw new InvalidDataException("Missing artifact site or invalid berserk duration");
  Copy("ground",Ground);Copy("air",Air);Copy("other",Other);Matrix("relations",Relations);Matrix("deaths",Deaths);Matrix("live",Live);Matrix("kills",Kills);Copy("nearbyKills",NearbyCommanderKills);Copy("captures",Captures);Copy("visionBits",VisionBits);Matrix("reservations",TransportReservations);Matrix("scriptValues",Session.ScriptValues);
  Copy("healedHealth",HealedHealth);if(HealedHealth.Any(v=>v<0))throw new InvalidDataException("Invalid healing statistic");
  Copy("commanderRanks",CommanderRanks);if(CommanderRanks.Any(r=>r is <0 or >3)||VisionBits.Any(b=>b is <0 or >255))throw new InvalidDataException("Invalid team rank/vision state");
  var extra=n["combatResources"]??throw new InvalidDataException("Missing combat/resource state");var earned=c.Read<int[]>(extra["earned"]);var bursts=c.Read<int[]>(extra["bursts"]);if(earned.Length!=8||bursts.Length!=800||bursts.Any(v=>v<0||v>255))throw new InvalidDataException("Invalid combat/resource counters");earned.CopyTo(EarnedResources,0);bursts.CopyTo(ShotCounters,0);
  foreach(var layer in new[]{Ground,Air,Other})if(layer.Any(id=>id>=800||id<(ReferenceEquals(layer,Ground)?DiseaseGround:-2)))throw new InvalidDataException("Invalid occupancy reference");
  Missiles.AddRange(R<List<NativeMissile>>("missiles"));foreach(var p in R<Dictionary<int,NativeTransport>>("transports"))Transports.Add(p.Key,p.Value);
  foreach(var missile in Missiles)if(missile.State is <0 or >3||!Data.Weapons.ContainsKey(missile.Weapon)||missile.State==3&&(Data.Weapons[missile.Weapon].ProjectileKind!=4||missile.FlightRemaining is <0 or >844))throw new InvalidDataException("Invalid missile/ground-effect lifecycle");
  for(int at=0;at<Ground.Length;at++)if(Ground[at]==DiseaseGround&&!Missiles.Any(m=>m.State==3&&FieldCovers(m,new(at%Data.Scenario.Map.Width,at/Data.Scenario.Map.Width))))throw new InvalidDataException("Orphaned Disease ground reservation");
  foreach(string k in new[]{"visible","explored"}){var dst=k=="visible"?Sight.Visible:Sight.Explored;var src=R<bool[][]>(k);if(src.Length!=8)throw new InvalidDataException("Sight teams");for(int i=0;i<8;i++){if(src[i].Length!=dst[i].Length)throw new InvalidDataException("Sight size");src[i].CopyTo(dst[i],0);}}
  var teams=n["teams"]!.AsArray();if(teams.Count!=Teams.Length)throw new InvalidDataException("Team count");for(int i=0;i<Teams.Length;i++){var t=Teams[i];c.FieldsInto(teams[i]!["fields"]!,t);CommanderIds[i]=t.CommanderIds;var cities=teams[i]!["cities"]!.AsArray();var queues=teams[i]!["queues"]!.AsArray();if(cities.Count!=15||queues.Count!=4)throw new InvalidDataException("City/queue count");for(int j=0;j<15;j++){var a=cities[j]!;var city=t.Cities[j];city.Level=a["level"]!.GetValue<int>();city.InitializationPending=a["pending"]!.GetValue<bool>();int? slot=a["entity"]?.GetValue<int>();city.Entity=slot.HasValue?Slots[slot.Value]:null;if(slot.HasValue&&slot!=city.NativeId)throw new InvalidDataException("City identity");}for(int j=0;j<4;j++){c.FieldsInto(queues[j]!["fields"]!,t.Production[j]);t.Production[j].Types.AddRange(c.Read<int[]>(queues[j]!["types"]));}}
  Session.MaximumArmy=R<int>("maximumArmy");Session.PreventHarvesterRetraction=R<bool>("preventRetraction");var remaining=R<int?[]>("scriptRemaining");if(remaining.Length!=128)throw new InvalidDataException("Script count");for(int i=0;i<128;i++){if((remaining[i]==null)!=(Session.ScriptBlocks[i]==null))throw new InvalidDataException("Script identity");if(Session.ScriptBlocks[i] is {} b)b.Remaining=remaining[i]!.Value;}
  var sources=n["sources"]!.AsArray();if(sources.Count!=Session.Sources.Length)throw new InvalidDataException("Source count");for(int i=0;i<sources.Count;i++){var a=Session.Sources[i];a.Initialized=sources[i]!["Initialized"]!.GetValue<byte>();var members=c.Read<int[]>(sources[i]!["Members"]);if(members.Length!=a.Members.Length)throw new InvalidDataException("Source membership");members.CopyTo(a.Members,0);}
 }
}
public sealed partial class OriginalMission {
 public string RunId {get;private set;}=Guid.NewGuid().ToString("N");
 public JsonObject Snapshot(){if(World.Faulted)throw new InvalidOperationException("Cannot save a faulted mission");var c=new SaveCodec(World.Data);var n=new JsonObject{["schema"]=MissionSaveSchema.For(World.Data),["campaign"]=CampaignId,["scenario"]=World.Data.Scenario.Scenario,["content"]=SaveCodec.Content(World.Data),["seed"]=World.Seed,["runId"]=RunId,["profile"]=c.Node(World.Session.Profile),["world"]=World.SaveState(c),["clock"]=Milliseconds,["messageIndex"]=MessageIndex,["messages"]=c.Node(Messages),["outcome"]=c.Node(Outcome),["finalized"]=outcomeFinalized,["resultProfile"]=c.Node(ResultProfile)};if(World.Data.ContentId==OriginalContent.Council){n["pack"]=OriginalContent.Council;n["ruleset"]="council-wars-1";}if(War!=null){n["warRevision"]=WarConfiguration.Revision;n["war"]=c.Node(War);}return n;}
 public static OriginalMission Restore(OriginalDefinitions data,JsonObject n){
  WarState? war=null;
  if(n["campaign"]?.GetValue<string>()=="war"){
   if(n["warRevision"]?.GetValue<int>()!=WarConfiguration.Revision||n["schema"]?.GetValue<int>()!=MissionSaveSchema.Revision)throw new InvalidDataException("Unsupported War save revision");
   war=new SaveCodec(data).Read<WarState>(n["war"]??throw new InvalidDataException("Missing War match identity"));
   if(war==null||war.Configuration==null||war.Starts.Length!=8||war.VisionOffers.Length!=8||war.Eliminated.Length!=8||war.VisionOffers.Any(v=>v<0||v>255)||n["profile"]!=null||n["resultProfile"]!=null)throw new InvalidDataException("Incomplete War state");
   data=war.Configuration.Load(data.DataRoot,war.Starts);
  }else if(!OriginalCampaign.Supported(data.Scenario.Scenario))throw new InvalidDataException("This mission is not available in this build");
  bool legacySchema=n["schema"]==null||n["schema"]!.GetValue<int>()==1;
  n=MissionSaveSchema.Upgrade(n,data);
  if(n["campaign"]?.GetValue<string>()!=(war!=null?"war":OriginalCampaign.Identity(data.Scenario.Scenario)))throw new InvalidDataException("Campaign identity does not match this save");
  // Pre-A05 saves had only a local profile rank; nonlocal native ranks start at0.
  // Preserve existing entity identities/poses, bind future arrivals from this state.
  if(legacySchema&&n["world"] is JsonObject rankWorld&&!rankWorld.ContainsKey("commanderRanks")&&data.Scenario.Scenario is "SCENARIO/ALIEN/ALIEN01.SCN" or "SCENARIO/ALIEN/ALIEN02.SCN" or "SCENARIO/ALIEN/ALIEN03.SCN" or "SCENARIO/ALIEN/ALIEN04.SCN"){
   n=(JsonObject)n.DeepClone();var ranks=new int[8];ranks[0]=n["profile"]?["Rank"]?.GetValue<int>()??0;n["world"]!["commanderRanks"]=JsonSerializer.SerializeToNode(ranks);
  }
  // Explicit additive migration of pre-A04 v1 saves. Those three scenarios had no
  // resource pickups or implemented burst roles: earned = balance + spent - start.
  // A04 always requires its counters; a partial A04 snapshot is never defaulted.
  if(legacySchema&&n["world"] is JsonObject oldWorld&&!oldWorld.ContainsKey("combatResources")&&data.Scenario.Scenario is "SCENARIO/ALIEN/ALIEN01.SCN" or "SCENARIO/ALIEN/ALIEN02.SCN" or "SCENARIO/ALIEN/ALIEN03.SCN"){
   n=(JsonObject)n.DeepClone();var w=n["world"]!;var teams=w["teams"]!.AsArray();var earned=Enumerable.Range(0,8).Select(i=>teams[i]!["fields"]!["Money"]!.GetValue<int>()+teams[i]!["fields"]!["SpentResources"]!.GetValue<int>()-data.Scenario.Teams[i].Money).ToArray();
   w["combatResources"]=new JsonObject{["earned"]=JsonSerializer.SerializeToNode(earned),["bursts"]=JsonSerializer.SerializeToNode(new int[800])};
  }
  string canonicalContent=SaveCodec.Content(data);
  if(n["content"]!.GetValue<string>()!=canonicalContent&&n["content"]!.GetValue<string>()==SaveCodec.LegacyRewardContent(data)){n=(JsonObject)n.DeepClone();n["content"]=canonicalContent;}
  if(n["scenario"]!.GetValue<string>()!=data.Scenario.Scenario||n["content"]!.GetValue<string>()!=canonicalContent)throw new InvalidDataException("Required mission content does not match this save");var c=new SaveCodec(data);var m=new OriginalMission(data,n["seed"]!.GetValue<int>(),c.Read<OriginalProfileContext>(n["profile"]),true,war?.Configuration);if(war!=null)m.War=war;
  m.World.RestoreState(n["world"]!,c);m.RunId=n["runId"]!.GetValue<string>();if(!Guid.TryParseExact(m.RunId,"N",out _))throw new InvalidDataException("Invalid run identity");m.Milliseconds=n["clock"]!.GetValue<long>();m.MessageIndex=n["messageIndex"]!.GetValue<int>();m.Messages.AddRange(c.Read<NativeMessage[]>(n["messages"]));m.Outcome=c.Read<OriginalOutcome>(n["outcome"]);m.outcomeFinalized=n["finalized"]!.GetValue<bool>();m.ResultProfile=c.Read<OriginalProfileContext>(n["resultProfile"]);
  if(m.ResultProfile!=null&&(m.ResultProfile.CampaignId!=m.CampaignId||m.ResultProfile.CampaignIndex!=m.Outcome?.NextIndex))throw new InvalidDataException("Result campaign identity");
  foreach(var t in m.World.Teams)if(t.Strategy is {} ai){if(ai.ProductionWeights==null||ai.ProductionWeights.Length!=9||ai.ProductionWeights.Any(v=>v<short.MinValue||v>short.MaxValue)||ai.Zones.Length!=256||ai.Memory.Length!=800||ai.Agents.Length!=800||ai.Tasks.Length!=4||ai.Tasks.Any(g=>g.Length!=16)||ai.Tasks.SelectMany(g=>g).Any(g=>g.Counts.Length!=9||g.Members.Any(id=>id<0||id>=800)))throw new InvalidDataException("Invalid strategy state");}
  if(m.World.Entities.Any(e=>e.Target< -1||e.Target>=800||e.Body==null||e.Blood==null||e.Overlay==null)||m.World.Missiles.Any(p=>p.Shooter<0||p.Shooter>=800||m.World.Slots[p.Shooter]==null||!data.Weapons.ContainsKey(p.Weapon)))throw new InvalidDataException("Invalid entity/projectile reference");
  if(m.MessageIndex<0||m.MessageIndex>m.Messages.Count||m.Milliseconds<0||!JsonNode.DeepEquals(n,m.Snapshot()))throw new InvalidDataException("Incomplete or inconsistent mission snapshot");return m;
 }
}
