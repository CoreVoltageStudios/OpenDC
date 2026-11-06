using Godot;
using DarkColony.ContentImport;
namespace DarkColony.Viewer;
public partial class Viewer {
 async void ContentUiCheck(){try{
  if(IsolatedPlayerData==null)throw new InvalidOperationException("Importer UI checks require an isolated profile");
  async Task Capture(string name){await ToSignal(GetTree().CreateTimer(.35),SceneTreeTimer.SignalName.Timeout);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var im=GetViewport().GetTexture().GetImage();im.SavePng(Path.Combine(output,name+".png"));}
  await Capture("01-content-setup");string? source=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--import-source="))?[16..];
  if(source!=null){contentSource.Text=source;contentConsent.ButtonPressed=true;RunContentTask(InspectContent);await importTask!;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);ProcessContentSetup();Check(contentPlan!=null&&!contentImport.Disabled,"Inspected source can be imported");await Capture("02-source-confirmation");
   bool captured=false;Action<ImportProgress> report=p=>{importEvents.Enqueue(p);if(p.Stage==ImportStage.Converting&&!captured){captured=true;CallDeferred(nameof(CaptureContentProgress));}};
   ImportReceipt? receipt=null;RunContentTask(async()=>{receipt=await ContentImporter.ImportAsync(contentPlan!,true,importCancel!.Token,report);contentPlan=null;contentChanged=true;});await importTask!;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Check(receipt!=null,"Import completed through responsive task wrapper");ProcessContentSetup();contentInstalled.Text=InstalledContentText();contentStatus.Text="Content is ready.";contentDetails.Text=$"{receipt!.Files.Length} files validated. Source files and player data are unchanged.";UpdateContentButtons();await Capture("04-content-complete");
   contentSource.Text=Path.Combine(output,"missing-source.iso");try{await InspectContent();throw new Exception("Missing source accepted");}catch(IOException e){contentStatus.Text="Source could not be opened. Installed content is unchanged.";contentDetails.Text=e.Message;}await Capture("05-recovery");
   OpenImportedMenu();ulong started=Time.GetTicksMsec();while(!ready&&Time.GetTicksMsec()-started<60000)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Check(ready&&mainMenu,"Imported main menu ready");await Capture("06-imported-main-menu");
  }
  File.WriteAllText(Path.Combine(output,"import-ui-check.txt"),"Rendered Godot controls and real importer pipeline; automated state setup, not Windows input.\n");await FinishFirstStrike(0);
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
 async void CaptureContentProgress(){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var im=GetViewport().GetTexture().GetImage();im.SavePng(Path.Combine(output,"03-content-progress.png"));}
}
