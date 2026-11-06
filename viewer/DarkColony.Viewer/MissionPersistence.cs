using Godot;
using DarkColony.Original;
using DarkColony.Assets;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 string persistenceNotice="";int overwriteSlot=-1;bool persistenceSkipDelta,campaignWritePending;
 string PersistenceRoot=>IsolatedPlayerData??(capturing?Path.Combine(output,"player-persistence"):PlayerDataRoot);
 readonly string[] saveLabels=new string[4],saveTitles=new string[4],saveDetails=new string[4];readonly bool[] saveExists=new bool[4];
 readonly OriginalProfileContext?[] campaignEntries=new OriginalProfileContext?[OriginalCampaign.SupportedTaarMissions];
 OriginalProfileContext? availableInterference=>campaignEntries[1];OriginalProfileContext? completedInterference=>campaignEntries[2];OriginalProfileContext? completedLegacy=>campaignEntries[3];OriginalProfileContext? completedBreakthrough=>campaignEntries[4];bool campaignReadFailed,taarCampaignComplete;
 void RefreshPersistenceCatalog(){
  councilAvailable=OriginalContent.Available(root,out councilUnavailable);
  continueSlot=-1;DateTime latest=DateTime.MinValue;
  for(int i=0;i<4;i++){
   saveExists[i]=File.Exists(SavePath(i));string? digest=CatalogDigest(i);
   if(!ReuseCatalog(i,digest)){
    saveInvalid[i]=false;saveLabels[i]=SlotDescription(i);
    if(mainMenu&&saveExists[i])try{ReadMissionSlot(i);}catch(Exception e){saveInvalid[i]=true;saveDetails[i]=e.Message;}
    RememberCatalog(i,digest);
   }
   if(mainMenu&&saveExists[i]&&!saveInvalid[i])try{var stamp=File.GetLastWriteTimeUtc(SavePath(i));if(stamp>latest){latest=stamp;continueSlot=i;}}catch(Exception e) when(e is IOException or UnauthorizedAccessException){saveInvalid[i]=true;}
  }
  try{for(int i=1;i<campaignEntries.Length;i++)campaignEntries[i]=CampaignProgress.Entry(CampaignPath,i,SelectedCampaign);taarCampaignComplete=CampaignProgress.Read(CampaignPath,SelectedCampaign)["completed"]![(OriginalCampaign.CatalogCount(SelectedCampaign)-1).ToString()]!=null;campaignReadFailed=false;}catch{Array.Clear(campaignEntries);campaignReadFailed=true;taarCampaignComplete=false;}
 }
 string SavePath(int slot)=>Path.Combine(PersistenceRoot,"saves",$"slot-{slot+1}.json");
 string CampaignPath=>Path.Combine(PersistenceRoot,(mainMenu?SelectedCampaign:mission?.CampaignId??SelectedCampaign) switch{"human"=>"human-campaign-v1.json","taar" or "war"=>"campaign-v1.json","cw-human"=>"council-human-campaign-v1.json","cw-taar"=>"council-taar-campaign-v1.json","training-human"=>"training-human-v1.json","training-taar"=>"training-taar-v1.json",_=>throw new InvalidDataException("Unknown campaign progress") });
 bool CanSaveMission=>!IsNetworkMatch&&!mainMenu&&firstStrike&&!missionBriefing&&!missionNext&&!mission.AtOutcome&&missionFault==null;
 void PersistenceError(Exception e){persistenceNotice=e is FileNotFoundException or DirectoryNotFoundException?e.Message:e is IOException?"Unable to access save storage. Check free space and folder permissions.":e is InvalidDataException?e.Message:"Save data is incomplete or damaged. Choose another slot.";surfaceErrorReturn=missionPanel=="save"||missionPanel=="overwrite"?"save":"load";OpenMissionPanel("save-error");focusedUi="error-close";GD.Print("PERSISTENCE_ERROR "+e.GetType().Name);}

 string SlotDescription(int i){
  saveCampaigns[i]="";saveTitles[i]="Empty";saveDates[i]="";saveDetails[i]="No mission saved in this slot.";
  if(File.Exists(SavePath(i)))try{var p=SaveCodec.Open(SavePath(i))["payload"]!;saveTitles[i]=p["title"]!.GetValue<string>();saveCampaigns[i]=WarCatalog.IsWar(p["mission"]!["scenario"]!.GetValue<string>())?"Single-player War":OriginalCampaign.Label(OriginalCampaign.Identity(p["mission"]!["scenario"]!.GetValue<string>()));saveDates[i]=p["date"]!.GetValue<string>();saveDetails[i]=saveDates[i]+"  |  Mission time "+TimeSpan.FromMilliseconds(p["mission"]!["clock"]!.GetValue<long>()).ToString(@"hh\:mm\:ss");}
  catch{saveInvalid[i]=true;saveTitles[i]="Unreadable / incompatible";saveDates[i]="Save could not be read";saveDetails[i]="This file is preserved. Choose another slot.";}
  return $"Slot {i+1} - {saveTitles[i]}";
 }
 void SaveMissionSlot(int i){try{if(!CanSaveMission)throw new InvalidOperationException("Save during active play; briefing and outcome screens cannot be saved.");
  // Synchronous main-thread boundary: no simulation tick interleaves this capture.
  var snap=mission.Snapshot();var selection=new JsonArray();foreach(var e in NativeSelected())selection.Add(new JsonArray(e.Slot,e.Incarnation));
  var assets=new JsonObject();foreach(var source in nativeData.Sources)assets[source.Path]=source.Sha256;
  var payload=new JsonObject{["assets"]=assets,["title"]=(IsWar?"War · ":IsCouncil||IsTraining?OriginalCampaign.Label(mission.CampaignId)+" · ":"")+campaignMission.Title,["date"]=DateTime.Now.ToString("yyyy-MM-dd HH:mm"),["mission"]=snap,["camera"]=new JsonArray(camera.X,camera.Y,zoom),["selection"]=selection};
  SaveCodec.Write(SavePath(i),payload);persistenceSkipDelta=true;persistenceNotice=$"Saved slot {i+1}.";RefreshPersistenceCatalog();overwriteSlot=-1;missionPanel="save";
 }catch(Exception e){PersistenceError(e);}}
 OriginalDefinitions PersistenceData(string scenario){if(!WarCatalog.IsWar(scenario)&&!OriginalCampaign.Supported(scenario))throw new InvalidDataException("This mission is not available in this build");string directory=OriginalContent.Root(root,WarCatalog.IsWar(scenario)?"taar":OriginalCampaign.Identity(scenario));if(OriginalContent.CouncilScenario(scenario))OriginalContent.ValidateCouncil(directory);return new(directory,OriginalScenarioImport.Load(directory,scenario));}
 void InstallMission(OriginalDefinitions data,OriginalMission restored,bool briefingMode){
  ClearLocalPause();
  // All snapshot/content validation happens before this presentation-only commit.
  persistenceNotice="";float oldUi=ui;bool oldSpeed=fastGame;string suite=missionSuite;
  ShutdownMissionMedia();markerOffsets.Clear();interfaceClips.Clear();interfaceBanks.Clear();miniBase=miniPixels=miniVisibility=Array.Empty<byte>();miniSelector=-1;missionRewards=null;
  HideWarSetup();mainMenu=false;mission=restored;nativeWorld=restored.World;data=nativeWorld.Data;if(!restored.IsWar)SelectedCampaign=restored.CampaignId;nativeData=data;economicMission=data.Scenario.Entities.Any(e=>e.Kind=="city"&&e.Owner==LocalTeam);campaignEntry=restored.World.Session.Profile?.Copy();nativeSeed=restored.World.Seed;
  nativeOcclusion=new(data.Scenario.Map,data.Scenario.Terrain);nativePalette=GameColors.ReadGifPalette(data.Read(data.Scenario.Header.PaletteName));
  PrepareMissionPresentation(Array.Empty<string>());missionSuite=suite;ui=oldUi;fastGame=oldSpeed;
  ResetMissionInterface();ResetMissionAudio();artifactAudio.Clear();diseaseAudio.Clear();transportAudio.Clear();nativeSoundCursor=0;missionAudioAudit.Clear();messageSoundCursor=restored.MessageIndex;rewardsVisible=false;rewardMilliseconds=0;
  mission=restored;nativeWorld=restored.World;ApplySessionPlaytesting();persistenceSkipDelta=true;missionSounds=new(data.Read("SOUND/SLIST.DAT"),data.Read(AmbientPath(data)),1);
  if(economicMission)developmentCatalog=nativeWorld.ProductionCatalog(LocalTeam);
  missionFault=null;failureLogger.FirstError=null;nativeSelection.Clear();nativeAccumulator=0;missionWall=0;missionBriefing=briefingMode;missionNext=missionOutcomeShown=false;paused=briefingMode;nativeOmniscient=false;
  reinforcementScriptCursor=reinforcementWorldCursor=0;reinforcementTraceFailed=false;
  ResetMissionCamera();
  foreach(int selector in Enumerable.Range(0,8))PrepareNativeTerrain(selector);ClearNativeBodies();miniTick=-100;
  if(!IsWar)ResetBriefingPresentation();if(briefingMode)briefingVoice=PlayMissionSound(1000);else{briefingVoice=null;missionMusic?.Start();}
  GetWindow().Title="OpenDC | "+campaignMission.Title;QueueRedraw();
 }
 (OriginalDefinitions Data,OriginalMission Mission,Vector3 Camera,(int Slot,int Incarnation)[] Selection) ReadMissionSlot(int i){
  var p=SaveCodec.Open(SavePath(i))["payload"]!.AsObject();var snap=p["mission"]!.AsObject();string scenario=snap["scenario"]!.GetValue<string>();string contentRoot=OriginalContent.Root(root,WarCatalog.IsWar(scenario)?"taar":OriginalCampaign.Identity(scenario));if(OriginalContent.CouncilScenario(scenario))OriginalContent.ValidateCouncil(contentRoot);var assets=p["assets"]!.AsObject();foreach(var asset in assets){string basePath=Path.GetFullPath(contentRoot)+Path.DirectorySeparatorChar;string path=Path.GetFullPath(Path.Combine(basePath,asset.Key));if(!path.StartsWith(basePath,StringComparison.OrdinalIgnoreCase)||!File.Exists(path)||Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))!=asset.Value!.GetValue<string>())throw new InvalidDataException("Required content is missing or changed: "+Path.GetFileName(asset.Key));}
  var data=PersistenceData(scenario);var restored=OriginalMission.Restore(data,snap);data=restored.World.Data;
  if(restored.AtOutcome)throw new InvalidDataException("This slot is not an active-play save");
  var cam=p["camera"]!.AsArray();if(cam.Count!=3)throw new InvalidDataException("Camera metadata");float x=cam[0]!.GetValue<float>(),y=cam[1]!.GetValue<float>(),z=cam[2]!.GetValue<float>();if(!float.IsFinite(x)||!float.IsFinite(y)||!float.IsFinite(z)||z<.2||z>6)throw new InvalidDataException("Camera values");
  var ids=p["selection"]!.AsArray().Select(n=>(Slot:n![0]!.GetValue<int>(),Incarnation:n[1]!.GetValue<int>())).ToArray();if(ids.Any(id=>id.Slot<0||id.Slot>=800))throw new InvalidDataException("Selection values");
  // Decode required presentation inputs while the old session is still intact.
  GameColors.ReadGifPalette(data.Read(data.Scenario.Header.PaletteName));new OriginalSoundRules(data.Read("SOUND/SLIST.DAT"),data.Read(AmbientPath(data)),1);
  if(OriginalCampaign.HasEconomy(data))OriginalInterface.Controls(data.Read("INTRFACE/MAINE"));
  foreach(var e in restored.World.Entities)foreach(var a in new[]{e.Body,e.Blood,e.Overlay})if(a.Mode!=2&&a.Pose(e.Angle) is {} pose)foreach(var c in pose.Cells)data.Sprite(c.Bank);
  CaptureCatalogInputs(i,p["assets"]!,data.DataRoot);
  return (data,restored,new(x,y,z),ids);
 }
 void LoadMissionSlot(int i){if(IsNetworkMatch)return;try{
  var save=ReadMissionSlot(i);InstallMission(save.Data,save.Mission,false);camera=new(save.Camera.X,save.Camera.Y);zoom=save.Camera.Z;ClampMissionCamera();foreach(var id in save.Selection)if(nativeWorld.Slots[id.Slot] is {Living:true} e&&e.Owner==LocalTeam&&e.Incarnation==id.Incarnation)nativeSelection.Add(id);
  foreach(var e in nativeWorld.Entities.Where(e=>e.Body.Mode!=2&&nativeWorld.Transports.TryGetValue(e.Slot,out var t)&&(t.Stage<2||e.Actions.Any(a=>a.Kind==22)))){var spatial=NativeSpatialSound.Transport(SoundCamera.X,SoundCamera.Z,e.X,e.Z,e.Height,false);if(spatial.Audible&&PlayMissionSound(e.Type==93?82:45,e.Slot,spatial) is {} voice)transportAudio[(e.Slot,e.Incarnation)]=voice;}
  persistenceNotice=$"Loaded slot {i+1}.";RefreshPersistenceCatalog();missionPanel="";
 }catch(Exception e){PersistenceError(e);}}
 void RecordCampaign(){if(IsWar){campaignWritePending=false;return;}if(godMode||godModeRuns.Contains(mission.RunId)){campaignWritePending=false;return;}try{if(CampaignProgress.Complete(CampaignPath,mission))persistenceNotice=IsTraining?"Training progress saved.":"Campaign progress saved.";campaignWritePending=false;}catch(Exception e){campaignWritePending=true;PersistenceError(e);}}
 bool PersistenceAction(string id){
  if(id.StartsWith("edition:")){SelectedCampaign=(id[8..]=="training"?"training-":id[8..]=="council"?"cw-":"")+(OriginalCampaign.IsHuman(SelectedCampaign)?"human":"taar");campaignScroll=0;RefreshPersistenceCatalog();focusedUi=id;return true;}
  if(id.StartsWith("faction:")){SelectedCampaign=(IsTraining?"training-":IsCouncil?"cw-":"")+id[8..];OriginalCampaign.SupportedCount(SelectedCampaign);campaignScroll=0;RefreshPersistenceCatalog();focusedUi=id;return true;}
  if(id=="load-selected"){LoadMissionSlot(surfaceSave);return true;}
  if(id=="error-close"){RefreshPersistenceCatalog();OpenMissionPanel(surfaceErrorReturn);persistenceNotice="";return true;}
  if(id is "save" or "load" or "campaign"){persistenceNotice="";overwriteSlot=-1;RefreshPersistenceCatalog();surfaceSave=Math.Max(0,continueSlot);OpenMissionPanel(id);return true;}
  if(id.StartsWith("save-slot:")){int i=int.Parse(id[10..]);if(File.Exists(SavePath(i))){overwriteSlot=i;OpenMissionPanel("overwrite");focusedUi="save";}else SaveMissionSlot(i);return true;}
  if(id=="overwrite-yes"){SaveMissionSlot(overwriteSlot);return true;}
  if(id.StartsWith("load-slot:")){LoadMissionSlot(int.Parse(id[10..]));return true;}
  if(id.StartsWith("campaign-start:")){try{int index=int.Parse(id[15..]);var target=OriginalCampaign.Playable(OriginalCampaign.Catalog(SelectedContentRoot,SelectedCampaign),index);var profile=CampaignProgress.Entry(CampaignPath,index,SelectedCampaign);if(index>0&&profile==null&&!godMode)throw new InvalidDataException("Complete the preceding campaign mission to unlock this mission");var data=PersistenceData(target.Scenario);InstallMission(data,new OriginalMission(data,nativeSeed,profile),true);}catch(Exception e){PersistenceError(e);}return true;}
  if(id=="retry-progress"){RecordCampaign();return true;}return false;
 }
 void DrawPersistencePanel(float x,float y,Rect2 r){
  if(missionPanel is "save" or "load"){
   UiWrap(missionPanel=="save"?"Save this paused battle. Gameplay resumes when you return.":mainMenu?"Choose a saved mission to continue. Your global options stay unchanged.":"Loading replaces unsaved play. Global options remain unchanged.",x,y,620,18,uiDim,2);y+=65;
   for(int i=0;i<4;i++){UiButton((missionPanel=="save"?"save-slot:":"load-slot:")+i,saveLabels[i]??$"Slot {i+1}",new(x,y,640,62),"Mission and date identify this slot.",missionPanel=="save"?CanSaveMission:saveExists[i]);UiLabel(saveDetails[i],x+14,y+51,14,uiDim);y+=72;}
  }else if(missionPanel=="overwrite"){UiWrap($"Replace slot {overwriteSlot+1}? The previous save is retained if writing fails.",x,y,620,22,uiText);UiButton("overwrite-yes","Overwrite",new(x,y+110,300,48),"Confirm replacement");UiButton("save","Cancel",new(x+320,y+110,300,48),"Keep existing save");
  }else{
   UiLabel(OriginalCampaign.Label(SelectedCampaign).ToUpperInvariant(),x,y+18,24,uiGreen);UiWrap("Choose an unlocked mission. Existing saves and earned progress are kept.",x,y+45,620,16,uiDim,2);y+=95;
   try{if(campaignReadFailed)throw new InvalidDataException();
    var entries=campaignEntries;
    foreach(var item in OriginalCampaign.Catalog(SelectedContentRoot,SelectedCampaign).Where(m=>m.Implemented)){
     bool available=godMode||item.Index==0||entries[item.Index]!=null;
     UiButton("campaign-start:"+item.Index,item.Title+ (available?" - available":" - locked"),new(x,y+item.Index*57,640,49),"Begin "+item.Title+" with earned campaign progress",available&&File.Exists(Path.Combine(SelectedContentRoot,item.Scenario)));
    }
   }catch{UiWrap("Campaign record cannot be read. Existing progress has been preserved.",x,y,620,20,uiText);}
  }
  UiWrap(persistenceNotice,x,r.End.Y-135,620,16,uiGreen,3);
 }
}
