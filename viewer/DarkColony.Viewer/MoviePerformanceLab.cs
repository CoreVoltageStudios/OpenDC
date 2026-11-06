using Godot;
using System.Diagnostics;
using System.Text.Json;
namespace DarkColony.Viewer;

// Opt-in timing diagnostic: no texture readback, hashing, audio recording or disk
// writes inside playback. The older video lab remains the separate pixel/audio oracle.
public partial class MoviePerformanceLab : Node {
 readonly record struct Sample(double Wall,double Gap,double Position,int Frame,int Starvations,double Upload,long Allocated,int Gen0,int Gen1,int Gen2,double GcPause);
 public override async void _Ready(){
  var args=OS.GetCmdlineUserArgs();string Arg(string p,string d)=>args.FirstOrDefault(a=>a.StartsWith(p))?[p.Length..]??d;
  string output=Path.GetFullPath(Arg("--output=","movie-performance")),clip=Path.GetFullPath(Arg("--clip=",""));Directory.CreateDirectory(output);
  try{
   var size=Arg("--size=","1920x1080").Split('x');GetWindow().Mode=Window.ModeEnum.Windowed;GetWindow().Size=new(int.Parse(size[0]),int.Parse(size[1]));
   DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);Engine.MaxFps=60;
   for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   var runs=new List<object>();int repeats=int.Parse(Arg("--repeats=","3"));
   for(int run=0;run<repeats+1;run++){
    var samples=new List<Sample>(12000);bool skip=run==repeats,finished=false;var timer=Stopwatch.StartNew();
    var movie=new OriginalMovie();movie.Load(clip);AddChild(movie);movie.Size=GetViewport().GetVisibleRect().Size;movie.VolumeDb=-8;
    double loadMs=timer.Elapsed.TotalMilliseconds;movie.Finished+=()=>finished=true;
    var failure=(Exception?)null;movie.Failed+=e=>failure=e;timer.Restart();movie.Play();double previous=0;
    while(!finished&&timer.Elapsed.TotalSeconds<movie.Duration+3){
     await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
     double now=timer.Elapsed.TotalSeconds;
     samples.Add(new(now,(now-previous)*1000,movie.PositionSeconds,movie.Frame,movie.BufferStarvations,movie.UploadMilliseconds,GC.GetTotalAllocatedBytes(false),GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2),GC.GetTotalPauseDuration().TotalMilliseconds));previous=now;
     if(failure!=null)throw failure;
     if(skip&&now>=2){movie.Stop();break;}
    }
    double wall=timer.Elapsed.TotalSeconds;long retained=movie.RetainedVideoBytes;int starvations=movie.BufferStarvations;
    movie.QueueFree();for(int i=0;i<6;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    if((!finished&&!skip)||IndexedMovieFrames.ActiveWorkers!=0||movie.RetainedVideoBytes!=0)throw new InvalidOperationException("Movie completion/skip cleanup failed");
    using(var writer=new StreamWriter(Path.Combine(output,$"movie-{run}.csv"))){writer.WriteLine("wall,gap_ms,position,frame,starvations,upload_ms,allocated,gen0,gen1,gen2,gc_pause_ms");foreach(var s in samples)writer.WriteLine(FormattableString.Invariant($"{s.Wall:F6},{s.Gap:F4},{s.Position:F6},{s.Frame},{s.Starvations},{s.Upload:F4},{s.Allocated},{s.Gen0},{s.Gen1},{s.Gen2},{s.GcPause:F4}"));}
    runs.Add(new{run,skip,finished,loadMs,wall,retained,starvations,frames=samples.Count,workers=IndexedMovieFrames.ActiveWorkers,privateBytes=Process.GetCurrentProcess().PrivateMemorySize64});
   }
   File.WriteAllText(Path.Combine(output,"movie-performance.json"),JsonSerializer.Serialize(new{clip,viewport=GetViewport().GetVisibleRect().Size.ToString(),cap=Engine.MaxFps,vsync=DisplayServer.WindowGetVsyncMode().ToString(),classification="Optimized rendered playback; no readbacks/log writes/PCM recorder during samples; same-machine, not acoustic or display-scanout measurements",runs},new JsonSerializerOptions{WriteIndented=true}));
   GD.Print("MOVIE_PERFORMANCE_PASS");GetTree().Quit();
  }catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());GD.PrintErr(e);GetTree().Quit(1);}
 }
}
