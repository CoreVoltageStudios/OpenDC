using DarkColony.Assets;
namespace DarkColony.Original;

public sealed record OriginalSound(int Id,string Path,int Copies,int RawFlag,int Volume,bool Loop) {
 public static Dictionary<int,OriginalSound> Parse(byte[] bytes) {
  var result=new Dictionary<int,OriginalSound>();
  foreach(var line in OriginalScenarioImport.Lines(bytes,"SOUND2")) {
   if(line.Text=="*")break;
   var p=line.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
   if(p.Length!=6)throw new InvalidDataException("SOUND2 record");
   var s=new OriginalSound(int.Parse(p[0]),p[1].Replace('\\','/').TrimStart('.','/'),int.Parse(p[2]),int.Parse(p[3]),int.Parse(p[4]),p[5]=="1");
   if(s.Copies<1||s.Volume is < -10000 or >0||!result.TryAdd(s.Id,s))throw new InvalidDataException("SOUND2 pool/volume/identity");
  }
  return result;
 }
}
public readonly record struct NativeSpatialSound(int Volume,int Pan) {
 // 431f44 uses fixed-point differences; transport431b84/431d10 truncate each difference first.
 public static NativeSpatialSound Immediate(int cx,int cz,int x,int z) {
  int dx=cx-x,dz=cz-z,v=-((unchecked(dx*dx+dz*dz))>>17);return new(v,cx>x?v:-v);
 }
 public static NativeSpatialSound Transport(int cx,int cz,int x,int z,int height,bool initial) {
  int dx=(cx-x)>>8,dz=(cz-z)>>8,h=height>>8,v=-((unchecked(dx*dx+dz*dz+h*h))>>1);
  if(initial&&h!=0&&v< -8000)v=-8000;return new(v,cx>x?v:-v);
 }
 public bool Audible=>Volume>=-8000;
 public (float Left,float Right) Gains=>((float)Math.Pow(10,(Volume-Math.Max(0,Pan))/2000.0),(float)Math.Pow(10,(Volume+Math.Min(0,Pan))/2000.0));
}
public sealed record NativeAudioCue(long Tick,string Kind,int Slot,int Incarnation,int Type,int X,int Z,int Height,int Value);

// Presentation-only PCM mixer. Native bounded per-sound pools, first free copy, no stealing.
// DirectSound hundredth-dB gains and retained Stop cursor; linear resampling is a modern adapter.
// No world state, clock or gameplay RNG is accessed here.
public sealed class OriginalAudioMixer {
 public sealed class Voice {
  public int Sound,Copy,Volume,Pan;public bool Playing;public double Cursor;
  internal float[] Samples=Array.Empty<float>();internal int Channels,Rate;internal bool Loop;
 }
 readonly IReadOnlyDictionary<int,OriginalSound> definitions;readonly Func<string,byte[]> read;
 readonly Dictionary<int,Voice[]> pools=new();
 public IEnumerable<Voice> Voices=>pools.Values.SelectMany(v=>v);
 public int Dropped {get;private set;}public float Peak {get;private set;}public long MixedFrames {get;private set;}
 public OriginalAudioMixer(IReadOnlyDictionary<int,OriginalSound> definitions,Func<string,byte[]> read){this.definitions=definitions;this.read=read;}
 Voice[] Pool(int sound) {
  if(pools.TryGetValue(sound,out var pool))return pool;
  var d=definitions[sound];var p=PcmWave.Decode(read(d.Path));var samples=new float[p.SignedSamples.Length/(p.Bits/8)];
  for(int i=0;i<samples.Length;i++)samples[i]=p.Bits==8?unchecked((sbyte)p.SignedSamples[i])/128f:BitConverter.ToInt16(p.SignedSamples,i*2)/32768f;
  if(samples.Length==0)throw new InvalidDataException("Empty audio source "+d.Path);
  return pools[sound]=Enumerable.Range(0,d.Copies).Select(i=>new Voice{Sound=sound,Copy=i,Samples=samples,Channels=p.Channels,Rate=p.SampleRate,Loop=d.Loop}).ToArray();
 }
 public Voice? Play(int sound,int volume=1,int? pan=null) {
  if(!definitions.ContainsKey(sound))return null;
  var v=Pool(sound).FirstOrDefault(v=>!v.Playing);if(v==null){Dropped++;return null;}
  Set(v,volume==1?definitions[sound].Volume:volume,pan);v.Playing=true;return v;
 }
 public void Set(Voice v,int volume,int? pan) {
  // Native DS calls reject out-of-range arguments independently, leaving prior settings.
  if(volume is >=-10000 and <=0)v.Volume=volume;if(pan is >=-10000 and <=10000)v.Pan=pan.Value;
 }
 public void Stop(Voice? v){if(v!=null)v.Playing=false;}
 public void StopAll(){foreach(var v in Voices)v.Playing=false;}
 public void Mix(Span<float> stereo,int rate=44100,float master=.25f) {
  if(stereo.Length%2!=0||rate<=0)throw new ArgumentException("Stereo output frames/rate");stereo.Clear();
  foreach(var v in Voices.Where(v=>v.Playing)) {
   var gain=new NativeSpatialSound(v.Volume,v.Pan).Gains;int frames=v.Samples.Length/v.Channels;double step=(double)v.Rate/rate;
   for(int i=0;i<stereo.Length/2;i++) {
    if(v.Cursor>=frames){if(v.Loop)v.Cursor%=frames;else{v.Cursor=0;v.Playing=false;break;}}
    int at=(int)v.Cursor,next=at+1<frames?at+1:v.Loop?0:at;float f=(float)(v.Cursor-at);
    float Sample(int channel){int c=Math.Min(channel,v.Channels-1);float a=v.Samples[at*v.Channels+c];return a+(v.Samples[next*v.Channels+c]-a)*f;}
    stereo[i*2]+=Sample(0)*gain.Left*master;stereo[i*2+1]+=Sample(1)*gain.Right*master;v.Cursor+=step;
   }
   if(!v.Loop&&v.Cursor>=frames){v.Cursor=0;v.Playing=false;}
  }
  foreach(float s in stereo)Peak=Math.Max(Peak,Math.Abs(s));MixedFrames+=stereo.Length/2;
 }
}
