using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyBriefingFollowup(){
  missionHarnessManual=true;
  Check(ClosingEnd("no closing marker",false)==17,"No-marker fallback uses text end");
  Check(ClosingEnd("...MIND LINK BROKEN...\n",false)==22,"Taar closing dots included");
  foreach(var (scenario,label) in new[]{("ALIEN/ALIEN01","taar-short"),("ALIEN/ALIEN04","taar-long"),("HUMAN/HUMAN01","human-short"),("HUMAN/HUMAN05","human-long")}){
   var d=PersistenceData("SCENARIO/"+scenario+".SCN");InstallMission(d,new OriginalMission(d,0),true);
   foreach(bool fast in new[]{false,true})foreach(int fps in new[]{30,60,144}){
    fastGame=fast;ResetBriefingPresentation();for(int n=0;n<fps*3;n++)ProcessBriefing(1d/fps);
    Check(Math.Abs(briefingCharacters-100)<=1,"Three seconds at "+fps+"FPS / fast="+fast+" "+label);
   }
   ResetBriefingPresentation();fastGame=label.EndsWith("long");
   Check(!BriefingTransmissionComplete,"New transmission active "+label);
   // Record real elapsed presentation, not accelerated simulation, through natural completion.
   using var rec=new AudioEffectRecord{Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,rec);
   missionMixer.Stop(briefingVoice);ClearMissionAudioBuffer();rec.SetRecordingActive(true); // typing-only diagnostic
   var rows=new List<object>();await UiCapture(label+"-start");bool middle=false;
   while(BriefingRevealing){
    await ToSignal(GetTree().CreateTimer(.1),SceneTreeTimer.SignalName.Timeout);
    rows.Add(new{briefingElapsed,briefingCharacters,briefingClosingEnd,status=BriefingStatus,typing=briefingTyping?.Playing??false,fastGame});
    if(!middle&&briefingElapsed>2){
     await UiCapture(label+"-revealing");ScrollBriefing(-12);
     await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
     Check(briefingScroll==0&&!briefingFollow,"Manual reading not overridden "+label);
     briefingFollow=true;middle=true;
    }
    Check(briefingElapsed<120,"Bounded reveal "+label);
   }
   await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
   rec.SetRecordingActive(false);using(var wav=rec.GetRecording())wav.SaveToWav(Path.Combine(output,label+"-typing.wav"));AudioServer.RemoveBusEffect(0,AudioServer.GetBusEffectCount(0)-1);
   Check(BriefingTransmissionComplete&&briefingTyping==null,"Natural marker completion closes status and sound "+label);
   Check(briefingScroll==Math.Max(0,briefingTotalLines-briefingVisibleLines)&&briefingFollow,"Natural reveal automatically follows to final line "+label);
   ScrollBriefing(briefingTotalLines);await UiCapture(label+"-complete");
   File.WriteAllText(Path.Combine(output,label+"-timing.json"),JsonSerializer.Serialize(rows));
   ResetFirstStrike();Check(!BriefingTransmissionComplete&&briefingCharacters==0,"Restart reopens status "+label);
   await Send(Key.Space);Check(missionBriefing&&BriefingTransmissionComplete&&briefingTyping==null,"Space closes without battle "+label);await UiCapture(label+"-skipped");
   Check(briefingScroll==Math.Max(0,briefingTotalLines-briefingVisibleLines),"Reveal all follows to end "+label);
   ResetFirstStrike();await SessionClick("brief-reveal");Check(missionBriefing&&BriefingTransmissionComplete,"Reveal button closes "+label);
   ResetFirstStrike();ProcessBriefing(.3);StartFirstStrike();Check(briefingTyping==null,"Early battle stops typing "+label);
   ResetFirstStrike();ReturnMainMenu();Check(briefingTyping==null,"Early menu exit stops typing "+label);
  }
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));GD.Print("FIRST_STRIKE_SUITE_PASS briefing-followup");
 }
}
