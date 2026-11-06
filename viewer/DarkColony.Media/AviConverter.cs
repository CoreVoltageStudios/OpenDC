using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
namespace DarkColony.Media;

public sealed record MovieInfo(int Width,int Height,int Rate,int Scale,int Frames,int AudioRate,int Channels);
public sealed record MovieConversion(MovieInfo Info,string RgbHash,string PcmHash,long OutputBytes);
// RIFF/Cinepak/PCM only. Validated original sources; no shell tools or installed codecs.
public static class AviConverter {
 readonly record struct Chunk(string Id,long At,int Size);
 static IEnumerable<Chunk> Chunks(Stream s,long start,long end){
  byte[] header=new byte[8];for(long p=start;p+8<=end;){s.Position=p;s.ReadExactly(header);int size=BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));if(size<0||p+8+size>end)throw new InvalidDataException("Truncated RIFF chunk");yield return new(Encoding.ASCII.GetString(header,0,4),p+8,size);p+=8L+size+(size&1);}
 }
 static byte[] Read(Stream s,long at,int count){if(count>64*1024*1024)throw new InvalidDataException("Oversized AVI packet");s.Position=at;var b=new byte[count];s.ReadExactly(b);return b;}
 static string Tag(Stream s,long at)=>Encoding.ASCII.GetString(Read(s,at,4));
 static IEnumerable<Chunk> Packets(Stream s,long start,long end,int depth=0){if(depth>8)throw new InvalidDataException("AVI nesting limit");foreach(var c in Chunks(s,start,end)){if(c.Id=="LIST"){foreach(var v in Packets(s,c.At+4,c.At+c.Size,depth+1))yield return v;}else if(c.Id is "00dc" or "00db" or "01wb")yield return c;}}
 public static MovieConversion Convert(string source,string target,CancellationToken cancel,Action<int,int>? progress=null){
  using var s=File.OpenRead(source);var root=Read(s,0,12);if(Encoding.ASCII.GetString(root,0,4)!="RIFF"||Encoding.ASCII.GetString(root,8,4)!="AVI ")throw new InvalidDataException("Expected AVI RIFF");
  long end=8L+BinaryPrimitives.ReadUInt32LittleEndian(root.AsSpan(4));if(end>s.Length)throw new InvalidDataException("Truncated AVI");
  var hdrl=Chunks(s,12,end).First(c=>c.Id=="LIST"&&Tag(s,c.At)=="hdrl");var streams=new List<Dictionary<string,byte[]>>();
  foreach(var c in Chunks(s,hdrl.At+4,hdrl.At+hdrl.Size))if(c.Id=="LIST"&&Tag(s,c.At)=="strl")streams.Add(Chunks(s,c.At+4,c.At+c.Size).ToDictionary(x=>x.Id,x=>Read(s,x.At,x.Size)));
  if(streams.Count!=2||Encoding.ASCII.GetString(streams[0]["strh"],0,4)!="vids"||Encoding.ASCII.GetString(streams[1]["strh"],0,4)!="auds")throw new InvalidDataException("Unsupported AVI stream order");
  var v=streams[0];var a=streams[1];int I(byte[] b,int p)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p));int U(byte[] b,int p)=>BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(p));
  var info=new MovieInfo(I(v["strf"],4),Math.Abs(I(v["strf"],8)),I(v["strh"],24),I(v["strh"],20),I(v["strh"],32),I(a["strf"],4),U(a["strf"],2));
  int bits=U(a["strf"],14);
  if(Encoding.ASCII.GetString(v["strf"],16,4)!="cvid"||I(v["strh"],28)!=0||I(a["strh"],28)!=0||U(a["strf"],0)!=1||bits is not(8 or 16)||info.Channels is <1 or >2||info.AudioRate is <8000 or >48000||info.Frames is <1 or >20000||info.Rate<=0||info.Scale<=0)throw new InvalidDataException("Unsupported original movie codec/timing");
  var movi=Chunks(s,12,end).First(c=>c.Id=="LIST"&&Tag(s,c.At)=="movi");var packets=Packets(s,movi.At+4,movi.At+movi.Size).ToArray();var video=packets.Where(c=>c.Id is "00dc" or "00db").ToArray();var audio=packets.Where(c=>c.Id=="01wb").ToArray();
  if(video.Length!=info.Frames||video[0].Size==0)throw new InvalidDataException("AVI frame timeline differs from header");int audioBytes=checked(audio.Sum(x=>x.Size)*(bits==8?2:1));
  if(audioBytes>256*1024*1024||audioBytes%(2*info.Channels)!=0)throw new InvalidDataException("Invalid AVI PCM length");
  Directory.CreateDirectory(Path.GetDirectoryName(target)!);using var f=new FileStream(target,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,1024*1024);using var w=new BinaryWriter(f,Encoding.ASCII,true);
  w.Write("ODCV0002"u8);foreach(int n in new[]{info.Width,info.Height,info.Rate,info.Scale,info.Frames,info.AudioRate,info.Channels,audioBytes})w.Write(n);
  using var pcmHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);using var rgbHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
  foreach(var packet in audio){cancel.ThrowIfCancellationRequested();var raw=Read(s,packet.At,packet.Size);byte[] pcm=raw;if(bits==8){pcm=new byte[raw.Length*2];for(int i=0;i<raw.Length;i++)BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i*2),(short)((raw[i]-128)<<8));}f.Write(pcm);pcmHash.AppendData(pcm);}
  long index=f.Position;var lengths=new int[info.Frames];f.Position+=info.Frames*4;var decoder=new Cinepak(info.Width,info.Height);
  for(int i=0;i<video.Length;i++){
   cancel.ThrowIfCancellationRequested();var c=video[i];if(c.Size>0)decoder.Decode(Read(s,c.At,c.Size));rgbHash.AppendData(decoder.Rgb);
   long at=f.Position;using(var gzip=new GZipStream(f,CompressionLevel.Optimal,true))gzip.Write(decoder.Rgb);lengths[i]=checked((int)(f.Position-at));if(i%16==0||i==video.Length-1)progress?.Invoke(i+1,video.Length);
  }
  long bytes=f.Position;f.Position=index;foreach(int length in lengths)w.Write(length);w.Flush();f.Flush(true);
  return new(info,System.Convert.ToHexString(rgbHash.GetHashAndReset()),System.Convert.ToHexString(pcmHash.GetHashAndReset()),bytes);
 }
}
