using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 void HumanResult(int number,bool independentRun=false){
  var d=PersistenceData($"SCENARIO/HUMAN/HUMAN{number:00}.SCN");
  var s=SaveCodec.Open(Path.Combine(root,$"generated/{(number==9?"h09-war":number<=5?"human15":"human615")}/h{number:00}/controlled-success.json"))["payload"]!.AsObject();
  // A second controlled playthrough needs its own identity; reusing the earlier
  // God-mode run must (correctly) remain ineligible for permanent credit.
  if(independentRun)s["runId"]=Guid.NewGuid().ToString("N");
  InstallMission(d,OriginalMission.Restore(d,s),false);
 }
 async Task VerifyLateHumanEffects(int number){
  var fixture=number switch{
   7=>("h07-artifacts/human-excavated-save.json",63,"solaris"),
   8=>("h08-kaox/kaox-activating-save.json",66,""),
   10=>("h10-siphon/sarge-deploying-save.json",4,""),
   12=>("h12-artifacts/artifact-activating-save.json",64,""),
   15=>("h15-esgaard-valid/esgaard-projectile-save.json",68,""),
   _=>("",0,"")};
  if(fixture.Item1.Length==0)return;
  var s=SaveCodec.Open(Path.Combine(root,"generated/human615",fixture.Item1))["payload"]!.AsObject();
  InstallMission(nativeData,OriginalMission.Restore(nativeData,s),false);
  var unit=nativeWorld.Entities.First(e=>e.Owner==0&&e.Type==fixture.Item2);
  camera=NativePosition(unit);ClampMissionCamera();nativeSelection.Clear();nativeSelection.Add((unit.Slot,unit.Incarnation));
  if(fixture.Item3.Length>0){await UiCapture("late-ability-ready");Check(UnitEnabled(fixture.Item3),"Native artifact command available");await SessionClick(fixture.Item3);}
  for(int f=0;f<16;f++){await UiCapture($"late-effect-{f:00}");AdvanceInterference(number==15?2:6);}
  Check(missionFault==null,"Late Human effect temporal continuation");
 }
 async Task VerifyLateHumanCampaign(){
  Check(!godMode,"Human late session starts God OFF");SelectedCampaign="human";RefreshPersistenceCatalog();
  string human=CampaignPath,taar=Path.Combine(PersistenceRoot,"campaign-v1.json");
  if(missionSuite is "human-late-reset" or "human-reset"){
   Check(File.ReadAllText(taar)==File.ReadAllText(Path.Combine(PersistenceRoot,"retained-taar.txt")),"Retained legacy Taar progress unchanged after restart");
   Check(CampaignProgress.Read(human,"human")["completed"]!["14"]!=null,"Human ending completion survives process restart");
   OpenMissionPanel("campaign");campaignScroll=14;await MenuCapture("human-complete-restart");
   Check(missionHits.Any(h=>h.Id=="campaign-start:14"&&h.Enabled)&&!missionHits.Any(h=>h.Id=="campaign-start:15"),"Final replay available; no mission16");return;
  }
  var td=PersistenceData("SCENARIO/ALIEN/ALIEN15.SCN");var ts=SaveCodec.Open(Path.Combine(root,"generated/final-evacuation/mission-final/success.json"))["payload"]!.AsObject();
  CampaignProgress.Complete(taar,OriginalMission.Restore(td,ts));
  var legacy=SaveCodec.Open(taar)["payload"]!.AsObject();foreach(var record in legacy["completed"]!.AsObject())foreach(string field in new[]{"entry","profile"})record.Value![field]!.AsObject().Remove("CampaignId");SaveCodec.Write(taar,legacy);
  string retained=File.ReadAllText(taar);File.WriteAllText(Path.Combine(PersistenceRoot,"retained-taar.txt"),retained);
  OpenMissionPanel("campaign");await MenuCapture("human-normal-locked");
  Check(missionHits.Count(h=>h.Id.StartsWith("campaign-start:")&&h.Enabled)==1,"Human starts at mission1 only");CloseRelayPanel();
  await SessionClick("options");await SessionClick("playtesting");await SessionClick("god-mode");CloseRelayPanel();CloseRelayPanel();
  foreach(int n in new[]{6,7,8,9,10,11,12,13,14,15}){
   OpenMissionPanel("campaign");campaignScroll=Array.FindIndex(OriginalCampaign.Catalog(nativeData.DataRoot,"human").Where(m=>m.Implemented).ToArray(),m=>m.Index==n-1);await MenuCapture("god-entry-"+n);await SessionClick("campaign-start:"+(n-1));
   Check(IsHuman&&nativeWorld.GodMode&&mission!.CampaignIndex==n-1,"Temporary Human entry "+n);ReturnMainMenu();
  }
  foreach(float scale in new[]{.75f,1f,1.5f}){ui=scale;OpenMissionPanel("campaign");campaignScroll=0;await MenuCapture("human-list-top-"+scale);RelayLayout("Human catalog top");await Send(Key.Pagedown);await Send(Key.Pagedown);await MenuCapture("human-list-bottom-"+scale);RelayLayout("Human catalog bottom");Check(missionHits.Any(h=>h.Id=="campaign-start:14"&&h.Enabled),"Final Human row reachable at scale "+scale);CloseRelayPanel();}ui=1;
  Check(OriginalCampaign.Implemented("human",8),"Corrected H09 included in temporary access");
  var testData=PersistenceData("SCENARIO/HUMAN/HUMAN15.SCN");InstallMission(testData,new(testData,0),false);AdvanceInterference(240);
  var hit=typeof(OriginalWorld).GetMethod("ApplyDamage",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
  var enemy=nativeWorld.Entities.First(e=>e.Living&&e.Owner==1);
  foreach(var target in new[]{nativeWorld.Entities.First(e=>e.Living&&e.Owner==0&&e.Type==68),nativeWorld.Teams[0].Cities[0].Entity!}){int hp=target.Health;hit.Invoke(nativeWorld,new object[]{enemy,target,100});Check(target.Health==hp,"God protects Human unit and building");}
  HumanResult(15);godMode=false;ApplySessionPlaytesting();RecordCampaign();Check(!File.Exists(human),"God final outcome writes no Human progress");ReturnMainMenu();godMode=false;ApplySessionPlaytesting();
  // Controlled completion fixtures test route/storage, never an ordinary campaign claim.
  foreach(int n in new[]{1,2,3,4,5,6,7,8,9,10,11,12,13,14,15}){
   HumanResult(n,true);RecordCampaign();ReleaseMissionVideo();ContinueCampaign();
   if(n==15)Check(mainMenu&&mission==null&&studioIntro==null,"Final Human flow ends in menu without startup replay");
   else Check(IsHuman&&mission!.CampaignIndex==n&&missionBriefing&&!godMode,"Earned original-order continuation "+n);
   ReturnMainMenu();
  }
  Check(CampaignProgress.Read(human,"human")["completed"]!["14"]!=null,"Independent God-OFF final result persisted");
  Check(File.ReadAllText(taar)==retained,"Human records preserve Taar progress byte-for-byte");
  SelectedCampaign="human";RefreshPersistenceCatalog();OpenMissionPanel("campaign");campaignScroll=14;await MenuCapture("human-final-earned");
  await SessionClick("campaign-start:14");await Send(Key.Space);await Send(Key.Enter);AdvanceInterference(240);SaveMissionSlot(0);CloseRelayPanel();ReturnMainMenu();
  await SessionClick("resume-save");Check(IsHuman&&mission!.CampaignIndex==14,"Continue routes late Human save");
  ReturnMainMenu();OpenMissionPanel("campaign");await SessionClick("faction:taar");Check(taarCampaignComplete,"Taar completion retained across faction changes");await MenuCapture("taar-retained");
  await SessionClick("campaign-start:0");await Send(Key.Space);await Send(Key.Enter);AdvanceInterference(160);SaveMissionSlot(1);CloseRelayPanel();
  LoadMissionSlot(0);Check(IsHuman&&mission!.CampaignIndex==14,"Return from Taar to late Human save");LoadMissionSlot(1);Check(!IsHuman&&mission!.CampaignIndex==0,"Taar save still loads after Human continuation");
  ReturnMainMenu();OpenMissionPanel("load");RefreshPersistenceCatalog();await MenuCapture("mixed-campaign-saves");Check(saveCampaigns[0]=="Human"&&saveCampaigns[1]=="Taar","Save metadata retains both campaign identities");CloseRelayPanel();
  godMode=true;ApplySessionPlaytesting();await MenuCapture("god-on-before-process-restart");
 }
 async Task VerifyHumanEnding(){
  missionHarnessManual=true;HumanResult(15);await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
  Check(missionVideo is {Streaming:true,Enhanced:false}&&missionVideo.RetainedVideoBytes<2000000,"Human original ending uses bounded buffers");
  foreach(int aspect in new[]{0,16,21}){maximumAspect=aspect;ApplyAspectLimit();await UiCapture("human-ending-aspect-"+aspect);Check(GetViewportRect().Encloses(missionVideo!.GetGlobalRect()),"Human movie active viewport bounds");}
  await Send(Key.Escape);await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);
  Check(rewardsVisible&&missionVideo==null&&IndexedMovieFrames.ActiveWorkers==0,"Human skip closes decoder and enters results");
  Check(resultFinal&&resultContinueAvailable&&focusedUi=="continue","Human final default completion action");await UiCapture("human-final-skipped");
  ContinueCampaign();Check(mainMenu&&studioIntro==null&&!missionNext,"Human ending returns to menu without mission16 or intro");
  HumanResult(15);await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);Check(missionVideo is {Streaming:true},"Human ending restarts cleanly after menu return");ulong start=Time.GetTicksMsec();int last=0;
  while(missionVideo!=null&&Time.GetTicksMsec()-start<110000){
   await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);
   if(missionVideo!=null&&missionVideo.Frame-last>450){last=missionVideo.Frame;await UiCapture("human-ending-frame-"+last);}
  }
  Check(rewardsVisible&&missionVideo==null&&IndexedMovieFrames.ActiveWorkers==0,"Full Human original ending reaches EOS and cleans decoder");
  await UiCapture("human-final-eos");Check(Time.GetTicksMsec()-start>100000,"Full authored101-second ending played without time skipping");
  await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);
  Check(mainMenu&&studioIntro==null&&CampaignProgress.Read(CampaignPath,"human")["completed"]!["14"]!=null,"Enter completes final Human campaign and retains completion");
 }
}
