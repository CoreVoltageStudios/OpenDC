using Godot;
using DarkColony.ContentImport;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;

// Explicit exported-build diagnostics. No normal startup, profile or game rules change.
public partial class Viewer {
 static void WriteEngineNotices(string destination){
  if(!Path.IsPathFullyQualified(destination))throw new ArgumentException("An absolute notices destination is required");
  Directory.CreateDirectory(destination);
  File.WriteAllText(Path.Combine(destination,"GODOT-LICENSE.txt"),Engine.GetLicenseText());
  File.WriteAllText(Path.Combine(destination,"GODOT-COMPONENTS.json"),Godot.Json.Stringify(Engine.GetCopyrightInfo(),"  "));
  File.WriteAllText(Path.Combine(destination,"GODOT-THIRD-PARTY-LICENSES.json"),Godot.Json.Stringify(Engine.GetLicenseInfo(),"  "));
  File.WriteAllText(Path.Combine(destination,"GODOT-VERSION.json"),Godot.Json.Stringify(Engine.GetVersionInfo(),"  "));
  GD.Print("ENGINE_NOTICES_EXTRACTED");
 }
 bool portabilityStarted;
 string? PortabilityMode=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--portability-check="))?[20..];
 async void RunPortabilityCheck(){
  if(portabilityStarted)return;portabilityStarted=true;
  try{
   Check(!SourceLaunch,"Portability checks use the exported application");
   Check(IsolatedPlayerData!=null||installProfile.StartsWith("portability-"),"Isolated comparison profile required");
   string Arg(string key)=>OS.GetCmdlineUserArgs().Single(a=>a.StartsWith(key+"="))[(key.Length+1)..];
   Directory.CreateDirectory(output);missionHarnessManual=true;
   File.WriteAllText(Path.Combine(output,"godot-license.txt"),Engine.GetLicenseText());
   File.WriteAllText(Path.Combine(output,"godot-license-components.json"),Godot.Json.Stringify(Engine.GetCopyrightInfo()));
   File.WriteAllText(Path.Combine(output,"godot-third-party-licenses.txt"),string.Join("\n\n",Engine.GetLicenseInfo().Select(k=>k.Key+"\n"+k.Value)));
   async Task Frames(int n=3){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
   async Task Capture(string name){await Frames(5);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(output,name+".png"));}
   async Task Menu(){OpenImportedMenu();ulong start=Time.GetTicksMsec();while(!ready&&Time.GetTicksMsec()-start<90000)await Frames(1);Check(ready&&mainMenu&&contentSetup==null,"Imported main menu opened");await Frames();}
   var resources=new List<object>();
   void Inventory(string path){using var dir=DirAccess.Open(path);dir.IncludeHidden=true;foreach(string file in dir.GetFiles()){string resource=(path=="res://"?path:path+"/")+file;Check(!resource.Contains("private/")&&!resource.Contains("generated/")&&!new[]{".tro",".avi",".odcv",".spr",".iso",".cue",".mdf"}.Any(resource.EndsWith),"No original content in PCK: "+resource);resources.Add(new{path=resource,readable=Godot.FileAccess.FileExists(resource),resourceAvailable=ResourceLoader.Exists(resource)});}foreach(string child in dir.GetDirectories())Inventory((path=="res://"?path:path+"/")+child);}
   Inventory("res://");File.WriteAllText(Path.Combine(output,"pck-resources.json"),JsonSerializer.Serialize(resources,new JsonSerializerOptions{WriteIndented=true}));
   var mode=PortabilityMode!;if(mode=="inventory"){await Capture("bootstrap");GD.Print("PORTABILITY_PASS inventory");GetTree().Quit();return;}var watch=System.Diagnostics.Stopwatch.StartNew();
   string? before=contentStore?.ActiveRoot;
   if(mode is "import-base" or "add-council" or "cancel" or "interrupt" or "repair"){
    if(contentSetup==null)ShowContentSetup();
    await Capture("01-setup");contentSource.Text=Arg("--import-source");
    contentDestination.Text=Arg("--import-destination");contentConsent.ButtonPressed=true;
    RunContentTask(InspectContent);await importTask!;await Frames();ProcessContentSetup();
    Check(contentPlan!=null&&!contentImport.Disabled,"Source inspection permits import");await Capture("02-confirmation");
    string? prior=new ContentStore(contentDestination.Text).ActiveRoot;
    if(mode=="cancel"){
     RunContentTask(async()=>{await ContentImporter.ImportAsync(contentPlan!,true,importCancel!.Token,p=>{importEvents.Enqueue(p);if(p.Stage==ImportStage.Copying)importCancel.Cancel();});});
     await importTask!;await Frames();Check(new ContentStore(contentDestination.Text).ActiveRoot==prior,"Cancelled import retains prior active generation");
     await Capture("03-cancelled");
    }else{
     bool captured=false;
     RunContentTask(async()=>{
      var receipt=await ContentImporter.ImportAsync(contentPlan!,true,importCancel!.Token,p=>{importEvents.Enqueue(p);if(p.Stage==ImportStage.Converting&&!captured){captured=true;CallDeferred(nameof(CaptureContentProgress));GD.Print("PORTABILITY_CONVERTING");if(mode=="interrupt")while(!importCancel.IsCancellationRequested)Thread.Sleep(100);}});
      contentPlan=null;contentChanged=true;RememberContentStore();contentInstalled.Text=InstalledContentText();
      File.WriteAllText(Path.Combine(output,"receipt-summary.json"),JsonSerializer.Serialize(new{receipt.BaseEdition,receipt.CouncilEdition,receipt.Warnings,files=receipt.Files.Length,receipt.ConversionRuntime}));
     });await importTask!;await Frames();ProcessContentSetup();
     Check(contentChanged,"Import activated");await Capture("04-complete");await Menu();
    }
   }
   if(mode!="cancel"&&mode!="interrupt"){
    Check(ready&&mainMenu,"Main menu ready without source lookup");
    Check(Path.GetFullPath(root)==new ContentStore(ChosenContentStore()).ActiveRoot,"Profile remembers activated content store without command-line override");
    Check(File.Exists(ContentLocationRecord),"Content location saved in selected profile");
    if(mode is "base" or "council"){
     Check(Math.Abs(sfxVolume-.625f)<.001f&&Math.Abs(ui-1.25f)<.001f&&maximumAspect==16,"Settings survive process restart");
     var expected=JsonNode.Parse(File.ReadAllText(Path.Combine(PlayerDataRoot,"portability-save-expected.json")));
     LoadMissionSlot(3);Check(JsonNode.DeepEquals(mission.Snapshot(),expected),"Cross-process saved War state restored exactly");ReturnMainMenu();
    }
    await Capture("05-main-menu");
    bool expansion=contentStore!.Receipt!.CouncilEdition!=null;
    Check(councilAvailable==expansion,"Optional expansion availability matches activated receipt");
    foreach(string campaign in expansion?new[]{"cw-human","cw-taar"}:new[]{"human","taar","training-human","training-taar"}){
     ReturnMainMenu();SelectedCampaign=campaign;RefreshPersistenceCatalog();PersistenceAction("campaign-start:0");
     Check(missionBriefing&&!godMode,"Ordinary first-entry briefing "+campaign);await Frames(18);await Capture(campaign+"-briefing");
     await Send(Key.Space);Check(!BriefingRevealing&&missionBriefing,"Reveal does not enter battle");await Send(Key.Enter);
     AdvanceInterference(160);Check(!missionBriefing&&missionFault==null,"Imported gameplay "+campaign);await Capture(campaign+"-gameplay");
     var snap=mission.Snapshot();SaveMissionSlot(0);LoadMissionSlot(0);Check(JsonNode.DeepEquals(snap,mission.Snapshot()),"Save/load "+campaign);
    }
    ReturnMainMenu();StartWarMatch(WarConfiguration.Default("D4PLAY04",37));AdvanceInterference(200);Check(IsWar&&!godMode,"Imported Single-player War");
    SaveMissionSlot(3);File.WriteAllText(Path.Combine(PlayerDataRoot,"portability-save-expected.json"),mission.Snapshot().ToJsonString());await Capture("war-gameplay");
    ReturnMainMenu();ui=1.25f;sfxVolume=.625f;maximumAspect=16;SavePresentationPreferences();SaveSettings(p=>ArtworkPreference.SaveAspect(p,maximumAspect),"Aspect save failed");ApplyAspectLimit();
    OpenMissionPanel("options");await Frames();
    // Landfall deliberately scrolls these rows at enlarged UI sizes. Follow the
    // visible controls, rather than assuming the old single-screen Options layout.
    for(int i=0;i<8&&!missionHits.Any(h=>h.Id=="game-content");i++)await SessionClick("options-down");
    await SessionClick("game-content");Check(contentSetup!=null,"Options content management");await Capture("content-management");await Menu();
    Check(contentStore.Validate(true,CancellationToken.None).Length==0,"Activated content validates completely");
   }
   File.WriteAllText(Path.Combine(output,"portability-result.json"),JsonSerializer.Serialize(new{mode,seconds=watch.Elapsed.TotalSeconds,playerData=PlayerDataRoot,profileKind=PlayerDataKind,contentRecord=ContentLocationRecord,store=ChosenContentStore(),active=contentStore?.ActiveRoot,before,conversionRuntime=ContentImporter.ConversionRuntime,sourceLaunch=SourceLaunch,workingDirectory=System.Environment.CurrentDirectory,executable=System.Environment.ProcessPath,modules=System.Diagnostics.Process.GetCurrentProcess().Modules.Cast<System.Diagnostics.ProcessModule>().Select(m=>m.FileName).ToArray(),classification="Exported real renderer and importer; scripted UI/commands. Not human-input or clean-machine acceptance."},new JsonSerializerOptions{WriteIndented=true}));
   GD.Print("PORTABILITY_PASS "+mode);ShutdownMissionMedia();GetTree().Quit();
  }catch(Exception e){GD.PushError("PORTABILITY_FAILED "+e);GetTree().Quit(1);}
 }
}
