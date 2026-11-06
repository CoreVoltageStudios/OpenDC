using Godot;
using System.Text.Json;
using System.Security.Cryptography;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task StartStandalone(){
  // Explicit windowed requests must override the packaged fullscreen project default.
  // This export template retained that default despite --windowed on the CLI.
  var launchArgs=System.Environment.GetCommandLineArgs(); // Godot removes consumed display switches from OS.GetCmdlineArgs.
  if(launchArgs.Contains("--windowed")||launchArgs.Contains("-w")){
   GetWindow().Mode=Window.ModeEnum.Windowed;
   int resolution=Array.IndexOf(launchArgs,"--resolution");var requested=new Vector2I(1920,1080);
   if(resolution>=0&&resolution+1<launchArgs.Length){var parts=launchArgs[resolution+1].Split('x');if(parts.Length==2&&int.TryParse(parts[0],out int w)&&int.TryParse(parts[1],out int h)&&w>=960&&h>=540)requested=new(w,h);}
   GetWindow().Size=requested;GetWindow().MoveToCenter();
  }
  var identity=Godot.FileAccess.GetFileAsString("res://build.json");
  GuardPlayerBuild(identity);
  using(var build=JsonDocument.Parse(identity))productVersion=build.RootElement.GetProperty("version").GetString()!;
  await StartImportedContent();
 }
}
