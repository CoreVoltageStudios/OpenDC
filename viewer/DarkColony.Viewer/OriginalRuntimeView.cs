using Godot;
using DarkColony.Assets;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;

public partial class Viewer {
 bool nativeMode,nativeOmniscient,nativeAttackMove,nativeRange;int nativeSeed;double nativeAccumulator;
 OriginalDefinitions nativeData=null!;OriginalWorld nativeWorld=null!;GifPalette nativePalette=null!;EffectPalette nativeEffects=null!;TerrainOcclusion nativeOcclusion=null!;
 readonly HashSet<(int Slot,int Incarnation)> nativeSelection=new();
 readonly Dictionary<int,(Texture2D Ground,Texture2D Foreground)> nativeTerrain=new();
 readonly Dictionary<int,(string Key,List<NativePart> Parts)> nativeBodies=new();
 readonly Dictionary<int,List<(string Key,List<NativePart> Parts)>> nativePoseFrames=new();
 readonly HashSet<string> nativePresentationGaps=new();
 Vector2 nativeDragStart;bool nativeSelecting; // World-space anchor survives camera movement and zoom.
 Rect2 NativeArea=>firstStrike||interferenceDev?MissionBattlefield:new(0,86*ui,GetViewportRect().Size.X,Math.Max(1,GetViewportRect().Size.Y-182*ui));
 Vector2 NativeOffset=>NativeArea.GetCenter()-camera*zoom;
 Vector2 NativePosition(NativeEntity e){var p=OriginalCoordinates.Project(e.X,e.Z,e.Height,nativeData.Scenario.Map.Height);return new(p.X,p.Y);}
 Cell NativeCell(Vector2 world)=>new((int)Math.Floor(world.X/32),nativeData.Scenario.Map.Height-1-(int)Math.Floor(world.Y/32));
 bool NativeVisible(NativeEntity e)=>nativeOmniscient||(e.Type!=37||nativeWorld.CanDiscoverArtifacts(LocalTeam))&&(e.Owner==LocalTeam||nativeWorld.Visible(LocalTeam,e.Cell));
 void PrepareNative(string[] args) {
  if(args.Contains("--play")||args.Contains("--original-state"))throw new ArgumentException("Original mechanics has a separate launch mode");
  bool trainingLaunch=args.Any(a=>a.StartsWith("--training="));
  bool councilLaunch=args.Any(a=>a.StartsWith("--council="));
  bool warLaunch=args.Any(a=>a.StartsWith("--war="));
  bool humanLaunch=args.Any(a=>a.StartsWith("--human-mission="));
  // Game content may retain a Council campaign selection. Resolve its own
  // namespace; if a base-only library replaces it, reset only the menu selection.
  if(args.Contains("--main-menu")&&OriginalCampaign.IsCouncil(SelectedCampaign)&&!OriginalContent.Available(root,out _))SelectedCampaign=OriginalCampaign.IsHuman(SelectedCampaign)?"human":"taar";
  nativeMode=true;economicMission=args.Contains("--final-evacuation")||(args.Contains("--earth-intervenes")||args.Contains("--then-there-was-one"))||args.Contains("--spider-canyon")||args.Contains("--animal-instinct")||args.Contains("--screamer")||args.Contains("--civil-war")||args.Contains("--puppet-masters")||args.Contains("--breakthrough")||args.Contains("--interference")||args.Contains("--legacy-revealed");firstStrike=trainingLaunch||councilLaunch||warLaunch||humanLaunch||args.Contains("--the-answer")||args.Contains("--main-menu")||args.Contains("--first-strike")||economicMission||args.Contains("--buried-treasure")||args.Contains("--roswell");nativeSeed=int.Parse(args.FirstOrDefault(a=>a.StartsWith("--seed="))?[7..]??"0");
  string dataRoot=args.Contains("--main-menu")?SelectedContentRoot:Path.Combine(root,"private/DC");int launchIndex=args.Contains("--final-evacuation")?14:args.Contains("--the-answer")?13:args.Contains("--earth-intervenes")?12:args.Contains("--then-there-was-one")?11:args.Contains("--spider-canyon")?10:args.Contains("--animal-instinct")?9:args.Contains("--buried-treasure")?8:args.Contains("--screamer")?7:args.Contains("--civil-war")?6:args.Contains("--puppet-masters")?5:args.Contains("--roswell")?4:args.Contains("--breakthrough")?3:args.Contains("--legacy-revealed")?2:economicMission?1:0;
  if(trainingLaunch){SelectedCampaign="training-"+args.Single(a=>a.StartsWith("--training="))[11..];OriginalCampaign.SupportedCount(SelectedCampaign);launchIndex=int.Parse(args.FirstOrDefault(a=>a.StartsWith("--lesson="))?[9..]??"1")-1;}
  if(humanLaunch){SelectedCampaign="human";launchIndex=int.Parse(args.Single(a=>a.StartsWith("--human-mission="))[16..])-1;}
  if(councilLaunch){SelectedCampaign="cw-"+args.Single(a=>a.StartsWith("--council="))[10..];OriginalCampaign.SupportedCount(SelectedCampaign);dataRoot=SelectedContentRoot;OriginalContent.ValidateCouncil(dataRoot);launchIndex=int.Parse(args.FirstOrDefault(a=>a.StartsWith("--council-mission="))?[18..]??"1")-1;}
  nativeData=new(dataRoot,OriginalScenarioImport.Load(dataRoot,OriginalCampaign.Playable(OriginalCampaign.Catalog(dataRoot,SelectedCampaign),launchIndex).Scenario));
  if(trainingLaunch||humanLaunch||councilLaunch)economicMission=OriginalCampaign.HasEconomy(nativeData);
  nativePalette=GameColors.ReadGifPalette(nativeData.Read(nativeData.Scenario.Header.PaletteName));nativeEffects=new(System.Text.Encoding.ASCII.GetString(nativeData.Read("COLOUR.SET")));
  nativeOcclusion=new(nativeData.Scenario.Map,nativeData.Scenario.Terrain);
  if(firstStrike){ui=Math.Clamp(GetViewportRect().Size.Y/1080f,1,1.5f);GetWindow().MinSize=new(960,540);GetTree().AutoAcceptQuit=false;PrepareArtworkPreference(args);if(!args.Contains("--content-return"))PrepareAnimationAudit(args);missionSuite=args.FirstOrDefault(a=>a.StartsWith("--mission-suite="))?[16..]??"smoke";if(args.Contains("--main-menu")){PrepareMainMenu(args);ready=true;GD.Print("MAIN_MENU_READY");QueueRedraw();if(args.Contains("--restoration-replay"))CallDeferred(nameof(ReplayRestoration));else if(PortabilityMode!=null)CallDeferred(nameof(RunPortabilityCheck));else if(args.Contains("--desktop-review")&&OS.HasFeature("editor"))CallDeferred(MethodName.PrepareDesktopReview);else if((capturing||missionSuite=="aspect-window")&&!OS.GetCmdlineUserArgs().Contains("--content-ui-check")&&!args.Contains("--content-return"))CallDeferred(MethodName.MainMenuSuite);return;}if(!warLaunch)PrepareMissionPresentation(args);}
  if(warLaunch){var setup=WarConfiguration.Default(args.Single(a=>a.StartsWith("--war="))[6..],nativeSeed);StartWarMatch(setup);ready=true;if(capturing)CallDeferred(MethodName.FirstStrikeSuite);return;}
  ResetNative();
  // Prepare the eight exact terrain palettes before accepting play. Lazy whole-map
  // rasterization paused drawing for ~406 ms at the first day/night transition.
  if(firstStrike)foreach(int selector in Enumerable.Range(0,8))PrepareNativeTerrain(selector);
  ready=true;GetWindow().Title=firstStrike?"OpenDC | "+campaignMission.Title:"OpenDC | Original mechanics diagnostic fixture";
  GD.Print((firstStrike?"FIRST_STRIKE_READY":"ORIGINAL_CORE_READY")+" seed="+nativeSeed+" missionScripts="+firstStrike);QueueRedraw();
  if(args.Contains("--performance-lifecycle"))CallDeferred(MethodName.RunPerformanceLifecycle);
  else if(args.Contains("--performance-check"))CallDeferred(MethodName.RunPerformanceBenchmark);
  else if(capturing)CallDeferred(firstStrike?MethodName.FirstStrikeSuite:MethodName.NativeCoreSuite);
 }
 void ResetNative() {
  if(firstStrike){ResetFirstStrike();return;}
  nativeWorld=new(nativeData,nativeSeed);nativeSelection.Clear();nativeAccumulator=0;paused=true;nativeAttackMove=false;nativeOmniscient=false;
  // Internal selected-cast verification setup, explicitly not an original opening or release mission.
  var commander=nativeWorld.Spawn(73,0,new(32,28),"diagnostic/core/commander");
  for(int i=0;i<4;i++)nativeWorld.Spawn(8,0,new(33,28),"diagnostic/core/gray"+i);
  nativeWorld.Spawn(69,1,new(45,31),"diagnostic/core/human-commander");
  nativeSelection.Add((commander.Slot,commander.Incarnation));camera=NativePosition(commander);zoom=2;
  ClearNativeBodies();
 }
 void ClearNativeArtBodies(){
  foreach(var pair in nativeBodies)if(!nativePoseFrames.ContainsKey(pair.Key))foreach(var p in pair.Value.Parts)DisposeNativeTexture(p.Texture);
  foreach(var frames in nativePoseFrames.Values)foreach(var frame in frames)foreach(var p in frame.Parts)DisposeNativeTexture(p.Texture);
  nativePoseFrames.Clear();nativeBodies.Clear();ClearSharedArt();
 }
 void ClearNativeBodies(){ClearNativeArtBodies();ClearNativeMissiles();ClearNativeFog();nativePrevious.Clear();nativePreviousTick=-2;nativeSpriteQueue.Clear();nativeDrawn.Clear();}
 IEnumerable<NativeEntity> NativeSelected()=>nativeSelection.Select(id=>nativeWorld.Slots[id.Slot]).Where(e=>(!IsNetworkMatch||!network!.Match!.Surrendered[LocalTeam])&&(!IsWar||!mission.War!.Eliminated[LocalTeam])&&e is {Living:true}&&e.Owner==LocalTeam&&nativeSelection.Contains((e.Slot,e.Incarnation))).Cast<NativeEntity>();
 void NativeCommand(OriginalOrder order,Cell? cell=null,int target=-1) {
  if(IsNetworkMatch){QueueNetworkUnits(NativeSelected(),order,cell,target,cell.HasValue&&order is OriginalOrder.MoveOnly or OriginalOrder.MoveAttack);if(order!=OriginalOrder.Inspire)QueueMissionVoice("ACK");return;}
  if(cell.HasValue&&order is OriginalOrder.MoveOnly or OriginalOrder.MoveAttack)nativeWorld.CommandGroup(NativeSelected(),order,cell.Value);
  else foreach(var e in NativeSelected().OrderBy(e=>e.Slot))nativeWorld.Command(new(e.Slot,e.Incarnation,order,cell.HasValue?cell.Value.X*256+128:0,cell.HasValue?cell.Value.Z*256+128:0,Target:target,Issuer:LocalTeam));
  if(order!=OriginalOrder.Inspire)QueueMissionVoice("ACK");
 }
 void ProcessNative(double delta) {
  if(interferenceDev){ProcessDevelopment(delta);return;}
  if(originalStateMode){ProcessInspection(delta);return;}
  if(firstStrike){ProcessFirstStrike(delta);return;}
  if(!paused){nativeAccumulator+=delta;while(nativeAccumulator>=.066){nativeAccumulator-=.066;nativeWorld.Step();}QueueRedraw();}
 }
 void NativeInput(InputEvent input) {
  if(originalStateMode){OriginalInput(input);return;}
  if(interferenceDev&&DevelopmentInput(input))return;
  if(firstStrike&&FirstStrikeInput(input))return;
  if(input is InputEventKey k&&k.Pressed&&!k.Echo) {
   inputEvents++;
   switch(k.Keycode) {
    case Key.Escape:if(firstStrike)RequestMissionExit(missionFaultCount>0?1:0);else GetTree().Quit();break;case Key.Space:if(!firstStrike){paused=!paused;nativeAccumulator=0;}break;
    case Key.Period:if(paused&&!firstStrike)nativeWorld.Step();break;
    case Key.R:ResetNative();break;case Key.S:NativeCommand(OriginalOrder.Stop);break;case Key.I:NativeCommand(OriginalOrder.Inspire);break;
    case Key.A:nativeAttackMove=true;break;
    case Key.F3:nativeOmniscient=!nativeOmniscient;break;
    case Key.V:nativeRange=!nativeRange;break;
    case Key.F:camera=new(nativeData.Scenario.Map.Width*16,nativeData.Scenario.Map.Height*16);zoom=Math.Min(NativeArea.Size.X/(nativeData.Scenario.Map.Width*32),NativeArea.Size.Y/(nativeData.Scenario.Map.Height*32));break;
    case Key.Tab:var commander=nativeWorld.Entities.FirstOrDefault(e=>e.Owner==LocalTeam&&e.Living&&nativeWorld.IsCommander(e));if(commander!=null){nativeSelection.Clear();nativeSelection.Add((commander.Slot,commander.Incarnation));camera=NativePosition(commander);QueueMissionVoice("SEL");}break;
    case Key.Equal:case Key.Plus:zoom=Math.Clamp(zoom*1.25f,.2f,6);break;case Key.Minus:zoom=Math.Clamp(zoom*.8f,.2f,6);break;
    case Key.Bracketleft:ui=Math.Max(.75f,ui-.125f);SavePresentationPreferences();break;case Key.Bracketright:ui=Math.Min(1.5f,ui+.125f);SavePresentationPreferences();break;
    case Key.Left:if(!firstStrike)camera.X-=96/zoom;break;case Key.Right:if(!firstStrike)camera.X+=96/zoom;break;case Key.Up:if(!firstStrike)camera.Y-=96/zoom;break;case Key.Down:if(!firstStrike)camera.Y+=96/zoom;break;
   }
  }
  if(input is InputEventMouseButton b) {
   if(capturing&&firstStrike&&missionSuite=="input-map")GD.Print($"INPUT_MAP pressed={b.Pressed} position={b.Position} area={NativeArea} window={GetWindow().Size} viewport={GetViewportRect().Size} final={GetViewport().GetFinalTransform()}");
   if(b.ButtonIndex==MouseButton.Middle)dragging=b.Pressed;
   if(b.ButtonIndex==MouseButton.Left&&nativeSelecting&&!b.Pressed) {
    nativeSelecting=false;var end=(b.Position-NativeOffset)/zoom;var rect=new Rect2(nativeDragStart,end-nativeDragStart).Abs();nativeSelection.Clear();
    var candidates=nativeWorld.Entities.Where(e=>e.Owner==LocalTeam&&e.Living&&NativeVisible(e));
    if(rect.Size.Length()*zoom<8) {var e=firstStrike||interferenceDev?UiHover(b.Position):candidates.OrderBy(e=>(NativePosition(e)*zoom+NativeOffset-b.Position).LengthSquared()).FirstOrDefault();if(e!=null&&e.Owner==LocalTeam&&(firstStrike||interferenceDev||(NativePosition(e)*zoom+NativeOffset-b.Position).Length()<40*zoom))nativeSelection.Add((e.Slot,e.Incarnation));}
    else foreach(var e in candidates)if(rect.HasPoint(NativePosition(e)))nativeSelection.Add((e.Slot,e.Incarnation));
    QueueMissionVoice("SEL");
   }
   if(b.Pressed&&NativeArea.HasPoint(b.Position)) {
    inputEvents++;var world=(b.Position-NativeOffset)/zoom;var cell=NativeCell(world);
    if(b.ButtonIndex==MouseButton.Left) {
     if(nativeAttackMove){NativeCommand(OriginalOrder.MoveAttack,cell);nativeAttackMove=false;}
     else {nativeDragStart=world;nativeSelecting=true;}
    }
    if(b.ButtonIndex==MouseButton.Right&&nativeWorld.Navigation.Inside(cell)) {
     var target=nativeWorld.Entities.Where(e=>e.Living&&e.Owner<8&&!nativeWorld.Relations[LocalTeam,e.Owner]&&NativeVisible(e))
      .OrderBy(e=>NativePosition(e).DistanceSquaredTo(world)).FirstOrDefault();
     if(target!=null&&NativePosition(target).DistanceTo(world)<25)NativeCommand(OriginalOrder.Attack,target:target.Slot);
     else NativeCommand(OriginalOrder.MoveOnly,cell);
    }
    if(b.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown){zoom=Math.Clamp(zoom*(b.ButtonIndex==MouseButton.WheelUp?1.25f:.8f),.2f,6);camera=world-(b.Position-NativeArea.GetCenter())/zoom;}
   }
  }
  if(input is InputEventMouseMotion motion&&dragging)camera-=motion.Relative/zoom;
  QueueRedraw();
 }
 List<NativePart> NativeParts(NativeEntity e,Vector2 at,float displayHeight) {
  // 439b07..439b0e: inactive body skips the entire entity, including attachments.
  bool bodyActive=e.Body.Pose(e.Angle)!=null;
  int groundY=(int)(at.Y+displayHeight);
  bool spatial=bodyActive&&new[]{e.Body,e.Blood,e.Overlay}.Where(a=>a.Mode!=2).Any(a=>a.Pose(e.Angle)?.Cells.Any(c=>c.UnknownWords[2]!=3&&(e.Height==0&&c.UnknownWords[0]!=2||nativeShadows&&c.UnknownWords[2] is 1 or 2))==true);
  string position=spatial?$"{(int)at.X}:{(int)at.Y}:{groundY}:{displayHeight:R}:{e.Height}":"unmasked";
  string key=$"{e.Incarnation}:{e.Owner}:{(e.Owner<8?nativeWorld.Teams[e.Owner].Color:0)}:{position}:{e.Angle}:{e.Body.Set?.Name}:{e.Body.Frame}:{e.Body.Mode}:{e.Blood.Set?.Name}:{e.Blood.Frame}:{e.Blood.Mode}:{e.Overlay.Set?.Name}:{e.Overlay.Frame}:{e.Overlay.Mode}";
  if(nativeBodies.TryGetValue(e.Slot,out var cached)&&cached.Key==key)return cached.Parts;
  // Fixed cities and entirely unmasked airborne poses can reuse their textures.
  // In particular the construction saucer's pixels do not change with altitude.
  // Fixed city FIN loops exceed eight frames (e.g. ROBOPOD2STAND). Retain
  // up to32 city poses to avoid rebuilding the same large pixels each cycle;
  // moving/unmasked entities keep eight. Both bounds clear on reset/map/filter.
  List<(string Key,List<NativePart> Parts)>? poseFrames=null;
  if(e.Slot<120&&e.Slot%15<6||!spatial||nativePoseFrames.ContainsKey(e.Slot)){
   if(!nativePoseFrames.TryGetValue(e.Slot,out poseFrames)){
    // Ownership moves from the single-pose cache to the bounded pose cache.
    if(cached.Parts!=null)foreach(var p in cached.Parts)DisposeNativeTexture(p.Texture);
    nativePoseFrames[e.Slot]=poseFrames=new();
   }
   int index=poseFrames.FindIndex(f=>f.Key==key);
   if(index>=0){var found=poseFrames[index];poseFrames.RemoveAt(index);poseFrames.Add(found);nativeBodies[e.Slot]=found;return found.Parts;}
  }
  ulong buildStart=Time.GetTicksUsec();
  animationBodyBuilds++;if(perfActive)perfBuilds++;
  if(poseFrames==null&&cached.Parts!=null)foreach(var p in cached.Parts)DisposeNativeTexture(p.Texture);
  var parts=new List<NativePart>();int ordinal=0;
  void Add(NativeAnimation animation) {
   if(animation.Mode==2)return; // 426364: completed/disabled FIN is inactive, including extracted commanders.
   var frame=animation.Pose(e.Angle);if(frame==null)return;
   // 439934 draws Cells from three animation channels. Parts are event/attachment anchors, not automatic child images.
   foreach(var c in frame.Cells) {
    int cellOrdinal=ordinal++;int mode=c.UnknownWords[2];if(mode==3)continue; //454da2 skips mode3; it is not a body shadow.
    var sprite=nativeData.Sprite(c.Bank).Cells[c.CellId];var placement=OriginalShadow.BodyPlacement(c,sprite);Raster raster;
    if(nativeShadows&&mode is 1 or 2) {
     var bounds=OriginalShadow.Bounds(sprite,c);
     bool masked=!nativeShareUnmasked||nativeOcclusion.MayMask((int)at.X+bounds.X,groundY+bounds.Y,bounds.Width,bounds.Height,groundY+c.Y,-(sprite.Height*40>>8));
     Texture2D BuildShadow(){var shadow=OriginalShadow.Project(sprite,c);return NativeShadowTexture(masked?OriginalShadow.Mask(shadow,nativeOcclusion,(int)at.X,groundY,groundY+c.Y):shadow.Pixels);}
     Texture2D shadowTexture;
     if(masked||!SharedArt(new(c.Bank,c.CellId,0,c.UnknownWords[3]!=0,2),BuildShadow,bounds.Width*bounds.Height*4,out shadowTexture))shadowTexture=BuildShadow();
     parts.Add(new(shadowTexture,new(bounds.X,bounds.Y+displayHeight),cellOrdinal,c.UnknownWords[0],c.X));
    }
    if(mode==2)continue;
    if(mode is not (0 or 1 or 5)){nativePresentationGaps.Add("Unmapped selected FIN blitter "+mode);continue;}
    bool maskBody=e.Height==0&&c.UnknownWords[0]!=2&&(!nativeShareUnmasked||nativeOcclusion.MayMask((int)at.X+placement.X,(int)at.Y+placement.Y,sprite.Width,sprite.Height,(int)at.Y+c.Y));
    Texture2D BuildBody(){
     raster=mode==5?nativeEffects.Decode(sprite):GameColors.Body(sprite,NativeBodyColors(e.Owner<8?nativeWorld.Teams[e.Owner].Color:0));
     if(c.UnknownWords[3]!=0)raster=OriginalShadow.Mirror(raster);
     if(maskBody){var visual=new VisualAsset("original",raster,sprite.Width,sprite.Height,sprite.Width,sprite.Height,-placement.X,-placement.Y);raster=nativeOcclusion.ApplyGroundBody(visual,(int)at.X,(int)at.Y,(int)at.Y+c.Y);}
     return NativeArtwork(raster);
    }
    Texture2D bodyTexture;
    int palette=mode==5?0:e.Owner<8?nativeWorld.Teams[e.Owner].Color:0;
    if(!nativeShareUnmasked||maskBody||!SharedArt(new(c.Bank,c.CellId,palette,c.UnknownWords[3]!=0,mode==5?5:0),BuildBody,(sprite.Width+2)*(sprite.Height+2)*4,out bodyTexture))bodyTexture=BuildBody();
    parts.Add(new(bodyTexture,new(placement.X,placement.Y),cellOrdinal,c.UnknownWords[0],c.X));
   }
  }
  if(bodyActive){Add(e.Body);if(e.Blood.Mode!=2)Add(e.Blood);if(e.Overlay.Mode!=2)Add(e.Overlay);}
  nativeBodies[e.Slot]=(key,parts);
  if(poseFrames!=null){poseFrames.Add((key,parts));if(poseFrames.Count>(e.Slot<120&&e.Slot%15<6?32:8)){foreach(var p in poseFrames[0].Parts)DisposeNativeTexture(p.Texture);poseFrames.RemoveAt(0);}}
  if(animationProfile!=null&&Time.GetTicksUsec()-buildStart>15000)GD.Print($"ART_BUILD_SLOW tick={nativeWorld.Tick} type={e.Type} body={e.Body.Set?.Name}/{e.Body.Frame} overlay={e.Overlay.Set?.Name}/{e.Overlay.Frame} us={Time.GetTicksUsec()-buildStart}");
  return parts;
 }
 (Texture2D Ground,Texture2D Foreground) PrepareNativeTerrain(int selector) {
  if(nativeTerrain.TryGetValue(selector,out var terrain))return terrain;
  animationTerrainBuilt=true;
  var colors=GameColors.Table(nativePalette,selector,16,ColorBank.Terrain);
  var ground=Presentation.Terrain(nativeData.Scenario.Map,nativeData.Scenario.Terrain,false,colors);
  var foreground=Presentation.Terrain(nativeData.Scenario.Map,nativeData.Scenario.Terrain,true,colors);
  if(firstStrike||interferenceDev||originalStateMode){Presentation.Blit(ground,foreground,0,0);terrain=(NativeArtwork(ground,true),OriginalTexture(new Raster(1,1,new byte[4])));}
  else terrain=(OriginalTexture(ground),OriginalTexture(foreground));
  nativeTerrain[selector]=terrain;return terrain;
 }
 void DrawNative() {
  if(firstStrike&&missionClosing)return;
  if(mainMenu){DrawMainMenu();return;}
  if(missionVideo is {Visible:true}){DrawRect(GetViewportRect(),Colors.Black);return;}
  BeginAnimationDraw();long perfDrawStart=perfActive?System.Diagnostics.Stopwatch.GetTimestamp():0;perfBuilds=0;
  if(firstStrike&&missionFault!=null){DrawSetTransform(Vector2.Zero);DrawFirstStrike();return;}
  var size=GetViewportRect().Size;var map=nativeData.Scenario.Map;
  int selector=TerrainSelectors.LowSelector(nativeWorld.Coefficient);
  var terrain=PrepareNativeTerrain(selector);
  DrawRect(new(Vector2.Zero,size),firstStrike?Colors.Black:new Color("091310"));DrawSetTransform(NativeOffset,0,Vector2.One*zoom);
  DrawNativeArtwork(terrain.Ground,Vector2.Zero);DrawNativeArtwork(terrain.Foreground,Vector2.Zero);
  if(nativeRange)foreach(var e in NativeSelected())if(nativeWorld.Weapon(e.Type,e.Owner) is {} weapon)
   foreach(var ring in nativeData.Reference.Rings.Take(weapon.Range+1))foreach(var d in ring) {
    int x=e.Cell.X+d.X,z=e.Cell.Z+d.Z;bool explicitInside=d.X*d.X+d.Z*d.Z<weapon.Range*weapon.Range;
    DrawRect(new(x*32,(map.Height-1-z)*32,32,32),explicitInside?new Color(.4f,.75f,.3f,.22f):new Color(.95f,.65f,.2f,.45f),false,1/zoom);
   }
  // Fog shades ground knowledge; an eligible airborne body may project above that
  // terrain footprint. Source-cell filtering below still gates every body/shadow.
  long perfPart=perfActive?System.Diagnostics.Stopwatch.GetTimestamp():0;DrawNativeFog();if(perfActive){perfFogMs=PerfElapsed(perfPart);perfPart=System.Diagnostics.Stopwatch.GetTimestamp();}
  nativeDrawn.Clear();nativeSpriteQueue.Clear();
  foreach(var source in nativeWorld.Entities.Where(NativeVisible)) {
   var e=originalStateMode?inspectionArt[source.Slot]:source;
   var display=NativeRenderPosition(e);var at=display.Body;
   if(nativeViewportCull&&!NativeIntersectsViewport(e,at,display.Height))continue;
   nativeDrawn.Add((e.Slot,e.Incarnation));var drawnParts=NativeParts(e,at,display.Height);foreach(var p in drawnParts)QueueNativePart(p.Texture,at+p.Offset,OriginalSpriteOrder.Key(e.Z,e.Slot<120&&e.Slot%15==2&&nativeWorld.Teams[e.Slot/15].Race==1,p.Ordinal,p.Layer,(int)at.X+p.SortX),e.Slot,p.Ordinal,p.Layer);
   AuditEntityDraw(e,at,display.Height,drawnParts);
  }
  DrawNativeMissiles();FlushNativeSprites();if(perfActive){perfArtMs=PerfElapsed(perfPart);perfPart=System.Diagnostics.Stopwatch.GetTimestamp();}
  if(!firstStrike&&!interferenceDev)foreach(var e in NativeSelected().Where(NativeVisible)){
   var at=NativeRenderPosition(e).Body;
   DrawArc(at,14,0,Mathf.Tau,32,accent,1.5f/zoom);
   DrawRect(new(at+new Vector2(-16,-48),new(32,3)),new Color("281d1b"));
   DrawRect(new(at+new Vector2(-16,-48),new(Math.Clamp(e.Health/(float)nativeData.Scenario.Types[e.Type].MaximumHealth,0,1)*32,3)),accent);
  }
  DrawSetTransform(Vector2.Zero);
  if(firstStrike||interferenceDev)DrawMissionMarkings();
  if(nativeSelecting){var start=nativeDragStart*zoom+NativeOffset;DrawRect(new Rect2(start,(firstStrike?missionPointer:GetViewport().GetMousePosition())-start).Abs().Intersection(NativeArea),accent,false,1);}
  if(firstStrike){DrawFirstStrike();EndAnimationDraw();if(perfActive){perfHudMs=PerfElapsed(perfPart);perfDrawMs=PerfElapsed(perfDrawStart);}return;}
  if(interferenceDev){DrawDevelopment();EndAnimationDraw();return;}
  if(originalStateMode){DrawInspection();EndAnimationDraw();return;}
  DrawRect(new(0,0,size.X,86*ui),panel);DrawRect(new(0,size.Y-96*ui,size.X,96*ui),panel);
  Text("OpenDC / ORIGINAL MECHANICS / INTERNAL VERIFICATION FIXTURE",new(24,29),22,accent);
  Text($"{(paused?"PAUSED":"RUNNING")}  |  update {nativeWorld.Tick}  |  {(nativeWorld.Phase==1?"NIGHT":"DAY")}  |  seed {nativeSeed}  |  {(nativeOmniscient?"OMNISCIENT DIAGNOSTIC":"TEAM 0 VIEW")}  |  scripts disabled",new(24,61),16,ink);
  float y=size.Y/ui-69;
  Text("Click / drag: select   Right-click: move / attack   A then click: attack-move   S: stop   I: Inspire   Tab: commander",new(24,y),16,ink);
  Text("Space: pause   R: restart fixture   F3: diagnostic view   V: range cells (amber: acquisition only)   arrows / drag: pan   wheel: zoom   [ / ]: UI",new(24,y+26),15,muted);
  var selected=NativeSelected().ToArray();Text($"Selected {selected.Length}"+(selected.Length==1?$"   HP {selected[0].Health}   energy {selected[0].Energy}   inspiration {selected[0].Inspired}":"")+(nativeAttackMove?"   ATTACK-MOVE: choose destination":""),new(24,y+52),15,accent);
 }
 void DrawNativeMissiles() {
  foreach(var m in nativeWorld.Missiles.Where(m=>m.Animation.Mode!=2&&m.Delay==0)) {
   if(!nativeOmniscient&&!nativeWorld.Visible(LocalTeam,new(m.X>>8,m.Z>>8)))continue; //43a0e8 current sight, separate from sound explored gate.
   var p=OriginalCoordinates.Project(m.X,m.Z,m.Height,nativeData.Scenario.Map.Height);var at=new Vector2(p.X,p.Y);
   var frame=m.Animation.Pose(m.Angle);if(frame==null)continue;
   int ordinal=0;foreach(var c in frame.Cells) {
    int cellOrdinal=ordinal++;if(c.UnknownWords[2]==3)continue;
    var sprite=nativeData.Sprite(c.Bank).Cells[c.CellId];var placement=OriginalShadow.BodyPlacement(c,sprite);
    //43a0e8 uses missile shooter-slot low3 bits, unusually not the current team color.
    // Effect blitter5 never reads a team palette. Retaining one identical raster
    // per shooter-slot color multiplied warm combat texture retention by up to8.
    int color=c.UnknownWords[2]==5?0:m.Shooter&7;
    string key=$"original-missile:{c.Bank}:{c.CellId}:{c.UnknownWords[2]}:{c.UnknownWords[3]}:{color}";
    if(!nativeMissileTextures.TryGetValue(key,out var texture)) {
     var raster=c.UnknownWords[2]==5?nativeEffects.Decode(sprite):GameColors.Body(sprite,NativeBodyColors(m.Shooter&7));
     if(c.UnknownWords[3]!=0)raster=OriginalShadow.Mirror(raster);
     nativeMissileTextures[key]=texture=NativeArtwork(raster);
    }
    QueueNativePart(texture,at+new Vector2(placement.X,placement.Y),OriginalSpriteOrder.Key(m.Z,false,cellOrdinal,c.UnknownWords[0],(int)at.X+c.X),-1-m.Id,cellOrdinal,c.UnknownWords[0]);
    AuditMissileDraw(m,at,c,texture,new(placement.X,placement.Y));
   }
  }
 }
 async Task NativeCapture(string name) {
  animationCaptureActive=true;
  QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();
  if(image.SavePng(Path.Combine(output,name+".png"))!=Error.Ok)throw new IOException("Original capture failed");
  File.WriteAllText(Path.Combine(output,name+".json"),JsonSerializer.Serialize(new{name,nativeWorld.Tick,paused,nativeSeed,nativeOmniscient,inputEvents,viewport=new[]{image.GetWidth(),image.GetHeight()},entities=nativeWorld.Entities.Select(e=>new{e.Identity,e.Slot,e.Type,e.Owner,e.X,e.Z,e.Health,e.State,e.Energy,e.Inspired}),events=nativeWorld.Events,gaps=nativePresentationGaps},new JsonSerializerOptions{WriteIndented=true}));
  animationCaptureActive=false;
 }
 Vector2 NativeWindowPoint(Vector2 viewportPoint)=>GetViewport().GetFinalTransform()*viewportPoint; // Includes Godot's integer-rounded letterbox offsets/scales.
 async Task NativeMouse(MouseButton button,Vector2 position) {
  position=NativeWindowPoint(position); // ParseInputEvent receives window pixels; Godot applies content scaling.
  Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=button,Position=position,Pressed=true});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=button,Position=position,Pressed=false});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 }
 public async void NativeCoreSuite(){try {
  await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await Send(Key.Period);await NativeCapture("00-original-core");
  await Send(Key.I);await Send(Key.Period);await Send(Key.Period);Check(nativeWorld.Events.Any(e=>e.Kind=="inspire"),"Inspire through Godot input");await NativeCapture("01-inspire");
  await Send(Key.A);var destination=new Vector2(37*32+16,(nativeData.Scenario.Map.Height-1-32)*32+16);
  await NativeMouse(MouseButton.Left,destination*zoom+NativeOffset);Check(nativeWorld.Events.Any(e=>e.Kind=="order"&&e.Value==(int)OriginalOrder.MoveAttack),"attack-move through Godot input");
  for(int i=0;i<650;i++){nativeWorld.Step();if(i%32==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
  Check(nativeWorld.Events.Any(e=>e.Kind=="launch"),"native selected-cast combat launches");await NativeCapture("02-combat");
  await Send(Key.F3);await Send(Key.V);await NativeCapture("03-omniscient-range");await Send(Key.R);Check(nativeWorld.Tick==0&&nativeWorld.Missiles.Count==0,"restart clears simulation and missiles");await NativeCapture("99-reset");
  await Send(Key.A);await NativeMouse(MouseButton.Left,destination*zoom+NativeOffset);await Send(Key.Space);
  await ToSignal(GetTree().CreateTimer(8),SceneTreeTimer.SignalName.Timeout);await Send(Key.Space);
  Check(nativeWorld.Tick>=120&&nativeWorld.Tick<160,"normal-time66ms execution");Check(nativeWorld.Events.Any(e=>e.Kind=="launch"),"normal-time selected-cast combat");await NativeCapture("04-realtime-combat");
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));GD.Print("ORIGINAL_CORE_SUITE_PASS");GetTree().Quit();
 }catch(Exception error){File.WriteAllText(Path.Combine(output,"failure.txt"),error.ToString());GD.PushError(error.ToString());GetTree().Quit(1);}}
}
