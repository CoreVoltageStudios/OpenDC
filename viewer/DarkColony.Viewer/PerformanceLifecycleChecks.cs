using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 public async void RunPerformanceLifecycle(){
  try{
   if(IsolatedPlayerData==null)throw new InvalidOperationException("Isolated profile required");
   string fixtures=OS.GetCmdlineUserArgs().First(a=>a.StartsWith("--perf-fixture-root="))[20..];
   var rows=new List<object>();
   foreach(bool skip in new[]{false,true}){
    missionHarnessManual=true;var data=PersistenceData("SCENARIO/COUNCIL/COUN01.SCN");
    var snapshot=SaveCodec.Open(Path.Combine(fixtures,"generated/council-wars-first/checks/cw-taar-success.json"))["payload"]!.AsObject();
    InstallMission(data,OriginalMission.Restore(data,snapshot),false);await WarFrames(2);
    Check(missionVideo!=null,"Controlled Council result enters original movie");var movie=missionVideo!;
    ulong begin=Time.GetTicksMsec();int starvation=0;long bytes=0;
    while(missionVideo!=null&&Time.GetTicksMsec()-begin<20000){
     starvation=Math.Max(starvation,movie.BufferStarvations);bytes=Math.Max(bytes,movie.RetainedVideoBytes);
     if(skip&&movie.PositionSeconds>=2){await Send(Key.Escape);break;}
     await WarFrames(1);
    }
    await WarFrames(3);Check(missionVideo==null&&rewardsVisible&&IndexedMovieFrames.ActiveWorkers==0,"Movie natural/skip cleanup and input guard");
    await MissionCapture(skip?"movie-skipped-result":"movie-natural-result");
    rows.Add(new{skip,duration=movie.Duration,starvation,bytes,workers=IndexedMovieFrames.ActiveWorkers});
    ReturnMainMenu();await WarFrames(3);Check(mainMenu&&studioIntro==null&&missionMusic==null,"Return to menu without startup/media retention");
   }
   await MenuCapture("movie-return-menu");
   await VerifyMenuPause();
   File.WriteAllText(Path.Combine(output,"lifecycle.json"),JsonSerializer.Serialize(new{classification="Controlled outcome fixtures and Godot-dispatched skip; no screenshots during timed playback; pause state assertions",rows,checks},new JsonSerializerOptions{WriteIndented=true}));
   GD.Print("PERFORMANCE_LIFECYCLE_PASS");await FinishFirstStrike(0);
  }catch(Exception e){File.WriteAllText(Path.Combine(output,"lifecycle-failure.txt"),e.ToString());GD.PrintErr(e);await FinishFirstStrike(1);}
 }
}
