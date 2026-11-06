using Godot;
using DarkColony.Assets;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;

public partial class Viewer {
 bool interferenceDev;InterferenceDevelopment development=null!;string developmentNotice="Select the Brozaar, then right-click a Petra-7 vent to harvest.";
 double developmentProfileClock;int developmentFrames;double developmentWorst;long developmentAllocStart;int developmentRank;
 int developmentTab=1;
 OriginalDependency[] developmentCatalog=Array.Empty<OriginalDependency>();
 Dictionary<int,OriginalInterfaceControl> economicArt=new();
 void PrepareInterference(string[] args){
  interferenceDev=nativeMode=true;nativeSeed=int.Parse(args.FirstOrDefault(a=>a.StartsWith("--seed="))?[7..]??"0");
  developmentRank=int.Parse(args.FirstOrDefault(a=>a.StartsWith("--prior-rank="))?[13..]??"0");if(developmentRank is <0 or >3)throw new ArgumentException("Prior rank must be0..3");
  nativeData=new(Path.Combine(root,"private/DC"),OriginalScenarioImport.Load(Path.Combine(root,"private/DC"),"SCENARIO/ALIEN/ALIEN02.SCN"));
  nativePalette=GameColors.ReadGifPalette(nativeData.Read(nativeData.Scenario.Header.PaletteName));nativeEffects=new(System.Text.Encoding.ASCII.GetString(nativeData.Read("COLOUR.SET")));nativeOcclusion=new(nativeData.Scenario.Map,nativeData.Scenario.Terrain);
  economicArt=OriginalInterface.Controls(nativeData.Read("INTRFACE/MAINE"));
  PrepareArtworkPreference(args);PrepareAnimationAudit(args);PrepareEdgeScrolling(args);fastGame=ArtworkPreference.LoadFast(artworkPreferencePath);
  missionSoundPaths=OriginalSound.Parse(nativeData.Read("SOUND/SOUND2.DAT"));ambientTypes=OriginalSoundRules.TerrainTypes(nativeData.Scenario.Map);
  ui=Math.Clamp(GetViewportRect().Size.Y/1080,1,1.5f);GetWindow().MinSize=new(960,720);GetTree().AutoAcceptQuit=false;
  ResetDevelopment();foreach(int selector in Enumerable.Range(0,8))PrepareNativeTerrain(selector);
  ready=true;GetWindow().Title="OpenDC | Interference M2 development (incomplete mission)";
  GD.Print("INTERFERENCE_M2_READY original opening + autonomous units; later scripts/strategy/outcomes gated; priorRank="+developmentRank);
  developmentAllocStart=GC.GetAllocatedBytesForCurrentThread();QueueRedraw();if(capturing)CallDeferred(MethodName.InterferenceSuite);
 }
 void ResetDevelopment(){
  ResetMissionInterface();ResetMissionAudio();transportAudio.Clear();nativeSoundCursor=0;missionAudioAudit.Clear();
  missionSounds=new(nativeData.Read("SOUND/SLIST.DAT"),nativeData.Read("SOUND/DESERT.AMB"),1);
  development=new(nativeData,nativeSeed,developmentRank);nativeWorld=development.World;nativeSelection.Clear();nativeAccumulator=0;paused=false;nativeOmniscient=false;nativeAttackMove=false;missionBriefing=false;missionFault=null;
  developmentCatalog=nativeWorld.ProductionCatalog(LocalTeam);
  miniTick=-100;Array.Fill(miniVisibility,(byte)255);ClearNativeBodies();camera=new(12*32,(nativeData.Scenario.Map.Height-74)*32);zoom=1.5f;ClampMissionCamera();
  developmentNotice="Select the Brozaar, then right-click a Petra-7 vent to harvest.";
 }
 void ProcessDevelopment(double delta){
  animationDelta=delta;UpdateMissionInterface();ProcessEdgeScrolling(delta);
  if(GetWindow().HasFocus()&&focusedUi.Length==0&&!dragging&&!minimapDragging&&!uiPointerOwned){var direction=new Vector2((Input.IsKeyPressed(Key.Right)?1:0)-(Input.IsKeyPressed(Key.Left)?1:0),(Input.IsKeyPressed(Key.Down)?1:0)-(Input.IsKeyPressed(Key.Up)?1:0));if(direction!=Vector2.Zero)camera+=direction.Normalized()*600*(float)Math.Min(delta,.1)/zoom;ClampMissionCamera();}
  try{
   if(!paused){nativeAccumulator+=AdmittedMissionTime(delta,fastGame);while(nativeAccumulator>.066){nativeAccumulator-=.066;RememberNativePositions();nativeWorld.Step();DispatchMissionSounds();}}
   DispatchMissionSounds();PumpMissionAudio();
  }catch(Exception error){paused=true;developmentNotice="Simulation halted. Restart with R; diagnostic saved.";missionFault=error.ToString();File.WriteAllText(Path.Combine(output,"m2-failure.txt"),missionFault);GD.PushError(error.ToString());}
  developmentFrames++;developmentWorst=Math.Max(developmentWorst,delta*1000);developmentProfileClock+=delta;
  if(developmentProfileClock>=5){File.AppendAllText(Path.Combine(output,"m2-performance.jsonl"),JsonSerializer.Serialize(new{tick=nativeWorld.Tick,frames=developmentFrames,seconds=developmentProfileClock,worstMs=developmentWorst,allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-developmentAllocStart,entities=nativeWorld.Entities.Count(),missiles=nativeWorld.Missiles.Count})+System.Environment.NewLine);developmentFrames=0;developmentWorst=0;developmentProfileClock=0;developmentAllocStart=GC.GetAllocatedBytesForCurrentThread();}
  QueueRedraw();
 }
 string ProductName(OriginalDependency d)=>IsHuman?HumanProductName(d):d.Id switch{84=>"Zisp",14=>"Mind Hive",15=>"War Hive",16=>"Breeder Pod",17=>"Genetics Sac",18=>"Breeder Pod II",19=>"Genetics Sac II",20=>"Neural Hive",21=>"Brozaar",22=>"Xenowort",23=>"Gray Warrior",24=>"Ortu",25=>"Sy Demon",26=>"Atril",28=>"Slom",30=>"Warrior weapon I",31=>"Warrior weapon II",32=>"Warrior armor I",33=>"Warrior armor II",37=>"Xenowort weapon I",38=>"Xenowort weapon II",40=>"Xenowort armor II",39=>"Xenowort armor I",41=>"Sy Demon weapon I",42=>"Sy Demon weapon II",43=>"Sy Demon armor I",44=>"Sy Demon armor II",45=>"Ortu weapon I",47=>"Ortu armor I",49=>"Atril weapon I",51=>"Atril armor I",_=>"Product "+d.Id};
 void DrawDevelopment()=>DrawBattlefieldSidebar();
 int developmentPage;
 void DrawSidebarEconomy(float x,float y,float bottom){
  DrawRelayProduction(x,y,bottom);
 }

 string UnitTitleForType(int type)=>ProductName(developmentCatalog.First(d=>d.Kind==1&&d.Subject==type));
 void EconomicButton(OriginalDependency d,string name,string status,Rect2 r,string help,bool enabled,bool active){
  string id="product:"+d.Id;var hit=new UiHit(id,Px(r),help,enabled);missionHits.Add(hit);
  bool hover=hit.Rect.HasPoint(missionPointer),pressed=pressedUi==id;
  DrawRect(hit.Rect,pressed?new Color("101610"):active?new Color("26321b"):hover&&enabled?new Color("242a23"):new Color("111514"));
  DrawRect(hit.Rect,active?uiGreen:hover&&enabled?chrome:new Color("545851"),false,ui);
  var art=economicArt[d.Icon]; // No guessed icon fallback: the original control mapping is required.
  if(r.Size.Y<44){
   UiImage("MAINBUT",pressed&&art.Pressed>=0?art.Pressed:art.Normal,new(r.Position.X+3,r.Position.Y+2,36,r.Size.Y-4),enabled?Colors.White:new Color(.45f,.45f,.45f));
   UiLabel(name,r.Position.X+44,r.Position.Y+(r.Size.Y+10)/2,13,enabled?uiText:uiDim);
   UiLabel(status,r.Position.X+208,r.Position.Y+(r.Size.Y+10)/2,11,active?uiGreen:uiDim);
  }else{
   UiImage("MAINBUT",pressed&&art.Pressed>=0?art.Pressed:art.Normal,new(r.Position.X+4,r.Position.Y+5,65,r.Size.Y-10),enabled?Colors.White:new Color(.45f,.45f,.45f));
   UiLabel(name,r.Position.X+77,r.Position.Y+Math.Min(23,r.Size.Y*.45f),16,enabled?uiText:uiDim);
   UiLabel(status,r.Position.X+77,r.End.Y-8,14,active?uiGreen:uiDim);
  }
 }
 void ActivateDevelopment(string id){
  if(ActivateUnitCommand(id))return;
  if(id.StartsWith("tab:")){relayPages[developmentTab]=developmentPage;developmentTab=int.Parse(id[4..]);developmentPage=relayPages[developmentTab];return;}
  if(id.StartsWith("product:")){int dependency=int.Parse(id[8..]);if(IsNetworkMatch){network!.Submit(new("production",LocalTeam,Value:dependency));developmentNotice="Purchase requested";return;}var result=nativeWorld.RequestProduction(LocalTeam,dependency);relayStateTick=-1;developmentNotice=ProductName(nativeData.Scenario.Dependencies.Single(d=>d.Id==dependency))+": "+(result.Accepted?$"committed {result.Charged} Petra-7":result.Reason);return;}
  switch(id){case "products-prev":developmentPage=Math.Max(0,developmentPage-1);break;case "products-next":developmentPage++;break;case "dev-speed":fastGame=!fastGame;SaveSettings(p=>ArtworkPreference.SaveFast(p,fastGame),"Game speed could not be saved.");break;case "dev-art":ChooseArtwork(nativeSampling==0?ArtworkStyle.Classic:ArtworkStyle.Crisp);break;case "dev-pause":paused=!paused;nativeAccumulator=0;break;case "dev-restart":ResetDevelopment();break;}
 }
 bool DevelopmentInput(InputEvent input){
  if(input is InputEventMouseMotion motion){missionPointer=motion.Position;if(minimapDragging){MoveMinimap(motion.Position);return true;}return uiPointerOwned;}
  if(input is InputEventKey k&&k.Pressed&&!k.Echo){if(UnitCommandKey(k.Keycode))return true;switch(k.Keycode){
   case Key.Escape:if(nativeAttackMove||targetMode.Length>0){nativeAttackMove=false;targetMode="";}else RequestMissionExit(missionFault==null?0:1);return true;
   case Key.R:ResetDevelopment();return true;case Key.Space:ActivateDevelopment("dev-pause");return true;case Key.C:ActivateDevelopment("dev-art");return true;
   case Key.F8:developerUi=!developerUi;return true;
   case Key.Left:case Key.Right:case Key.Up:case Key.Down:return true;
  }}
  if(input is InputEventMouseButton b){missionPointer=b.Position;
   if(b.ButtonIndex==MouseButton.Left&&!b.Pressed){
    if(minimapDragging){MoveMinimap(b.Position);minimapDragging=false;return true;}
    if(uiPointerOwned){var hit=missionHits.LastOrDefault(h=>h.Id==pressedUi&&h.Enabled&&h.Rect.HasPoint(b.Position));uiPointerOwned=false;pressedUi="";if(hit!=null)ActivateDevelopment(hit.Id);return true;}
   }
   if(b.Pressed&&b.ButtonIndex==MouseButton.Left){var hit=missionHits.LastOrDefault(h=>h.Rect.HasPoint(b.Position));if(hit!=null){uiPointerOwned=true;pressedUi=hit.Enabled?hit.Id:"";return true;}if(MiniMapRect.HasPoint(b.Position)){minimapDragging=true;MoveMinimap(b.Position);return true;}}
   if(!NativeArea.HasPoint(b.Position)&&!nativeSelecting){if(b.ButtonIndex==MouseButton.Middle&&!b.Pressed)dragging=false;return true;}
   if(b.Pressed&&b.ButtonIndex==MouseButton.Right&&(nativeAttackMove||targetMode.Length>0)){nativeAttackMove=false;targetMode="";return true;}
  }
  if(input is InputEventMouseButton button&&UnitTargetInput(button))return true;
  return false;
 }
 public async void InterferenceSuite(){try{await VerifyInterference();await FinishFirstStrike(0);}catch(Exception error){File.WriteAllText(Path.Combine(output,"m2-capture-failure.txt"),error.ToString());GD.PushError(error.ToString());await FinishFirstStrike(1);}}
}
