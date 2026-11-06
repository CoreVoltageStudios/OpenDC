using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 int reinforcementScriptCursor,reinforcementWorldCursor;bool reinforcementTraceFailed;
 void TraceMissionReinforcements(){
  if(reinforcementTraceFailed)return;
  var lines=new List<string>();
  foreach(var e in mission.Events.Skip(reinforcementScriptCursor))if(e.Kind is "trigger" or "action")lines.Add(JsonSerializer.Serialize(new{kind="script",ev=e}));
  foreach(var e in nativeWorld.Events.Skip(reinforcementWorldCursor))if(e.Kind is "transport-created" or "unloaded" or "transport-release" or "capture")lines.Add(JsonSerializer.Serialize(new{kind="world",ev=e}));
  reinforcementScriptCursor=mission.Events.Count;reinforcementWorldCursor=nativeWorld.Events.Count;
  if(lines.Count==0)return;
  try{File.AppendAllLines(Path.Combine(output,"mission-events.jsonl"),lines);}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException){reinforcementTraceFailed=true;Godot.GD.Print("Mission event log unavailable; gameplay continues.");}
 }
}
