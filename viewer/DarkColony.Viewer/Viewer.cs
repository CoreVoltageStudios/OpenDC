using Godot;
using DarkColony.Assets;
using System.Text.Json;
namespace DarkColony.Viewer;

public partial class Viewer : Node2D {
 OriginalVisuals visuals=null!;
 readonly Dictionary<string,ImageTexture> textures=new();
 readonly List<(Rect2 Rect,string Action,string Label)> buttons=new();
 readonly List<object> checks=new();
 Vector2 camera=new(1024,896),unit=new(1024,896);
 float zoom=2,ui=1,fps=8;int frame;bool paused=true,grid,coords,bounds=true,embedded,dragging,capturing;
 double clock;string output="",root="";Font font=null!;int inputEvents,captureIndex;bool ready,overlap,terrainRegression;JsonElement fixture;string fixturePosition="";ImageTexture? bodyTexture;string bodyKey="";
 int MapWidth=>visuals.Map.Width*32;int MapHeight=>visuals.Map.Height*32;
 Rect2 Area=>new(0,140*ui,GetViewportRect().Size.X,Math.Max(1,GetViewportRect().Size.Y-216*ui));
 Vector2 Offset=>Area.GetCenter()-camera*zoom;
 readonly Color ink=new("d9e5e1"),muted=new("9db5ae"),accent=new("a9d179"),panel=new("142320");
 public override async void _Ready() {
  try {
   if(OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--engine-notices=")) is {} noticeArg){WriteEngineNotices(noticeArg[17..]);GetTree().Quit();return;}
   if(OS.GetCmdlineUserArgs().Contains("--departure-lab")){OS.AddLogger(failureLogger);AddChild(new WarDepartureLab());return;}
   if(OS.GetCmdlineUserArgs().Contains("--network-lab")){OS.AddLogger(failureLogger);AddChild(new WarNetworkLab());return;}
   ConfigureWindowsIdentity();
   if(OS.GetCmdlineUserArgs().Contains("--movie-performance")){OS.AddLogger(failureLogger);AddChild(new MoviePerformanceLab());return;}
   if(OS.GetCmdlineUserArgs().Contains("--video-lab")){OS.AddLogger(failureLogger);AddChild(new VideoPlaybackLab());return;}
   if(!OS.HasFeature("editor")){await StartStandalone();return;}
   if(!OS.GetCmdlineUserArgs().Contains("--development-content")){await StartImportedContent();return;}
   root=Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"),"../.."));
   var args=OS.GetCmdlineUserArgs();string Arg(string prefix,string fallback)=>args.FirstOrDefault(x=>x.StartsWith(prefix))?[prefix.Length..]??fallback;
   playMode=args.Contains("--play");laneMode=args.Contains("--lane");terrainSelectorLab=args.Contains("--terrain-selectors");paletteLab=args.Contains("--palette-lab");terrainRegression=args.Contains("--terrain-regression");overlap=args.Contains("--overlap")||paletteLab||terrainSelectorLab;capturing=args.Contains("--capture");output=Path.GetFullPath(Arg("--output=",Path.Combine(root,"generated/interactive/manual")));Directory.CreateDirectory(output);
   OS.AddLogger(failureLogger);GetWindow().Title="OpenDC | Research Viewer";
   if(capturing) {
    var size=Arg("--size=","1920x1080").Split('x');var target=new Vector2I(int.Parse(size[0]),int.Parse(size[1]));
    // Native window may be smaller than the display target. Godot renders a real target-sized framebuffer.
    GetWindow().ContentScaleSize=target;GetWindow().ContentScaleMode=Window.ContentScaleModeEnum.Viewport;
    GetWindow().ContentScaleAspect=Window.ContentScaleAspectEnum.Keep;
    GetWindow().Size=new Vector2I(1280,720);
   }
   else {GetWindow().ContentScaleMode=Window.ContentScaleModeEnum.Disabled;}
   TextureFilter=TextureFilterEnum.Nearest; font=ThemeDB.FallbackFont;
   bool humanLaunch=args.Any(a=>a.StartsWith("--human-mission=")||a.StartsWith("--council=")||a.StartsWith("--training="));
   bool menuLaunch=args.Contains("--main-menu")||(!args.Any(a=>a.StartsWith("--war="))&&!humanLaunch&&!playMode&&!overlap&&!args.Contains("--original-core")&&!args.Contains("--first-strike")&&!args.Contains("--interference")&&!args.Contains("--legacy-revealed")&&!args.Contains("--breakthrough")&&!(args.Contains("--earth-intervenes")||args.Contains("--then-there-was-one"))&&!args.Contains("--spider-canyon")&&!args.Contains("--animal-instinct")&&!args.Contains("--final-evacuation")&&!args.Contains("--the-answer")&&!args.Contains("--buried-treasure")&&!args.Contains("--screamer")&&!args.Contains("--civil-war")&&!args.Contains("--puppet-masters")&&!args.Contains("--roswell")&&!args.Contains("--original-state")&&!args.Contains("--interference-dev"));
   developmentProfile=menuLaunch?"Campaign":"Direct";if(capturing&&IsolatedPlayerData==null)GD.Print("PLAYER_DATA capture output "+output);else ReportPlayerData();
   if(menuLaunch&&args.Contains("--studio-intro-check")&&capturing){StudioIntroCheck(args);return;}
   if(menuLaunch){PrepareArtworkPreference(args);if(!capturing&&!args.Contains("--skip-intro")&&!reducedMotion){BeginStudioIntro();await EndStudioIntro();}PrepareNative(args.Append("--main-menu").ToArray());return;}
   if(args.Any(a=>a.StartsWith("--war="))||humanLaunch||args.Contains("--original-core")||args.Contains("--first-strike")||args.Contains("--interference")||args.Contains("--legacy-revealed")||args.Contains("--breakthrough")||args.Contains("--roswell")||(args.Contains("--earth-intervenes")||args.Contains("--then-there-was-one"))||args.Contains("--spider-canyon")||args.Contains("--animal-instinct")||args.Contains("--final-evacuation")||args.Contains("--the-answer")||args.Contains("--buried-treasure")||args.Contains("--screamer")||args.Contains("--civil-war")||args.Contains("--puppet-masters")){PrepareNative(args);return;}
   if(args.Contains("--original-state")){PrepareOriginalState(args);return;}
   if(args.Contains("--interference-dev")){PrepareInterference(args);return;}
   if(playMode) {PreparePlay(args);if(args.Contains("--squad")||args.Contains("--combat"))PrepareSquad();if(args.Contains("--combat"))PrepareCombat(args);}
   else if(overlap) {
    fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"fixtures/foreground-human04.json"))).RootElement.Clone();
    visuals=new OriginalVisuals(Path.Combine(root,"private/DC"),fixture.GetProperty("map").GetString()!,fixture.GetProperty("terrain").GetString()!);
    camera=new(fixture.GetProperty("camera")[0].GetSingle(),fixture.GetProperty("camera")[1].GetSingle());unit=new(1184,816);zoom=fixture.GetProperty("zoom").GetSingle();bounds=false;
   } else visuals=new OriginalVisuals(Path.Combine(root,"private/DC"));ready=true;
   GD.Print($"VIEWER_READY Godot={Engine.GetVersionInfo()} CLR={System.Environment.Version} screen={DisplayServer.ScreenGetSize()} capture={capturing}");
   File.WriteAllText(Path.Combine(output,"sources.json"),JsonSerializer.Serialize(visuals.Sources,new JsonSerializerOptions{WriteIndented=true}));
   if(paletteLab)PreparePaletteLab();
   if(terrainSelectorLab)PrepareTerrainSelectorLab();
   Log("ready");QueueRedraw();
   if(args.Contains("--headless-check")){CallDeferred(MethodName.HeadlessEffectsCheck);return;}
   if(capturing)CallDeferred(playMode?MethodName.MovementSuite:terrainSelectorLab?MethodName.SelectorSuite:paletteLab?MethodName.PaletteSuite:overlap?MethodName.OverlapSuite:MethodName.CaptureSuite);
  } catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
 }
 ImageTexture Texture(string id) {
  if(textures.TryGetValue(id,out var t))return t;
  var p=visuals.Resolve(id).Pixels;var image=Image.CreateFromData(p.Width,p.Height,false,Image.Format.Rgba8,p.Rgba);
  t=ImageTexture.CreateFromImage(image);textures[id]=t;return t;
 }
 void DrawAsset(string id,Vector2 anchor) {
  var v=visuals.Resolve(id);DrawTextureRect(Texture(id),new Rect2(anchor-new Vector2(v.OriginX,v.OriginY),new Vector2(v.LogicalWidth,v.LogicalHeight)),false);
 }
 void DrawGroundTrooper() {
  var v=visuals.Resolve(Trooper);int ax=(int)unit.X,ay=(int)unit.Y;
  string key=$"{Trooper}:{ax}:{ay}";
  if(key!=bodyKey) {
   var raster=visuals.Occlusion.ApplyGroundBody(v,ax,ay,ay+visuals.BottomOffset(frame));
   using var image=Image.CreateFromData(raster.Width,raster.Height,false,Image.Format.Rgba8,raster.Rgba);
   bodyTexture?.Dispose();bodyTexture=ImageTexture.CreateFromImage(image);bodyKey=key;
  }
  DrawTextureRect(bodyTexture!,new Rect2(new Vector2(ax-v.OriginX,ay-v.OriginY),new(v.LogicalWidth,v.LogicalHeight)),false);
 }
 string Trooper=>$"trsc/move0/{frame}/"+(embedded?"embedded":"scene");
 public override void _Draw() {
  if(!ready)return;if(nativeMode){if(firstStrike||interferenceDev)BeginRelayControls();DrawNative();if(firstStrike||interferenceDev)EndRelayControls();return;}if(originalStateMode){DrawOriginalState();return;}if(playMode){DrawPlay();return;}if(terrainSelectorLab){DrawTerrainSelectorLab();return;}if(paletteLab){DrawPaletteLab();return;}ClampCamera();var size=GetViewportRect().Size;
  DrawRect(new Rect2(Vector2.Zero,size),new Color("091310"));
  DrawSetTransform(Offset,0,Vector2.One*zoom);
  DrawAsset("map/background",Vector2.Zero);
  // 0x4547fc builds terrain first, then the body blitter consults the foreground mask.
  DrawAsset("map/foreground",Vector2.Zero);
  DrawGroundTrooper();
  DrawRect(new Rect2(0,0,MapWidth,MapHeight),accent,false,2/zoom);
  if(grid) {
   for(int x=0;x<=visuals.Map.Width;x++)DrawLine(new(x*32,0),new(x*32,MapHeight),new Color(0.85f,1,0.85f,.2f),1/zoom);
   for(int y=0;y<=visuals.Map.Height;y++)DrawLine(new(0,y*32),new(MapWidth,y*32),new Color(0.85f,1,0.85f,.2f),1/zoom);
  }
  if(coords)for(int x=0;x<visuals.Map.Width;x+=4)for(int z=0;z<visuals.Map.Height;z+=4)
   DrawString(font,new(x*32+3,(visuals.Map.Height-1-z)*32+14),$"{x},{z}",HorizontalAlignment.Left,-1,Math.Max(8,(int)(14/zoom)),new Color(1,1,1,.7f));
  if(bounds){var v=visuals.Resolve(Trooper);DrawRect(new Rect2(unit-new Vector2(v.OriginX,v.OriginY),new(v.LogicalWidth,v.LogicalHeight)),new Color("f0b97b"),false,1/zoom);DrawLine(unit-new Vector2(6,0),unit+new Vector2(6,0),Colors.Magenta,1/zoom);DrawLine(unit-new Vector2(0,6),unit+new Vector2(0,6),Colors.Magenta,1/zoom);}
  DrawSetTransform(Vector2.Zero);DrawHud(size);
 }
 void Text(string text,Vector2 at,int size,Color color)=>DrawString(font,at*ui,text,HorizontalAlignment.Left,-1,(int)(size*ui),color);
 void DrawHud(Vector2 size) {
  buttons.Clear();DrawRect(new(0,0,size.X,140*ui),panel);
  Text("OpenDC",new(24,34),26,accent);Text(overlap?"HUMAN04   /   FOREGROUND FIXTURE":"MARTIAN ZOO   /   PRESENTATION LAB",new(244,32),18,muted);
  Text($"{(paused?"PAUSED":"PLAYING")}   FRAME {frame+1}/8   FIN {16+frame}   WORLD {zoom:0.00}x   UI {ui:0.00}x",new(24,64),18,ink);
  float x=24;
  void Button(string action,string label,float w){var r=new Rect2(new Vector2(x,82)*ui,new Vector2(w,36)*ui);buttons.Add((r,action,label));DrawRect(r,new Color("263b32"));DrawRect(r,new Color("486450"),false);Text(label,new(x+10,106),16,ink);x+=w+8;}
  Button("pause",paused?"Play [Space]":"Pause [Space]",132);Button("prev","< [,]",62);Button("next","> [.]",62);
  Button("slower","- FPS",70);Button("faster",$"+ FPS  {fps:0}",104);
  Button("grid",$"Grid {(grid?"ON":"off")} [G]",122);Button("coords",$"Coords {(coords?"ON":"off")} [C]",140);Button("bounds",$"Bounds {(bounds?"ON":"off")} [B]",146);
  Button("palette",embedded?"Raw SPR [P]":"DC16 T0 L16 [P]",160);Button("fit","Fit [F]",76);Button("reset","Reset [R]",100);Button("ui-","UI -",58);Button("ui+","UI +",58);
  // Inspector is presentation-only. Native source texels drawn at exactly 4 UI pixels per logical pixel.
  float right=size.X/ui-274;
  DrawRect(new Rect2(new Vector2(right,158)*ui,new Vector2(250,274)*ui),new Color(.05f,.09f,.08f,.95f));
  Text("TRSC / SECURITY TROOPER",new(right+14,185),16,accent);
  var visual=visuals.Resolve(Trooper);var anchor=new Vector2(right+116,380)*ui;
  DrawTextureRect(Texture(Trooper),new Rect2(anchor-new Vector2(visual.OriginX,visual.OriginY)*4*ui,new Vector2(visual.LogicalWidth,visual.LogicalHeight)*4*ui),false);
  DrawLine(anchor-new Vector2(7,0),anchor+new Vector2(7,0),Colors.Magenta);DrawLine(anchor-new Vector2(0,7),anchor+new Vector2(0,7),Colors.Magenta);
  Text("4x logical size / fixed anchor",new(right+14,414),14,muted);
  float y=size.Y/ui-76;DrawRect(new(0,y*ui,size.X,76*ui),panel);
  Text("Middle/right drag or WASD: pan   Wheel / +/-: zoom   Click terrain: place unit   F12: capture   Esc: quit",new(24,y+24),17,ink);
  Text(embedded?"Raw SPR6 comparison, provisional scaling; terrain SCN S / bypass level16 RGB565. FPS provisional; shadow omitted.":"Static DC16 colours: JUNGLE.GIF / RGB565 / body T0 L16; terrain SCN initial S / bypass level16. FPS provisional; shadow omitted.",new(24,y+51),17,new Color("e6bc84"));
 }
 void ClampCamera() {
  var half=Area.Size/(2*zoom);
  camera.X=half.X>=MapWidth/2f?MapWidth/2f:Math.Clamp(camera.X,half.X,MapWidth-half.X);
  camera.Y=half.Y>=MapHeight/2f?MapHeight/2f:Math.Clamp(camera.Y,half.Y,MapHeight-half.Y);
 }
 Vector2 World(Vector2 screen)=>(screen-Offset)/zoom;
 void Zoom(float multiplier,Vector2 screen){var before=World(screen);zoom=Math.Clamp(zoom*multiplier,.25f,8);camera=before-(screen-Area.GetCenter())/zoom;ClampCamera();}
 void Action(string action) {
  switch(action){
   case "pause":paused=!paused;clock=0;break;
   case "prev":paused=true;frame=(frame+7)%8;clock=0;break;
   case "next":paused=true;frame=(frame+1)%8;clock=0;break;
   case "slower":fps=Math.Max(1,fps-1);break;case "faster":fps=Math.Min(30,fps+1);break;
   case "grid":grid=!grid;break;case "coords":coords=!coords;break;case "bounds":bounds=!bounds;break;case "palette":embedded=!embedded;break;
   case "fit":zoom=Math.Min(Area.Size.X/MapWidth,Area.Size.Y/MapHeight);camera=new(MapWidth/2f,MapHeight/2f);break;
   case "reset":camera=overlap?new(1184,832):new(MapWidth/2f,MapHeight/2f);unit=camera;zoom=2;break;
   case "ui-":ui=Math.Max(.75f,ui-.125f);break;case "ui+":ui=Math.Min(1.125f,ui+.125f);break;
  }
  ClampCamera();Log("action:"+action);QueueRedraw();
 }
 public override void _UnhandledInput(InputEvent e) {
  if(!ready)return;
  if(nativeMode){NativeInput(e);return;}
  if(originalStateMode){OriginalInput(e);return;}
  if(playMode){PlayInput(e);return;}
  if(terrainSelectorLab){SelectorInput(e);return;}
  if(paletteLab){PaletteInput(e);return;}
  if(e is InputEventKey k && k.Pressed && !k.Echo) {
   inputEvents++; var action=k.Keycode switch {Key.Space=>"pause",Key.Comma=>"prev",Key.Period=>"next",Key.G=>"grid",Key.C=>"coords",Key.B=>"bounds",Key.P=>"palette",Key.F=>"fit",Key.R=>"reset",Key.Bracketleft=>"slower",Key.Bracketright=>"faster",_=>""};
   if(action!="")Action(action);
   else if(k.Keycode==Key.Escape)GetTree().Quit();
   else if(k.Keycode==Key.F12)CaptureManual();
   else if(k.Keycode is Key.Equal or Key.Plus or Key.KpAdd){Zoom(1.25f,Area.GetCenter());Log("key:zoom-in");}
   else if(k.Keycode is Key.Minus or Key.KpSubtract){Zoom(.8f,Area.GetCenter());Log("key:zoom-out");}
   else {var delta=k.Keycode switch{Key.A or Key.Left=>new Vector2(-64,0),Key.D or Key.Right=>new Vector2(64,0),Key.W or Key.Up=>new Vector2(0,-64),Key.S or Key.Down=>new Vector2(0,64),_=>Vector2.Zero};camera+=delta;ClampCamera();Log("key:pan");}
   QueueRedraw();
  }
  if(e is InputEventMouseButton m) {
   if(m.ButtonIndex is MouseButton.Middle or MouseButton.Right){dragging=m.Pressed;inputEvents++;}
   if(m.Pressed && m.ButtonIndex==MouseButton.Left){inputEvents++;var button=buttons.FirstOrDefault(b=>b.Rect.HasPoint(m.Position));if(button.Action!=null)Action(button.Action);else if(Area.HasPoint(m.Position) && m.Position.X<GetViewportRect().Size.X-280*ui){var p=World(m.Position);unit=new(Math.Clamp((int)p.X,0,MapWidth-1),Math.Clamp((int)p.Y,0,MapHeight-1));Log("click:unit");}}
   if(m.Pressed && Area.HasPoint(m.Position) && m.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown){inputEvents++;Zoom(m.ButtonIndex==MouseButton.WheelUp?1.25f:.8f,m.Position);Log("wheel:zoom");}
   QueueRedraw();
  }
  if(e is InputEventMouseMotion motion && dragging){inputEvents++;camera-=motion.Relative/zoom;ClampCamera();Log("drag:pan");QueueRedraw();}
 }
 public override void _Process(double delta){ProcessContentSetup();ProcessLandfall(delta);if(ready)ProcessNetwork(delta);DrainRetiredMissionAudio();ApplyAspectLimit();UpdateGodModeIndicator();if(CheckEngineFailure())return;if(!ready)return;if(nativeMode){PerfSample(delta);long perfStart=perfActive?System.Diagnostics.Stopwatch.GetTimestamp():0;perfSimulationMs=0;ProcessNative(delta);if(perfActive)perfProcessMs=PerfElapsed(perfStart);return;}if(originalStateMode || paletteLab || terrainSelectorLab)return;if(playMode){ProcessPlay(delta);return;}if(!paused){clock+=delta;while(clock>=1/fps){clock-=1/fps;frame=(frame+1)%8;}QueueRedraw();}}
 object State(string name)=>new{name,map=visuals.MapId,fixturePosition,bodyBottom=(int)unit.Y+visuals.BottomOffset(frame),frame,paused,fps,zoom,ui,camera=new[]{camera.X,camera.Y},unit=new[]{unit.X,unit.Y},viewport=new[]{GetViewportRect().Size.X,GetViewportRect().Size.Y},area=new[]{Area.Position.X,Area.Position.Y,Area.Size.X,Area.Size.Y},visibleWorld=new[]{Area.Size.X/zoom,Area.Size.Y/zoom},grid,coords,bounds,embedded,inputEvents,renderer=RenderingServer.GetVideoAdapterName(),display=DisplayServer.GetName()};
 void Log(string name){var json=JsonSerializer.Serialize(State(name));GD.Print(json);File.AppendAllText(Path.Combine(output,"events.jsonl"),json+System.Environment.NewLine);File.WriteAllText(Path.Combine(output,"latest-state.json"),json);}
 async Task Save(string name) {
  QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
  using var image=GetViewport().GetTexture().GetImage();var err=image.SavePng(Path.Combine(output,name+".png"));if(err!=Error.Ok)throw new IOException(err.ToString());
  File.WriteAllText(Path.Combine(output,name+".json"),JsonSerializer.Serialize(State(name),new JsonSerializerOptions{WriteIndented=true}));GD.Print($"CAPTURE {name} {image.GetWidth()}x{image.GetHeight()}");
 }
 async void CaptureManual(){try{await Save($"manual-{captureIndex++:D2}");}catch(Exception e){GD.PushError(e.ToString());}}
 void Check(bool passed,string name){checks.Add(new{name,passed});if(!passed)throw new Exception("Verification failed: "+name);}
 async Task Send(Key key){Input.ParseInputEvent(new InputEventKey{Keycode=key,Pressed=true});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Input.ParseInputEvent(new InputEventKey{Keycode=key,Pressed=false});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
 public async void OverlapSuite() {
  try {
   for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   paused=true;grid=false;coords=false;bounds=false;
   foreach(var pos in fixture.GetProperty("positions").EnumerateArray()) {
    fixturePosition=pos.GetProperty("name").GetString()!;
    unit=new(pos.GetProperty("x").GetInt32(),pos.GetProperty("y").GetInt32());
    for(frame=0;frame<8;frame++)await Save($"{fixturePosition}-{frame:D2}");frame=0;
   }
   fixturePosition="overlay";unit=new(1184,816);bounds=true;grid=true;await Save("overlay");
   int start=inputEvents;await Send(Key.Period);Check(frame==1 && inputEvents>start,"fixture stepping through Godot input");
   await Send(Key.B);Check(!bounds,"fixture bounds toggle through Godot input");
   File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
   GD.Print("OVERLAP_SUITE_PASS");GetTree().Quit();
  }catch(Exception e){GD.PushError(e.ToString());File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());GetTree().Quit(1);}
 }
 public async void CaptureSuite() {
  try {
   for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   if(terrainRegression) {
    paused=true;frame=0;grid=false;coords=false;bounds=false;zoom=4;unit=new(112,1328);camera=new(240,1328);
    await Save("martian-zoo-regression");GD.Print("TERRAIN_REGRESSION_PASS");GetTree().Quit();return;
   }
   paused=true;frame=0;camera=new(1024,896);zoom=2;await Save("center");
   for(frame=0;frame<8;frame++)await Save($"animation-{frame:D2}");frame=0;
   // Deterministic setup is separate from these engine-injected input events, which traverse _UnhandledInput.
   int start=inputEvents;await Send(Key.Period);Check(frame==1&&paused&&inputEvents>start,"Godot input stepping");
   await Send(Key.Space);Check(!paused,"Godot input play");await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);Check(frame!=1,"animation advances with elapsed time");await Send(Key.Space);Check(paused,"Godot input pause");
   int frozen=frame;await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);Check(frame==frozen,"paused frame stable");
   var before=camera;await Send(Key.D);Check(camera.X>before.X,"Godot input pan");float old=zoom;await Send(Key.Equal);Check(zoom>old,"Godot input zoom");
   await Send(Key.Bracketright);Check(fps==9,"Godot input speed");await Send(Key.G);Check(grid,"Godot input grid");await Save("input-result");
   await Send(Key.C);Check(coords,"Godot input coordinates");await Send(Key.C);
   await Send(Key.B);Check(!bounds,"Godot input bounds");await Send(Key.B);
   await Send(Key.P);Check(embedded,"Godot input palette");await Send(Key.P);
   Action("reset");frame=0;paused=true;grid=false;coords=false;
   var z=zoom;Action("ui+");Check(zoom==z,"UI scale independent of zoom");await Save("ui-large");Action("ui-");
   Action("fit");await Save("map-edges");
   zoom=2;camera=new(-10000,-10000);ClampCamera();
   Check(World(Area.Position).IsEqualApprox(Vector2.Zero),"camera clamps at upper-left map edge");await Save("edge-upper-left");
   camera=new(10000,10000);ClampCamera();
   Check(World(Area.End).IsEqualApprox(new Vector2(2048,1792)),"camera clamps at lower-right map edge");await Save("edge-lower-right");
   zoom=3;unit=new(3*32+16,(55-14)*32+16);camera=unit;ClampCamera();await Save("foreground-interaction");
   var original=visuals.Resolve("trsc/move0/0/terrain");var replacement=original with{Pixels=new Raster(original.Pixels.Width*2,original.Pixels.Height*2,Array.Empty<byte>())};
   Check(original.LogicalWidth==replacement.LogicalWidth && original.OriginX==replacement.OriginX,"replacement resolution does not change footprint (metadata test)");
   File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));GD.Print("CAPTURE_SUITE_PASS");GetTree().Quit();
  }catch(Exception e){GD.PushError(e.ToString());File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());GetTree().Quit(1);}
 }
}
