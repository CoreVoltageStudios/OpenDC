using Godot;
namespace DarkColony.StudioIntro;

// Core Voltage Studios intro, refined Concept C (Claude Design "Core Voltage Intro
// Refined", cv-intro-refined.jsx). One mechanism: an ignition pulse reveals the
// containment tracks, the core feeds the inner ring through arcs that chase its
// drawing head, the inner ring relays charge to the outer ring, crackles fade as the
// loop stabilises, then the overload flash and logo reveal. Ported as a pure function
// of authored time T on the design's 1920x1080 stage (emblem units are 7 stage px
// before the reveal scale), scaled to the window. Self-contained: Godot, its font and
// its sound only.
public partial class CoreVoltageIntro : Control {
 public const double Length=7.0;
 public const string FontPath="res://Fonts/RedHatDisplay-Variable.ttf";
 public const string SoundPath="res://Branding/CoreVoltage/core-voltage-intro.wav";
 // OM_SCENES starts: Ignition .9, Transfer 1.15, Formation .95, Stabilise .72, Reveal 2.28, Handoff 1.
 const double Ignition=0,Transfer=.9,Formation=2.05,Stabilise=3.0,Reveal=3.72,Handoff=6.0,SkipFade=.35;
 const double RIn=27,ROut=40,WIn=5.5,WOut=2.4,LIn=252,LOut=302,RotIn=-33,RotOut=20,PX=7,K=1; // K: "Balanced"
 const double IStart=Transfer+.04,IEnd=Formation+.05,OStart=Formation+.02,OEnd=Stabilise+.05;
 static readonly Vector2 Stage=new(1920,1080);
 static readonly Color Void=new("050609"),ArcBlue=new("8FE3FF"),Ion=new("4D8DFF"),Title=new("EAF6FF");
 enum Kind{FeedI,Jump,FeedO,GapI,GapO}
 record struct Strike(double T,Kind Kind,double Amp,double Life);
 // Keep in sync with tools/prepare_core_voltage_intro_audio.py (feed crackles and locks).
 static readonly Strike[] Strikes=BuildStrikes();
 static Strike[] BuildStrikes(){
  var s=new List<Strike>();double[] fi={0,.16,.31,.47,.62,.78,.92},fa={1,.9,.95,.85,.78,.66,.5};
  for(int i=0;i<fi.Length;i++)s.Add(new(IStart+fi[i],Kind.FeedI,fa[i],.13));
  s.Add(new(Formation-.01,Kind.Jump,1,.2));
  double[] oi={.14,.3,.46,.62,.76},oa={.9,.82,.72,.6,.48};
  for(int i=0;i<oi.Length;i++)s.Add(new(OStart+oi[i],Kind.FeedO,oa[i],.13));
  s.Add(new(Stabilise+.05,Kind.GapI,.62,.12));s.Add(new(Stabilise+.2,Kind.GapO,.46,.12));
  s.Add(new(Stabilise+.36,Kind.GapI,.3,.12));s.Add(new(Stabilise+.5,Kind.GapO,.18,.12));
  return s.ToArray();
 }

 public event Action? Finished;
 public bool KeepResidual {get;set;}
 public float VolumeLinear {get;set;}=1;
 public bool Done {get;private set;}
 public bool Skipped=>skipAt>=0;
 AudioStreamPlayer? sound;
 Texture2D halo=null!,core=null!,flash=null!,backdrop=null!,line=null!;
 Font light=null!,bold=null!;
 ulong startTicks;double time,skipAt=-1;bool started,manual;
 public double Elapsed=>time;
 public double? SoundPosition=>sound!=null&&sound.Playing?sound.GetPlaybackPosition():null;

 public override void _Ready(){
  SetAnchorsPreset(LayoutPreset.FullRect);MouseFilter=MouseFilterEnum.Stop;TextureFilter=TextureFilterEnum.Linear;
  halo=Radial(new[]{0f,.2f,.5f,1f},new[]{Colors.White,new Color("CFF4FF",.75f),new Color("2F7BFF",.28f),new Color("2F7BFF",0)});
  // Hard edge at the circle; the texture corners beyond it stay transparent.
  core=Radial(new[]{0f,.6f,.985f,1f},new[]{Colors.White,new Color("F0FCFF"),ArcBlue,new Color(ArcBlue,0)});
  // radial-gradient(circle, ...) in an 1800 box: offsets of the farthest-corner radius.
  flash=Radial(new[]{0f,.22f,.38f,.68f,1f},new[]{Colors.White,Colors.White,new Color(234/255f,246/255f,1,.85f),new Color(ArcBlue,0),new Color(ArcBlue,0)});
  // radial-gradient(ellipse 52% 62% at 50% 43%, #0B1A3A 0%, #050609 72%).
  backdrop=Radial(new[]{0f,.72f,1f},new[]{new Color("0B1A3A"),Void,Void});
  line=new GradientTexture2D{Width=512,Height=1,Gradient=new Gradient{Offsets=new[]{0f,.38f,.5f,.62f,1f},Colors=new[]{new Color(ArcBlue,0),new Color(ArcBlue,.6f),Colors.White,new Color(ArcBlue,.6f),new Color(ArcBlue,0)}}};
  var face=GD.Load<FontFile>(FontPath);
  light=Weight(face,300);bold=Weight(face,700);
  if(ResourceLoader.Exists(SoundPath)){sound=new AudioStreamPlayer{Stream=GD.Load<AudioStream>(SoundPath),VolumeDb=VolumeLinear<=0?-80:Mathf.LinearToDb(VolumeLinear)};AddChild(sound);}
 }
 // Full-rect anchors only apply under a Control; under a Node2D host fill the viewport.
 void Fit(){if(GetParent() is Control)return;var r=GetViewportRect().Size;if(Size!=r)Size=r;Position=Vector2.Zero;}
 static Texture2D Radial(float[] offsets,Color[] colors)=>new GradientTexture2D{Width=256,Height=256,Fill=GradientTexture2D.FillEnum.Radial,FillFrom=new(.5f,.5f),FillTo=new(1,.5f),Gradient=new Gradient{Offsets=offsets,Colors=colors}};
 static Font Weight(FontFile face,int weight)=>new FontVariation{BaseFont=face,VariationOpentype=new Godot.Collections.Dictionary{{TextServerManager.GetPrimaryInterface().NameToTag("wght"),weight}}};

 public void Begin(){
  if(started)return;started=true;startTicks=Time.GetTicksUsec();sound?.Play();QueueRedraw();
 }
 // Fixed-time preview for design review and diagnostics: draws one authored time, silent.
 public void ShowAt(double seconds){Fit();started=true;manual=true;time=Math.Clamp(seconds,0,Length);QueueRedraw();}
 public void Skip(){
  if(!started||Done||skipAt>=0)return;skipAt=time;
 }
 // Presentation clock: the sound's playback position (corrected for mix latency) when
 // audible, otherwise wall time; never runs backwards.
 double Clock(){
  double wall=(Time.GetTicksUsec()-startTicks)/1e6;
  if(sound!=null&&sound.Playing){double audio=sound.GetPlaybackPosition()+AudioServer.GetTimeSinceLastMix()-AudioServer.GetOutputLatency();if(Math.Abs(audio-wall)<.25)wall=audio;}
  return Math.Max(time,wall);
 }
 public override void _Process(double delta){
  Fit();
  if(!started||Done||manual)return;
  time=Clock();
  if(skipAt>=0&&sound!=null)sound.VolumeDb=Mathf.LinearToDb(Mathf.Max(.0001f,VolumeLinear*(float)(1-Tw(0,1,skipAt,skipAt+SkipFade,Linear,time))));
  if(time>=Length||(skipAt>=0&&time>=skipAt+SkipFade)){Done=true;sound?.Stop();QueueRedraw();Finished?.Invoke();return;}
  QueueRedraw();
 }
 public override void _Input(InputEvent e){
  if(!started||Done||manual)return;
  if(e is InputEventKey{Pressed:true,Echo:false}||e is InputEventMouseButton{Pressed:true}||e is InputEventJoypadButton{Pressed:true})Skip();
  if(e is not InputEventMouseMotion)GetViewport().SetInputAsHandled();
 }

 // animations-v3 easing and animate({from,to,start,end,ease}): from before start, to after end.
 static double Linear(double t)=>t;
 static double OutCubic(double t){t-=1;return t*t*t+1;}
 static double InOutCubic(double t)=>t<.5?4*t*t*t:(t-1)*(2*t-2)*(2*t-2)+1;
 static double OutBack(double t){const double c1=1.70158,c3=c1+1;return 1+c3*Math.Pow(t-1,3)+c1*Math.Pow(t-1,2);}
 static double Tw(double from,double to,double start,double end,Func<double,double> ease,double t)=>t<=start?from:t>=end?to:from+(to-from)*ease((t-start)/(end-start));
 static double Cl(double v,double a=0,double b=1)=>Math.Max(a,Math.Min(b,v));
 static double Rnd(double s){double x=Math.Sin(s*127.1+311.7)*43758.5453;return x-Math.Floor(x);}
 static Vector2 P(double r,double a){double t=a*Math.PI/180;return new((float)(r*Math.Cos(t)),(float)(r*Math.Sin(t)));}
 static double Mod(double a,double m)=>(a%m+m)%m;

 // Ring state: inner draws counter-clockwise from -50 deg and turns at -33 deg/s; the
 // outer starts where the inner head stood at OStart, draws clockwise, turns +20 deg/s.
 static (bool On,double Len,double Tail,double Head) Inner(double t){
  double len=Tw(0,LIn,IStart,IEnd,OutCubic,t),tail=-50+(t>IStart?RotIn*(t-IStart):0);return(t>=IStart,len,tail,tail-len);
 }
 static readonly double OuterOrigin=Inner(OStart).Head;
 static (bool On,double Len,double Tail,double Head) Outer(double t){
  double len=Tw(0,LOut,OStart,OEnd,OutCubic,t),tail=OuterOrigin+(t>OStart?RotOut*(t-OStart):0);return(t>=OStart,len,tail,tail+len);
 }
 // Drawing-head heat: normalised draw speed, faded in over 60 ms.
 static double Heat(double t,double length,double s,double e){
  if(t<s||t>e)return 0;
  double d=(Tw(0,length,s,e,OutCubic,t+.005)-Tw(0,length,s,e,OutCubic,t-.005))/.01;
  return Cl(d/(3*length/(e-s)))*Cl((t-s)/.06);
 }

 Vector2 centre;float unit;
 Vector2 E(Vector2 p)=>centre+p*unit;
 void Circle(double r,Color c){if(r>0)DrawCircle(centre,(float)r*unit,c,true,-1,true);}
 void Stroke(double r,double a0,double a1,double w,Color c){
  double d=a1-a0;if(Math.Abs(d)<.05)return;if(Math.Abs(d)>359.8)d=Math.Sign(d)*359.8;
  float start=Mathf.DegToRad((float)a0),end=Mathf.DegToRad((float)(a0+d));
  DrawArc(centre,(float)r*unit,start,end,Math.Max(8,(int)(Math.Abs(d)/3)),c,Math.Max(1,(float)w*unit),true);
 }
 void Ring(double r,double w,Color color,double glow,(bool On,double Len,double Tail,double Head) st,double heat){
  if(!st.On||st.Len<.2)return;
  double lo=Math.Min(st.Tail,st.Head),hi=Math.Max(st.Tail,st.Head),dir=Math.Sign(st.Head-st.Tail);
  Stroke(r,lo,hi,w*2,new Color(color,(float)Cl(.07*glow)));
  Stroke(r,lo,hi,w,color);
  if(heat<=.01)return;
  Stroke(r,st.Head-dir*Math.Min(46,st.Len),st.Head,w*.8,new Color(1,1,1,(float)(.7*heat)));
  var hp=E(P(r,st.Head));
  DrawCircle(hp,(float)(w*.4+.6)*unit,new Color(1,1,1,(float)heat),true,-1,true);
  DrawCircle(hp,(float)(w*.7+3)*unit,new Color(ArcBlue,(float)(.16*heat)),true,-1,true);
 }
 // Jittered bolt between two polar points; the jitter re-seeds 24 times a second.
 void Bolt(int key,double r0,double a0,double r1,double a1raw,double amp,double op,double t,bool keepSpan=false){
  // Shortest turn: ((x % 360 + 540) % 360 - 180); C# and JavaScript % agree on doubles.
  double a1=keepSpan?a1raw:a0+(((a1raw-a0)%360+540)%360-180);
  var p0=P(r0,a0);var p1=P(r1,a1);
  int n=Math.Max(6,(int)Math.Round((p1-p0).Length()/2.6,MidpointRounding.AwayFromZero));
  double seed=Math.Floor(t*24)*13+key*101;
  var pts=new Vector2[n+1];for(int i=0;i<=n;i++){double u=(double)i/n;pts[i]=P(r0+(r1-r0)*u,a0+(a1-a0)*u);}
  var path=new Vector2[n+1];
  for(int i=0;i<=n;i++){
   if(i==0||i==n){path[i]=E(pts[i]);continue;}
   var a=pts[i-1];var b=pts[i+1];var normal=new Vector2(-(b.Y-a.Y),b.X-a.X);float m=normal.Length();normal=m>0?normal/m:new(0,0);
   double j=(Rnd(seed+i)-.5)*2*amp*K*2.1*Math.Sin(Math.PI*i/n);
   path[i]=E(pts[i]+normal*(float)j);
  }
  double o=op*Math.Min(1,.55+.45*K);
  DrawPolyline(path,new Color("5FB8FF",(float)(.22*o)),Math.Max(1,2.2f*unit),true);
  DrawPolyline(path,new Color("E6FAFF",(float)o),Math.Max(1,.5f*unit),true);
  DrawCircle(E(p1),1.6f*unit,new Color(1,1,1,(float)(.7*o)),true,-1,true);
  DrawCircle(E(p1),4f*unit,new Color(ArcBlue,(float)(.18*o)),true,-1,true);
 }

 public override void _Draw(){
  var size=Size;DrawRect(new Rect2(Vector2.Zero,size),Void);
  if(!started)return;
  double T=Math.Min(time,Length);
  float s=Math.Min(size.X/Stage.X,size.Y/Stage.Y);var origin=(size-Stage*s)/2;
  Vector2 S(double x,double y)=>origin+new Vector2((float)x,(float)y)*s;
  var inS=Inner(T);var outS=Outer(T);
  double hIn=Heat(T,LIn,IStart,IEnd),hOut=Heat(T,LOut,OStart,OEnd);

  double ign=Ignition+.3;
  double kick=0;
  foreach(var (st,i) in Strikes.Select((x,i)=>(x,i))){
   double u=(T-st.T)/st.Life;if(u<0||u>1)continue;
   double env=Math.Pow(1-u,.7)*(.6+.4*Rnd(Math.Floor(T*30)+i*7));
   if(st.Kind is Kind.FeedI or Kind.Jump)kick=Math.Max(kick,env*st.Amp);
  }
  double charge=.34*Tw(0,1,ign,ign+.22,OutBack,T)+.16*Tw(0,1,Transfer,Formation,OutCubic,T)+.2*Tw(0,1,Formation,Stabilise,OutCubic,T)
   +.15*Tw(0,1,Stabilise,Stabilise+.4,OutCubic,T)+.2*Tw(0,1,Stabilise+.38,Reveal,InOutCubic,T)-.15*Tw(0,1,Reveal+.05,Reveal+.7,OutCubic,T);
  double coreR=12.5*charge*(1-.1*kick),coreGlow=charge+.25*kick;
  double pre=T<ign&&((T>.08&&T<.13)||(T>.18&&T<.23))?.6:0;
  double pulseR=Tw(5,47,ign,Transfer,OutCubic,T),pulseO=T>ign?.75*(1-Tw(0,1,ign,Transfer,OutCubic,T)):0;
  double ghostFade=1-Tw(0,1,Reveal-.1,Reveal+.3,OutCubic,T);
  double ghostI=(T>ign?Cl((pulseR-RIn)/5):0)*ghostFade,ghostO=(T>ign?Cl((pulseR-ROut)/5):0)*ghostFade;
  double ringGlow=1+.8*Tw(0,1,Stabilise+.3,Reveal,InOutCubic,T)-.8*Tw(0,1,Reveal,Reveal+.6,OutCubic,T);
  double flashLevel=Tw(0,1,Reveal-.1,Reveal+.03,InOutCubic,T)*(1-Tw(0,1,Reveal+.03,Reveal+.9,OutCubic,T));
  double settle=Tw(0,1,Reveal,Reveal+.9,OutCubic,T);
  double scale=Tw(.94,1,0,Reveal,InOutCubic,T)*(1-.4*settle)+.012*Tw(0,1,Reveal+.9,Handoff+1,InOutCubic,T);
  double cy=540-70*settle;
  double bg=Tw(0,1,Reveal,Reveal+.8,OutCubic,T);
  double t1=Tw(0,1,Reveal+.45,Reveal+1.3,OutCubic,T),t2=Tw(0,1,Reveal+.75,Reveal+1.5,OutCubic,T);
  double lens=Tw(0,1,Reveal-.02,Reveal+.25,OutCubic,T);
  double fade=1-Tw(0,1,Handoff,Handoff+.9,InOutCubic,T);

  if(bg>0){var r=new Vector2(.52f*Stage.X,.62f*Stage.Y)*s;var at=S(960,.43*Stage.Y);DrawTextureRect(backdrop,new Rect2(at-r,r*2),false,new Color(1,1,1,(float)bg));}
  double lensAlpha=lens*(.7-.35*settle);
  if(lensAlpha>0){float w=(float)(1520*(.2+.8*lens))*s;var at=S(960,cy);DrawTextureRect(line,new Rect2(at.X-w/2,at.Y,w,Math.Max(1,s)),false,new Color(1,1,1,(float)lensAlpha));}

  centre=S(960,cy);unit=(float)(PX*scale)*s;
  double haloR=18+44*coreGlow;
  if(coreGlow>0){float hr=(float)haloR*unit;DrawTextureRect(halo,new Rect2(centre-Vector2.One*hr,Vector2.One*hr*2),false,new Color(1,1,1,(float)Cl(coreGlow*1.1)));}
  if(ghostI>0)Stroke(RIn,0,360,.4,new Color(Ion,(float)(.22*ghostI)));
  if(ghostO>0)Stroke(ROut,0,360,.35,new Color(ArcBlue,(float)(.2*ghostO)));
  if(pulseO>.01)Stroke(pulseR,0,360,1.4-pulseR/40,new Color("BFF1FF",(float)pulseO));
  Ring(RIn,WIn,Ion,ringGlow,inS,hIn);
  Ring(ROut,WOut,ArcBlue,ringGlow,outS,hOut);
  for(int i=0;i<Strikes.Length;i++){
   var st=Strikes[i];double u=(T-st.T)/st.Life;if(u<0||u>1)continue;
   double env=Math.Pow(1-u,.7)*(.6+.4*Rnd(Math.Floor(T*30)+i*7));
   switch(st.Kind){
    case Kind.FeedI:Bolt(i,12.5*.5,inS.Head+28,RIn-WIn/2,inS.Head,st.Amp,env,T);break;
    case Kind.Jump:Bolt(i,RIn+WIn/2,inS.Head,ROut-WOut/2,inS.Head+6,st.Amp,env,T);break;
    case Kind.FeedO:{
     double span=inS.Tail-inS.Head,rel=Mod(outS.Head-22-inS.Head,360);
     double src=inS.Head+(rel<=span?rel:(rel-span<360-rel?span:0));
     Bolt(i,RIn+WIn/2,src,ROut-WOut/2,outS.Head,st.Amp,env,T);break;}
    case Kind.GapI:Bolt(i,RIn,inS.Head,RIn,inS.Head-(360-LIn),st.Amp*.6,env,T,true);break;
    case Kind.GapO:Bolt(i,ROut,outS.Head,ROut,outS.Head+(360-LOut),st.Amp*.5,env,T,true);break;
   }
  }
  if(pre>0)Circle(.7,new Color(1,1,1,(float)pre));
  if(coreR>.05){float cr=(float)coreR*unit;DrawTextureRect(core,new Rect2(centre-Vector2.One*cr,Vector2.One*cr*2),false);}

  // Wordmark: top 712, gap 18; CORE VOLTAGE 300/40px tracking (.62 + .3(1 - t1))em with
  // a fixed .62em left pad; STUDIOS 700/14px .9em, Arc colour.
  int titleSize=Math.Max(1,(int)Math.Round(40*s)),studioSize=Math.Max(1,(int)Math.Round(14*s));
  float top=S(0,712).Y;
  if(t1>0)Tracked(light,"CORE VOLTAGE",titleSize,(float)(.62+.3*(1-t1))*titleSize,.62f*titleSize,top,new Color(Title,(float)t1));
  if(t2>0)Tracked(bold,"STUDIOS",studioSize,.9f*studioSize,.9f*studioSize,top+light.GetHeight(titleSize)+18*s,new Color(ArcBlue,(float)t2));

  if(flashLevel>0){
   float fr=(float)(900*Math.Sqrt(2))*s;var at=S(960,cy);
   DrawTextureRect(flash,new Rect2(at-Vector2.One*fr,Vector2.One*fr*2),false,new Color(1,1,1,(float)flashLevel));
   DrawRect(new Rect2(Vector2.Zero,size),new Color(1,1,1,(float)(.22*flashLevel)));
  }
  // Handoff fade (the composition's opacity over the Void stage), or a skip fade.
  double hide=Math.Max(1-fade,skipAt>=0?Tw(0,1,skipAt,skipAt+SkipFade,InOutCubic,T):0);
  if(hide>0)DrawRect(new Rect2(Vector2.Zero,size),new Color(Void,(float)hide));
  if(KeepResidual&&(T>=Handoff||skipAt>=0)){
   float travel=(float)Tw(0,1,skipAt>=0?skipAt:Handoff,skipAt>=0?skipAt+SkipFade:Length,InOutCubic,T);
   var at=S(960,470).Lerp(new Vector2(size.X*.72f,size.Y*.36f),travel);
   float hr=34*s;DrawTextureRect(halo,new Rect2(at-Vector2.One*hr,Vector2.One*hr*2),false,new Color(1,1,1,.6f));DrawCircle(at,5.5f*s,new Color("d4f5ff"));
  }
 }
 // CSS letter-spacing after every glyph plus padding-left, centred as a block: the ink
 // centre sits (pad - tracking)/2 right of the stage centre.
 void Tracked(Font font,string text,int fontSize,float tracking,float pad,float top,Color colour){
  float ink=-tracking;foreach(char c in text)ink+=font.GetStringSize(c.ToString(),HorizontalAlignment.Left,-1,fontSize).X+tracking;
  float x=Size.X/2+(pad-tracking)/2-ink/2,baseline=top+font.GetAscent(fontSize);
  foreach(char c in text){DrawChar(font,new Vector2(x,baseline),c.ToString(),fontSize,colour);x+=font.GetStringSize(c.ToString(),HorizontalAlignment.Left,-1,fontSize).X+tracking;}
 }
}
