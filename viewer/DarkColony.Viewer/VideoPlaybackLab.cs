using Godot;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace DarkColony.Viewer;

// Explicit media-only diagnostic. Never enters a mission or writes player progress.
public partial class VideoPlaybackLab : Node2D {
 public override async void _Ready() {
  var args=OS.GetCmdlineUserArgs();string Arg(string p,string d)=>args.FirstOrDefault(a=>a.StartsWith(p))?[p.Length..]??d;
  string output=Path.GetFullPath(Arg("--output=","video-check")),clip=Arg("--clip=","");Directory.CreateDirectory(output);
  var dimensions=Arg("--size=","1920x1080").Split('x');GetWindow().Mode=Window.ModeEnum.Windowed;GetWindow().Borderless=true;GetWindow().Position=Vector2I.Zero;GetWindow().Size=new(int.Parse(dimensions[0]),int.Parse(dimensions[1]));
  await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  TextureFilter=TextureFilterEnum.Nearest;RenderingServer.SetDefaultClearColor(Colors.Black);
  if(args.Any(a=>a.StartsWith("--compare-frame="))){await CompareFrame(clip,output,int.Parse(Arg("--compare-frame=","0")),Arg("--reference=",""),Arg("--old-texture=",""));return;}
  long memoryBefore=Process.GetCurrentProcess().PrivateMemorySize64;var loadWatch=Stopwatch.StartNew();
  bool legacy=clip.EndsWith(".ogv",StringComparison.OrdinalIgnoreCase);VideoStreamPlayer? player=null;OriginalMovie? movie=null;
  const float volumeDb=-8; // Same baseline as mission playback with SFX at 100%.
  if(legacy){player=new(){Stream=new VideoStreamTheora{File=clip},Expand=true,VolumeDb=volumeDb};AddChild(player);}else {movie=new();movie.Load(clip);movie.VolumeDb=volumeDb;AddChild(movie);}
  double loadMilliseconds=loadWatch.Elapsed.TotalMilliseconds;long memoryLoaded=Process.GetCurrentProcess().PrivateMemorySize64;
  var size=GetViewportRect().Size;float width=Math.Min(size.X,size.Y*16/9);if(player!=null){player.Size=new(width,width*9/16);player.Position=(size-player.Size)/2;}else movie!.Size=size;
  var record=new AudioEffectRecord{Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,record);record.SetRecordingActive(true);
  using var trace=new StreamWriter(Path.Combine(output,"presented.csv"));trace.WriteLine("wall,position,frame,sha256,readback_ms,source_frame,upload_ms");
  bool finished=false;if(player!=null)player.Finished+=()=>finished=true;else movie!.Finished+=()=>finished=true;var watch=Stopwatch.StartNew();if(player!=null)player.Play();else movie!.Play();string previous="";int frame=0,lastSource=-2;bool capture=false;double captureMilliseconds=0;
  while(!finished&&watch.Elapsed.TotalSeconds<(movie?.Duration+3??25)){
   await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
   double now=watch.Elapsed.TotalSeconds;var read=Stopwatch.StartNew();string hash=previous;
   if(player!=null||lastSource!=movie!.Frame){using var texture=(player!=null?player.GetVideoTexture():movie!.VideoTexture).GetImage();texture.Convert(Image.Format.Rgb8);var bytes=texture.GetData();hash=Convert.ToHexString(SHA256.HashData(bytes));lastSource=movie?.Frame??-2;
   if(hash!=previous){if(!args.Contains("--hash-only"))File.WriteAllBytes(Path.Combine(output,$"texture-{frame:D4}.rgb"),bytes);previous=hash;frame++;}}
   trace.WriteLine(FormattableString.Invariant($"{now:F6},{(player?.StreamPosition??movie!.PositionSeconds):F6},{frame-1},{hash},{read.Elapsed.TotalMilliseconds:F4},{movie?.Frame??-1},{movie?.UploadMilliseconds??0:F4}"));
   if(args.Contains("--screens")&&!capture&&now>=4){var captureWatch=Stopwatch.StartNew();using var screen=GetViewport().GetTexture().GetImage();screen.SavePng(Path.Combine(output,"screen-4s.png"));captureMilliseconds=captureWatch.Elapsed.TotalMilliseconds;capture=true;}
  }
  record.SetRecordingActive(false);using var audio=record.GetRecording();audio.SaveToWav(Path.Combine(output,"mixer.wav"));AudioServer.RemoveBusEffect(0,AudioServer.GetBusEffectCount(0)-1);record.Dispose();
  File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{clip,finished,timingClassification="Pixel/audio oracle with synchronous texture readback; not a low-overhead performance measurement. Use --movie-performance for frame pacing.",captureMilliseconds,seconds=watch.Elapsed.TotalSeconds,frames=frame,viewport=new[]{size.X,size.Y},legacy,enhanced=movie?.Enhanced??false,streaming=movie?.Streaming??false,retainedVideoBytes=movie?.RetainedVideoBytes??0,loadMilliseconds,memoryBefore,memoryLoaded,memoryEnd=Process.GetCurrentProcess().PrivateMemorySize64,bufferStarvations=movie?.BufferStarvations??0,volumeDb,filter=(player?.TextureFilter??movie!.TextureFilter).ToString()}));
  if(player!=null){player.Stop();var stream=player.Stream;player.Stream=null;stream.Dispose();player.QueueFree();}else {movie!.Stop();movie.QueueFree();}
  for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  File.WriteAllText(Path.Combine(output,"cleanup.json"),JsonSerializer.Serialize(new{activeWorkers=IndexedMovieFrames.ActiveWorkers,retainedVideoBytes=movie?.RetainedVideoBytes??0}));
  GetTree().Quit(finished&&IndexedMovieFrames.ActiveWorkers==0?0:1);
 }
 async Task CompareFrame(string clip,string output,int frame,string reference,string oldTexture){
  var movie=new OriginalMovie();movie.Load(clip);AddChild(movie);movie.Size=GetViewportRect().Size;movie.InspectFrame(frame);
  async Task Capture(string name){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var screen=GetViewport().GetTexture().GetImage();screen.SavePng(Path.Combine(output,name+".png"));}
  await Capture("after");var rect=movie.PictureRect;using var actual=movie.VideoTexture.GetImage();File.WriteAllBytes(Path.Combine(output,"actual-source-frame.rgb"),actual.GetData());movie.Hide();
  byte[] all=File.ReadAllBytes(reference),rgb=all.AsSpan(frame*320*180*3,320*180*3).ToArray();
  using var im=Image.CreateFromData(320,180,false,Image.Format.Rgb8,rgb);using var texture=ImageTexture.CreateFromImage(im);
  var control=new TextureRect{Texture=texture,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,TextureFilter=TextureFilterEnum.Linear,Position=rect.Position,Size=rect.Size};AddChild(control);await Capture("independent-linear");
  using var oldImage=Image.CreateFromData(320,180,false,Image.Format.Rgb8,File.ReadAllBytes(oldTexture));using var old=ImageTexture.CreateFromImage(oldImage);control.Texture=old;control.TextureFilter=TextureFilterEnum.Nearest;await Capture("before-matched-size");
  var size=GetViewportRect().Size;float width=Math.Min(size.X,size.Y*16/9);control.Size=new(width,width*9/16);control.Position=(size-control.Size)/2;await Capture("before-old-framing");
  File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{frame,clip,reference,oldTexture,viewport=new[]{size.X,size.Y},picture=new[]{rect.Position.X,rect.Position.Y,rect.Size.X,rect.Size.Y},exactIndependentRgb=rgb.SequenceEqual(actual.GetData()),before="Captured legacy Godot movie texture, same source frame, former nearest filtering. Matched-size and original-fit layouts separately labelled."}));
  control.Texture=null;control.QueueFree();movie.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);GetTree().Quit();
 }
}
