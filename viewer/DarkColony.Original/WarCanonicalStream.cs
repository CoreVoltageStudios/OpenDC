using System.Reflection;
using System.Text.Json;
namespace DarkColony.Original;

// Same explicit save-domain inventory, streamed without allocating a JsonNode
// for every occupancy/visibility cell. Cross-checked against Canonical() in proof.
public sealed partial class OriginalWorld {
 internal void WriteCanonical(Utf8JsonWriter writer,SaveCodec codec){
  void N<T>(string name,T value){writer.WritePropertyName(name);JsonSerializer.Serialize(writer,value,codec.Options);}
  void Fields(object o,bool team=false){writer.WriteStartObject();foreach(var f in o.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance)){if(team&&f.Name=="AvailabilityCache")continue;writer.WritePropertyName(f.Name);JsonSerializer.Serialize(writer,f.GetValue(o),f.FieldType,codec.Options);}writer.WriteEndObject();}
  writer.WriteStartObject();N("healedHealth",HealedHealth);N("artifacts",ArtifactContents);N("commanderRanks",CommanderRanks);
  writer.WritePropertyName("combatResources");writer.WriteStartObject();N("earned",EarnedResources);N("bursts",ShotCounters);writer.WriteEndObject();
  N("tick",Tick);N("phase",Phase);N("day",DayCounter);N("coefficient",Coefficient);N("nextDynamic",NextDynamicId);N("nextMissile",nextMissile);N("aiCursor",aiCursor);N("armyLimit",ArmyLimit);N("rng",new long[]{Random.Index,Random.Draws});N("navigation",Navigation.Generation);
  N("slots",Slots);N("ground",Ground);N("air",Air);N("other",Other);N("relations",Relations.Cast<bool>());N("deaths",Deaths.Cast<int>());N("live",Live.Cast<int>());N("kills",Kills.Cast<int>());N("nearbyKills",NearbyCommanderKills);N("captures",Captures);N("visionBits",VisionBits);N("missiles",Missiles);N("transports",Transports);N("reservations",TransportReservations.Cast<bool>());N("visible",Sight.Visible);N("explored",Sight.Explored);
  writer.WritePropertyName("teams");writer.WriteStartArray();foreach(var t in Teams){writer.WriteStartObject();writer.WritePropertyName("fields");Fields(t,true);writer.WritePropertyName("cities");writer.WriteStartArray();foreach(var city in t.Cities){writer.WriteStartObject();N("level",city.Level);N("pending",city.InitializationPending);N("entity",city.Entity?.Slot);writer.WriteEndObject();}writer.WriteEndArray();writer.WritePropertyName("queues");writer.WriteStartArray();foreach(var q in t.Production){writer.WriteStartObject();writer.WritePropertyName("fields");Fields(q);N("types",q.Types);writer.WriteEndObject();}writer.WriteEndArray();writer.WriteEndObject();}writer.WriteEndArray();
  N("maximumArmy",Session.MaximumArmy);N("preventRetraction",Session.PreventHarvesterRetraction);N("scriptValues",Session.ScriptValues.Cast<int>());N("scriptRemaining",Session.ScriptBlocks.Select(b=>b?.Remaining));N("sources",Session.Sources.Select(s=>new{s.Initialized,s.Members}));writer.WriteEndObject();
 }
}
