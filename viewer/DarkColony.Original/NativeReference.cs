using System.Buffers.Binary;
using System.Security.Cryptography;
namespace DarkColony.Original;

public readonly record struct Cell(int X,int Z);
public sealed record SightNode(Cell Offset,SightNode[] Children);

// Read-only data adapter. Never loads or invokes native code. Tables remain private assets.
public sealed class NativeReference {
 public const string ExpectedHash="3159A24C69B4299E668D981DACA0A21F5A8C1ADE7BCC48C9F89379450D27762F";
 public const string CouncilHash="3B930BA92CFD07AB4403C499D5251D604E660F4E8B092303691315E13A1737F4";
 readonly byte[] image;
 readonly SortedDictionary<uint,byte> extracted=new();
 readonly HashSet<uint> requested=new();
 readonly (uint Address,uint Size,int Offset)[] sections;
 public string Hash {get;}
 public string ContentHash=>Convert.ToHexString(SHA256.HashData(ExportTables()));
 public int[] RandomTable {get;}
 public int[] PathCosts {get;}
 public int[] DirectionWheel {get;}
 public int[] DirectionInverse {get;}
 public int[] YieldIdle {get;}
 public int[] YieldCombat {get;}
 public int[] AnimationFallback {get;}
 public Cell[] Directions {get;}
 public Cell[][] Rings {get;}
 public SightNode[] Sight {get;}
 public short[] Sines {get;}
 public short[] ArcTangents {get;}
 public NativeReference(byte[] bytes) {
  image=(byte[])bytes.Clone();Hash=Convert.ToHexString(SHA256.HashData(image));
  if(bytes.Length>0&&bytes[0]==(byte)'{') {
   var table=System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(bytes)!;
   if(table["source"]!=ExpectedHash&&table["source"]!=CouncilHash)throw new InvalidDataException("Native table source identity");
   Hash=table["source"];sections=Array.Empty<(uint,uint,int)>();
   foreach(var item in table.Where(p=>p.Key!="source")){uint a=uint.Parse(item.Key);var b=Convert.FromBase64String(item.Value);for(int i=0;i<b.Length;i++)extracted[a+(uint)i]=b[i];}
  } else {
  if(Hash!=ExpectedHash)throw new InvalidDataException("Unsupported original executable data identity");
  int pe=BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(60));
  int count=BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(pe+6));
  int directory=pe+24+BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(pe+20));
  uint imageBase=BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(pe+52));
  sections=Enumerable.Range(0,count).Select(i=>{
   var s=image.AsSpan(directory+i*40);return(imageBase+BinaryPrimitives.ReadUInt32LittleEndian(s[12..]),
    BinaryPrimitives.ReadUInt32LittleEndian(s[16..]),BinaryPrimitives.ReadInt32LittleEndian(s[20..]));
  }).ToArray();
  }
  RandomTable=Ints(0x488e4c,256);PathCosts=Ints(0x48a9fc,81);
  DirectionWheel=Ints(0x489290,8);DirectionInverse=Ints(0x4892b0,8);
  YieldIdle=Ints(0x4892d0,7);YieldCombat=Ints(0x4892ec,5);
  AnimationFallback=Ints(0x489554,32);
  var directions=Ints(0x489250,16);Directions=Enumerable.Range(0,8).Select(i=>new Cell(directions[2*i],directions[2*i+1])).ToArray();
  Sines=Shorts(0x489700,2049);ArcTangents=Shorts(0x48a702,257);
  var rings=new List<Cell[]>();var ring=new List<Cell>();uint address=0x4343e0;
  while(rings.Count<17) {
   var xy=Shorts(address,2);address+=4;
   if(xy[0]==99){rings.Add(ring.ToArray());ring.Clear();}
   else ring.Add(new(xy[0],xy[1]));
   if(address>=0x435800)throw new InvalidDataException("Native acquisition ring terminator");
  }
  Rings=rings.ToArray();Sight=new SightNode[11];
  var nodes=new Dictionary<int,SightNode>();
  SightNode ReadNode(int a) {
   if(nodes.TryGetValue(a,out var known))return known;
   var row=Ints((uint)a,11);int n=row[2]/4+1;
   if(row[2]%4!=0||n<0||n>8)throw new InvalidDataException("Native sight tree shape");
   var children=new SightNode[n];var node=new SightNode(new(row[0],row[1]),children);nodes.Add(a,node);
   for(int i=0;i<n;i++)children[i]=ReadNode(row[3+i]);return node;
  }
  for(int radius=1;radius<=10;radius++)Sight[radius]=ReadNode(Ints(0x499010+(uint)radius*4,1)[0]);
 }
 public int[] Ints(uint address,int count){var b=Bytes(address,count*4);return Enumerable.Range(0,count).Select(i=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(i*4))).ToArray();}
 public short[] Shorts(uint address,int count){var b=Bytes(address,count*2);return Enumerable.Range(0,count).Select(i=>BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(i*2))).ToArray();}
 public string Text(uint address,int limit=64) {
  var bytes=new List<byte>();for(int i=0;i<limit;i++){byte b=Bytes(address+(uint)i,1)[0];if(b==0)return System.Text.Encoding.ASCII.GetString(bytes.ToArray());bytes.Add(b);}
  throw new InvalidDataException("Native string exceeds bounded length");
 }
 byte[] Bytes(uint address,int count) {
  for(int i=0;i<count;i++)requested.Add(address+(uint)i);
  if(sections.Length==0)return Enumerable.Range(0,count).Select(i=>extracted[address+(uint)i]).ToArray();
  var s=sections.Single(s=>address>=s.Address&&(ulong)address+(uint)count<=(ulong)s.Address+s.Size);
  var result=image.AsSpan(checked(s.Offset+(int)(address-s.Address)),count).ToArray();
  for(int i=0;i<count;i++)extracted[address+(uint)i]=result[i];return result;
 }
 public byte[] ExportTables() {
  // Runtime lazy reads must not depend on what happened during content discovery.
  // OriginalEconomy: production exits (4 categories x 3 variants), city queue map,
  // and city component offsets. OriginalStrategy: building-choice table.
  // OriginalCombat: projectile wave. Data spans only; never ship the executable.
  Ints(0x41ae00,24);Ints(0x41ae60,15);Ints(0x48ab40,30);
  Ints(0x49903c,18);Ints(0x48a948,32);Ints(0x48a904,17); // native artillery arc, no executable runtime dependency.
  Ints(0x48aff0,60);Ints(0x48abb8,30);Ints(0x41adc0,16);Ints(0x401010,6);
  foreach(var p in Ints(0x488cec,8))Text((uint)p);
  var table=new Dictionary<string,string>{{"source",Hash}};
  foreach(var group in extracted.Where(p=>requested.Contains(p.Key)).Select((p,i)=>(p,i)).GroupBy(x=>(long)x.p.Key-x.i))table[group.First().p.Key.ToString()]=Convert.ToBase64String(group.Select(x=>x.p.Value).ToArray());
  return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(table);
 }
 public int Angle8192(int dx,int dz) {
  if(dx==0&&dz==0)return 0;
  int x=Math.Abs(dx),z=Math.Abs(dz),a;
  if(x<z){a=ArcTangents[(x<<8)/z];return dx<0?(dz<0?6144-a:2048+a):(dz<0?6144+a:2048-a);}
  if(x==z)return dx>0?(dz>0?1024:7168):(dz>0?3072:5120);
  a=ArcTangents[(z<<8)/x];return dx<0?(dz<0?4096+a:4096-a):(dz<0&&a!=0?8192-a:a);
 }
 public int Angle(int dx,int dz)=>Angle8192(dx,dz)/32&255;
 public (int Sin,int Cos) SinCos(int a) {
  int t=a&2047;return ((a&8191)>>11) switch {
   0=>(Sines[t],Sines[2048-t]),1=>(Sines[2048-t],-Sines[t]),
   2=>(-Sines[t],-Sines[2048-t]),_=>(-Sines[2048-t],Sines[t])};
 }
}

// 411dd4/411de4 and inlined callers: a cyclic native table, not System.Random/LCG.
// Seed selects an index (low byte); the original fresh campaign seed is not claimed.
public sealed class NativeRandom {
 readonly int[] table;
 public int Index {get;private set;}
 public long Draws {get;private set;}
 public NativeRandom(NativeReference reference,int seed){table=reference.RandomTable;Index=seed&255;}
 internal void Restore(int index,long draws){Index=index;Draws=draws;}
 public int Next(){Index=(Index+1)&255;Draws++;return table[Index];}
}
