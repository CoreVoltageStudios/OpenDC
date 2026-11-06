using DarkColony.Assets;
namespace DarkColony.Original;

// Explicit partial-execution mode: original block1 and autonomous units run;
// later strategy/events/outcomes are retained in Session but gated for M3.
public sealed class InterferenceDevelopment {
 public OriginalWorld World {get;}
 public bool OpeningDelivered=>World.Events.Count(e=>e.Kind=="unloaded"&&e.Other==7)==7;
 public InterferenceDevelopment(OriginalDefinitions data,int seed=0,int priorRank=0){
  if(!data.Scenario.Scenario.EndsWith("ALIEN02.SCN",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Interference development requires original ALIEN02");
  World=new(data,seed,0,new OriginalProfileContext(1,priorRank,0,Array.Empty<int>(),"M2 diagnostic prior-rank selection; no campaign result inferred"));
  World.NormalScan+=Opening;
 }
 void Opening(OriginalWorld w){
  var b=w.Session.ScriptBlocks[1]!;if(b.Remaining==0||w.Tick>>4<=0)return;
  foreach(var a in b.Actions){
   int[] p=a.Arguments;
   switch(a.Name){
    case "msg":break; // Development guidance is shown separately; original message/media routing is M3.
    case "waypoint":
     p=a.SourceText.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(int.Parse).ToArray();
     var subject=w.Entities.Single(e=>e.Living&&e.Cell==new Cell(p[0],p[1]));
     w.AssignPatrol(subject,Enumerable.Range(0,p[2]).Select(i=>new Cell(p[3+2*i],p[4+2*i])).ToArray());break;
    case "reinforce":case "reinforce2":
     var pairs=Enumerable.Range(0,5).Select(i=>(Type:p[3+2*i],Count:p[4+2*i])).ToArray();
     if(a.Name=="reinforce")w.Reinforce(p[0],new(p[1],p[2]),pairs,"TRO/1/"+a.SourceLine);
     else foreach(var pair in pairs)for(int i=0;i<pair.Count;i++)w.Spawn(pair.Type,p[0],new(p[1],p[2]),"TRO/1/"+a.SourceLine+"/"+i);
     break;
    default:throw new NotSupportedException("Opening action not supported: "+a.SourceText);
   }
  }
  b.Remaining=unchecked((byte)(b.Remaining-1));
 }
}

public sealed partial class OriginalWorld {
 // Player adapter validates identity/ownership through the same Command gate.
 // Original patrol action9 already owns the route and persists its current point.
 public bool CommandPatrol(int slot,int incarnation,int issuer,IReadOnlyList<Cell> points){
  if(points.Count is <1 or >8||points.Any(p=>!Navigation.Inside(p)))return false;
  if(!Command(new(slot,incarnation,OriginalOrder.Patrol,Issuer:issuer)))return false;
  Slots[slot]!.Patrol=points.ToArray();return true;
 }
 public void AssignPatrol(NativeEntity e,IReadOnlyList<Cell> points){
  if(points.Count is <1 or >8||points.Any(p=>!Navigation.Inside(p)))throw new ArgumentException("Native waypoint count/bounds");
  e.Patrol=points.ToArray();e.PendingOrder=new(e.Slot,e.Incarnation,OriginalOrder.Patrol,Issuer:e.Owner);Emit("patrol-assigned",e,points.Count);
 }
 bool PatrolStep(NativeEntity e,NativeAction a){
  if(e.PendingOrder!=null){ReplaceOrder(e);return true;}
  if(a.Word>=e.Patrol.Length)a.Word=0;var point=e.Patrol[a.Word++];e.DestinationX=point.X*256+128;e.DestinationZ=point.Z*256+128;
  Plan(e,1);return true; // 41623a EBX=1: native waypoint traversal acquires in range and resumes.
 }
}
