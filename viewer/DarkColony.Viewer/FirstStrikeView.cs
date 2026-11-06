using Godot;
using DarkColony.Original;
using DarkColony.Assets;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace DarkColony.Viewer;

public partial class Viewer {
 bool fastGame=true;
 static string AmbientPath(OriginalDefinitions data)=>"SOUND/"+Path.GetFileNameWithoutExtension(data.Scenario.Header.TerrainName)+".AMB";
 bool economicMission,finalCampaignMission;OriginalCampaignMission campaignMission=null!;
 OriginalProfileContext? campaignEntry;string nextMissionNotice="";
 bool firstStrike,missionBriefing=true,missionNext,missionOutcomeShown,missionHarnessManual;OriginalMission mission=null!;
 OriginalMovie? missionVideo;OriginalAudioMixer.Voice? briefingVoice;
 string briefing="",outcomeText="",missionSuite="";double missionWall;int nativeSoundCursor;
 Dictionary<int,OriginalSound> missionSoundPaths=new();
 OriginalSoundRules missionSounds=null!;Dictionary<int,int> ambientTypes=null!;
 OriginalAudioMixer missionMixer=null!;AudioStreamPlayer? missionAudioPlayer;AudioStreamGeneratorPlayback? missionPlayback;
 readonly Dictionary<(int Slot,int Incarnation),OriginalAudioMixer.Voice> transportAudio=new();
 readonly List<object> missionAudioAudit=new();
 ulong missionHeartbeat;
 void PrepareMissionPresentation(string[] args) {
  if(IsWar){var map=WarCatalog.Find(nativeData.DataRoot,mission.War!.Configuration.MapId);campaignMission=new(-1,map.Scenario,map.Title,map.Terrain,"","","",Array.Empty<int>(),0,Array.Empty<int>());finalCampaignMission=false;briefing="";}else{
  var catalog=OriginalCampaign.For(nativeData);
  campaignMission=catalog.Single(m=>m.Scenario.Equals(nativeData.Scenario.Scenario,StringComparison.OrdinalIgnoreCase));finalCampaignMission=campaignMission.Index==catalog.Length-1;
  PrepareBriefingText();}
  if(economicMission)economicArt=OriginalInterface.Controls(nativeData.Read("INTRFACE/MAINE"));
  missionSuite=args.FirstOrDefault(a=>a.StartsWith("--mission-suite="))?[16..]??"smoke";
  missionSoundPaths=OriginalSound.Parse(nativeData.Read("SOUND/SOUND2.DAT"));
  if(!IsWar&&!IsTraining)missionSoundPaths[1000]=new(1000,$"MISSION/{(IsHuman?"H":"G")}{campaignMission.Index+1}.WAV",1,1,0,false); // Separate briefing media source, not a SOUND2 pool.
  ambientTypes=OriginalSoundRules.TerrainTypes(nativeData.Scenario.Map);
  PrepareMissionMusic();
  PrepareEdgeScrolling(args);
  fastGame=ArtworkPreference.LoadFast(artworkPreferencePath);GD.Print("GAME_SPEED "+(fastGame?"Fast 150%":"Classic 100%"));
  if(args.Contains("--music-trace"))BeginOrdinaryMusicTrace();
 }
 string ReadMissionText(string path){
  // The preserved training sources reference a base-loss result that is absent.
  // Explicit presentation fallback only; keep the authored bail/result identity.
  if(path.Equals("SCENARIO/TEST/HTRAIN6.004",StringComparison.OrdinalIgnoreCase)&&!File.Exists(Path.Combine(nativeData.DataRoot,path)))return "Your base was destroyed. Retry the lesson.\n\nThe original result text is unavailable in this content set.";
  return Regex.Replace(System.Text.Encoding.ASCII.GetString(nativeData.Read(path)),"~[0-9]","").Replace("\r","");
 }
 void ResetFirstStrike() {
  if(IsNetworkMatch)return;
  if(IsWar){StartWarMatch(mission.War!.Configuration);return;}
  campaignWritePending=false;persistenceNotice="";
  ResetMissionInterface();
  missionAudioPlayer?.Stop();
  ReleaseMissionVideo();briefingVoice=null;
  ResetMissionAudio();artifactAudio.Clear();diseaseAudio.Clear();transportAudio.Clear();nativeSoundCursor=0;missionAudioAudit.Clear();messageSoundCursor=0;rewardsVisible=false;rewardMilliseconds=0;
  mission=new(nativeData,nativeSeed,campaignEntry);nativeWorld=mission.World;ApplySessionPlaytesting();
  if(economicMission)developmentCatalog=nativeWorld.ProductionCatalog(LocalTeam);
  reinforcementScriptCursor=reinforcementWorldCursor=0;reinforcementTraceFailed=false;
  missionSounds=new(nativeData.Read("SOUND/SLIST.DAT"),nativeData.Read(AmbientPath(nativeData)),1); // Explicit presentation CRT seed.
  missionWall=0;nativeAccumulator=0;missionFault=null;failureLogger.FirstError=null;
  nativeSelection.Clear();nativeAttackMove=false;nativeOmniscient=false;missionBriefing=true;missionNext=false;missionOutcomeShown=false;paused=true;
  ResetMissionCamera();ClearNativeBodies();
  ResetBriefingPresentation();briefingVoice=PlayMissionSound(1000);
 }
 void StartFirstStrike(){StopBriefingTyping();missionMixer.Stop(briefingVoice);briefingVoice=null;ClearMissionAudioBuffer();missionMusic?.Start();GD.Print("MISSION_MUSIC_START track="+(missionMusic?.Track??0));missionBriefing=false;paused=false;missionWall=0;nativeAccumulator=0;QueueRedraw();}
 void ShutdownMissionMedia() {
  ReleasePointerBoundary();
  StopBriefingTyping();
  DisposeMissionInterface();
  EndOrdinaryMusicTrace();
  missionMusic?.Dispose();missionMusic=null;
  ClearNativeBodies();nativeBodyColors.Clear();miniTeamColors.Clear();
  ClearNativeMissiles();
  foreach(var t in nativeTerrain.Values){DisposeNativeTexture(t.Ground);DisposeNativeTexture(t.Foreground);}nativeTerrain.Clear();
  missionAudioPlayer?.Stop();
  ReleaseMissionVideo();
  missionMixer?.StopAll();artifactAudio.Clear();diseaseAudio.Clear();RetireMissionAudio(true);
  foreach(var texture in rewardTextures.Values)texture.Dispose();rewardTextures.Clear();
 }
 void ReleaseMissionVideo() {
  if(!GodotObject.IsInstanceValid(missionVideo)){missionVideo=null;return;}
  missionVideo!.Stop();missionVideo.QueueFree();missionVideo=null;
 }
 bool FirstStrikeInput(InputEvent input) {
  return MissionInterfaceInput(input);
 }
 static double AdmittedMissionTime(double delta,bool fast)=>Math.Clamp(delta,0,.1)*(fast?1.5:1);
 void ProcessFirstStrike(double delta) {
  SyncLocalPause();
  if(mainMenu){ui=Math.Min(ui,Math.Max(.75f,GetViewportRect().Size.Y/720));Input.MouseMode=Input.MouseModeEnum.Visible;QueueRedraw();return;}
  if(persistenceSkipDelta){delta=0;persistenceSkipDelta=false;}
  animationDelta=delta;
  long sectionStart=perfActive?System.Diagnostics.Stopwatch.GetTimestamp():0;
  double uiMs=0,worldMs=0,historyMs=0,musicMs=0;
  UpdateMissionInterface();
  ProcessBriefing(delta);
  ProcessKeyboardCamera(delta);ProcessEdgeScrolling(delta);
  if(perfActive){uiMs=PerfElapsed(sectionStart);sectionStart=System.Diagnostics.Stopwatch.GetTimestamp();}
  if(missionFault!=null)return;
  try {
  if(capturing){if(Time.GetTicksMsec()-missionHeartbeat>=5000){missionHeartbeat=Time.GetTicksMsec();if(missionSuite is "animation-fixtures" or "facing-fixtures" or "facing-observe")GD.Print($"ANIMATION_FIXTURE_HEARTBEAT tick={animationFixtureTick}");else GD.Print($"MISSION_HEARTBEAT tick={nativeWorld.Tick} clock={mission.Milliseconds} briefing={missionBriefing} outcome={mission.AtOutcome} manual={missionHarnessManual} paused={SinglePlayerPaused} session={mission.RunId}");}}
  if(!IsNetworkMatch&&!SinglePlayerPaused&&!missionHarnessManual&&!missionBriefing&&!mission.AtOutcome&&!missionNext) {
   missionWall+=delta;nativeAccumulator+=AdmittedMissionTime(delta,fastGame);
   // Authorized speed scales only admitted simulation time; preserve 66ms strict > step and residual phase.
   // Bound stalls to 100ms before scaling. UI pause admits no time. Messages/bail use simulation time.
   while(nativeAccumulator>.066&&!mission.AtOutcome){nativeAccumulator-=.066;interferenceRoute?.Update(nativeWorld);legacyRoute?.Update(nativeWorld);breakthroughRoute?.Update(nativeWorld);roswellRoute?.Update(nativeWorld);RememberNativePositions();mission.SetClock((nativeWorld.Tick+1)*66);long perfSimStart=perfActive?System.Diagnostics.Stopwatch.GetTimestamp():0;mission.Step();if(perfActive)perfSimulationMs+=PerfElapsed(perfSimStart);DispatchMissionSounds();}
   DispatchMissionSounds();DispatchAmbient();DispatchMessageSound();
  }
  if(perfActive){worldMs=PerfElapsed(sectionStart);sectionStart=System.Diagnostics.Stopwatch.GetTimestamp();}
  TraceMissionReinforcements();TrimPresentationHistory();
  if(!SinglePlayerPaused&&mission.AtOutcome&&!missionOutcomeShown){missionOutcomeShown=true;if(IsWar)BeginWarResults();else{RecordCampaign();outcomeText=ReadMissionText(mission.Outcome!.TextPath);PlayOutcomeMedia();}}
  if(rewardsVisible&&!IsWar){rewardMilliseconds+=delta*1000;if(mission.Outcome!.Success)missionRewardAnimation.Step(rewardMilliseconds,missionRewards!.Medal(IsHuman?0:6).Cells.Length);}
  if(perfActive){historyMs=PerfElapsed(sectionStart);sectionStart=System.Diagnostics.Stopwatch.GetTimestamp();}
  try{missionMusic?.Poll((long)Time.GetTicksMsec());}catch(IOException error){UnavailableMissionMusic(error);}
  if(perfActive){musicMs=PerfElapsed(sectionStart);sectionStart=System.Diagnostics.Stopwatch.GetTimestamp();}
  PumpMissionAudio();TraceOrdinaryMusic(delta);
  if(perfActive){double audioMs=PerfElapsed(sectionStart);if(uiMs+worldMs+historyMs+musicMs+audioMs>25)GD.Print(FormattableString.Invariant($"PERF_SLOW_PROCESS tick={nativeWorld.Tick} ui={uiMs:F3} worldIncludingSound={worldMs:F3} simulation={perfSimulationMs:F3} history={historyMs:F3} music={musicMs:F3} audioPump={audioMs:F3}"));}
  QueueRedraw();
  }catch(Exception error){HaltFirstStrike(error);}
 }
 void PlayOutcomeMedia() {
  missionMusic?.Stop();
  missionMixer.StopAll();missionAudioPlayer?.Stop();
  string name=Path.GetFileNameWithoutExtension(mission.Outcome!.Success?campaignMission.VictoryMovie:campaignMission.DefeatMovie).ToLowerInvariant(),path=Path.Combine(root,IsCouncil?"generated/council-wars-first/video":"generated/original-video/v1",name+".odcv");
  if(IsTraining&&name=="pp"&&!File.Exists(path)){BeginRewards();return;} //Native401028 skips absent placeholder; no fabricated clip.
  if(!File.Exists(path)){nativePresentationGaps.Add("Original movie missing: tools/prepare_original_video.py");BeginRewards();return;}
  ReleaseMissionVideo();
  missionVideo=new(){Size=GetViewportRect().Size};
  missionVideo.Load(path);
  missionVideo.VolumeDb=sfxVolume<=0?-80:-8+20*MathF.Log10(sfxVolume);
  AddChild(missionVideo);missionVideo.Finished+=()=>{ReleaseMissionVideo();BeginRewards();};missionVideo.Play();
  missionVideo.Failed+=error=>{ReleaseMissionVideo();BeginRewards();};
  GD.Print("MOVIE_PLAY "+name+" Original");
  missionAudioAudit.Add(new{nativeWorld.Tick,media="AVI/"+name.ToUpperInvariant()+".AVI",adapter=missionVideo.Streaming?"lossless indexed RGB/PCM ODCV0002":"lossless RGB/PCM ODCV0001"});
 }
 OriginalAudioMixer.Voice? PlayMissionSound(int id,int slot=-1,NativeSpatialSound? spatial=null) {
  if(!missionSoundPaths.TryGetValue(id,out var source))return null;
  var voice=missionMixer.Play(id,spatial?.Volume??1,spatial?.Pan);
  missionAudioAudit.Add(new{nativeWorld.Tick,id,source.Path,source.Loop,slot,copy=voice?.Copy??-1,volume=voice?.Volume,pan=voice?.Pan});return voice;
 }
 (int X,int Z) SoundCamera=>((int)(camera.X*8),(int)((nativeData.Scenario.Map.Height*32-camera.Y)*8));
 void SoundList(int id,string kind,NativeAudioCue ev) {
  var cell=new Cell(ev.X>>8,ev.Z>>8);
  if(!nativeWorld.Navigation.Inside(cell)||!nativeWorld.Sight.Explored[LocalTeam][cell.Z*nativeData.Scenario.Map.Width+cell.X])return; // 4320f8 explored bit, not current visibility.
  var c=SoundCamera;var spatial=NativeSpatialSound.Immediate(c.X,c.Z,ev.X,ev.Z);
  if(spatial.Audible&&missionSounds.Immediate(id,kind) is int sound)PlayMissionSound(sound,ev.Slot,spatial);
 }
 void QueueMissionVoice(string kind) {
  if(!firstStrike&&!interferenceDev)return;
  foreach(var e in NativeSelected().OrderBy(e=>e.Slot))missionSounds.Queue(e.Type,kind);
 }
 void DispatchAmbient() {
  var map=nativeData.Scenario.Map;var area=NativeArea;
  var low=NativeCell((new Vector2(area.Position.X,area.End.Y)-NativeOffset)/zoom);
  var high=NativeCell((new Vector2(area.End.X,area.Position.Y)-NativeOffset)/zoom);
  var types=new List<int>();
  for(int z=Math.Max(0,low.Z);z<=Math.Min(map.Height-1,high.Z);z++)for(int x=Math.Max(0,low.X);x<=Math.Min(map.Width-1,high.X);x++)if(nativeWorld.Visible(LocalTeam,new(x,z))) {
   var cell=map.At(x,z);types.Add(ambientTypes[cell.ForegroundId!=0?cell.ForegroundId:cell.BackgroundId]);
  }
  if(missionSounds.Ambient((long)Time.GetTicksMsec(),nativeWorld.Phase,OriginalSoundRules.DominantTerrain(types)) is int sound)PlayMissionSound(sound);
 }
 void DispatchMissionSounds() {
  foreach(var ev in nativeWorld.AudioCues.Skip(nativeSoundCursor)) {
   if(ev.Kind=="launch")SoundList(nativeData.Weapons[ev.Value].Sound,"GUN",ev);
   if(ev.Kind=="death")SoundList(ev.Type,"DEA",ev);
   if(ev.Kind=="artifact-found"&&missionSounds.Immediate(7,"XTR") is int found)PlayMissionSound(found,ev.Slot);
   if(ev.Kind=="artifact-activate")SoundList(ev.Type,"DPY",ev);
   if(ev.Kind is "inspire-start" or "heal")SoundList(ev.Type,"DPY",ev);
   if(ev.Kind=="vent-eruption"&&missionSounds.Immediate(1,"XTR") is int erupt)PlayMissionSound(erupt,ev.Slot);
   if(ev.Kind=="capture"&&missionSounds.Immediate(3,"XTR") is int captured)PlayMissionSound(captured,ev.Slot); //41425e..67, nonspatial/unconditional.
   var key=(ev.Slot,ev.Incarnation);var c=SoundCamera;
   if(ev.Kind=="transport-created") {var spatial=NativeSpatialSound.Transport(c.X,c.Z,ev.X,ev.Z,ev.Height,true);if(spatial.Audible&&PlayMissionSound(ev.Type==93?82:45,ev.Slot,spatial) is {} voice)transportAudio[key]=voice;}
   if(ev.Kind=="transport-volume"&&transportAudio.TryGetValue(key,out var moving)&&moving.Playing) {var spatial=NativeSpatialSound.Transport(c.X,c.Z,ev.X,ev.Z,ev.Height,false);missionMixer.Set(moving,spatial.Volume,spatial.Pan);}
   if(ev.Kind=="transport-sound-stop"&&transportAudio.Remove(key,out var loop))missionMixer.Stop(loop);
  }
  SyncArtifactAudio();SyncDiseaseAudio();nativeSoundCursor=nativeWorld.AudioCues.Count;foreach(int sound in missionSounds.Flush())PlayMissionSound(sound);
 }
 void Wrapped(string text,float x,float y,float width,int fontSize,Color color) {
  float lineHeight=fontSize*1.45f;foreach(var paragraph in text.Split('\n')) {
   string line="";foreach(var word in paragraph.Split(' ',StringSplitOptions.RemoveEmptyEntries)) {
    string next=line.Length==0?word:line+" "+word;if(font.GetStringSize(next,fontSize:fontSize).X>width&&line.Length>0){Text(line,new(x,y),fontSize,color);y+=lineHeight;line=word;}else line=next;
   }
   Text(line,new(x,y),fontSize,color);y+=lineHeight;
  }
 }
 void DrawFirstStrike() {
  if(missionBriefing||mission.AtOutcome||missionFault!=null||missionNext)DrawMissionFrontend();else if(economicMission)DrawDevelopment();else DrawMissionHud();
 }
 void ContinueCampaign(){
  if(mission.Outcome is not {Success:true}||mission.ResultProfile==null)return;
  RecordCampaign();
  if(campaignWritePending)return; // Keep the result available for retry instead of discarding it.
  int next=mission.ResultProfile.CampaignIndex;var catalog=OriginalCampaign.For(nativeData);
  if(next==catalog.Length&&mission.CampaignIndex==catalog.Length-1){bool training=IsTraining;ReturnMainMenu();if(training)PersistenceAction("campaign");persistenceNotice=OriginalCampaign.Label(SelectedCampaign)+(training?" complete.":" campaign complete.");return;}
  var target=next<catalog.Length?catalog[next]:null;
  if(IsCouncil&&target is {Implemented:false}){ReturnMainMenu();PersistenceAction("campaign");persistenceNotice="Mission complete. Further Council Wars missions are not yet available.";return;}
  if(target is not {Implemented:true}||!File.Exists(Path.Combine(nativeData.DataRoot,target.Scenario))){
   missionNext=true;nextMissionNotice=(target?.Title??"The next mission")+" is not available in this build. "+
    (godMode||godModeRuns.Contains(mission.RunId)?"Session playtesting does not write campaign progress.":"Your completed mission result is retained in campaign progress (check any storage error above).");return;
  }
  var result=mission.ResultProfile.Copy();
  try{result=CampaignProgress.Entry(CampaignPath,next,mission.CampaignId)??result;}catch(Exception e){PersistenceError(e);}
  var data=PersistenceData(target.Scenario);InstallMission(data,new OriginalMission(data,nativeSeed,result),true);
 }
 async Task MissionCapture(string name) {
  await NativeCapture(name);File.WriteAllText(Path.Combine(output,name+"-mission.json"),JsonSerializer.Serialize(new{mission.World.Tick,mission.Milliseconds,mission.Outcome,mission.Events,mission.Messages,audio=missionAudioAudit},new JsonSerializerOptions{WriteIndented=true}));
 }
 public async void FirstStrikeSuite() {
  try {
   if(missionSuite=="council-direct"){await VerifyCouncilDirect();await FinishFirstStrike(0);return;}
   if(missionSuite=="war"){await VerifyWarInterface();await FinishFirstStrike(0);return;}
   AudioEffectRecord? smokeAudio=null;
   if(missionSuite=="smoke"&&campaignMission.Index==0){smokeAudio=new(){Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,smokeAudio);smokeAudio.SetRecordingActive(true);await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);}
   await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await MissionCapture("00-briefing");
   if(missionSuite=="repair-polish"){await VerifyRepairPolish();await FinishFirstStrike(0);return;}
   if(missionSuite=="briefing-input"){await VerifyBriefingInput();await FinishFirstStrike(0);return;}
   if(missionSuite=="briefing-followup"){await VerifyBriefingFollowup();await FinishFirstStrike(0);return;}
   if(missionSuite=="human"){await VerifyHumanMission();File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));GD.Print("HUMAN_SUITE_PASS");await FinishFirstStrike(0);return;}
   if(missionSuite=="persistence-load"){await VerifyPersistenceUi(true);await FinishFirstStrike(0);return;}
   await Send(Key.Enter);
   if(missionSuite=="fault-restart") {nativeWorld.Slots[152]!.Actions.Clear();nativeWorld.Slots[152]!.Actions.Add(new(999));return;} // Deliberately failing containment fixture.
   if(missionSuite=="engine-error") {GD.PushError("INTENTIONAL_ENGINE_ERROR_CONTAINMENT_FIXTURE");return;}
   if(missionSuite=="input-map") {await SelectWholeSquad();GD.Print("INPUT_SELECTION "+string.Join(',',NativeSelected().Select(e=>e.Slot)));await MissionCapture("input-map");await FinishFirstStrike(0);return;}
   if(missionSuite=="audio-replacement")await VerifyAudioReplacement();
   else if(missionSuite=="performance-menu"){await VerifyMenuPerformance();
   }else if(missionSuite=="shared-art"){await VerifySharedArtwork();
   }else if(missionSuite=="deployment-queue"){await VerifyDeploymentQueue();
   }else if(missionSuite is "video-lifecycle" or "video-failure"){await VerifyOriginalVideoLifecycle();
   }else if(missionSuite=="persistence-temporal"){await VerifyPersistenceTemporal();
   }else if(missionSuite=="persistence-save"){await VerifyPersistenceUi(false);
   }else if(missionSuite=="commands"){await VerifyUnitCommands();
   }else if(missionSuite=="construction-ground"){await VerifyCityGroundOcclusion();
   }else if(missionSuite=="construction-occlusion"){await VerifyConstructionOcclusion();
   }else if(missionSuite=="brozaar-preserved"){await VerifyPreservedBrozaar();}else if(missionSuite=="brozaar-deployment"){await VerifyBrozaarDeployment();}else if(missionSuite=="viewport-culling"){await VerifyNativeViewportCulling();
   }else if(missionSuite=="human-ending"){await VerifyHumanEnding();
   }else if(missionSuite=="final-ending"){await VerifyFinalEnding();
   }else if(campaignMission.Index>=9){await VerifyLateTaarMission();
   }else if(campaignMission.Index==8){if(missionSuite=="buried-temporal")await VerifyBuriedTemporal();else await VerifyBuriedMission();
   }else if(campaignMission.Index==7){if(missionSuite=="screamer-temporal")await VerifyScreamerTemporal();else await VerifyScreamerMission();
   }else if(campaignMission.Index==6){if(missionSuite=="civil-war-temporal")await VerifyCivilWarTemporal();else await VerifyCivilWarMission();
   }else if(campaignMission.Index==5){await VerifyPuppetMission();
   }else if(campaignMission.Index==4){if(missionSuite=="success")await RunRoswellMission();else await VerifyRoswellMission();
   }else if(campaignMission.Index==3){if(missionSuite is "success" or "full-route")await RunBreakthroughMission();else await VerifyBreakthroughMission();
   }else if(campaignMission.Index==2){if(missionSuite=="success")await RunLegacyMission();else if(missionSuite=="visual")await VerifyLegacyPalette();else await VerifyLegacyMission();
   }else if(economicMission){if(missionSuite=="success")await RunInterferenceMission();else await VerifyInterferenceMission();
   }else if(missionSuite=="campaign") {
    await VerifyCampaignTransition();
   }else if(missionSuite is "qol" or "qol-visual") {
    await VerifyQol();
   }else if(missionSuite is "reinforcement" or "qol-transport") {
    await VerifyReinforcementRoute();
   }else if(missionSuite is "edge-scrolling" or "edge-reload") {
    await VerifyEdgeScrolling();
   }else if(missionSuite is "rendering-settings" or "rendering-settings-reload") {
    await VerifyArtworkSettings();
   }else if(missionSuite=="rendering-sampling") {
    await VerifyRenderingSampling();
   }else if(missionSuite=="rendering") {
    await VerifyRenderingPresentation();
   }else if(missionSuite=="ui") {
    await VerifyMissionInterface();
   }else if(missionSuite is "facing-fixtures" or "facing-observe") {
    await VerifyFacingFixtures();
   }else if(missionSuite=="animation-fixtures") {
    await VerifyAnimationFixtures();
   }else if(missionSuite=="animation-repro") {
    await VerifyAnimationReproduction();
   }else if(missionSuite=="visual") {
    await VerifyVisualPresentation();
   }else if(missionSuite=="rewards") {
    await VerifyRewardPresentation();
   }else if(missionSuite=="music") {
    await VerifyMusicPresentation();
   }else if(missionSuite=="audio") {
    await VerifyAudioPresentation();
   }else if(missionSuite is "reproduce" or "varied") {
    await VerifyMixedAttack();
   }else if(missionSuite=="reset") {
    await VerifyMissionResets();
   }else if(missionSuite=="smoke") {
    await ToSignal(GetTree().CreateTimer(13),SceneTreeTimer.SignalName.Timeout);Check(nativeWorld.Entities.Any(e=>e.Type==73&&e.Owner==LocalTeam),"opening Gray unload in real time");
    await Send(Key.Tab);await Send(Key.S);await Send(Key.I);await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);await MissionCapture("01-opening");
    smokeAudio!.SetRecordingActive(false);smokeAudio.GetRecording().SaveToWav(Path.Combine(output,"smoke-mixer.wav"));await Send(Key.R);Check(nativeWorld.Tick==0&&mission.Messages.Count==0,"restart clears opening and queue");await MissionCapture("02-restart");
   }else await RunFirstStrikeSession(missionSuite=="success");
   Check(missionFaultCount==0&&failureLogger.FirstError==null,"no simulation or engine faults during entire run");
   File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));GD.Print("FIRST_STRIKE_SUITE_PASS "+missionSuite);await FinishFirstStrike(missionFaultCount==0&&failureLogger.FirstError==null?0:1);
  }catch(Exception error){if(missionFault!=null)return;File.WriteAllText(Path.Combine(output,"failure.txt"),error.ToString());GD.PushError(error.ToString());await FinishFirstStrike(1);}
 }
 async Task SelectWholeSquad() {
  await Send(Key.F);
  Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=NativeWindowPoint(NativeArea.Position+new Vector2(2,2)),Pressed=true});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=NativeWindowPoint(NativeArea.End-new Vector2(2,2)),Pressed=false});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 }
 async Task WaitMissionTick(long tick) {
  while(nativeWorld.Tick<tick) {if(missionFault!=null)throw new InvalidOperationException("Mission failed during input verification");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
 }
 async Task ClickNativeCell(MouseButton button,Cell cell)=>await NativeMouse(button,new Vector2(cell.X*32+16,(nativeData.Scenario.Map.Height-1-cell.Z)*32+16)*zoom+NativeOffset);
 async Task VerifyMixedAttack() {
  await WaitMissionTick(180);await SelectWholeSquad();Check(NativeSelected().Any(e=>e.Type==89),"stationary CENT included in mixed selection");
  await Send(Key.A);await ClickNativeCell(MouseButton.Left,new(43,29));
  NativeEntity? target=null;
  while(target==null&&nativeWorld.Tick<700){await WaitMissionTick(nativeWorld.Tick+1);target=nativeWorld.Entities.FirstOrDefault(e=>e.Living&&e.Owner==1&&nativeWorld.Visible(LocalTeam,e.Cell));}
  Check(target!=null,"enemy appears through autonomous encounter");await NativeMouse(MouseButton.Right,NativePosition(target!)*zoom+NativeOffset);
  GD.Print($"MIXED_EXPLICIT_ATTACK tick={nativeWorld.Tick} target={target!.Slot}");
  await WaitMissionTick(nativeWorld.Tick+45);Check(nativeWorld.Slots[152]!.Cell==new Cell(23,25),"CENT stays at source cell after explicit attack");
  await MissionCapture("mixed-explicit-attack");
  if(missionSuite=="varied") {
   foreach(var cell in new[]{new Cell(23,25),new Cell(44,32),new Cell(43,29),new Cell(0,0),new Cell(22,31)}) {
    await Send(Key.S);await ClickNativeCell(MouseButton.Right,cell);await Send(Key.A);await ClickNativeCell(MouseButton.Left,new(43,29));await Send(Key.S);await ClickNativeCell(MouseButton.Right,new(44,32));
    await WaitMissionTick(nativeWorld.Tick+90);
   }
   await WaitMissionTick(nativeWorld.Tick+180);Check(nativeWorld.Events.Any(e=>e.Kind=="launch"),"varied commands continue into combat");
   await MissionCapture("varied-progress");await Send(Key.R);Check(nativeWorld.Tick==0&&mission.Milliseconds==0&&missionWall==0,"restart clears both clocks");
   await Send(Key.Enter);await WaitMissionTick(180);await MissionCapture("varied-restarted");
  }
 }
 async Task VerifyMissionResets() {
  // Accelerated integrated state, Godot commands; explicitly not a normal-time mission run.
  missionHarnessManual=true;
  while(nativeWorld.Tick<16){mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();}
  Check(nativeWorld.Transports.Count==1&&nativeWorld.Slots[7]!.Height==8700,"active descent before reset");
  await MissionCapture("10-active-transport");await Send(Key.R);
  Check(nativeWorld.Tick==0&&nativeWorld.Transports.Count==0&&nativeWorld.Events.Count==0,"Godot restart clears active transport and events");
  await Send(Key.Enter);
  while(nativeWorld.Tick<3000&&!nativeWorld.Events.Any(e=>e.Kind=="launch")) {
   mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();
   if(nativeWorld.Tick>=180&&nativeWorld.Tick%60==0) {
    await Send(Key.Tab);await Send(Key.F);var destination=nativeWorld.Tick%120==0?new Cell(63,42):new Cell(67,45);
    await NativeMouse(MouseButton.Right,new Vector2(destination.X*32+16,(nativeData.Scenario.Map.Height-1-destination.Z)*32+16)*zoom+NativeOffset);
   }
  }
  Check(nativeWorld.Missiles.Count>0,"active combat pending missile before reset");DispatchMissionSounds();await Send(Key.Tab);zoom=2;await MissionCapture("11-active-combat");
  await Send(Key.R);Check(nativeWorld.Tick==0&&nativeWorld.Missiles.Count==0&&mission.Messages.Count==0&&mission.Outcome==null,"Godot restart clears combat, missiles and queues");
  Check(nativeWorld.Entities.Count()==46&&nativeWorld.Entities.All(e=>e.PendingOrder==null),"restart restores actual initial state");
  await MissionCapture("12-restarted");missionHarnessManual=false;
 }
 async Task RunFirstStrikeSession(bool success) {
  // Verification commands only. Production mission behavior never reads this route.
  var route=new[]{new Cell(28,20),new Cell(22,31),new Cell(43,29),new Cell(44,32),new Cell(35,48),new Cell(47,52),new Cell(44,54),new Cell(47,66),new Cell(47,70),new Cell(43,68),new Cell(7,47),new Cell(5,45),new Cell(70,33),new Cell(80,55),new Cell(82,53)};
  int goal=0;long handled=0,lastCapture=0;
  var audioRecord=new AudioEffectRecord{Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,audioRecord);audioRecord.SetRecordingActive(true);
  while(!mission.AtOutcome&&nativeWorld.Tick<30000) {
   await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   int commandCadence=success?120:60;
   if(nativeWorld.Tick>=180&&nativeWorld.Tick/commandCadence>handled/commandCadence) {
    handled=nativeWorld.Tick;await Send(Key.Tab);
    if(success) {
     if(NativeSelected().Any(e=>e.Energy>=32))await Send(Key.I);
     if(handled%600<16||handled<260) {
      var destination=route[Math.Min(goal,route.Length-1)];
      if(nativeWorld.Entities.Any(e=>e.Living&&e.Owner==LocalTeam&&e.Type is 8 or 73&&Math.Abs(e.Cell.X-destination.X)+Math.Abs(e.Cell.Z-destination.Z)<4)&&goal<route.Length-1)destination=route[++goal];
      await Send(Key.F);
      Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=NativeWindowPoint(NativeArea.Position+new Vector2(2,2)),Pressed=true});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
      Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=NativeWindowPoint(NativeArea.End-new Vector2(2,2)),Pressed=false});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
      Check(NativeSelected().Count(e=>e.Type is 8 or 73)>=2,"Godot drag selects squad");
      await Send(Key.A);await NativeMouse(MouseButton.Left,new Vector2(destination.X*32+16,(nativeData.Scenario.Map.Height-1-destination.Z)*32+16)*zoom+NativeOffset);
     }
    }else {
     var destination=handled%120<16?new Cell(63,42):new Cell(67,45);await Send(Key.F);await NativeMouse(MouseButton.Right,new Vector2(destination.X*32+16,(nativeData.Scenario.Map.Height-1-destination.Z)*32+16)*zoom+NativeOffset);
    }
   }
   if(nativeWorld.Tick-lastCapture>=1500) {lastCapture=nativeWorld.Tick;await Send(Key.Tab);zoom=2;await MissionCapture($"session-{nativeWorld.Tick:D5}");GD.Print($"MISSION_PROGRESS success={success} tick={nativeWorld.Tick} SALA={nativeWorld.Deaths[1,82]} commander={nativeWorld.Deaths[0,73]} goal={goal}");}
  }
  Check(mission.AtOutcome&&mission.Outcome!.Success==success,success?"full combat success":"full combat failure");
  if(success)Check(nativeWorld.Deaths[1,82]==11,"eleven SALA deaths from integrated combat");else Check(nativeWorld.Deaths[0,73]==1,"commander lethal damage through integrated combat");
  await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);await MissionCapture(success?"89-success-media":"89-failure-media");
  await ToSignal(GetTree().CreateTimer(12),SceneTreeTimer.SignalName.Timeout);await MissionCapture(success?"90-success":"90-failure");audioRecord.SetRecordingActive(false);var recording=audioRecord.GetRecording();recording.SaveToWav(Path.Combine(output,"session-mixer.wav"));
  File.WriteAllText(Path.Combine(output,"session-proof.json"),JsonSerializer.Serialize(new{mode="normal time, Godot-dispatched input",nativeWorld.Tick,mission.Milliseconds,inputEvents,mission.Outcome,events=mission.Events,nativeEvents=nativeWorld.Events,audio=missionAudioAudit},new JsonSerializerOptions{WriteIndented=true}));
  await Send(Key.R);Check(nativeWorld.Tick==0&&nativeWorld.Missiles.Count==0&&mission.Outcome==null,"restart after outcome");await MissionCapture("99-restart");
 }
}
