using Godot;
using DarkColony.Original;
using System.Text.Json.Nodes;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 void RelayLayout(string name){
  var hits=missionHits.ToArray();Check(hits.All(h=>GetViewportRect().Encloses(h.Rect)),name+" controls inside viewport");
  Check(!hits.Where((a,i)=>hits.Skip(i+1).Any(b=>a.Rect.Intersects(b.Rect))).Any(),name+" controls do not overlap");
  Check(hits.Select(h=>h.Id).Distinct().Count()==hits.Length,name+" unique actions");
  if(mainMenu&&missionPanel=="campaign")Check(hits.Where(h=>h.Id.StartsWith("campaign-start:")).All(h=>h.Rect.End.Y<=(UiSize.Y-70)*ui+1),name+" mission rows clear Surface footer");
 }
 async Task RelayKey(Key key,bool ctrl=false){Input.ParseInputEvent(new InputEventKey{Keycode=key,Pressed=true,CtrlPressed=ctrl});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Input.ParseInputEvent(new InputEventKey{Keycode=key,Pressed=false,CtrlPressed=ctrl});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
 async Task VerifyRelaySurface(){
  await MenuCapture("surface-first-launch");
  foreach(float scale in new[]{.75f,GetViewportRect().Size.Y/1080,1.5f}){
   ui=scale;await MenuCapture("surface-scale-"+scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));RelayLayout("Main "+scale);
   foreach(string panel in new[]{"campaign","options"}){await UiClick(panel);await MenuCapture("surface-"+panel+"-"+scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));RelayLayout(panel+scale);await UiClick("close");}
  }
  await UiClick("quit");await MenuCapture("surface-quit");Check(focusedUi=="close","Quit focuses safe Cancel");await Send(Key.Escape);
  for(int n=1;n<=5;n++){
   var data=PersistenceData($"SCENARIO/ALIEN/ALIEN{n:00}.SCN");InstallMission(data,new OriginalMission(data,0),false);AdvanceInterference(250);
   ui=GetViewportRect().Size.Y/1080;missionPointer=Vector2.Zero;await MissionCapture($"relay-{n}-empty");RelayLayout("Mission "+n);
   await Send(Key.Tab);Check(focusedUi.Length==0&&NativeSelected().Any(e=>nativeWorld.IsCommander(e)),"Plain Tab selects owned commander without HUD focus "+n);await Send(Key.M);Check(targetMode=="move","Move only retained "+n);await Send(Key.Escape);
   await MissionCapture($"relay-{n}-commander");
   foreach(float scale in new[]{.75f,1.25f,1.5f}){ui=scale;await MissionCapture($"relay-{n}-scale-"+scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));RelayLayout("Mission "+n+" scale "+scale);}
   if(economicMission){
    foreach(int tab in new[]{0,2,1}){await UiClick("tab:"+tab);await MissionCapture($"relay-{n}-tab-{tab}");RelayLayout("Category "+n+"/"+tab);}
    var product=missionHits.First(h=>h.Id.StartsWith("product:"));focusedUi=product.Id;relayPointerHelp=false;await MissionCapture($"relay-{n}-focus-tooltip");
    Check(relayTooltip!.Visible&&GetViewportRect().Encloses(relayTooltipBounds),"Rendered tooltip fits "+n);
    var previous=camera;float zoomBefore=zoom;Input.ParseInputEvent(new InputEventMouseMotion{Position=NativeWindowPoint(NativeArea.GetCenter()),Relative=Vector2.Right});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await RelayKey(Key.Right);await RelayKey(Key.Down);Check(camera==previous,$"Grid focus does not move camera {n}: {previous} -> {camera}; focus={focusedUi}");
    await NativeMouse(MouseButton.WheelDown,relayGrid.GetCenter());Check(zoom==zoomBefore,"Grid wheel cannot zoom world "+n);await Send(Key.Escape);
    if(n==2){
     nativeWorld.Teams[0].Money=20000;await UiClick("tab:0");await MissionCapture("relay-funded-hive");
     var buy=missionHits.First(h=>h.Id.StartsWith("product:")&&h.Enabled);int id=int.Parse(buy.Id[8..]);int price=developmentCatalog.Single(d=>d.Id==id).Cost;int before=nativeWorld.Teams[0].Money;
     await UiClick(buy.Id);Check(nativeWorld.Teams[0].Money==before-price,"Hive purchase uses authoritative price");await MissionCapture("relay-construction");Check(relayProducts[id].Constructing,"Construction tile binds pending city");
     await UiClick("tab:1");await UiClick("product:23");AdvanceInterference(3);await MissionCapture("relay-production");Check(relayProducts[23].Count>0,"Paid production state shown");
    }
    if(n==3){
     var commander=NativeSelected().First();var mine=nativeWorld.Spawn(44,0,commander.Cell,"fixture/relay-mine");var turret=nativeWorld.Spawn(9,0,commander.Cell,"fixture/relay-turret");var harvester=nativeWorld.Spawn(48,0,commander.Cell,"fixture/relay-harvester");
     foreach(var e in new[]{mine,turret,harvester})nativeSelection.Add((e.Slot,e.Incarnation));commander.Energy=255;
     await MissionCapture("relay-all-abilities");RelayLayout("Seven commands");Check(new[]{"inspire","deploy","turret","retract"}.All(id=>missionHits.Any(h=>h.Id==id)),"Mixed selection exposes every ability");
     await UiClick("turret");Check(turret.PendingOrder?.Order==OriginalOrder.Deploy&&mine.PendingOrder==null&&harvester.PendingOrder==null,"Turret dispatch excludes other roles");turret.PendingOrder=null;
     await UiClick("inspire");Check(commander.PendingOrder?.Order==OriginalOrder.Inspire&&turret.PendingOrder==null,"Mixed Inspire only eligible commander");commander.PendingOrder=null;
     await UiClick("deploy");Check(mine.PendingOrder?.Order==OriginalOrder.Deploy&&harvester.PendingOrder==null,"Deploy mine excludes retract");mine.PendingOrder=null;
     await UiClick("retract");Check(harvester.PendingOrder?.Order==OriginalOrder.Deploy&&turret.PendingOrder==null,"Retract excludes turret");
     // End this synthetic state before save/restore; next fresh mission starts normally.
     InstallMission(data,new OriginalMission(data,0),false);AdvanceInterference(250);
    }
   }else Check(!missionHits.Any(h=>h.Id.StartsWith("product:")||h.Id.StartsWith("tab:")),"Cityless mission has selected group without economy "+n);
   if(n==5){var foreign=nativeWorld.Spawn(72,7,new(60,50),"fixture/relay-foreign-commander");nativeSelection.Add((foreign.Slot,foreign.Incarnation));UpdateMissionInterface();Check(!NativeSelected().Contains(foreign),"Foreign commander excluded from summaries and commands");var before=foreign.PendingOrder;await Send(Key.I);Check(foreign.PendingOrder==before,"Inspire cannot command foreign commander");await MissionCapture("relay-foreign-ownership");InstallMission(data,new OriginalMission(data,0),false);AdvanceInterference(250);}
   await Send(Key.Escape);await UiClick("save");await UiClick("save-slot:0");if(missionPanel=="overwrite")await UiClick("overwrite-yes");long tick=nativeWorld!.Tick;await MenuCapture($"surface-save-{n}");await UiClick("close");await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");await MenuCapture($"surface-continue-{n}");Check(mission==null&&nativeWorld==null,"No mission behind Surface "+n);await UiClick("resume-save");Check(nativeWorld!.Tick==tick,"Restored tick "+n);await Send(Key.R);Check(missionBriefing&&nativeWorld!.Tick==0,"Restart "+n);ReturnMainMenu();
  }
  await VerifySurfaceStorage();
 }
 async Task VerifySurfaceStorage(){
  // Retained, unmodified pre-A04/A05 save, copied into this suite's isolated profile.
  string older=Path.Combine(root,"generated/persistence/main-menu-1080/player-persistence/saves/slot-1.json");
  File.Copy(older,SavePath(1),true);var oldBytes=File.ReadAllBytes(SavePath(1));ReadMissionSlot(1);
  File.WriteAllText(SavePath(2),"{broken fixture");File.SetLastWriteTimeUtc(SavePath(2),DateTime.UtcNow.AddMinutes(2));
  RefreshPersistenceCatalog();Check(continueSlot==0,"Continue skips newer invalid file and chooses valid saved state");
  foreach(float scale in new[]{GetViewportRect().Size.Y/1080,1.5f}){ui=scale;await UiClick("load");await MenuCapture("surface-load-"+scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));RelayLayout("Load "+scale);await UiClick("close");}
  await UiClick("load");await UiClick("load-slot:2");Check(mainMenu&&mission==null&&missionPanel=="save-error","Invalid save shows error without starting session");await MenuCapture("surface-invalid-save");await UiClick("error-close");Check(File.ReadAllText(SavePath(2))=="{broken fixture","Invalid save preserved");
  await UiClick("load-slot:1");Check(!mainMenu&&mission!.CampaignIndex==0,"Older compatible save loads through menu");Check(oldBytes.SequenceEqual(File.ReadAllBytes(SavePath(1))),"Older save not rewritten by migration");await MenuCapture("relay-older-save");
  await Send(Key.Escape);await UiClick("options");await MenuCapture("relay-options");long tick=nativeWorld!.Tick;missionHarnessManual=false;await ToSignal(GetTree().CreateTimer(.6),SceneTreeTimer.SignalName.Timeout);missionHarnessManual=true;Check(nativeWorld!.Tick>tick,"In-game options retain running simulation");
  ui=.875f;sfxVolume=.6f;musicVolume=.4f;SavePresentationPreferences();ArtworkPreference.SaveNavigation(artworkPreferencePath,false,.75f);ArtworkPreference.SaveFast(artworkPreferencePath,false);ArtworkPreference.SaveEnhancedVideo(artworkPreferencePath,false);ArtworkPreference.Save(artworkPreferencePath,ArtworkStyle.Crisp);
  var node=JsonNode.Parse(File.ReadAllText(artworkPreferencePath))!.AsObject();node["unrelatedFixturePreference"]="preserved";File.WriteAllText(artworkPreferencePath,node.ToJsonString());SavePresentationPreferences();Check(JsonNode.Parse(File.ReadAllText(artworkPreferencePath))!["unrelatedFixturePreference"]!.GetValue<string>()=="preserved","Settings update preserves unrelated values");
  string invalid=Path.Combine(output,"invalid-settings.json");File.WriteAllText(invalid,"broken settings");string? kept=ArtworkPreference.SavePresentation(invalid,1,1,1);Check(kept!=null&&File.ReadAllText(kept)=="broken settings"&&JsonNode.Parse(File.ReadAllText(invalid))!["interfaceScale"]!.GetValue<float>()==1,"Malformed settings preserved beside a recovered file, never silently replaced");
  ReturnMainMenu();ui=GetViewportRect().Size.Y/1080;
  // Isolated campaign-routing fixture, not mission wins or earned human progress.
  var completed=new JsonObject();var codec=new SaveCodec(nativeData);
  for(int i=0;i<4;i++)completed[i.ToString()]=new JsonObject{["runId"]=Guid.NewGuid().ToString("N"),["entry"]=codec.Node(new OriginalProfileContext(i,0,0,new int[4],"UI fixture")),["profile"]=codec.Node(new OriginalProfileContext(i+1,0,0,new int[4],"UI fixture"))};
  SaveCodec.Write(CampaignPath,new JsonObject{["completed"]=completed});RefreshPersistenceCatalog();
  for(int i=0;i<5;i++){await UiClick("campaign");if(i==0){await MenuCapture("surface-unlocked-campaign");Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:"))==5,"Exactly five mission routes; no Puppet Masters");}await UiClick("campaign-start:"+i);Check(missionBriefing&&mission!.CampaignIndex==i,"Unlocked campaign route "+i);ReturnMainMenu();}
  await MenuCapture("surface-final");
 }
 async Task VerifyRelayResume(){
  Check(Math.Abs(ui-.875)<.001&&Math.Abs(sfxVolume-.6)<.001&&Math.Abs(musicVolume-.4)<.001,"Sound and independent UI scale persist in fresh process");
  Check(!fastGame&&nativeSampling==0&&!enhancedVideo&&!edgeScrolling&&Math.Abs(edgeSpeed-.75)<.001,"All existing saved options persist");
  Check(continueSlot==0,"Fresh Continue ignores corrupted newer slot");await UiClick("resume-save");Check(mission!.CampaignIndex==4,"Fresh Continue restores Roswell");await MenuCapture("surface-fresh-continue");
 }
 async Task VerifyRelayStorageErrors(){
  Directory.CreateDirectory(Path.GetDirectoryName(SavePath(0))!);
  string older=Path.Combine(root,"generated/persistence/main-menu-1080/player-persistence/saves/slot-1.json");
  File.Copy(older,SavePath(0));var incompatible=JsonNode.Parse(File.ReadAllText(older))!.AsObject();incompatible["version"]=999;
  string invalid=incompatible.ToJsonString();File.WriteAllText(SavePath(1),invalid);RefreshPersistenceCatalog();
  Check(continueSlot==0&&saveInvalid[1],"Unsupported save version cannot displace valid Continue");
  await UiClick("load");await MenuCapture("surface-storage-slots");Check(!missionHits.Any(h=>h.Id=="load-slot:3"&&h.Enabled),"Empty slot has no enabled load action");
  await UiClick("load-slot:1");await MenuCapture("surface-version-attempt");Check(mainMenu&&mission==null&&missionPanel=="save-error","Unsupported version shows error without session mutation");await MenuCapture("surface-incompatible-save");await UiClick("error-close");
  Check(File.ReadAllText(SavePath(1))==invalid,"Unsupported save retained byte-for-byte");
  File.Move(SavePath(0),SavePath(0)+".preserved");await UiClick("load-slot:0");
  Check(mainMenu&&mission==null&&missionPanel=="save-error","File disappearing after catalog shows error without reset");await MenuCapture("surface-missing-save");
  Check(File.Exists(SavePath(0)+".preserved")&&!File.Exists(SavePath(0)),"Missing file not silently recreated");
  await UiClick("error-close");Check(!saveExists[0]&&continueSlot<0,"Dismissing missing-save error refreshes slot and Continue state");
 }
}
