using System.Text.Json;
namespace DarkColony.Original;

public sealed record OriginalIssuedOrder(long Tick,NativeCommand Command,bool Accepted);

public sealed partial class OriginalWorld {
 // Diagnostic state is not gameplay state. Preserve the first fault and never retry its partial update.
 public string? FaultJson {get;private set;}
 public bool Faulted {get;private set;}
 public int DispatchSlot {get;private set;}=-1;
 public List<OriginalIssuedOrder> RecentOrders {get;}=new();
 object? movementDiagnostic,reservationDiagnostic;
 public string DiagnosticJson(Exception? error=null)=>JsonSerializer.Serialize(new {
  version=1,Tick,Seed,Phase,DayCounter,Coefficient,DispatchSlot,NextDynamicId,
  rng=new{Random.Index,Random.Draws},exception=error?.ToString(),
  movement=movementDiagnostic,reservation=reservationDiagnostic,recentOrders=RecentOrders,
  entity=DispatchSlot>=0&&Slots[DispatchSlot] is {} current?DiagnosticEntity(current):null,
  entities=Entities.Select(DiagnosticEntity),Ground,Air,Other,
  recentEvents=Events.TakeLast(64),missiles=Missiles.Select(m=>new{m.Id,m.Shooter,m.ShooterIncarnation,m.Weapon,m.X,m.Z,m.Height,m.Delay,m.Age,m.State})
 },new JsonSerializerOptions{WriteIndented=true,IncludeFields=true});
 object DiagnosticEntity(NativeEntity e)=>new {
  e.Identity,e.SourceIdentity,e.Slot,e.Incarnation,e.Type,e.Owner,e.State,e.Flag,e.X,e.Z,e.Height,e.Cell,e.Health,
  e.Angle,e.Energy,e.Inspired,e.DeathWord,e.Target,e.DestinationX,e.DestinationZ,e.YieldDirection,
  e.PendingOrder,e.Actions,speed=Data.Scenario.Types[e.Type].Fields[2],
  turnSpeed=Data.Scenario.Types[e.Type].Fields[1],weapon=Weapon(e.Type,e.Owner)?.Id,
  occupancy=new {ground=Indices(Ground,e.Slot),air=Indices(Air,e.Slot),other=Indices(Other,e.Slot)}
 };
 int[] Indices(int[] layer,int slot)=>Enumerable.Range(0,layer.Length).Where(i=>layer[i]==slot).ToArray();
 void RecordOrder(NativeCommand command,bool accepted) {
  RecentOrders.Add(new(Tick,command,accepted));if(RecentOrders.Count>128)RecentOrders.RemoveAt(0);
 }
 public void Step() {
  if(Faulted)throw new InvalidOperationException("Original simulation is halted after its first fault; restart with a fresh world.");
  try{StepCore();}
  catch(Exception error){
   Faulted=true; // Latch before diagnostics: diagnostic failure must never enable a retry.
   try{FaultJson=DiagnosticJson(error);}catch(Exception diagnosticError){FaultJson=JsonSerializer.Serialize(new{Tick,error=error.ToString(),diagnosticError=diagnosticError.ToString()});}
   throw;
  }
 }
}
