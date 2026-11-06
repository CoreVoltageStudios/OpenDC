using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace DarkColony.Original;

// Transport-neutral commands. Lobby slot identity is never a simulation owner.
public sealed record WarUnit(int Slot,int Incarnation);
public sealed record WarCommand(string Kind,int Owner,WarUnit[]? Units=null,OriginalOrder Order=OriginalOrder.Stop,
 int X=0,int Z=0,int Target=-1,int TargetIncarnation=0,int Value=0,bool Enabled=false);

public sealed class SynchronizedWar {
 public OriginalMission Mission {get;}
 public bool[] Departed {get;}=new bool[8];
 public bool[] Surrendered {get;}=new bool[8];
 public bool Paused {get;private set;}
 public bool Fast {get;private set;}
 readonly SaveCodec codec;
 readonly MemoryStream hashBuffer=new(1024*1024);
 public SynchronizedWar(string root,WarConfiguration configuration,int localSlot){
  if(!configuration.Multiplayer||localSlot is <0 or >7||configuration.Slots[localSlot].Controller is not (WarController.Local or WarController.Remote))throw new InvalidDataException("Invalid network player assignment.");
  var frozen=configuration.Copy();Mission=new(frozen.Load(root),frozen.Seed,war:frozen,viewSlot:localSlot);
  Mission.World.PlayerOrdersAllowed=t=>!Mission.War!.Eliminated[t]&&!Departed[t]&&!Surrendered[t];
  codec=new(Mission.World.Data);
 }
 public bool Valid(WarCommand c,bool system=false){
  var w=Mission.World;
  if(c.Owner is <0 or >7||Mission.War!.Slot(c.Owner)==null)return false;
  if(c.Kind is "depart" or "surrender")return system;
  if(c.Kind is "pause" or "speed")return system&&c.Owner==Mission.War.LocalTeam;
  if(Departed[c.Owner]||Surrendered[c.Owner]||Mission.War.Eliminated[c.Owner]||Mission.AtOutcome)return false;
  if(c.Kind=="production")return c.Value>=0&&w.Data.Scenario.Dependencies.Any(d=>d.Id==c.Value);
  if(c.Kind is "alliance" or "vision")return c.Value is >=0 and <8&&c.Value!=c.Owner&&Mission.War.Slot(c.Value)!=null;
  if(c.Kind is not ("units" or "group")||!Enum.IsDefined(c.Order)||c.Units==null||c.Units.Length is <1 or >150||c.Units.Any(u=>u==null)||c.Units.Select(u=>u.Slot).Distinct().Count()!=c.Units.Length)return false;
  if(c.X<0||c.Z<0||c.X>=w.Data.Scenario.Map.Width*256||c.Z>=w.Data.Scenario.Map.Height*256)return false;
  foreach(var u in c.Units)if(u.Slot is <0 or >=800||w.Slots[u.Slot] is not {Living:true} e||e.Incarnation!=u.Incarnation||e.Owner!=c.Owner)return false;
  if(c.Order==OriginalOrder.Attack){
   if(c.Target is <0 or >=800||w.Slots[c.Target] is not {Living:true} target||target.Incarnation!=c.TargetIncarnation)return false;
   // Entitled sight, including reciprocal vision, is checked at execution time.
   if(!w.Visible(c.Owner,target.Cell))return false;
  }
  return c.Kind!="group"||c.Order is OriginalOrder.MoveOnly or OriginalOrder.MoveAttack or OriginalOrder.Patrol;
 }
 public bool Apply(WarCommand c,bool system=false){
  if(!Valid(c,system))return false;
  var w=Mission.World;
  if(c.Kind is "depart" or "surrender"){
   if(c.Kind=="surrender")Surrendered[c.Owner]=true;
   if(!Departed[c.Owner]){Departed[c.Owner]=true;w.Teams[c.Owner].Ai=3;}
   // Native41db42 AI3 takeover. No reservation subtraction: ODC purchases
   // already commit atomically, rather than native's pending-money accumulator.
   return true;
  }
  if(c.Kind=="pause"){Paused=c.Enabled;return true;}
  if(c.Kind=="speed"){Fast=c.Enabled;return true;}
  w.OrderedIssuer=c.Owner;
  try{
   if(c.Kind=="production")return w.RequestProduction(c.Owner,c.Value).Accepted;
   if(c.Kind is "vision" or "alliance")return Mission.OfferWarRelationship(c.Owner,c.Value,c.Kind=="vision",c.Enabled);
   if(c.Kind=="group")w.CommandGroup(c.Units!.Select(u=>w.Slots[u.Slot]!),c.Order,new(c.X>>8,c.Z>>8));
   else foreach(var u in c.Units!)w.Command(new(u.Slot,u.Incarnation,c.Order,c.X,c.Z,c.Target,c.Owner));
   return true;
  }finally{w.OrderedIssuer=null;}
 }
 public void Step(){if(Paused||Mission.AtOutcome)return;Mission.SetClock((Mission.World.Tick+1)*66);Mission.Step();}
 public JsonObject Canonical(){
  var world=Mission.World.SaveState(codec);
  // Derived UI eligibility is refreshed by local presentation; never a rule input.
  foreach(var team in world["teams"]!.AsArray())team!["fields"]!.AsObject().Remove("AvailabilityCache");
  return new(){["world"]=world,["clock"]=Mission.Milliseconds,["war"]=codec.Node(Mission.War),
   ["departed"]=codec.Node(Departed),["surrendered"]=codec.Node(Surrendered),["paused"]=Paused,["fast"]=Fast,
   ["outcome"]=Mission.Outcome==null?null:codec.Node(new{Mission.Outcome.Winner,Mission.Outcome.Deadline})};
 }
 public string Hash()=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical().ToJsonString())));
 public byte[] CanonicalBytes(){
  hashBuffer.SetLength(0);using(var writer=new Utf8JsonWriter(hashBuffer)){
   void N<T>(string key,T value){writer.WritePropertyName(key);JsonSerializer.Serialize(writer,value,codec.Options);}
   writer.WriteStartObject();writer.WritePropertyName("world");Mission.World.WriteCanonical(writer,codec);N("clock",Mission.Milliseconds);N("war",Mission.War);N("departed",Departed);N("surrendered",Surrendered);N("paused",Paused);N("fast",Fast);N("outcome",Mission.Outcome==null?null:new{Mission.Outcome.Winner,Mission.Outcome.Deadline});writer.WriteEndObject();
  }return hashBuffer.ToArray();
 }
 public static string? FirstDifference(JsonNode? a,JsonNode? b,string path="$ "){
  if(a is JsonObject ao&&b is JsonObject bo){foreach(var k in ao.Select(p=>p.Key).Union(bo.Select(p=>p.Key)).Order())if(FirstDifference(ao[k],bo[k],path+"."+k) is {} d)return d;return null;}
  if(a is JsonArray aa&&b is JsonArray ba){if(aa.Count!=ba.Count)return path+".length";for(int i=0;i<aa.Count;i++)if(FirstDifference(aa[i],ba[i],path+"["+i+"]") is {} d)return d;return null;}
  return JsonNode.DeepEquals(a,b)?null:path+": "+a+" != "+b;
 }
}
