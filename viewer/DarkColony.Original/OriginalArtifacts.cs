namespace DarkColony.Original;
public sealed partial class OriginalWorld {
 void ArtifactDeath(NativeEntity e){
  //416338: no damage/kill-stat credit; retain health, but run city loss and
  // ordinary commander recovery.437098 removes the occupancy separately.
  e.State=10;e.DeathWord=0;e.PendingOrder=null;e.Actions.Clear();e.Actions.Add(new(10));ClearOccupancy(e);EconomicDeath(e);Emit("death",e);
  if(IsCommander(e))CommanderRecovery?.Invoke(this,e);
 }
 void ActivateSolaris(NativeEntity source){
  //417b87..417bd4: stationary ground-level weapon46, zero flight counter,
  // no launch-anchor scatter/cooldown. BOOM11 supplies the original blast matrix.
  var missile=new NativeMissile{Id=nextMissile++,Shooter=source.Slot,ShooterIncarnation=source.Incarnation,Weapon=46,
   X=source.X,Z=source.Z,Height=0,OriginX=source.X,OriginZ=source.Z,LaunchTick=Tick,State=1,FlightRemaining=0,VisualPhase=Random.Next()&255};
  Missiles.Insert(0,missile);Emit("launch",source,-1,46);source.Health=1;ArtifactDeath(source);Emit("solaris-activated",source);
 }
 void ActivateMaktor(NativeEntity source){
  var center=source.Cell;int x256=source.X,z256=source.Z;ArtifactDeath(source);Emit("maktor-activated",source);
  //416c6c: inclusive15x15, ground then air, every owner.4169a0 excludes
  // immobile/dead entities, clears occupancy/orders and pulls without kill credit.
  for(int x=center.X-7;x<=center.X+7;x++)for(int z=center.Z-7;z<=center.Z+7;z++){
   var cell=new Cell(x,z);if(!Navigation.Inside(cell))continue;
   for(int layer=0;layer<2;layer++){
    int id=Occupant(cell,layer==0?Ground:Air);if(id<0||Slots[id] is not {Living:true} target||Data.Scenario.Types[target.Type].Fields[2]==0||DamageProtected(target))continue;
    int dx=target.X-x256,dy=target.Height,dz=target.Z-z256,distance=Math.Abs(dx)+Math.Abs(dy)+Math.Abs(dz);
    ClearOccupancy(target);target.PendingOrder=null;target.Actions.Clear();target.State=10;target.DeathWord=0;
    // Coincident ground target has zero distance: finish instead of native /0.
    if(distance==0){ArtifactDeath(target);continue;}
    target.Actions.Add(new(23){Word=distance,Vx=dx*256/distance,Health=dy*256/distance,Vz=dz*256/distance,Angle=(Data.Reference.Angle(dx,dz)+128)&255});
    target.Body.Start(Data.Clips[target.Type].Move,0);Emit("maktor-pull",target,source.Slot);
   }
  }
 }
 void MaktorStep(NativeEntity e,NativeAction a){
  //416b58: turn toward center; inverse-distance acceleration, integer truncation.
  Turn(e,a.Angle);if(a.Word<256){ArtifactDeath(e);return;}
  int speed=65536/Math.Min(a.Word,1024),dx=a.Vx*speed/512,dy=a.Health*speed/512,dz=a.Vz*speed/512;
  a.Word=unchecked((short)(a.Word-Math.Abs(dx)-Math.Abs(dy)-Math.Abs(dz)));
  e.X=unchecked((ushort)(e.X-dx));e.Height=unchecked((ushort)(e.Height-dy));e.Z=unchecked((ushort)(e.Z-dz));
 }
 void ActivateKaox(NativeEntity source){
  //417804: source plus four freshly allocated balls at its exact3D anchor.
  source.PendingOrder=null;source.Actions.Clear();source.Body.Start(Data.Clips[66].Fire[0],0);ClearOccupancy(source);
  for(int i=0;i<4;i++){var ball=Spawn(66,source.Owner,source.Cell,"kaox/"+source.Identity+"/"+i);ClearOccupancy(ball);ball.X=source.X;ball.Z=source.Z;ball.Height=source.Height;ball.Actions.Clear();ball.Actions.Add(new(24));}
  source.Actions.Add(new(24));Emit("kaox-activated",source);
 }
 void KaoxStep(NativeEntity e,NativeAction a){
  //417544: descend first; ground-only hits, direct400HP, city floor600.
  // Every timed turn/collision consumes the original three RNG draws;33rd ends.
  int speed=Data.Scenario.Types[e.Type].Fields[2];if(e.Height>0){e.Height=Math.Max(0,e.Height-speed*2);return;}
  bool turn=a.Word==0;if(!turn)a.Word--;
  int id=Occupant(e.Cell,Ground);if(id>=0&&Slots[id] is {} target){
   turn=true;if(!DamageProtected(target)){int hp=target.Health;if(target.Slot>=120||hp>600)hp-=400;if(hp<0)ArtifactDeath(target);else target.Health=hp;}
  }
  int oldX=e.X,oldZ=e.Z;e.X=unchecked((ushort)(e.X+a.Vx));e.Z=unchecked((ushort)(e.Z+a.Vz));
  if(e.X>32768||e.Z>32768||!Navigation.Inside(e.Cell)){e.X=oldX;e.Z=oldZ;turn=true;}
  if(!turn)return;
  a.Word=(Random.Next()&8)+4;if(++a.Health>32){ArtifactDeath(e);return;}
  int sign=(Random.Next()&1)==0?1:-1;e.Angle=(e.Angle+sign*((Random.Next()&31)+48))&255;
  var trig=Data.Reference.SinCos(e.Angle*32);a.Vx=trig.Cos*speed/800;a.Vz=trig.Sin*speed/800;
 }
 // Native site registry/FIFO (440760..440aa0). Progress is the site's idle Word;
 // worker identity is resolved from authoritative ground occupancy every update.
 public Dictionary<int,List<int>> ArtifactContents {get;}=new();
 public bool CanDiscoverArtifacts(int team)=>team is >=0 and <8&&Teams[team].Cities[4].Health>0;
 public NativeEntity? ArtifactAt(Cell cell)=>Entities.FirstOrDefault(e=>e.Living&&e.Type==37&&e.Cell==cell&&ArtifactContents.ContainsKey(e.Slot));
 public NativeEntity? ArtifactWorker(NativeEntity site){int id=Occupant(site.Cell,Ground);return id>=0&&Slots[id] is {Living:true} e&&Excavating(e)?e:null;}
 bool Excavating(NativeEntity worker)=>worker.Type is 6 or 14&&worker.Owner<8&&CanDiscoverArtifacts(worker.Owner)&&
  worker.Actions.Count>0&&worker.Actions[0].Kind==1&&ArtifactAt(worker.Cell)!=null&&Occupant(worker.Cell,Ground)==worker.Slot;
 bool ArtifactStep(NativeEntity site,NativeAction action){
  int id=Occupant(site.Cell,Ground);var worker=id>=0?Slots[id]:null;
  if(worker is not {Living:true}||!Excavating(worker)){
   action.Word=450;return false;
  }
  // 4131ec -> type+9c, bound to original FUNK by43c43a..43c48b.
  worker.Body.Start(Data.Clips[worker.Type].Critical,0);worker.YieldDirection=-1;
  if(--action.Word>0)return false;
  action.Word=450;
  if(!ArtifactContents.TryGetValue(site.Slot,out var contents)||contents.Count==0){
   worker.Body.Start(Data.Clips[worker.Type].Stand,0);Retire(site);Emit("artifact-exhausted",site);return false;
  }
  int type=contents[0];contents.RemoveAt(0);
  var artifact=Spawn(type,worker.Owner,site.Cell,"artifact/"+site.Identity+"/"+Tick);
  Emit("artifact-excavated",artifact,site.Slot,type);AudioCue("artifact-found",artifact);
  return false;
 }
 void ActivateLunatek(NativeEntity source){
  var center=source.Cell;KillEconomicEntity(source);Emit("lunatek-activated",source);
  //416d8c: inclusive15x15 square; ground then air. No ownership transfer.
  // Invalid/non-player occupant ends that column's Z scan in the source.
  for(int x=center.X-7;x<=center.X+7;x++)for(int z=center.Z-7;z<=center.Z+7;z++){
   var cell=new Cell(x,z);if(!Navigation.Inside(cell))continue;bool endColumn=false;
   foreach(var layer in new[]{Ground,Air}){
    int id=Occupant(cell,layer);if(id<0)continue;
    var target=Slots[id];if(target is not {Living:true,Owner:<8}){endColumn=true;break;}
    target.PendingOrder=new(target.Slot,target.Incarnation,OriginalOrder.Stop,Issuer:target.Owner);
    target.Berserk=(Random.Next()&31)+25;Emit("berserk",target,source.Slot,target.Berserk);
   }
   if(endColumn)break;
  }
 }
 void ActivateTektaara(NativeEntity source){
  var center=source.Cell;KillEconomicEntity(source);Emit("tektaara-activated",source);
  // DC16 4173dc: inclusive128-cell square, X then Z, ground then air.
  // Budget uses native stat6 (allocated troops, including corpses), not sight.
  int budget=ArmyLimit;for(int type=0;type<Live.GetLength(1);type++)budget-=Live[source.Owner,type];
  for(int x=center.X-128;x<=center.X+128;x++)for(int z=center.Z-128;z<=center.Z+128;z++){
   if(budget<0)return;var cell=new Cell(x,z);if(!Navigation.Inside(cell))continue;
   Capture(Occupant(cell,Ground));Capture(Occupant(cell,Air));
  }
  void Capture(int id){
   if(id<0||Slots[id] is not {Owner:9} target)return;
   target.Owner=source.Owner;budget--;Emit("tektaara-captured",target,source.Slot);
   // Native changes only ownership. Keep pending orders and source membership.
  }
 }
}
