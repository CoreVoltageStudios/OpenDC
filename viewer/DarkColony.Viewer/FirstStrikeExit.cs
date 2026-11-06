using Godot;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool missionClosing;
 public override void _Notification(int what){
  if(what==NotificationWMWindowFocusOut||what==NotificationWMCloseRequest)ReleasePointerBoundary();
  if(contentSetup!=null&&what==NotificationWMCloseRequest){if(importTask is {IsCompleted:false}){contentExit=true;importCancel?.Cancel();}else{contentPlan?.Dispose();GetTree().Quit();}return;}
  if(!firstStrike&&!interferenceDev)return;
  if(what==NotificationWMCloseRequest){if(capturing||interferenceDev)RequestMissionExit(missionFaultCount>0?1:0);else AskAbandon("quit");}
  if(what==NotificationWMMouseExit){edgePointerExited=true;CancelCameraGesture();}
  if(what==NotificationWMMouseEnter)edgePointerExited=false;
  if(what==NotificationWMWindowFocusOut){CancelCameraGesture();targetMode="";nativeAttackMove=false;Input.MouseMode=Input.MouseModeEnum.Visible;}
 }
 async void RequestMissionExit(int code)=>await FinishFirstStrike(code);
 async Task FinishFirstStrike(int code) {
  if(missionClosing)return;
  CloseNetwork();missionClosing=true;ready=false;SetProcess(false);SetProcessUnhandledInput(false);
  animationTrace?.Dispose();animationTrace=null;animationTextureProof.Clear();
  animationProfile?.Dispose();animationProfile=null;
  // Stop while the audio/main loops still run. AudioServer owns the playback until
  // its deferred fade-out/removal has completed; _ExitTree is too late to await it.
  try {
   ShutdownMissionMedia();ulong start=Time.GetTicksMsec();var references=new List<int>();
   while(retiringMissionAudio.Count>0||retiredNetworks.Count>0){
    ProcessNetwork(0);
    DrainRetiredMissionAudio();references.Add(retiringMissionAudio.Count);
    if(retiringMissionAudio.Count==0&&retiredNetworks.Count==0)break;
    if(Time.GetTicksMsec()-start>2000)throw new InvalidOperationException("Audio playback did not release its engine references before exit");
    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   }
   if(capturing)File.WriteAllText(Path.Combine(output,"audio-exit-drain.json"),JsonSerializer.Serialize(new{retiredCounts=references,milliseconds=Time.GetTicksMsec()-start,passed=true}));
   await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); // Release queued media nodes before engine teardown.
   GetTree().Quit(code!=0?code:missionFaultCount>0||failureLogger.FirstError!=null?1:0);
  }catch(Exception error) {
   File.WriteAllText(Path.Combine(output,"exit-failure.txt"),error.ToString());GD.PushError(error.ToString());GetTree().Quit(1);
  }
 }
}
