namespace DarkColony.Original;
public sealed partial class OriginalWorld {
 // Destination deconfliction, not a formation: preserve existing routes, layers and speeds.
 // Reserve distinct reachable endpoints once per player order, never each frame.
 public void CommandGroup(IEnumerable<NativeEntity> selection,OriginalOrder order,Cell destination){
  var units=selection.Where(e=>e.Living&&e.Owner==PlayerCommandTeam&&e.Type is not (47 or 48)&&Data.Scenario.Types[e.Type].Fields[2]>0).OrderBy(e=>e.Slot).ToArray();
  var vent=LiveVentAt(destination)??(CanDiscoverArtifacts(PlayerCommandTeam)?ArtifactAt(destination):null);
  var worker=vent==null?null:units.FirstOrDefault(e=>e.Type is 6 or 14);
  var reserved=new HashSet<(bool,Cell)>();
  foreach(var e in units.OrderBy(e=>e==worker?0:1)){
   bool flying=Data.Scenario.Types[e.Type].Flying!=0;Cell? chosen=null;
   for(int r=0;r<=8&&chosen==null;r++)for(int z=-r;z<=r&&chosen==null;z++)for(int x=-r;x<=r;x++){
    if(Math.Max(Math.Abs(x),Math.Abs(z))!=r)continue;
    var c=new Cell(destination.X+x,destination.Z+z);
    if(!Navigation.Inside(c)||(flying&&c.Z>Data.Scenario.Map.Height-3)||(!flying&&!Navigation.Ground(c))||reserved.Contains((flying,c)))continue;
    if(worker!=null&&e!=worker&&!flying&&c==destination)continue;
    int occupied=Occupant(c,Layer(e));
    // An allied mobile blocker can yield to the worker's exact resource order.
    // Occupied deployed harvesters/buildings are never displaced.
    bool canYield=e==worker&&c==destination&&occupied>=0&&Slots[occupied] is {} blocker&&Relations[e.Owner,blocker.Owner]&&blocker.Type is not (47 or 48)&&Data.Scenario.Types[blocker.Type].Fields[2]>0;
    if(occupied!=-1&&occupied!=e.Slot&&!canYield)continue;
    if(e==worker&&c!=destination)continue;
    if(!Navigation.Find(e.Cell,c,flying:flying).Found)continue;
    chosen=c;break;
   }
   if(chosen is {} target){reserved.Add((flying,target));Command(new(e.Slot,e.Incarnation,order,target.X*256+128,target.Z*256+128,Issuer:PlayerCommandTeam));}
   else {Command(new(e.Slot,e.Incarnation,OriginalOrder.Stop,Issuer:PlayerCommandTeam));Emit("move-unreachable",e);}
  }
 }
 NativeEntity? LiveVentAt(Cell cell){
  foreach(var e in Slots)if(e is {Living:true,Type:40,ResourceFlow:>0}&&e.Cell==cell)return e;
  return null;
 }
}
