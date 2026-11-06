using System.Buffers.Binary;
using System.Text.Json;
using DarkColony.Original;
namespace DarkColony.ContentImport;
internal static class NativeTables {
 public static byte[] Base(byte[] executable){var n=new NativeReference(executable);using var mappings=JsonDocument.Parse(ContentContract.Resource("council-native-map.json"));foreach(var r in mappings.RootElement.EnumerateArray())if(r.TryGetProperty("bytes",out var size))n.Shorts(Convert.ToUInt32(r.GetProperty("base").GetString()!,16),size.GetInt32()/2);foreach(int p in n.Ints(0x488cec,8))n.Text((uint)p);return n.ExportTables();}
 public static byte[] Council(byte[] executable,byte[] baseTables){
  if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(executable))!=NativeReference.CouncilHash)throw new InvalidDataException("Unsupported Council Wars executable data");
  var table=JsonSerializer.Deserialize<Dictionary<string,string>>(baseTables)!;var canonical=new SortedDictionary<uint,byte>();foreach(var kv in table.Where(k=>k.Key!="source")){uint a=uint.Parse(kv.Key);foreach(byte b in Convert.FromBase64String(kv.Value))canonical[a++]=b;}
  byte[] B(uint at,int n)=>Enumerable.Range(0,n).Select(i=>canonical[at+(uint)i]).ToArray();
  int I(int at)=>BinaryPrimitives.ReadInt32LittleEndian(executable.AsSpan(at));int pe=I(60),count=BinaryPrimitives.ReadUInt16LittleEndian(executable.AsSpan(pe+6)),directory=pe+24+BinaryPrimitives.ReadUInt16LittleEndian(executable.AsSpan(pe+20));uint imageBase=(uint)I(pe+52);
  byte[] C(uint address,int n){for(int i=0;i<count;i++){int p=directory+i*40;uint va=imageBase+(uint)I(p+12),size=(uint)I(p+16);if(address>=va&&(ulong)address+(uint)n<=(ulong)va+size)return executable.AsSpan(I(p+20)+(int)(address-va),n).ToArray();}throw new InvalidDataException("Native mapped span outside image");}
  uint U(byte[] b,int at=0)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));uint A(string a)=>Convert.ToUInt32(a,16);
  using var mappings=JsonDocument.Parse(ContentContract.Resource("council-native-map.json"));var graph=new Dictionary<uint,uint>();foreach(var r in mappings.RootElement.EnumerateArray()){
   if(r.TryGetProperty("bytes",out var n)){uint a=A(r.GetProperty("base").GetString()!),ca=A(r.GetProperty("council").GetString()!);if(!B(a,n.GetInt32()).SequenceEqual(C(ca,n.GetInt32())))throw new InvalidDataException("Council native mapping mismatch: "+r.GetProperty("name").GetString());}
   if(r.TryGetProperty("mapping",out var map))foreach(var pair in map.EnumerateObject())graph.Add(A(pair.Name),A(pair.Value.GetString()!));
  }
  foreach(var pair in graph){var b=B(pair.Key,44);var c=C(pair.Value,44);if(!b.AsSpan(0,12).SequenceEqual(c.AsSpan(0,12)))throw new InvalidDataException("Council sight node differs");int n=BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(8))/4+1;for(int i=0;i<n;i++)if(graph[U(b,12+4*i)]!=U(c,12+4*i))throw new InvalidDataException("Council sight edge differs");}
  for(uint i=1;i<=10;i++)if(graph[U(B(0x499010+i*4,4))]!=U(C(0x49910c+i*4,4)))throw new InvalidDataException("Council sight root differs");
  for(int i=0;i<8;i++){uint a=U(B(0x488cec+(uint)i*4,4)),ca=U(C(0x488de8+(uint)i*4,4));for(int j=0;j<64;j++){byte value=B(a+(uint)j,1)[0];if(value!=C(ca+(uint)j,1)[0])throw new InvalidDataException("Council rank string differs");if(value==0)break;}}
  table["source"]=NativeReference.CouncilHash;return JsonSerializer.SerializeToUtf8Bytes(table);
 }
}
