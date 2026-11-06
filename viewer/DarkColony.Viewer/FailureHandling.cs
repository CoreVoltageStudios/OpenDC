using Godot;
namespace DarkColony.Viewer;

// The logger can be called on mixer/render threads. Only publish a string here.
public partial class FailureLogger : Logger {
 public string? FirstError;public string? LogPath;
 readonly object logLock=new();
 public static string Redact(string value)=>System.Text.RegularExpressions.Regex.Replace(value,@"[A-Za-z]:[\\/]Users[\\/][^\\/\s]+","[USER]");
 public override void _LogMessage(string message,bool error){if(LogPath!=null)lock(logLock)File.AppendAllText(LogPath,Redact(message)+System.Environment.NewLine);}

 public override void _LogError(string function,string file,int line,string code,string rationale,bool editorNotify,int errorType,Godot.Collections.Array<ScriptBacktrace> scriptBacktraces) {
  if(errorType!=(int)ErrorType.Warning)System.Threading.Interlocked.CompareExchange(ref FirstError,$"{file}:{line} {function}: {code} {rationale}",null);
 }
}

public partial class Viewer {
 FailureLogger failureLogger=new();string? missionFault;int missionFaultCount;bool failureCaptureStarted;
 void RemoveFailureLogger(){OS.RemoveLogger(failureLogger);failureLogger.Dispose();}
 void HaltFirstStrike(Exception error) {
  if(missionFault!=null)return;
  missionFault=FailureLogger.Redact(error.ToString());missionFaultCount++;paused=true;nativeAccumulator=0;
  missionVideo?.Stop();missionMusic?.Stop();missionMixer?.StopAll();ClearMissionAudioBuffer();missionAudioPlayer?.Stop();
  File.WriteAllText(Path.Combine(output,"first-fault.json"),nativeWorld.FaultJson??nativeWorld.DiagnosticJson(error));
  File.WriteAllText(Path.Combine(output,"failure.txt"),missionFault);
  // One report, after durable diagnostics. _Process never enters this world again.
  GD.PushError("FIRST_STRIKE_HALTED "+missionFault);QueueRedraw();
  if(capturing&&!failureCaptureStarted){failureCaptureStarted=true;CaptureFailure();}
 }
 async void CaptureFailure() {
  try {
   long tick=nativeWorld.Tick,clock=mission.Milliseconds;
   await ToSignal(GetTree().CreateTimer(.35),SceneTreeTimer.SignalName.Timeout);
   await MissionCapture("fault-halted");
   if(nativeWorld.Tick!=tick||mission.Milliseconds!=clock)throw new InvalidOperationException("Faulted simulation advanced");
   File.WriteAllText(Path.Combine(output,"containment.txt"),$"First fault latched; tick={tick}, clock={clock} unchanged across frames. Run FAILED.\n");
   if(missionSuite=="fault-restart") {
    await Send(Key.R);
    if(nativeWorld.Tick!=0||missionFault!=null||mission.Milliseconds!=0)throw new InvalidOperationException("Fault restart retained state");
    await Send(Key.Enter);await WaitMissionTick(40);await MissionCapture("fault-restarted");
    File.AppendAllText(Path.Combine(output,"containment.txt"),"Godot R/Enter recovered fresh simulation; sticky failure count="+missionFaultCount+". Run remains FAILED.\n");
   }
  }catch(Exception error){File.AppendAllText(Path.Combine(output,"failure.txt"),"\nCapture: "+error);}
  await FinishFirstStrike(1);
 }
 bool CheckEngineFailure() {
  if(failureLogger.FirstError is not {} error)return false;
  if(mainMenu){
   if(ready){ready=false;File.WriteAllText(Path.Combine(output,"engine-error.txt"),FailureLogger.Redact(error));
    if(capturing)GetTree().Quit(1);
    else {var dialog=new AcceptDialog{Title="OpenDC",DialogText="The menu could not continue. Your saves and campaign progress are unchanged. See the runtime log for details."};AddChild(dialog);dialog.Confirmed+=()=>GetTree().Quit(1);dialog.Canceled+=()=>GetTree().Quit(1);dialog.PopupCentered(new Vector2I(640,160));}
   }return true;
  }
  if(firstStrike&&ready){if(missionFault==null)HaltFirstStrike(new InvalidOperationException("Godot error: "+error));}
  else if(capturing){File.WriteAllText(Path.Combine(output,"engine-error.txt"),error);GetTree().Quit(1);}
  return true;
 }
}
