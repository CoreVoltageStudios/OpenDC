namespace DarkColony.Original;
public sealed partial class OriginalWorld {
 // 417ef8/418058: original coordinate-targeted Gorrem alternate, not Deploy.
 public bool HasDisease(NativeEntity e)=>e.Type==12&&e.Owner<8&&Teams[e.Owner].WeaponTiers[e.Type]==2&&Data.Scenario.Types[e.Type].Fields[29]==51;
 public bool CanDisease(NativeEntity e)=>e.Living&&HasDisease(e)&&e.Energy==255;
 bool WithinDiseaseRange(NativeEntity e,OriginalWeapon weapon){long dx=e.DestinationX-e.X,dz=e.DestinationZ-e.Z;return dx*dx+dz*dz<=(long)weapon.Range*weapon.Range*65536;}
 void BeginDisease(NativeEntity e){
  if(!CanDisease(e)){ResetOrders(e);return;}
  e.Target=-1;var aim=new Cell(e.DestinationX>>8,e.DestinationZ>>8);
  e.Actions.Add(new(18){Destination=aim});
  if(!WithinDiseaseRange(e,Data.Weapons[51]))Plan(e,4);
 }
 bool DiseaseStep(NativeEntity e,NativeAction a){
  if(a.Mode==0){a.Mode=1;a.Word=(ushort)Tick;}
  if(!HasDisease(e)){ResetOrders(e);return true;}
  // Native unsigned16 clock values are promoted before subtraction, not wrapped.
  if((ushort)Tick-a.Word>4){ResetOrders(e);e.Energy=0;return true;}
  if(e.Energy<255||e.PendingOrder!=null){ResetOrders(e);return true;}
  a.Word=(ushort)Tick;
  FireAt(e,Data.Weapons[51],a.Destination.X*256+128,a.Destination.Z*256+128,0,-1);
  return false;
 }
 // Native0x3fe is also used by production. Keep a distinct adapter sentinel so
 // field cleanup cannot release an unrelated production reservation (-2).
 public const int DiseaseGround=-3;
 bool FieldCovers(NativeMissile m,Cell c){int r=Data.Booms[Data.Weapons[m.Weapon].Boom].Size/2;return Math.Abs((m.X>>8)-c.X)<=r&&Math.Abs((m.Z>>8)-c.Z)<=r;}
 void StepDiseaseField(NativeMissile m){
  int counter=m.FlightRemaining;
  if((counter&3)==0){
   var weapon=Data.Weapons[m.Weapon];var boom=Data.Booms[weapon.Boom];int radius=boom.Size/2,cx=m.X>>8,cz=m.Z>>8;
   bool expiry=counter>0x348;var shooter=Slots[m.Shooter]!;
   if(counter==0)Emit("disease-start",shooter,-1,m.Weapon);
   for(int x=cx-radius;x<=cx+radius;x++)for(int z=cz-radius;z<=cz+radius;z++){
    var cell=new Cell(x,z);if(!Navigation.Inside(cell))continue;int at=At(cell),id=Ground[at];
    if(expiry){
     // Other live clouds retain their cover. No list/set allocation per update.
     if(id==DiseaseGround){bool covered=false;foreach(var other in Missiles)if(other!=m&&other.State==3&&other.FlightRemaining<=0x348&&FieldCovers(other,cell)){covered=true;break;}if(!covered)Ground[at]=-1;}
    }else if(id==-1)Ground[at]=DiseaseGround;
    else if(id>=0&&(counter&63)==0&&Slots[id] is {Living:true} target){
     // 4426a1 ->441c80: ground only, authored factor, NO ally/owner reduction,
     // night penalty or Inspire multiplier. Attribution reads current slot.
     int factor=boom.Damage[(z-cz+radius)*boom.Size+x-cx+radius];
     int damage=(((Data.DamageFactors[weapon.Class][Data.Scenario.Types[target.Type].Fields[10]]*weapon.Damage>>8)*factor>>8)*Armor(target.Type,target.Owner))>>8;
     ApplyDamage(shooter,target,damage);
    }
   }
   if(expiry){m.State=4;Emit("disease-end",shooter,-1,m.Weapon);}
  }
  m.FlightRemaining++;
 }
}
