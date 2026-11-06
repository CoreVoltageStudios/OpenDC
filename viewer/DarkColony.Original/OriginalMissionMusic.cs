using System.Buffers.Binary;
using System.Security.Cryptography;
namespace DarkColony.Original;

// Adapter for the exact DC16 CDDA backend, not a guessed one-track loop.
// 42fbd4 ->451a08 seeks track2; MCI_PLAY flags0 runs to end of media.
// 432272/42fbf8 restart stopped media at track2 on strict >5000ms wall polls.
// 401a5a ->42fbb8 ->4515d8 stops before outcome media. No simulation clock/RNG.
public sealed class OriginalMissionMusic : IDisposable {
 readonly FileStream[] sources=new FileStream[4];readonly byte[] buffer=new byte[4096];
 readonly long[] frames=new long[4];int index;bool disposed;
 public bool Active {get;private set;}public bool Playing {get;private set;}
 public int Track=>Active?index+2:0;
 public long Frame {get;private set;}public long MixedFrames {get;private set;}
 public long LastPoll {get;private set;}public int Starts {get;private set;}public int Completions {get;private set;}
 public long TrackFrames(int track)=>frames[track-2];
 public OriginalMissionMusic(string directory,string content=OriginalContent.Base) {
  if(content is not (OriginalContent.Base or OriginalContent.Council))throw new InvalidDataException("Unknown music content");
  // Import validates output hashes; runtime accepts structurally compatible CDDA,
  // rather than requiring every legitimate pressing to match one reference.
  try {
   Span<byte> header=stackalloc byte[44];
   for(int i=0;i<4;i++) {
    var stream=sources[i]=new FileStream(Path.Combine(directory,$"track{i+2:00}.wav"),FileMode.Open,FileAccess.Read,FileShare.Read,65536);
    stream.Position=0;stream.ReadExactly(header);
    if(!header[..4].SequenceEqual("RIFF"u8)||!header[8..16].SequenceEqual("WAVEfmt "u8)||!header[36..40].SequenceEqual("data"u8)||
     BinaryPrimitives.ReadInt32LittleEndian(header[16..])!=16||BinaryPrimitives.ReadInt16LittleEndian(header[20..])!=1||
     BinaryPrimitives.ReadInt16LittleEndian(header[22..])!=2||BinaryPrimitives.ReadInt32LittleEndian(header[24..])!=44100||
     BinaryPrimitives.ReadInt32LittleEndian(header[28..])!=176400||BinaryPrimitives.ReadInt16LittleEndian(header[32..])!=4||
     BinaryPrimitives.ReadInt16LittleEndian(header[34..])!=16||BinaryPrimitives.ReadUInt32LittleEndian(header[40..])!=stream.Length-44)
     throw new InvalidDataException("Recovered CDDA WAV format");
    frames[i]=(stream.Length-44)/4;
   }
  }catch{Dispose();throw;}
 }
 public void Start(){ObjectDisposedException.ThrowIf(disposed,this);Active=true;Playing=true;Starts++;Seek(2,0);}
 public void Stop(){Active=false;Playing=false;Frame=0;}
 public void Poll(long wallMilliseconds) {
  if(!Active||wallMilliseconds-LastPoll<=5000)return;
  LastPoll=wallMilliseconds;if(!Playing)Start();
 }
 // Also used by controlled boundary fixtures; ordinary playback only seeks on Start.
 public void Seek(int track,long frame) {
  ObjectDisposedException.ThrowIf(disposed,this);
  if(track is <2 or >5||frame<0||frame>TrackFrames(track))throw new ArgumentOutOfRangeException();
  index=track-2;Frame=frame;sources[index].Position=44+frame*4;
 }
 public void AddTo(Span<float> stereo,float gain=.25f) {
  if(stereo.Length%2!=0)throw new ArgumentException("Stereo CDDA output");
  int done=0;
  while(Playing&&done<stereo.Length/2) {
   int count=(int)Math.Min(Math.Min(buffer.Length/4,stereo.Length/2-done),frames[index]-Frame);
   if(count>0) {
    sources[index].ReadExactly(buffer.AsSpan(0,count*4));
    for(int n=0;n<count*2;n++)stereo[done*2+n]+=BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(n*2,2))*(gain/32768f);
    done+=count;Frame+=count;MixedFrames+=count;
   }
   if(Frame==frames[index]) {
    if(index==3){Playing=false;Completions++;}
    else Seek(index+3,0);
   }
  }
 }
 public void Dispose(){if(disposed)return;Stop();foreach(var s in sources)s?.Dispose();disposed=true;}
}
