using Godot;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 OriginalMovie? councilIntroduction;
 ulong councilIntroductionAcceptAfter;
 string IntroductionPath(bool council)=>Path.Combine(root,council?"generated/council-wars-first/video/intro.odcv":"generated/original-video/v1/intro.odcv");
 void BeginCouncilIntroduction()=>BeginEditionIntroduction(true);
 void BeginEditionIntroduction(bool council){
  if(!mainMenu||council&&!councilAvailable||councilIntroduction!=null)return;
  var movie=new OriginalMovie{Size=GetViewportRect().Size,ZIndex=100};
  try{
   movie.Load(IntroductionPath(council));
   movie.VolumeDb=sfxVolume<=0?-80:-8+20*MathF.Log10(sfxVolume);
   AddChild(movie);councilIntroduction=movie;missionHits.Clear();focusedUi="";
   councilIntroductionAcceptAfter=Time.GetTicksMsec()+250;
   movie.Finished+=EndCouncilIntroduction;
   movie.Failed+=error=>{EndCouncilIntroduction();persistenceNotice="The introduction could not be played.";};
   movie.Play();QueueRedraw();
  }catch(IOException){movie.Free();persistenceNotice="The Council Wars introduction is unavailable.";}
 }
 void EndCouncilIntroduction(){
  if(councilIntroduction==null)return;
  councilIntroduction.Stop();councilIntroduction.QueueFree();councilIntroduction=null;
  councilIntroductionAcceptAfter=Time.GetTicksMsec()+250;focusedUi="council-introduction";QueueRedraw();
 }
 bool CouncilIntroductionInput(InputEvent input){
  if(councilIntroduction==null)return Time.GetTicksMsec()<councilIntroductionAcceptAfter;
  if(Time.GetTicksMsec()>=councilIntroductionAcceptAfter&&
   (input is InputEventKey{Pressed:true,Echo:false,Keycode:Key.Escape or Key.Space or Key.Enter or Key.KpEnter}||
    input is InputEventMouseButton{Pressed:true,ButtonIndex:MouseButton.Left}))EndCouncilIntroduction();
  return true;
 }
 async Task VerifyCouncilIntroduction(){
  await SessionClick("campaign");await SessionClick("edition:council");
  foreach(bool skip in new[]{true,false}){
   await SessionClick("council-introduction");var movie=councilIntroduction!;
   Check(movie.Streaming&&nativeWorld==null,"Introduction streams without mission simulation");
   await ToSignal(GetTree().CreateTimer(.6),SceneTreeTimer.SignalName.Timeout);Check(movie.Frame>0,"Introduction advances");
   await MenuCapture("council-intro-"+skip);long peak=0;int starvations=0;ulong began=Time.GetTicksMsec();
   if(skip)await Send(Key.Space);
   else while(councilIntroduction!=null&&Time.GetTicksMsec()-began<90000){peak=Math.Max(peak,movie.RetainedVideoBytes);starvations=Math.Max(starvations,movie.BufferStarvations);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
   await WarFrames();Check(councilIntroduction==null&&IndexedMovieFrames.ActiveWorkers==0&&mainMenu&&missionPanel=="campaign"&&nativeWorld==null,"Introduction returns cleanly without starting battle or replaying studio intro");
   if(!skip)File.WriteAllText(Path.Combine(output,"introduction.json"),JsonSerializer.Serialize(new{movie.Duration,peak,starvations,workers=IndexedMovieFrames.ActiveWorkers}));
   await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
  }
  foreach(int aspect in new[]{0,16})foreach(float scale in new[]{.75f,1f,1.5f}){maximumAspect=aspect;ApplyAspectLimit();ui=scale;await WarFrames();await MenuCapture($"final-campaign-intro-{aspect}-{scale}");RelayLayout("Complete expansion campaign");}
  GD.Print("COUNCIL_INTRODUCTION_PASS");
 }
}
