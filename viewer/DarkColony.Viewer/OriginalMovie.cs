using Godot;
using System.IO.Compression;
namespace DarkColony.Viewer;

// Original cinematics: lossless offline-decoded RGB/PCM (indexed for long clips), one reusable GPU
// texture. No external codec, gameplay clock, shader colour conversion or UI scale.
public partial class OriginalMovie : Control {
 byte[] pixels=Array.Empty<byte>(),frameBytes=Array.Empty<byte>();Image? image;ImageTexture? texture;
 AudioStreamPlayer? sound;AudioStreamWav? audio;int width,height,rate,scale,count;
 IndexedMovieFrames? indexed;
 public bool Enhanced=>false; // Encoding is independent of provenance; runtime loads originals only.
 public bool Streaming=>indexed!=null;
 public long RetainedVideoBytes=>pixels.LongLength+frameBytes.LongLength+(indexed?.BufferCapacityBytes??0);
 public int BufferStarvations=>indexed?.Starved??0;
 double audioStartDelay,audioDuration;ulong startUsec;bool playing,ended;
 public int Frame {get;private set;}=-1;
 public double PositionSeconds {get;private set;}
 public double Duration=>Math.Max((double)count*scale/rate,audioDuration);
 public float VolumeDb {get=>sound?.VolumeDb??0;set{if(sound!=null)sound.VolumeDb=value;}}
 public event Action? Finished;
 public event Action<Exception>? Failed;
 public double UploadMilliseconds {get;private set;}
 public Texture2D VideoTexture=>texture!;
 // Native cinema occupies 640x360 inside the 640x480 display. Retain that
 // framing on modern screens, with square source pixels and all rows intact.
 // No legacy scanlines/16-bit quantization or stretching the 4:3 stage to 16:9.
 public Rect2 PictureRect {
  get {float stage=Math.Min(Size.X/640f,Size.Y/480f);float factor=Math.Min(640f/width,360f/height)*stage;Vector2 draw=new(width*factor,height*factor);return new Rect2((Size-draw)/2,draw);}
 }
 internal void InspectFrame(int frame){Stop();if(frame<0||frame>=count)throw new ArgumentOutOfRangeException(nameof(frame));PositionSeconds=(double)frame*scale/rate;Upload(frame);}
 public void Load(string path) {
  Release();try{using var file=File.OpenRead(path);using var header=new BinaryReader(file);
  string format=System.Text.Encoding.ASCII.GetString(header.ReadBytes(8));if(format is not ("ODCV0001" or "ODCV0002"))throw new InvalidDataException("Unsupported original movie version");
  width=header.ReadInt32();height=header.ReadInt32();rate=header.ReadInt32();scale=header.ReadInt32();count=header.ReadInt32();int sampleRate=header.ReadInt32(),channels=header.ReadInt32(),audioBytes=header.ReadInt32();
  if(width<1||height<1||width>2048||height>2048||rate<1||rate>1000||scale<1||count<1||count>10000||sampleRate<8000||sampleRate>192000||channels is <1 or >2||audioBytes<2||audioBytes>64000000||audioBytes%(2*channels)!=0)throw new InvalidDataException("Invalid original movie header");
  int frameSize=checked(width*height*3);var pcm=new byte[audioBytes];
  if(format=="ODCV0002"){file.ReadExactly(pcm);indexed=new(path,file,count,frameSize);}
  else {int videoBytes=checked(frameSize*count);if(videoBytes>256000000)throw new InvalidDataException("Original movie exceeds memory limit");pixels=new byte[videoBytes];using var gzip=new GZipStream(file,CompressionMode.Decompress);gzip.ReadExactly(pixels);gzip.ReadExactly(pcm);if(gzip.ReadByte()!=-1)throw new InvalidDataException("Unexpected original movie data");}
  frameBytes=new byte[frameSize];image=Image.CreateEmpty(width,height,false,Image.Format.Rgb8);texture=ImageTexture.CreateFromImage(image);
  audio=new AudioStreamWav{Format=AudioStreamWav.FormatEnum.Format16Bits,MixRate=sampleRate,Stereo=channels==2,Data=pcm};audioDuration=(double)audioBytes/(sampleRate*channels*2);
  sound=new AudioStreamPlayer{Stream=audio,PitchScale=1};AddChild(sound);
  TextureFilter=TextureFilterEnum.Linear;MouseFilter=MouseFilterEnum.Ignore;SetProcess(false);Upload(0);
  }catch{Release();throw;}
 }
 public void Play(){Stop();ended=false;PositionSeconds=0;Upload(0);indexed?.Start();audioStartDelay=AudioServer.GetTimeToNextMix()+AudioServer.GetOutputLatency();startUsec=Time.GetTicksUsec();playing=true;sound!.Play();SetProcess(true);Show();}
 public void Stop(){playing=false;SetProcess(false);sound?.Stop();}
 public override void _Process(double delta){
  if(!playing)return;
  try{
  // Account for the next audio mix and output latency, then use monotonic real time. Raw sampled
  // playback-position clock jitter moved some 15fps boundaries by two refreshes.
  // Presentation clock only; drift is checked against captured audio, including SCRUB (47.2s).
  double wall=(Time.GetTicksUsec()-startUsec)/1000000.0;
  PositionSeconds=Math.Max(0,wall-audioStartDelay);
  Upload(Math.Min(count-1,(int)(PositionSeconds*rate/scale)),true);
  if(PositionSeconds>=Duration&&!ended){ended=true;Stop();Finished?.Invoke();}
  }catch(Exception error){Stop();GD.PrintErr("MOVIE_DECODE_ERROR "+error.GetType().Name);Failed?.Invoke(error);}
 }
 void Upload(int frame,bool buffered=false){
  if(Frame==frame)return;ulong begin=Time.GetTicksUsec();
  if(indexed!=null){if(buffered){if(!indexed.TryCopy(frame,frameBytes))return;frame=indexed.CopiedFrame;}else indexed.ReadFrame(frame,frameBytes);}
  else Buffer.BlockCopy(pixels,frame*frameBytes.Length,frameBytes,0,frameBytes.Length);
  image!.SetData(width,height,false,Image.Format.Rgb8,frameBytes);texture!.Update(image);Frame=frame;UploadMilliseconds=(Time.GetTicksUsec()-begin)/1000.0;QueueRedraw();
 }
 public override void _Draw(){
  DrawRect(new Rect2(Vector2.Zero,Size),Colors.Black);
  if(texture==null)return;DrawTextureRect(texture,PictureRect,false);
 }
 void Release(){Stop();indexed?.Dispose();indexed=null;if(sound!=null){sound.Stream=null;sound.QueueFree();sound=null;}audio?.Dispose();audio=null;texture?.Dispose();texture=null;image?.Dispose();image=null;pixels=frameBytes=Array.Empty<byte>();Frame=-1;}
 public override void _ExitTree(){Release();}
}
