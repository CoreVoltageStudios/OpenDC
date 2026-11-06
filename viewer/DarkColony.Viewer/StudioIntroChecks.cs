using Godot;
using System.Text.Json;
using DarkColony.StudioIntro;
namespace DarkColony.Viewer;
// Source-only verification of the Core Voltage Studios intro (--studio-intro-check):
// storyboard key frames at full size, a dense 0.1 s sequence for temporal review,
// a real-time run (length, sound clock agreement, frame gaps), Godot-dispatched key and
// mouse skips, then the ordinary launcher path into the main menu suite.
public partial class Viewer {
 async Task Frame(string name){
  QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
  using var image=GetViewport().GetTexture().GetImage();if(image.SavePng(Path.Combine(output,name+".png"))!=Error.Ok)throw new IOException("Capture failed: "+name);
 }
 async void StudioIntroCheck(string[] args){
  try{
   var results=new List<object>();
   var preview=new CoreVoltageIntro();AddChild(preview);
   // Refined cues: tick .30, feed .94, ring-1 lock 2.04, ring-2 lock 3.05, flash 3.72, handoff 6.0.
   foreach(double t in new[]{0,.1,.3,.6,.94,1.4,1.8,2.04,2.5,3.05,3.4,3.7,3.75,3.9,4.3,4.8,5.5,6.3,6.8})
    {preview.ShowAt(t);await Frame($"intro-key-{t*1000:0000}");}
   Directory.CreateDirectory(Path.Combine(output,"sequence"));
   for(int i=0;i<=70;i++){
    preview.ShowAt(i/10.0);QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
    using var image=GetViewport().GetTexture().GetImage();image.Resize(480,image.GetHeight()*480/image.GetWidth(),Image.Interpolation.Bilinear);image.SavePng(Path.Combine(output,"sequence",$"{i:000}.png"));
   }
   preview.QueueFree();

   var run=new CoreVoltageIntro();AddChild(run);var trace=new List<(double Wall,double Clock,double? Sound)>();
   ulong begin=Time.GetTicksUsec();run.Begin();
   while(!run.Done){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);trace.Add(((Time.GetTicksUsec()-begin)/1e6,run.Elapsed,run.SoundPosition));}
   double total=(Time.GetTicksUsec()-begin)/1e6;run.QueueFree();
   var audible=trace.Where(x=>x.Sound!=null&&x.Clock>.5&&x.Clock<6.4).ToList();
   double drift=audible.Count==0?double.NaN:audible.Max(x=>Math.Abs(x.Clock-x.Sound!.Value));
   double gap=trace.Zip(trace.Skip(1),(a,b)=>b.Wall-a.Wall).DefaultIfEmpty(0).Max();
   double bloomWall=trace.First(x=>x.Clock>=3.72).Wall;
   results.Add(new{run="real-time",total,frames=trace.Count,audibleFrames=audible.Count,maxClockMinusSound=drift,maxFrameGap=gap,bloomWall,outputLatency=AudioServer.GetOutputLatency(),device=AudioServer.OutputDevice});
   Check(total>=6.95&&total<7.4,"intro plays its 7.0 s length");
   Check(audible.Count>trace.Count/2,"intro sound plays during the sequence");
   Check(drift<.08,"picture clock follows the sound within 80 ms");
   Check(gap<.1,"no frame gap above 100 ms");

   foreach(var (name,at,input) in new (string,double,InputEvent)[]{("key-space",2.0,new InputEventKey{Keycode=Key.Space,Pressed=true}),("mouse-left",1.0,new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=GetViewportRect().Size/2})}){
    var skip=new CoreVoltageIntro();AddChild(skip);skip.Begin();
    while(skip.Elapsed<at)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    double pressed=skip.Elapsed;Input.ParseInputEvent(input);
    while(!skip.Done)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    results.Add(new{run="skip-"+name,pressed,finished=skip.Elapsed,skipped=skip.Skipped});
    Check(skip.Skipped&&skip.Elapsed-pressed<.5,"skip by "+name+" ends the intro promptly");
    skip.QueueFree();
   }
   File.WriteAllText(Path.Combine(output,"studio-intro-checks.json"),JsonSerializer.Serialize(new{results,checks},new JsonSerializerOptions{WriteIndented=true}));
   GD.Print("STUDIO_INTRO_CHECKS passed "+checks.Count);

   // Ordinary launcher path: intro, Escape skip, then the main menu and its suite.
   BeginStudioIntro();
   while(studioIntro!.Elapsed<3.2)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   await Frame("intro-before-menu");
   Input.ParseInputEvent(new InputEventKey{Keycode=Key.Escape,Pressed=true});
   await EndStudioIntro();
   PrepareNative(args.Append("--main-menu").ToArray());
  }catch(Exception e){GD.PushError(e.ToString());File.WriteAllText(Path.Combine(output,"studio-intro-failure.txt"),e.ToString());GetTree().Quit(1);}
 }
}
