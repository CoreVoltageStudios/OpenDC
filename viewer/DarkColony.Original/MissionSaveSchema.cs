using System.Reflection;
using System.Text.Json.Nodes;
namespace DarkColony.Original;
// Envelope/content versions are independent. Revision8 restores the two native
// campaign milestone medal slots and artifact action23(Maktor)/24(Kaox) using
// the existing action fields. Revision7 adds explicit campaign/profile identity.
// Revision6 adds AI3 production
// weights configured by original aimsg. Revision5 adds the miner's siphon
// claim. Revision4 introduces action18/order7,
// missile state3 and disease-owned ground reservations; existing fields are reused.
// Revision3 added healed-health totals.
// Revision2 added artifact inventories/berserk. Legacy revision1 is only A01..A05.
public static class MissionSaveSchema {
 public const int Revision=8; // Existing base/War snapshots remain byte-for-byte compatible.
 public const int CouncilRevision=10; // Adds persistent Council palace conversion flags; base/War stay8.
 public static int For(OriginalDefinitions data)=>data.ContentId==OriginalContent.Council?CouncilRevision:Revision;
 static readonly (Type Type,string Fields)[] inventory={
  (typeof(NativeEntity),"Slot Incarnation Type Owner X Z Height Health Flag Angle Energy State YieldDirection DestinationX DestinationZ Target IdleCycles LastIdleHealth ResourceFlow Inspired InspiringSlot DeathWord Berserk PendingBlood StrategyHit SourceIdentity PendingOrder Patrol Actions Body Blood Overlay"),
  (typeof(NativeAction),"Kind Word Health Mode Vx Vz Angle Siphoner Previous Destination Route Rejoin Retained"),
  (typeof(NativeMissile),"Id Shooter Weapon X Z Height Vx Vz Vh Angle Delay Age State ShooterIncarnation FlightRemaining VisualPhase OriginX OriginZ Target LaunchTick Animation"),
  (typeof(NativeTeamState),"Race Money Ai Color CommanderRecoveryDisabled AutomaticIncome Rally Base Allies AiAssignments WeaponTiers ArmorTiers AvailabilityCache ProductionIds CommanderIds DisabledDependencies SpentResources MiningMultiplier ConstructionSlot Strategy"),
  (typeof(NativeProductionQueue),"Building Cooldown ReservedExit StartedTick"),
  (typeof(NativeStrategyState),"Zones Memory Agents Tasks Updates ProductionRule ProductionResult FirstAssignment ProductionWeights"),
  (typeof(NativeStrategyZone),"Center Distance Neighbors AirOwner AirPower GroundOwner GroundPower FlyingOwner FlyingPower Unarmed Flags"),
  (typeof(NativeStrategyAgent),"Slot Incarnation Task Group Status Zone Stuck Last"),
  (typeof(NativeStrategyGroup),"Active CurrentZone TargetZone Mode Cursor Route Members Counts"),
  (typeof(NativeTransport),"Slot Team Reservation Stage Cursor DestinationX DestinationZ Requested Manifest Source"),
  (typeof(OriginalProfileContext),"CampaignId CampaignIndex Rank PromotionScore CommanderKills Medals")
 };
 static readonly Lazy<bool> verified=new(()=>{
  foreach(var (type,fields) in inventory){
   var actual=type.GetFields(BindingFlags.Public|BindingFlags.Instance).Select(f=>f.Name).Order(StringComparer.Ordinal);
   if(!actual.SequenceEqual(fields.Split(' ').Order(StringComparer.Ordinal)))throw new InvalidOperationException("Save field inventory changed: "+type.Name+". Review schema revision and migration.");
  }
  return true;
 });
 public static void VerifyInventory(){_ = verified.Value;}
 public static JsonObject Upgrade(JsonObject input,OriginalDefinitions data){
  int revision=input["schema"]?.GetValue<int>()??1;
  if(data.ContentId==OriginalContent.Council){
   if(revision is not (9 or CouncilRevision)||input["pack"]?.GetValue<string>()!=OriginalContent.Council||input["ruleset"]?.GetValue<string>()!="council-wars-1")throw new InvalidDataException("Unsupported or incomplete Council Wars save identity");
   if(revision==9){
    // Only released CW1..7 saves existed before palace state. Reject a falsely
    // tagged finale or new-state snapshot rather than silently discard flags.
    int index=OriginalCampaign.For(data).Single(m=>m.Scenario==data.Scenario.Scenario).Index;
    if(index>=7||input["world"]!.AsObject().ContainsKey("councilPalaces"))throw new InvalidDataException("Incorrect old-schema Council palace state");
    var upgraded=(JsonObject)input.DeepClone();upgraded["world"]!["councilPalaces"]=new JsonArray(Enumerable.Range(0,8).Select(_=>(JsonNode?)JsonValue.Create(false)).ToArray());upgraded["schema"]=CouncilRevision;return upgraded;
   }
   return input;
  }
  if(input.ContainsKey("pack")||input.ContainsKey("ruleset"))throw new InvalidDataException("Expansion save cannot load as base content");
  if(revision==Revision)return input;
  if(revision==7){
   if(!OriginalCampaign.Supported(data.Scenario.Scenario)||OriginalCampaign.IsTraining(OriginalCampaign.Identity(data.Scenario.Scenario)))throw new InvalidDataException("Unsupported revision7 mission");
   if(input["world"]!["slots"]!.AsArray().Any(e=>e!=null&&e["Actions"]!.AsArray().Any(a=>a!["Kind"]!.GetValue<int>() is 23 or 24)))throw new InvalidDataException("Incorrect old-schema artifact state");
   var upgraded=(JsonObject)input.DeepClone();UpgradeMedals(upgraded);upgraded["schema"]=Revision;return upgraded;
  }
  if(revision is not (1 or 2 or 3 or 4 or 5 or 6)||!Enumerable.Range(1,revision==1?5:revision==2?6:revision==3?8:revision==4?10:revision==5?14:15).Any(n=>data.Scenario.Scenario==$"SCENARIO/ALIEN/ALIEN{n:00}.SCN"))
   throw new InvalidDataException("Unsupported mission save schema; this build supports revision8, revision7 Human/Taar, revision6 Taar A01-A15, revision5 A01-A14, revision4 A01-A10, revision3 A01-A08, revision2 A01-A06 and legacy A01-A05.");
  var n=(JsonObject)input.DeepClone();var world=n["world"]!.AsObject();
  if(revision<4&&(world["ground"]!.AsArray().Any(v=>v!.GetValue<int>()< -2)||world["missiles"]!.AsArray().Any(v=>v!["State"]!.GetValue<int>()==3)||world["slots"]!.AsArray().Any(v=>v!=null&&(v["Actions"]!.AsArray().Any(a=>a!["Kind"]!.GetValue<int>()==18)||v["PendingOrder"]?["Order"]?.GetValue<int>()==7))))throw new InvalidDataException("Incorrect schema revision on Disease state");
  if(revision<5)foreach(var slot in world["slots"]!.AsArray())if(slot!=null){
   if(slot["Type"]!.GetValue<int>() is 77 or 78)throw new InvalidDataException("Incorrect schema revision on siphoning state");
   foreach(var action in slot["Actions"]!.AsArray()){
    if(action!.AsObject().ContainsKey("Siphoner"))throw new InvalidDataException("Incorrect schema revision on siphon claim");
    action["Siphoner"]=-1;
   }
  }
  if(revision<3&&world.ContainsKey("healedHealth"))throw new InvalidDataException("Incorrect schema revision on healing-aware state");
  if(revision==1){
   if(world.ContainsKey("artifacts")||world["slots"]!.AsArray().Any(s=>s is JsonObject o&&o.ContainsKey("Berserk")))
   throw new InvalidDataException("Missing/incorrect schema revision on artifact-aware state");
   foreach(var slot in world["slots"]!.AsArray())if(slot is JsonObject entity)entity["Berserk"]=0;
   world["artifacts"]=new JsonObject();
  }
  if(revision<3)world["healedHealth"]=new JsonArray(Enumerable.Range(0,8).Select(_=>(JsonNode?)JsonValue.Create(0)).ToArray());
  if(revision<6)foreach(var team in world["teams"]!.AsArray())if(team!["fields"]!["Strategy"] is JsonObject strategy){
   if(strategy.ContainsKey("ProductionWeights"))throw new InvalidDataException("Incorrect schema revision on configurable strategy");
   strategy["ProductionWeights"]=new JsonArray(new[]{1,2,1,2,4,2,10000,4,10000}.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray());
  }
  if(n.ContainsKey("campaign"))throw new InvalidDataException("Incorrect schema revision on campaign context");
  n["campaign"]="taar";
  foreach(string key in new[]{"profile","resultProfile"})if(n[key] is JsonObject profile){if(profile.ContainsKey("CampaignId"))throw new InvalidDataException("Incorrect schema revision on profile context");profile["CampaignId"]="taar";}
  UpgradeMedals(n);n["schema"]=Revision;return n;
 }
 static void UpgradeMedals(JsonObject n){
  foreach(string key in new[]{"profile","resultProfile","outcome"})if(n[key] is JsonObject record&&record["Medals"] is JsonArray medals){
   if(medals.Count!=4)throw new InvalidDataException("Incorrect old-schema campaign medal inventory");
   medals.Add(-1);medals.Add(-1);
  }
 }
}
