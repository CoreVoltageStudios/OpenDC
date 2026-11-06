namespace DarkColony.Assets;

// DC16 442ecc loads a 65536-byte table, then width*height family bytes into
// path row[z][x].family (+0xc of a 24-byte record). 4126d8 / 414d14 reject
// family zero for ground movement. Family numbers are NOT connected-component IDs.
public sealed record PathAsset(int Width,int Height,byte[] Table,byte[] Families,byte[] Source) {
 public byte At(int x,int z)=>x>=0&&z>=0&&x<Width&&z<Height?Families[z*Width+x]:throw new ArgumentOutOfRangeException();
 public static PathAsset Decode(byte[] source,int width,int height) {
  if(width<1||height<1||width>256||height>256||source.Length!=65536+width*height)throw new InvalidDataException("PTH: unsupported dimensions/layout");
  var families=source.Skip(65536).ToArray();
  if(families.Contains((byte)255))throw new InvalidDataException("PTH: interior family255 is unsupported; original uses255 for its boundary halo");
  return new(width,height,source.Take(65536).ToArray(),families,(byte[])source.Clone());
 }
}
public sealed record GroundNavigation(bool[] Allowed,int Blocked,int AdditionalPathBlocked) {
 public static GroundNavigation From(MapAsset map,PathAsset path) {
  if(path.Width!=map.Width||path.Height!=map.Height)throw new InvalidDataException("MAP/PTH dimensions differ");
  var allowed=new bool[path.Families.Length];int blocked=0,additional=0;
  for(int i=0;i<allowed.Length;i++) {
   allowed[i]=path.Families[i]!=0;bool mapFlag=(map.Cells[i].Attributes&0x200)!=0;
   if(mapFlag&&allowed[i])throw new InvalidDataException($"MAP/PTH static blocking conflict at {i%map.Width},{i/map.Width}");
   if(!allowed[i]){blocked++;if(!mapFlag)additional++;}
  }
  return new(allowed,blocked,additional);
 }
}
