using Godot;
using DarkColony.ContentImport;
using System.Text.Json;
namespace DarkColony.Viewer;

public partial class RestorationCharge : Control {
 public bool Reduced,Awake,Complete;public double Time,WakeTime;public Vector2 Tip;
 public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;TextureFilter=TextureFilterEnum.Linear;}
 public override void _Draw(){
  float u=Math.Min(Size.X/1920,Size.Y/1080);var p=Tip==Vector2.Zero?new Vector2(Size.X*.72f,Size.Y*.36f):Tip;
  float power=Complete?0:Awake?Math.Clamp(1-(float)WakeTime/2.5f,0,1):1;
  if(power<=0)return;float breath=Reduced?1:1+.06f*MathF.Sin((float)Time*MathF.Tau/8);
  for(int i=5;i>0;i--)DrawCircle(p,(7+i*6)*u*breath,new Color(.3f,.7f,1,.025f*power));DrawCircle(p,5.5f*u,new Color(.83f,.96f,1,power));
  if(!Reduced){float a=(float)Time*.14f;DrawArc(p,24*u,a,a+1.22f,24,new Color(.56f,.89f,1,.4f*power),u,true);DrawArc(p,31*u,-a,-a+.73f,24,new Color(.56f,.89f,1,.25f*power),u,true);
   if(Awake&&WakeTime<.9&&WakeTime>.55){var end=p+new Vector2(0,270)*u;for(int i=0;i<8;i++){var q=p.Lerp(end,i/8f);var r=p.Lerp(end,(i+1)/8f);q.X+=MathF.Sin(i*17+(float)Time*30)*8*u;r.X+=MathF.Sin((i+1)*17+(float)Time*30)*8*u;DrawLine(q,r,new Color("8fe3ff"),2*u,true);}}
  }
 }
}
public partial class Viewer {
 bool reducedMotion,restorationFirstRun,restorationSkipped,restorationReplay,restorationPendingMenu;
 ColorRect? restorationScrim;Control? restorationRoot;ColonyScene? restorationColony,menuColony;RestorationCharge? restorationCharge;
 Label? restorationLine,restorationFacts,restorationSkip,restorationMessage;AudioStreamPlayer? restorationSound,restorationHum;
 Task<ColonyScene.Art>? colonyTask;string colonySource="";double restorationClock,restorationContact=-1,restorationWake=-1;
 ImportStage restorationStage;bool restorationVerified;string restorationReceipt="";
 readonly Dictionary<string,AudioStreamWav> restorationOriginal=new();readonly HashSet<string> restorationPlayed=new();
 void OriginalRestorationSound(string name,float db){if(restorationRoot==null||!restorationOriginal.TryGetValue(name,out var sound)||!restorationPlayed.Add(name)||restorationSkipped)return;var player=new AudioStreamPlayer{Stream=sound,VolumeDb=db+Mathf.LinearToDb(Math.Max(.0001f,sfxVolume))};restorationRoot.AddChild(player);player.Finished+=()=>player.QueueFree();player.Play();}
 // OpenDC owns this preference. Missing/invalid values mean normal animation;
 // existing explicit booleans remain authoritative, independent of Windows.
 void LoadMotionPreference(){
  reducedMotion=false;
  try{using var d=JsonDocument.Parse(File.ReadAllText(artworkPreferencePath));if(d.RootElement.TryGetProperty("reducedMotion",out var v)&&v.ValueKind is JsonValueKind.True or JsonValueKind.False)reducedMotion=v.GetBoolean();}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException){}
 }
 void BeginRestorationHold(bool replay=false){
  EndRestoration();restorationReplay=replay;restorationFirstRun=!replay;restorationSkipped=false;restorationVerified=false;restorationClock=0;restorationContact=restorationWake=-1;colonyTask=null;
  restorationRoot=new Control{Name="Restoration",ZIndex=450,MouseFilter=Control.MouseFilterEnum.Ignore};AddChild(restorationRoot);restorationRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
  var bg=new ColorRect{Color=OdcUiTokens.Ground,MouseFilter=Control.MouseFilterEnum.Ignore};restorationRoot.AddChild(bg);bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
  restorationColony=new ColonyScene{Reduced=reducedMotion};restorationRoot.AddChild(restorationColony);
  restorationCharge=new RestorationCharge{Reduced=reducedMotion};restorationRoot.AddChild(restorationCharge);
  restorationScrim=new ColorRect{Color=new Color(0,0,0,.55f),Visible=false,MouseFilter=Control.MouseFilterEnum.Ignore};restorationRoot.AddChild(restorationScrim);
  restorationLine=new Label{Text="The colony lives again.",HorizontalAlignment=HorizontalAlignment.Center,Visible=false};restorationLine.AddThemeFontOverride("font",LandfallFont);restorationLine.AddThemeFontSizeOverride("font_size",46);restorationRoot.AddChild(restorationLine);
  restorationFacts=new Label{HorizontalAlignment=HorizontalAlignment.Center,AutowrapMode=TextServer.AutowrapMode.WordSmart,Visible=false};restorationFacts.AddThemeFontOverride("font",LandfallFont);restorationRoot.AddChild(restorationFacts);
  restorationMessage=new Label{HorizontalAlignment=HorizontalAlignment.Center,Visible=false};restorationMessage.AddThemeFontOverride("font",LandfallFont);restorationMessage.AddThemeColorOverride("font_color",new Color("7fd06b"));restorationRoot.AddChild(restorationMessage);
  restorationSkip=new Label{Text="Space / Esc · Skip awakening",HorizontalAlignment=HorizontalAlignment.Right};restorationSkip.AddThemeFontOverride("font",LandfallFont);restorationRoot.AddChild(restorationSkip);
  restorationHum=new AudioStreamPlayer{Stream=GD.Load<AudioStream>("res://Branding/Landfall/cue-residual-hold.wav"),VolumeDb=-22+Mathf.LinearToDb(Math.Max(.0001f,sfxVolume))};restorationRoot.AddChild(restorationHum);restorationHum.Finished+=()=>{if(restorationWake<0&&restorationRoot!=null)restorationHum.Play();};restorationHum.Play();
 }
 void RestorationCue(string name,float db=-15){if(restorationRoot==null||reducedMotion&&name.Contains("contact"))return;restorationSound?.Stop();restorationSound?.QueueFree();restorationSound=new AudioStreamPlayer{Stream=GD.Load<AudioStream>("res://Branding/Landfall/cue-"+name+".wav"),VolumeDb=db+Mathf.LinearToDb(Math.Max(.0001f,sfxVolume))};restorationRoot.AddChild(restorationSound);restorationSound.Play();}
 void AcceptRestorationProgress(ImportProgress p){
  if(restorationRoot==null)return;if(p.Stage!=restorationStage){restorationStage=p.Stage;if(p.Stage==ImportStage.Copying)RestorationCue("charge-transfer");else if(p.Stage is ImportStage.Converting or ImportStage.Validating)RestorationCue("milestone",-20);}
  if(p.Presentation!=null&&colonyTask==null&&restorationContact<0){var snapshot=p.Presentation;colonyTask=Task.Run(()=>ColonyScene.Build(snapshot));}
  if(p.Stage is ImportStage.Failed or ImportStage.Cancelled){restorationVerified=false;restorationWake=restorationContact=-1;colonyTask=null;restorationColony?.Hide();restorationSound?.Stop();restorationSkipped=false;}
 }
 async Task VerifyRestorationCompletion(){
  if(restorationRoot==null||contentStore?.ActiveRoot==null)return;
  // Import's Complete is emitted AFTER full hash validation and activation. Recheck
  // the active generation before granting presentation/menu readiness.
  var errors=await Task.Run(()=>contentStore.Validate(false,CancellationToken.None));if(errors.Any(RequiredBaseProblem))throw new InvalidDataException("Activated content needs repair: "+string.Join(", ",errors.Take(4)));
  var receipt=contentStore.Receipt!;restorationReceipt=$"Dark Colony verified · Council Wars {(receipt.CouncilEdition==null?"not added":"verified")} · "+(receipt.Files.Any(f=>f.Path.StartsWith("generated/original-music/"))?"CD music installed":"CD music not installed");
  if(restorationContact<0&&colonyTask==null){string data=Path.Combine(contentStore.ActiveRoot,"private/DC");colonyTask=Task.Run(()=>ColonyScene.Build(PresentationSnapshot.ReadVerified(data)));}
  string dataRoot=Path.Combine(contentStore.ActiveRoot,"private/DC");
  var sounds=await Task.Run(()=>new[]{"ACTIVE","MSG","BARRACK","TRP1SEL","DCHIRP2","BUG","CHIGGERS"}.Select(n=>(Name:n,Sound:DarkColony.Assets.PcmWave.Decode(File.ReadAllBytes(Path.Combine(dataRoot,"SOUND",n+".WAV"))))).ToArray());
  foreach(var pair in sounds)restorationOriginal[pair.Name]=new AudioStreamWav{Data=pair.Sound.SignedSamples,Format=pair.Sound.Bits==8?AudioStreamWav.FormatEnum.Format8Bits:AudioStreamWav.FormatEnum.Format16Bits,MixRate=pair.Sound.SampleRate,Stereo=pair.Sound.Channels==2};
  var messages=await File.ReadAllLinesAsync(Path.Combine(dataRoot,"SCENARIO/HUMAN/HUMAN01.MSG"));int message=Array.FindIndex(messages,l=>l.Trim()=="text 3");restorationMessage!.Text=message>=0&&message+1<messages.Length?messages[message+1]:"";
  restorationVerified=true;
 }
 async void ReplayRestoration(){
  if(!mainMenu||importTask is {IsCompleted:false})return;
  ShowContentSetup();BeginRestorationHold(true);contentPanel!.Hide();try{await VerifyRestorationCompletion();}catch(Exception e){EndRestoration();contentPanel.Show();contentStatus.Text="Restoration unavailable: "+e.Message;}
 }
 void EndRestoration(bool keepColony=false){if(keepColony&&restorationVerified&&restorationColony is {ArtReady:true}){menuColony?.QueueFree();menuColony=restorationColony;menuColony.TransferTo(this);menuColony.ZIndex=-1;menuColony.Settled=true;menuColony.Awakening=false;menuColony.Contact=1;colonySource=contentStore?.ActiveRoot??root;restorationColony=null;}restorationSound?.Stop();restorationHum?.Stop();if(restorationRoot!=null)foreach(var p in restorationRoot.GetChildren().OfType<AudioStreamPlayer>()){p.Stop();p.Stream=null;}foreach(var s in restorationOriginal.Values)s.Dispose();restorationOriginal.Clear();restorationPlayed.Clear();restorationRoot?.QueueFree();restorationRoot=null;restorationScrim=null;restorationSound=restorationHum=null;restorationColony=null;restorationCharge=null;colonyTask=null;restorationWake=-1;restorationPendingMenu=false;}
 bool RestorationInput(InputEvent e){
  if(restorationRoot==null)return false;
  if(e is InputEventKey{Pressed:true,Echo:false} key&&((key.Keycode is Key.Escape or Key.Space)||restorationWake>=0)){
   if(GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit&&restorationWake<0)return false;
   if(restorationWake<0){restorationSkipped=!restorationSkipped;restorationSkip!.Text=restorationSkipped?"Awakening skipped · Import continues":"Space / Esc · Skip awakening";}
   else{restorationSkipped=true;restorationWake=restorationClock-.5;}return true;
  }if(restorationWake>=0&&e is InputEventMouseButton{Pressed:true}){restorationSkipped=true;restorationWake=restorationClock-.5;return true;}return false;
 }
 void ProcessLandfall(double delta){
  if(restorationRoot!=null){restorationClock+=delta;var size=GetViewportRect().Size;restorationRoot.Size=size;restorationColony!.Size=size;restorationCharge!.Size=size;restorationColony.Reduced=restorationCharge.Reduced=reducedMotion;
   if(colonyTask is {IsCompleted:true}){try{restorationColony.Install(colonyTask.GetAwaiter().GetResult());restorationColony.Show();restorationContact=restorationClock;RestorationCue("first-contact");}catch(Exception e){GD.Print("RESTORATION_ART_UNAVAILABLE "+e.Message);restorationContact=restorationClock-3.2;}colonyTask=null;}
   if(restorationContact>=0)restorationColony.Contact=Math.Clamp((float)(restorationClock-restorationContact)/1.8f,0,1);
   if(restorationVerified&&restorationContact>=0&&restorationClock-restorationContact>=3.2&&restorationWake<0){restorationWake=restorationClock;contentPanel?.Hide();restorationHum?.Stop();if(!restorationSkipped)RestorationCue(reducedMotion?"residual-hold":"awakening",reducedMotion?-25:-12);GD.Print("RESTORATION_AWAKENING_VERIFIED");}
   double wake=restorationWake<0?0:restorationClock-restorationWake;restorationColony.Awakening=restorationWake>=0;restorationColony.AwakeningTime=wake;
   if(restorationWake>=0&&!restorationSkipped){if(wake>=1.55)OriginalRestorationSound("ACTIVE",-16);if(wake>=1.9)OriginalRestorationSound("MSG",-24);if(wake>=2.8)OriginalRestorationSound("BARRACK",-18);if(wake>=5.2)OriginalRestorationSound("DCHIRP2",-29);if(wake>=5.5)OriginalRestorationSound("TRP1SEL",-20);}
   restorationCharge.Time=restorationClock;restorationCharge.Awake=restorationWake>=0;restorationCharge.WakeTime=wake;restorationCharge.Tip=restorationColony.ArtReady?restorationColony.TowerTip:Vector2.Zero;restorationCharge.QueueRedraw();
   restorationMessage!.Visible=restorationWake>=0&&!restorationSkipped&&!reducedMotion&&wake>=1.9&&wake<4.6;restorationMessage.Position=new(size.X*.1f,size.Y-110*ui);restorationMessage.Size=new(size.X*.8f,50*ui);restorationMessage.AddThemeFontSizeOverride("font_size",(int)(17*ui));
   bool line=restorationWake>=0&&!restorationSkipped&&(reducedMotion?wake>1.6&&wake<3.8:wake>6.6&&wake<9.5);restorationScrim!.Visible=line;restorationScrim.Position=new(0,size.Y*.64f);restorationScrim.Size=new(size.X,180*ui);restorationLine!.Visible=restorationFacts!.Visible=line;restorationLine.Position=new(0,size.Y*.66f);restorationLine.Size=new(size.X,70);restorationFacts.Text=restorationReceipt;restorationFacts.Position=new(size.X*.12f,size.Y*.66f+78);restorationFacts.Size=new(size.X*.76f,64);restorationFacts.AddThemeFontSizeOverride("font_size",(int)(18*ui));restorationSkip!.Position=new(20,size.Y-40);restorationSkip.Size=new(size.X-40,30);
   if(restorationWake>=0&&wake>=(restorationSkipped?.5:reducedMotion?4.2:11)){bool replay=restorationReplay;EndRestoration(true);restorationPendingMenu=true;GD.Print("RESTORATION_FINISHED "+(replay?"replay":"first-run"));}
  }
  if(restorationPendingMenu&&importTask is not {IsCompleted:false}){restorationPendingMenu=false;OpenImportedMenu();}
  ProcessColonyAmbience(delta);
  if(ready&&mainMenu&&contentSetup==null){
   if(menuColony==null&&colonyTask==null&&colonySource!=root){colonySource=root;string data=Path.Combine(root,"private/DC");colonyTask=Task.Run(()=>ColonyScene.Build(PresentationSnapshot.ReadVerified(data)));}
   if(colonyTask is {IsCompleted:true}){try{menuColony=new ColonyScene{ZIndex=-1,Settled=true,Reduced=reducedMotion};menuColony.Install(colonyTask.GetAwaiter().GetResult());AddChild(menuColony);}catch(Exception e){GD.Print("MENU_COLONY_UNAVAILABLE "+e.Message);}colonyTask=null;}
   if(menuColony!=null){menuColony.Size=GetViewportRect().Size;menuColony.Reduced=reducedMotion;menuColony.Show();}
  }else if(menuColony!=null&&!contentOpening&&!restorationPendingMenu){menuColony.QueueFree();menuColony=null;colonySource="";}
 }
}
