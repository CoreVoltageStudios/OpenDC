using System.Text;
namespace DarkColony.Assets;
public sealed record PcmWave(int SampleRate,int Channels,int Bits,byte[] SignedSamples) {
 public static PcmWave Decode(byte[] bytes) {
  using var stream=new MemoryStream(bytes,false);using var reader=new BinaryReader(stream,Encoding.ASCII);
  string Tag()=>Encoding.ASCII.GetString(reader.ReadBytes(4));
  if(bytes.Length<12||Tag()!="RIFF")throw new InvalidDataException("WAV RIFF header");
  uint length=reader.ReadUInt32();if(length!=(uint)bytes.Length-8||Tag()!="WAVE")throw new InvalidDataException("WAV length/type");
  int rate=0,channels=0,bits=0,align=0;byte[]? data=null;
  while(stream.Position+8<=stream.Length) {
   string tag=Tag();uint size=reader.ReadUInt32();long end=stream.Position+size;if(end>stream.Length)throw new InvalidDataException("WAV chunk overrun");
   if(tag=="fmt ") {
    if(size<16||reader.ReadUInt16()!=1)throw new NotSupportedException("Only uncompressed PCM WAV");
    channels=reader.ReadUInt16();rate=reader.ReadInt32();int byteRate=reader.ReadInt32();align=reader.ReadUInt16();bits=reader.ReadUInt16();
    if((channels!=1&&channels!=2)||(bits!=8&&bits!=16)||rate<8000||rate>192000||align!=channels*bits/8||byteRate!=rate*align)throw new NotSupportedException("PCM WAV format");
   } else if(tag=="data"){if(data!=null)throw new NotSupportedException("Multiple WAV data chunks");data=reader.ReadBytes(checked((int)size));}
   // These original sounds omit the pad byte after the final odd data chunk.
   // Accept that exact EOF case; never seek beyond the preserved source.
   // Preserved GRAY3WEA.WAV also omits the odd-data pad immediately before its
   // terminal LIST metadata. Recognize this exact bounded layout; do not relax
   // truncated chunks or treat arbitrary data as a header.
   bool unpaddedList=tag=="data"&&(size&1)!=0&&end+8<=bytes.Length&&
    Encoding.ASCII.GetString(bytes,(int)end,4)=="LIST"&&end+8+BitConverter.ToUInt32(bytes,(int)end+4)==bytes.Length;
   stream.Position=end<stream.Length&&!unpaddedList?end+(size&1):end;
  }
  if(data==null||rate==0||data.Length%align!=0||stream.Position!=stream.Length)throw new InvalidDataException("Incomplete WAV");
  if(bits==8)for(int i=0;i<data.Length;i++)data[i]=(byte)(data[i]^128); // unsigned RIFF -> signed Godot PCM
  return new(rate,channels,bits,data);
 }
}
