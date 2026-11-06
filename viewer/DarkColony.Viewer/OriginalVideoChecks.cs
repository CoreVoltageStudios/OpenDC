using Godot;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyOriginalVideoLifecycle(){
  missionHarnessManual=true;
  if(OS.GetCmdlineUserArgs().Contains("--windows-video")){GetWindow().Mode=Window.ModeEnum.Windowed;GetWindow().Size=new(1280,720);}
  async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
  async Task Outcome(){
   if(mission.CampaignIndex==4){AdvanceInterference(250);var c=nativeWorld.Entities.Single(e=>e.Owner==0&&nativeWorld.IsCommander(e));c.Health=1;nativeWorld.Spawn(0,1,new(c.Cell.X+1,c.Cell.Z),"fixture/video-lethal-attack");for(int i=0;i<350&&mission.Outcome==null;i++)AdvanceInterference(1);}else if(mission.CampaignIndex==3&&missionSuite=="video-failure"){foreach(var c in nativeWorld.Teams[0].Cities.Take(5))if(c.Entity!=null)nativeWorld.Retire(c.Entity);}else if(mission.CampaignIndex==3){nativeWorld.Teams[1].Ai=0;foreach(var c in nativeWorld.Teams[1].Cities.Take(5))if(c.Entity!=null)nativeWorld.Retire(c.Entity);foreach(var guard in nativeWorld.Entities.Where(e=>e.Owner==2).ToArray())nativeWorld.Retire(guard);for(int i=0;i<100;i++){mission.SetClock(mission.Milliseconds+66);mission.Step();}}else if(mission.CampaignIndex==2){nativeWorld.Teams[1].Ai=0;mission.Blocks[3]!.Remaining=1;foreach(var c in nativeWorld.Teams[1].Cities.Take(5))if(c.Entity!=null)nativeWorld.Retire(c.Entity);}else if(economicMission)nativeWorld.Deaths[1,86]=6;else nativeWorld.Deaths[1,82]=11;
   for(int i=0;i<24;i++){mission.SetClock(mission.Milliseconds+66);mission.Step();}
   mission.SetClock(mission.Outcome!.Deadline+1);await Wait(.4);
   Check(missionVideo is {Visible:true}&&missionMusic is {Active:false}&&missionAudioPlayer is {Playing:false},"Movie exclusively owns audio after outcome");
  }
  await Send(Key.O);Check(!ArtworkPreference.LoadEnhancedVideo(artworkPreferencePath),"Legacy enhanced preference is ignored");await UiCapture("video-options-original");await Send(Key.Escape);
  await Outcome();Check(!missionVideo!.Enhanced,"Original option uses repaired native frames");var movie=missionVideo!;double at=movie.PositionSeconds;fastGame=false;await Wait(1);double middle=movie.PositionSeconds;fastGame=true;SetNativeSampling(0);ui=1.5f;await Wait(1);
  Check(Math.Abs(middle-at-1)<.1&&Math.Abs(movie.PositionSeconds-middle-1)<.1,"Classic/Fast and artwork/UI scale leave movie clock at1x");
  Check(movie.TextureFilter==TextureFilterEnum.Linear,"Movie filtering independent from Crisp artwork");
  var viewport=GetViewportRect().Size;var picture=movie.PictureRect;
  Check(Math.Abs(picture.Size.X/picture.Size.Y-16f/9)<.001&&picture.Position.X>=0&&picture.Position.Y>=0,"Square-pixel16:9 picture is centred, uncropped in native cinema stage");
  var previous=GetWindow().ContentScaleSize;GetWindow().ContentScaleSize=new(1600,900);await Wait(.1);
  Check(movie.Size==new Vector2(1600,900)&&movie.PictureRect.Size==new Vector2(1200,675),"Resize updates movie canvas without UI scaling or crop");
  GetWindow().ContentScaleSize=previous;await Wait(.1);
  await UiCapture("movie-active");
  bool windowsInput=OS.GetCmdlineUserArgs().Contains("--windows-video");
  if(windowsInput){GD.Print("VIDEO_WINDOWS_SKIP_READY");while(missionVideo!=null)await Wait(.05);Check(movie.PositionSeconds<movie.Duration-.5,"Actual Windows input skipped before natural completion");}
  else await Send(Key.Enter);
  await Wait(.1);Check(missionVideo==null&&rewardsVisible&&!GodotObject.IsInstanceValid(movie),"Skip releases decoded pixels, texture and audio before rewards");
  await Send(Key.Enter);Check(missionBriefing&&!rewardsVisible&&missionVideo==null,"Replay returns clean briefing");await Send(Key.Enter);await Send(Key.O);Check(!ArtworkPreference.LoadEnhancedVideo(artworkPreferencePath),"Old video settings cannot enable enhanced playback");await Send(Key.Escape);await Outcome();Check(!missionVideo!.Enhanced,"Replay remains original-source");
  movie=missionVideo!;double deadline=movie.Duration+2;while(!rewardsVisible&&deadline>0){await Wait(.25);deadline-=.25;}
  Check(rewardsVisible&&missionVideo==null,"Natural end reaches rewards and releases movie");
  await UiCapture("movie-natural-rewards");await Send(Key.Enter);await Send(Key.Escape);Check(mainMenu&&nativeWorld==null,"Replay then main menu releases session");
  await MenuCapture("movie-return-main-menu");
  File.WriteAllText(Path.Combine(output,"video-lifecycle.json"),JsonSerializer.Serialize(new{fixture=true,windowsInput,economicMission,checks}));
 }
}
