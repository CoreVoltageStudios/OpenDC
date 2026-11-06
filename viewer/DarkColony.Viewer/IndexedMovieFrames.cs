using System.Collections.Concurrent;
using System.IO.Compression;
namespace DarkColony.Viewer;

// Four reusable RGB buffers bound memory independently of movie duration. Only the
// decoder worker touches its file stream; Godot/image/audio calls stay on main thread.
internal sealed class IndexedMovieFrames : IDisposable {
 readonly string path;readonly long[] offsets;readonly int[] lengths;readonly int frameSize;
 BlockingCollection<byte[]>? free;BlockingCollection<(int Index,byte[] Pixels)>? ready;
 CancellationTokenSource? cancel;Task? worker;volatile Exception? failure;int copied=-1;
 public int Starved {get;private set;}
 static int activeWorkers;
 internal static int ActiveWorkers=>Volatile.Read(ref activeWorkers);
 public long BufferCapacityBytes=>4L*frameSize+lengths.Max()+12L*lengths.Length;
 public IndexedMovieFrames(string path,Stream file,int count,int frameSize){
  this.path=path;this.frameSize=frameSize;offsets=new long[count];lengths=new int[count];using var reader=new BinaryReader(file,System.Text.Encoding.UTF8,true);
  long at=checked(file.Position+count*4L);
  for(int i=0;i<count;i++){int length=reader.ReadInt32();if(length<18||length>frameSize+65536)throw new InvalidDataException("Invalid indexed movie frame size");offsets[i]=at;lengths[i]=length;at=checked(at+length);}
  if(at!=file.Length)throw new InvalidDataException("Incomplete indexed movie");
 }
 void Read(Stream file,int index,byte[] target,byte[] packed){
  file.Position=offsets[index];file.ReadExactly(packed.AsSpan(0,lengths[index]));
  using var input=new MemoryStream(packed,0,lengths[index],false);using var gzip=new GZipStream(input,CompressionMode.Decompress);gzip.ReadExactly(target);if(gzip.ReadByte()!=-1)throw new InvalidDataException("Invalid decoded movie frame size");
 }
 public void ReadFrame(int index,byte[] target){using var file=File.OpenRead(path);Read(file,index,target,new byte[lengths[index]]);}
 public void Start(){
  Dispose();failure=null;Starved=0;copied=0;cancel=new();free=new(4);ready=new(4);
  for(int i=0;i<4;i++)free.Add(new byte[frameSize]);var token=cancel.Token;
  worker=Task.Run(()=>{Interlocked.Increment(ref activeWorkers);try{using var file=File.OpenRead(path);var packed=new byte[lengths.Max()];for(int i=1;i<offsets.Length;i++){var pixels=free.Take(token);Read(file,i,pixels,packed);ready.Add((i,pixels),token);}}catch(OperationCanceledException){}catch(Exception e){failure=e;}finally{Interlocked.Decrement(ref activeWorkers);}});
 }
 public bool TryCopy(int index,byte[] target){
  if(failure!=null)throw new InvalidDataException("Indexed original movie decode failed",failure);
  bool changed=false;
  while(copied<index&&ready!.TryTake(out var next)){Buffer.BlockCopy(next.Pixels,0,target,0,frameSize);copied=next.Index;free!.Add(next.Pixels);changed=true;}
  if(copied<index)Starved++;
  return changed;
 }
 public int CopiedFrame=>copied;
 public void Dispose(){cancel?.Cancel();worker?.GetAwaiter().GetResult();worker=null;cancel?.Dispose();cancel=null;free?.Dispose();ready?.Dispose();free=null;ready=null;}
}
