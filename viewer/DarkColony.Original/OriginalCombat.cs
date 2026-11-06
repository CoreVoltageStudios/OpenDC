namespace DarkColony.Original;

public sealed partial class OriginalWorld {
 public bool GodMode {get;set;} // Session-only; deliberately absent from snapshots.
 bool DamageProtected(NativeEntity e)=>GodMode&&e.Owner==Session.LocalTeam;
 readonly Dictionary<int,(int Left,int Bottom,int Right,int Top)> hitBounds=new();
 public int Damage(int shooterType,int targetType,int targetTeam,int weaponId,int inspiration,int phase) {
  var weapon=Data.Weapons[weaponId];int factor=Data.DamageFactors[weapon.Class][Data.Scenario.Types[targetType].Fields[10]];
  int damage=(((factor*weapon.Damage>>8)*inspiration>>8)*Armor(targetType,targetTeam))>>8;
  bool unfavorable=Data.Scenario.Types[shooterType].Race==0?phase==1:phase==0;
  return unfavorable?damage*3>>2:damage;
 }
 void Fire(NativeEntity shooter,NativeEntity target) {
  if(target.State==0)return;var weapon=Weapon(shooter.Type,shooter.Owner);if(weapon==null)return;
  FireAt(shooter,weapon,target.X,target.Z,target.Height,target.Slot);
 }
 void FireAt(NativeEntity shooter,OriginalWeapon weapon,int targetX,int targetZ,int targetHeight,int targetSlot){
  if(weapon.Boom is not (0 or 1 or 2 or 5 or 9 or 12))throw new NotSupportedException("Unestablished BOOM behavior "+weapon.Boom);
  int face=Data.Reference.Angle(targetX-shooter.X,targetZ-shooter.Z);if(!Turn(shooter,face))return;
  var fire=Data.Clips[shooter.Type].Fire;var animation=fire[Random.Next()%fire.Length];shooter.Body.Start(animation,1);
  foreach(var anchor in animation.Anchors(shooter.Angle)) {
   int dx=targetX-anchor.X-shooter.X,dz=targetZ-anchor.Z-shooter.Z;
   if(weapon.Boom!=0){int draw=Random.Next()&255;var offset=ScatterOffset(weapon.Boom,draw,shooter.Inspired!=0);dx+=offset.X*256;dz+=offset.Z*256;}
   int angle=Data.Reference.Angle(dx,dz);var trig=Data.Reference.SinCos(angle*32);
   int vx=trig.Cos*weapon.Speed/2048,vz=trig.Sin*weapon.Speed/2048,vh=0;
   if(targetHeight!=shooter.Height) {
    int steps=Math.Abs(vx)>Math.Abs(vz)?(vx==0?0:dx/vx):(vz==0?0:dz/vz);
    if(steps!=0)vh=(targetHeight-shooter.Height)/steps;
   }
   var missile=new NativeMissile{Id=nextMissile++,Shooter=shooter.Slot,ShooterIncarnation=shooter.Incarnation,
    Weapon=weapon.Id,X=unchecked((ushort)(shooter.X+anchor.X)),Z=unchecked((ushort)(shooter.Z+anchor.Z)),Height=shooter.Height,
    Vx=vx,Vz=vz,Vh=vh,Angle=angle,Delay=anchor.Delay*4,State=anchor.Delay==0?1:0,
    OriginX=unchecked((ushort)(shooter.X+anchor.X)),OriginZ=unchecked((ushort)(shooter.Z+anchor.Z)),Target=targetSlot,LaunchTick=Tick};
   missile.VisualPhase=Random.Next()&255; // 441a60: retained even for straight missiles.
   if(weapon.Boom!=0)missile.FlightRemaining=Math.Abs(vx)>Math.Abs(vz)?(vx==0?0:dx/vx):(vz==0?0:dz/vz);
   if(Data.MissileBodies.GetValueOrDefault(weapon.Id) is {} body)missile.Animation.Start(body,0);
   if(shooter.Type is 45 or 46&&!DamageProtected(shooter)){shooter.Health-=300;if(shooter.Health<0)shooter.Health=1;}
   Missiles.Insert(0,missile);Emit("launch",shooter,targetSlot,weapon.Id);
   if(weapon.Raw[11]!=0)shooter.Energy=0; //41319f: weapon's alternate energy flag.
  }
  int cooldown=weapon.Rate;
  //4131b1..4131d9: count firing animations, not individual FIN launch anchors.
  if(weapon.Shots>0&&++ShotCounters[shooter.Slot]>=weapon.Shots){ShotCounters[shooter.Slot]=0;cooldown=weapon.Reload;}
  shooter.Actions.Add(new(11){Word=cooldown});
 }
 int[]? missileWave,missileArc;readonly List<NativeMissile> missileTrails=new();
 public Cell ScatterOffset(int boom,int draw,bool inspired=false){
  if(inspired)return new(0,0);int index;var scatter=Data.Booms[boom].Scatter;
  for(index=0;index<9&&draw>=scatter[index];index++)draw-=scatter[index];
  //43b694 truncates each weight.412f84..412f9b exits with BOTH counters3
  // when the six residual draws exhaust artillery's250/256 table. No renormalizing.
  return index==9?new(2,2):new(index%3-1,index/3-1);
 }
 void StepMissiles() {
  for(int sub=0;sub<4;sub++)foreach(var missile in Missiles) {
   if(missile.State==0){if(missile.Delay==0)missile.State=1;else missile.Delay--;}
   if(missile.State==3){StepDiseaseField(missile);continue;}
   if(missile.State!=1)continue;
   missile.X=unchecked((ushort)(missile.X+missile.Vx));missile.Z=unchecked((ushort)(missile.Z+missile.Vz));missile.Height=unchecked((ushort)(missile.Height+missile.Vh));
   var shooter=Slots[missile.Shooter]??throw new InvalidOperationException("Lost native shooter slot");
   var weapon=Data.Weapons[missile.Weapon];
   if(weapon.ProjectileKind==2){
    // 4427e8..442885: perpendicular oscillation from the original signed32 table;
    // phase advances per missile substep. Trail43 is a finite effect, not a weapon hit.
    int wave=(missileWave??=Data.Reference.Ints(0x48a948,32))[(missile.Age*4+missile.VisualPhase)&31];
    missile.X=unchecked((ushort)(missile.X+missile.Vz*wave/100));missile.Z=unchecked((ushort)(missile.Z-missile.Vx*wave/100));missile.Height=unchecked((ushort)(missile.Height+missile.Vh*wave/100));
    if(Data.MissileImpacts.GetValueOrDefault(43) is {} trail){var effect=new NativeMissile{Id=nextMissile++,Shooter=missile.Shooter,ShooterIncarnation=missile.ShooterIncarnation,Weapon=43,X=missile.X,Z=missile.Z,Height=missile.Height,State=2,LaunchTick=Tick};effect.Animation.Start(trail,1);missileTrails.Add(effect);}
   }
   if(weapon.Boom!=0){
    if(weapon.ProjectileKind is 1 or 4){int total=missile.FlightRemaining+missile.Age,index=total==0?0:missile.FlightRemaining*16/total;if(index is <0 or >16)throw new InvalidDataException("Native ballistic phase");missile.Height=unchecked((ushort)(((missileArc??=Data.Reference.Ints(0x48a904,17))[index]*total)>>6));}
    if(--missile.FlightRemaining<=0){Blast(shooter,missile);missile.Age++;continue;}
    if(++missile.Age>weapon.Lifetime)missile.State=4;continue;
   }
   int hit=HitAt(missile.X,missile.Z,shooter.Berserk>0?9:shooter.Owner,weapon.Class);
   if(hit>=0&&(hit==shooter.Slot||shooter.Berserk==0&&Relations[shooter.Owner,Slots[hit]!.Owner]))hit=-1;
   if(hit>=0) {
    var target=Slots[hit]!;ApplyHit(shooter,target,missile.Weapon);
    if(Data.MissileImpacts.GetValueOrDefault(missile.Weapon) is {} impact) {
     // 442bcc..442c40: even a single impact variant consumes the gameplay RNG.
     Random.Next();missile.X=target.X;missile.Z=target.Z;missile.Height=target.Height;
     missile.Animation.Start(impact,1);missile.State=2;Emit("impact",target,shooter.Slot,missile.Weapon);
    }
   }
   missile.Age++;
   if((hit>=0||missile.Age>Data.Weapons[missile.Weapon].Lifetime)&&missile.State==1)missile.State=4;
  }
  // Native inserts trails at the list head; state2 effects only advance at the end.
  foreach(var trail in missileTrails)Missiles.Insert(0,trail);missileTrails.Clear();
  // 442caf..442d04 advances FIN once after all four missile substeps.
  foreach(var missile in Missiles)if(missile.Animation.Set!=null) {
   missile.Animation.Tick(missile.Angle);if(missile.State==2&&missile.Animation.Mode==2)missile.State=4;
  }
  Missiles.RemoveAll(m=>m.State==4);
 }
 public int HitAt(int x,int z,int shooterTeam,int weaponClass) {
  var center=new Cell(x>>8,z>>8);if(!Navigation.Inside(center))return -1;
  int chosen=-1,score=0;
  for(int cx=center.X-1;cx<=center.X+1;cx++)for(int cz=center.Z-1;cz<=center.Z+1;cz++) {
   var c=new Cell(cx,cz);if(!Navigation.Inside(c))continue;
   foreach(var layer in new[]{Air,Ground,Other}) {
    if(ReferenceEquals(layer,Air)&&Data.DamageFactors[weaponClass][2]==0)continue;
    int id=layer[At(c)];if(id<0||Slots[id] is not {Living:true} e||e.Owner==8||e.Owner==shooterTeam)continue;
    var b=Bounds(e.Type);if(x<e.X+b.Left||x>e.X+b.Right||z<e.Z+b.Bottom||z>e.Z+b.Top)continue;
    int priority=1+(cx==center.X?1:0)+(cz==center.Z?1:0)+3;
    if(priority>score){chosen=id;score=priority;}
   }
  }
  return chosen;
 }
 public (int Left,int Bottom,int Right,int Top) Bounds(int type) {
  if(hitBounds.TryGetValue(type,out var cached))return cached;
  var set=Data.Clips[type].Stand;int left=10000,bottom=10000,right=-10000,top=-10000;
  foreach(var d in set.Directions.Distinct())for(int i=d.First;i<=d.Last;i++)foreach(var c in set.Asset.Frames[i].Cells) {
   var sprite=Data.Sprite(c.Bank).Cells[c.CellId];
   left=Math.Min(left,(c.X+sprite.OffsetX)*8);right=Math.Max(right,(c.X+sprite.OffsetX+sprite.Width)*8);
   bottom=Math.Min(bottom,-c.Y*8);top=Math.Max(top,(-c.Y+sprite.Height)*8);
  }
  if(Data.Scenario.Types[type].Flying==0){left=Math.Min(left,-96);bottom=Math.Min(bottom,-96);right=Math.Max(right,96);top=Math.Max(top,96);}
  return hitBounds[type]=(left,bottom,right,top);
 }
 void ApplyHit(NativeEntity shooter,NativeEntity target,int weapon) {
  int inspiration=256;
  if(shooter.Inspired!=0&&shooter.InspiringSlot>=0&&Slots[shooter.InspiringSlot] is {} commander)inspiration=Data.Scenario.Types[commander.Type].Fields[25]*256/100;
  int damage=Damage(shooter.Type,target.Type,target.Owner,weapon,inspiration,Phase);
  ApplyDamage(shooter,target,damage);
 }
 void Blast(NativeEntity shooter,NativeMissile missile){
  var weapon=Data.Weapons[missile.Weapon];var boom=Data.Booms[weapon.Boom];int radius=boom.Size/2,cx=missile.X>>8,cz=missile.Z>>8;
  var effects=Data.BlastEffects[weapon.Boom];missile.State=4;
  if(weapon.ProjectileKind==4){
   missile.State=3;missile.FlightRemaining=0;
   if(effects.Length>0)missile.Animation.Start(effects[Random.Next()%effects.Length],0);
   Emit("blast",shooter,weapon.Boom,missile.Weapon);return;
  }
  if(effects.Length>0){missile.Animation.Start(effects[Random.Next()%effects.Length],1);missile.State=2;}
  // 441f3c: ground then other, once PER occupied cell, no airborne target scan.
  // Same-owner quarter damage; no day/night penalty or commander multiplier.
  for(int x=cx-radius;x<=cx+radius;x++)for(int z=cz-radius;z<=cz+radius;z++){
   var cell=new Cell(x,z);if(!Navigation.Inside(cell))continue;int id=Ground[At(cell)];if(id<0)id=Other[At(cell)];
   if(id<0||Slots[id] is not {Living:true} target)continue;
   int factor=boom.Damage[(z-cz+radius)*boom.Size+x-cx+radius]*(target.Owner==shooter.Owner?64:256)>>8;
   int damage=(((Data.DamageFactors[weapon.Class][Data.Scenario.Types[target.Type].Fields[10]]*weapon.Damage>>8)*factor>>8)*Armor(target.Type,target.Owner))>>8;
   ApplyDamage(shooter,target,damage);
  }
  Emit("blast",shooter,weapon.Boom,missile.Weapon);
 }
 void ApplyDamage(NativeEntity shooter,NativeEntity target,int damage){
  if(DamageProtected(target))return;
  target.Health-=damage;target.PendingBlood=true;target.StrategyHit=true;Emit("hit",target,shooter.Slot,damage);
  if(target.Health>0)return;
  if(target.Owner<8)Deaths[target.Owner,target.Type]++;
  if(shooter.Owner<8) {
   if(shooter.Owner!=target.Owner)Kills[shooter.Owner,shooter.Type]++; //441c80: friendly casualties do not credit a kill.
   int id=CommanderIds[shooter.Owner][0];
   if(id>=0&&Slots[id] is {} ownCommander&&Math.Abs(ownCommander.Cell.X-target.Cell.X)+Math.Abs(ownCommander.Cell.Z-target.Cell.Z)<20)NearbyCommanderKills[shooter.Owner]++;
  }
  target.State=10;target.PendingOrder=null;target.Actions.Clear();target.Actions.Add(new(10));target.DeathWord=0;ClearOccupancy(target);
  EconomicDeath(target);
  Emit("death",target,shooter.Slot);
  if(IsCommander(target))CommanderRecovery?.Invoke(this,target);
 }
}
