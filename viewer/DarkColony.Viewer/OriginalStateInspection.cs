using Godot;
using DarkColony.Assets;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;

// Initialization inspection uses the SAME world, assets and renderer as First Strike.
// Only presentation clones advance. No Step(), script, AI, economy or command dispatch.
public partial class Viewer {
 bool originalStateMode,inspectionPreview=true;string originalScenario="";double inspectionClock;long inspectionArtTick;
 readonly Dictionary<int,NativeEntity> inspectionArt=new();
 int inspectionLandmark;
 ImageTexture OriginalTexture(Raster raster){using var image=Image.CreateFromData(raster.Width,raster.Height,false,Image.Format.Rgba8,raster.Rgba);var texture=ImageTexture.CreateFromImage(image);AuditTexture(texture,raster);return texture;}
 void PrepareOriginalState(string[] args) {
  originalStateMode=nativeMode=true;
  originalScenario=args.FirstOrDefault(a=>a.StartsWith("--scenario="))?[11..]??"SCENARIO/ALIEN/ALIEN01.SCN";
  nativeData=new(Path.Combine(root,"private/DC"),OriginalScenarioImport.Load(Path.Combine(root,"private/DC"),originalScenario));
  string palette=nativeData.Scenario.Header.PaletteName;
  nativePalette=GameColors.ReadGifPalette(nativeData.Read(palette));nativeEffects=new(System.Text.Encoding.ASCII.GetString(nativeData.Read("COLOUR.SET")));
  nativeOcclusion=new(nativeData.Scenario.Map,nativeData.Scenario.Terrain);PrepareArtworkPreference(args);PrepareAnimationAudit(args);
  ui=Math.Clamp(GetViewportRect().Size.Y/1080f,1,1.5f);GetWindow().MinSize=new(960,540);
  ResetInspection();ready=true;QueueRedraw();
  GetWindow().Title="OpenDC | Initialization inspection (not playable)";
  GD.Print("INITIALIZATION_READY "+originalScenario+" tick=0 entities="+nativeWorld.Entities.Count()+" sources="+nativeWorld.Session.Sources.Length);
  if(capturing)CallDeferred(MethodName.OriginalStateSuite);
 }
 void ResetInspection() {
  nativeWorld=new(nativeData,0);nativeSelection.Clear();nativeOmniscient=paused=true;nativeAttackMove=false;
  inspectionClock=0;inspectionArtTick=0;inspectionLandmark=0;inspectionTraceArtTick=-1;inspectionArt.Clear();ClearNativeBodies();
  foreach(var e in nativeWorld.Entities) {
   var p=new NativeEntity{Slot=e.Slot,Incarnation=e.Incarnation,Type=e.Type,Owner=e.Owner,X=e.X,Z=e.Z,Height=e.Height,Health=e.Health,Flag=e.Flag,Angle=e.Angle,Energy=e.Energy,SourceIdentity=e.SourceIdentity};
   p.Body.Start(nativeData.Clips[p.Type].Stand,0);inspectionArt.Add(e.Slot,p);
  }
  var start=nativeWorld.Entities.FirstOrDefault(e=>e.Slot==0)??nativeWorld.Entities.First();camera=NativePosition(start);zoom=1.5f;camera.Y-=80;ClampInspection();
  WriteInspection();
 }
 void ClampInspection(){var size=new Vector2(nativeData.Scenario.Map.Width*32,nativeData.Scenario.Map.Height*32);var half=NativeArea.Size/(2*zoom);camera=new(Mathf.Clamp(camera.X,Math.Min(half.X,size.X/2),Math.Max(size.X-half.X,size.X/2)),Mathf.Clamp(camera.Y,Math.Min(half.Y,size.Y/2),Math.Max(size.Y-half.Y,size.Y/2)));}
 void ProcessInspection(double delta) {
  animationDelta=delta;
  if(inspectionPreview){inspectionClock+=Math.Min(delta,.25);while(inspectionClock>=.066){inspectionClock-=.066;inspectionArtTick++;foreach(var e in inspectionArt.Values)e.Body.Tick(e.Angle);}}
  if(GetWindow().HasFocus()){var movement=new Vector2((Input.IsKeyPressed(Key.Right)?1:0)-(Input.IsKeyPressed(Key.Left)?1:0),(Input.IsKeyPressed(Key.Down)?1:0)-(Input.IsKeyPressed(Key.Up)?1:0));if(movement!=Vector2.Zero)camera+=movement.Normalized()*600*(float)Math.Min(delta,.1)/zoom;}else dragging=false;
  ClampInspection();QueueRedraw();
 }
 void OriginalInput(InputEvent input) {
  if(input is InputEventKey k&&k.Pressed&&!k.Echo){inputEvents++;switch(k.Keycode){
   case Key.Escape:GetTree().Quit();break;
   case Key.R:ResetInspection();break;
   case Key.P:inspectionPreview=!inspectionPreview;inspectionClock=0;break;
   case Key.C:ChooseArtwork(nativeSampling==0?ArtworkStyle.Classic:ArtworkStyle.Crisp);break;
   case Key.F:camera=new(nativeData.Scenario.Map.Width*16,nativeData.Scenario.Map.Height*16);zoom=Math.Min(NativeArea.Size.X/(nativeData.Scenario.Map.Width*32),NativeArea.Size.Y/(nativeData.Scenario.Map.Height*32));break;
   case Key.Tab:var landmarks=nativeWorld.Entities.Where(e=>(e.Slot<120&&e.Slot%15<6)||e.Type is 40 or 86 or 89).ToArray();var target=landmarks[inspectionLandmark++%landmarks.Length];camera=NativePosition(target);nativeSelection.Clear();nativeSelection.Add((target.Slot,target.Incarnation));break;
   case Key.Bracketleft:ui=Math.Max(.75f,ui-.125f);break;case Key.Bracketright:ui=Math.Min(1.5f,ui+.125f);break;
  }}
  if(input is InputEventMouseButton b){if(b.ButtonIndex==MouseButton.Middle)dragging=b.Pressed&&NativeArea.HasPoint(b.Position);if(b.Pressed&&NativeArea.HasPoint(b.Position)){
   inputEvents++;var world=(b.Position-NativeOffset)/zoom;
   if(b.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown){zoom=Math.Clamp(zoom*(b.ButtonIndex==MouseButton.WheelUp?1.25f:.8f),.25f,4);camera=world-(b.Position-NativeArea.GetCenter())/zoom;}
   if(b.ButtonIndex==MouseButton.Left){nativeSelection.Clear();var e=nativeWorld.Entities.OrderBy(e=>NativePosition(e).DistanceSquaredTo(world)).FirstOrDefault();if(e!=null&&NativePosition(e).DistanceTo(world)<60)nativeSelection.Add((e.Slot,e.Incarnation));}
  }}
  if(input is InputEventMouseMotion m&&dragging){if((m.ButtonMask&MouseButtonMask.Middle)==0)dragging=false;else camera-=m.Relative/zoom;}
  ClampInspection();QueueRedraw();
 }
 void DrawOriginalState()=>DrawNative();
 void DrawInspection() {
  var size=GetViewportRect().Size;DrawRect(new(0,0,size.X,86*ui),panel);DrawRect(new(0,size.Y-96*ui,size.X,96*ui),panel);
  Text("OpenDC / INITIALIZATION INSPECTION / "+nativeData.Scenario.MapName,new(24,29),22,accent);
  Text($"SIMULATION PAUSED AT TICK 0  |  OMNISCIENT  |  {(inspectionPreview?"ART PREVIEW":"ART FROZEN")} {inspectionArtTick}  |  NOT PLAYABLE",new(24,61),16,ink);
  float y=size.Y/ui-69;Text("Arrows / middle-drag: pan   Wheel: zoom   Tab: landmarks   F: fit map   R: reload   C: Classic/Crisp   P: art preview   [ / ]: UI",new(24,y),15,ink);
  Text($"{nativeWorld.Entities.Count()} initialized objects / {nativeWorld.Session.Sources.Length} registered sources / {nativeWorld.Session.ScriptBlocks.Count(b=>b!=null)} script blocks. Opening delivery, AI and economy have NOT run.",new(24,y+25),15,muted);
  var selected=nativeSelection.Select(p=>nativeWorld.Slots[p.Slot]).FirstOrDefault();
  Text(selected==null?"Profile/rank: unspecified inspection context. Click an object to inspect identity and ownership.":$"ID {selected.Slot}  {nativeData.Scenario.Types[selected.Type].Sprite}  team {selected.Owner}  HP {selected.Health}  world ({selected.X},{selected.Z})  {inspectionArt[selected.Slot].Body.Set?.Name}",new(24,y+50),15,accent);
 }
 object InspectionSnapshot()=>new{kind="post-initialization, before first update; presentation clock only",scenario=originalScenario,nativeWorld.Tick,inspectionArtTick,simulationPaused=true,profile=nativeWorld.Session.Profile,teams=nativeWorld.Teams.Select(t=>new{t.Id,t.Race,t.Money,t.Ai,t.Color,t.Base,t.Rally,t.Allies,t.AiAssignments,t.CommanderRecoveryDisabled,t.AutomaticIncome,t.CommanderIds,t.ProductionIds,t.WeaponTiers,t.ArmorTiers,t.DisabledDependencies,t.AvailabilityCache,cities=t.Cities.Select(c=>new{c.NativeId,c.Level,c.InitializationPending,c.Health,owner=c.Entity?.Owner})}),sourceManifest=nativeData.Scenario.Sources.Select(s=>new{s.Path,s.Sha256}),entities=nativeWorld.Entities.Select(e=>new{e.Slot,e.Type,e.Owner,e.X,e.Z,e.Health,e.Angle,e.ResourceFlow,logical=new[]{NativePosition(e).X,NativePosition(e).Y},drawn=new[]{NativeRenderPosition(e).Body.X,NativeRenderPosition(e).Body.Y},authoritativeMode=e.Body.Mode,actions=e.Actions.Select(a=>a.Kind),animation=inspectionArt[e.Slot].Body.Set?.Name,frame=inspectionArt[e.Slot].Body.Frame,delay=inspectionArt[e.Slot].Body.Delay}),sources=nativeData.Scenario.SpawnSources,scripts=nativeWorld.Session.ScriptBlocks.Where(b=>b!=null).Select(b=>new{b!.Id,b.Remaining,b.Condition,actions=b.Actions.Select(a=>a.SourceText)}),gaps=nativePresentationGaps};
 void WriteInspection()=>File.WriteAllText(Path.Combine(output,"initialization-state.json"),JsonSerializer.Serialize(InspectionSnapshot(),new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
 public async void OriginalStateSuite(){try{
  await NativeCapture("01-hive");WriteInspection();
  camera=new(nativeData.Scenario.Map.Width*16,nativeData.Scenario.Map.Height*16);zoom=Math.Min(NativeArea.Size.X/(nativeData.Scenario.Map.Width*32),NativeArea.Size.Y/(nativeData.Scenario.Map.Height*32));await NativeCapture("02-map");
  camera=NativePosition(nativeWorld.Entities.FirstOrDefault(e=>e.Slot<120&&e.Slot%15<6)??nativeWorld.Entities.First());zoom=1.5f;camera.Y-=80;ClampInspection();
  await NativeCapture("03-city-composition");
  await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);await NativeCapture("04-city-animation");
  await Send(Key.P);long frozen=inspectionArtTick;await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);Check(inspectionArtTick==frozen&&nativeWorld.Tick==0,"art pause leaves authoritative tick0");
  int sampling=nativeSampling;await Send(Key.C);Check(nativeSampling!=sampling&&nativeWorld.Tick==0,"artwork switch is presentation-only");await Send(Key.C);
  nativeWorld.Teams[0].Money=123;await Send(Key.R);Check(nativeWorld.Teams[0].Money==nativeData.Scenario.Teams[0].Money&&nativeWorld.Tick==0&&inspectionArtTick==0,"Godot-dispatched R rebuilds run-owned state");
  File.WriteAllText(Path.Combine(output,"inspection-input-checks.json"),JsonSerializer.Serialize(checks));
  WriteInspection();if(nativePresentationGaps.Count>0)throw new InvalidOperationException(string.Join(";",nativePresentationGaps));GD.Print("INITIALIZATION_CAPTURE_PASS");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
