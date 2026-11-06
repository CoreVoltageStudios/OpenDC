using Godot;
using DarkColony.Assets;
using DarkColony.ContentImport;
using DarkColony.Original;
namespace DarkColony.Viewer;

// Presentation-only reconstruction of the approved Red Landing composition.
// No world, orders, random simulation state or developer asset paths.
public partial class ColonyScene : Control {
 public sealed record Frame(Raster Pixels,int X,int Y,double Seconds);
 public sealed record Art(Raster Lit,Raster Dormant,Dictionary<string,Frame[]> Clips);
 readonly Dictionary<string,(ImageTexture Texture,int X,int Y,double Seconds)[]> clips=new();
 ImageTexture? lit,dormant;double clock;
 public bool Reduced,Awakening,Settled;public double AwakeningTime;
 public float Contact=1;
 public bool ArtReady=>lit!=null;
 public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;TextureFilter=TextureFilterEnum.Linear;ClipContents=true;}
 public void Install(Art art){Clear();lit=Texture(art.Lit);dormant=Texture(art.Dormant);foreach(var pair in art.Clips)clips[pair.Key]=pair.Value.Select(f=>(Texture(f.Pixels),f.X,f.Y,f.Seconds)).ToArray();QueueRedraw();}
 static ImageTexture Texture(Raster r){using var i=Image.CreateFromData(r.Width,r.Height,false,Image.Format.Rgba8,r.Rgba);return ImageTexture.CreateFromImage(i);}
 void Clear(){lit?.Dispose();dormant?.Dispose();lit=dormant=null;foreach(var frames in clips.Values)foreach(var f in frames)f.Texture.Dispose();clips.Clear();}
 bool transferring;
 public void TransferTo(Node parent){transferring=true;try{Reparent(parent);}finally{transferring=false;}}
 public override void _ExitTree(){if(!transferring)Clear();}
 public override void _Process(double delta){if(!Visible)return;clock+=delta;QueueRedraw();}
 static float Ease(float v){v=Math.Clamp(v,0,1);return v*v*(3-2*v);}
 // FIN city components already contain their offsets from this common artwork anchor.
 // Ground contacts sit inside the basin; sprite height may cross the skyline.
 static readonly Vector2 CityAnchor=new(1020,430);
 static readonly Vector2 DishAnchor=new(1080,480),BeaconAnchor=new(1050,545);
 static readonly Vector2 TowerApex=CityAnchor+new Vector2(0,-266);
 public Vector2 TowerTip {get;private set;}
 public override void _Draw(){
  if(lit==null)return;float s=Size.Y/480f;float opened=Settled?1:Reduced?0:Awakening?Ease((float)(AwakeningTime-4.2)/3):0;
  float anchor=Size.X*.5f+(Math.Min(Size.X,Size.Y*16/9)*.16f)*opened;
  var origin=new Vector2(anchor-1100*s,-101*s);if(Settled&&!Reduced)origin.X+=MathF.Sin((float)clock*MathF.PI/70)*6*s;TowerTip=origin+TowerApex*s;
  var r=new Rect2(origin,new Vector2(1700,640)*s);float life=Settled?1:Awakening?Ease((float)(AwakeningTime-.9)/5.5f):0;
  DrawTextureRect(dormant!,r,false,new Color(1,1,1,Contact));
  if(life>0){if(Reduced||Settled)DrawTextureRect(lit,r,false,new Color(1,1,1,life*Contact));else for(int x=0;x<1700;x+=34){float alpha=Math.Clamp((life*1800-Math.Abs(x-1100))/320,0,1);if(alpha>0)DrawTextureRectRegion(lit,new(origin+new Vector2(x*s,0),new Vector2(34,640)*s),new(x,0,34,640),new Color(1,1,1,alpha*Contact));}}
  double t=Settled?100+clock:AwakeningTime;
  void Sprite(string name,float x,float y,bool active,double start=0,bool once=false){
   string key=active?name:name+"Dorm";if(!clips.TryGetValue(key,out var frames))return;
   double at=Reduced?0:Math.Max(0,t-start),length=frames.Sum(f=>f.Seconds);if(length>0)at=once?Math.Min(at,length-.0001):at%length;
   var frame=frames[0];foreach(var f in frames){frame=f;if(at<f.Seconds)break;at-=f.Seconds;}
   DrawTextureRect(frame.Texture,new(origin+new Vector2(x+frame.X,y+frame.Y)*s,frame.Texture.GetSize()*s),false,new Color(1,1,1,Contact));
  }
  bool on=Settled||Awakening;
  Sprite("dish",DishAnchor.X,DishAnchor.Y,on&&t>=2.3,2.3);
  // Match the original city back-to-front order; logical slot offsets are not draw anchors.
  Sprite("tower",CityAnchor.X,CityAnchor.Y,on);
  Sprite("exco",CityAnchor.X,CityAnchor.Y,on&&t>=1,1);
  Sprite("brrk",CityAnchor.X,CityAnchor.Y,on&&t>=1,1);
  Sprite(on&&t>=1.6?"beaconOn":"beaconOff",BeaconAnchor.X,BeaconAnchor.Y,on&&t>=1.6,1.6);
  if(on&&t>=2.8&&!Reduced){double lift=clips["lift"].Sum(f=>f.Seconds);if(t<2.8+lift)Sprite("lift",CityAnchor.X,CityAnchor.Y,true,2.8,true);else{float walk=Math.Clamp((float)(t-2.8-lift)/3.2f,0,1);Sprite(walk<1?"walk":"stand",CityAnchor.X+2-102*walk,CityAnchor.Y+39+53*walk,true,2.8+lift);}}
  float side=(Size.X-Math.Min(Size.X,Size.Y*4/3))/2*(1-opened);if(side>0){DrawRect(new(0,0,side,Size.Y),new Color("090a08"));DrawRect(new(Size.X-side,0,side,Size.Y),new Color("090a08"));}
 }
 public static Art Build(PresentationSnapshot source){
  var pal=GameColors.ReadGifPalette(source.Read("DESERT.GIF"));var terrain=Import.Terrain(source.Read("SCENARIO/DESERT.BTS"));var map=Import.ResolveTerrain(Import.Map(source.Read("SCENARIO/HUMAN/HUMAN01.MAP")),terrain);
  var light=GameColors.Table(pal,5,16,ColorBank.Terrain);var body=GameColors.Table(pal,0,16,ColorBank.Body);
  // Authored dormant palette from the approved prototype, not a simulation selector.
  Rgb8[] Dim(int amount)=>Enumerable.Range(0,256).Select(i=>{int r=pal.ActiveRgb8[i*3],g=pal.ActiveRgb8[i*3+1],b=pal.ActiveRgb8[i*3+2],gray=(3*r+6*g+b)*9;return new Rgb8((r*20+gray)/110*amount/16,(g*20+gray)/110*amount/16,(b*20+gray)/110*amount/16);}).ToArray();
  var tiles=terrain.ByEditorId;
  Raster Ground(Rgb8[] colors){var r=new Raster(1700,640,new byte[1700*640*4]);for(int y=0;y<640;y++)for(int x=0;x<1700;x++){int mx=x+1332,my=y+1660;var c=map.At(mx/32,map.Height-1-my/32);int tx=mx%32,ty=my%32;int id=tiles[c.BackgroundId].Indices[ty*32+((c.Attributes&32)!=0?31-tx:tx)];if(c.ForegroundId!=0){int fg=tiles[c.ForegroundId].Indices[ty*32+((c.Attributes&64)!=0?31-tx:tx)];if(fg!=0)id=fg;}int p=(y*1700+x)*4;var color=colors[id];r.Rgba[p]=(byte)color.R;r.Rgba[p+1]=(byte)color.G;r.Rgba[p+2]=(byte)color.B;r.Rgba[p+3]=255;}return r;}
  var banks=new Dictionary<string,SpriteAsset>();var fins=new Dictionary<string,AnimationAsset>();var effects=new EffectPalette(System.Text.Encoding.ASCII.GetString(source.Read("COLOUR.SET")));
  Frame BuildFrame(AnimationFrame f,bool dim){var layers=new List<(Raster R,int X,int Y)>();foreach(var c in f.Cells){int mode=c.UnknownWords[2];if(mode is not(0 or 1 or 2 or 5)||dim&&mode==5)continue;if(!banks.TryGetValue(c.Bank,out var bank))banks[c.Bank]=bank=Import.Sprite(source.Read("SPRITES/"+c.Bank+".SPR"));var sp=bank.Cells[c.CellId];if(mode is 1 or 2){var sh=OriginalShadow.Project(sp,c);layers.Add((sh.Pixels,sh.X,sh.Y));}if(mode==2)continue;var pixels=mode==5?effects.Decode(sp):GameColors.Body(sp,dim?Dim(6):body);if(c.UnknownWords[3]!=0)pixels=OriginalShadow.Mirror(pixels);var at=OriginalShadow.BodyPlacement(c,sp);layers.Add((pixels,at.X,at.Y));}
   int x=layers.Min(l=>l.X),y=layers.Min(l=>l.Y),w=layers.Max(l=>l.X+l.R.Width)-x,h=layers.Max(l=>l.Y+l.R.Height)-y;var r=new Raster(w,h,new byte[w*h*4]);foreach(var l in layers)Blend(r,l.R,l.X-x,l.Y-y);return new(r,x,y,Math.Max(1,NativeAnimation.DurationTicks(f.Duration))*.066);}
  var clips=new Dictionary<string,Frame[]>();foreach(var spec in new[]{("tower","TOWR","TOWRSTAND0"),("exco","HUBU","EXCOPODSTAND0"),("brrk","HUBU","BRRKPODSTAND0"),("beaconOn","BEAC","BEACSTAND2"),("beaconOff","BEAC","BEEKSTAND2"),("dish","DISH","DISHSTAND0"),("lift","HUBU","TRSCBUILD0"),("walk","TRSC","TRSCMOVE10"),("stand","TRSC","TRSCSTAND10")}){if(!fins.TryGetValue(spec.Item2,out var fin))fins[spec.Item2]=fin=Import.Animation(source.Read("ANIMATE/"+spec.Item2+".FIN"));var a=fin.Animations.Single(a=>a.Name==spec.Item3);clips[spec.Item1]=Enumerable.Range(a.First,a.Last-a.First+1).Select(i=>BuildFrame(fin.Frames[i],false)).ToArray();clips[spec.Item1+"Dorm"]=new[]{BuildFrame(fin.Frames[a.First],true)};}
  return new(Ground(light),Ground(Dim(7)),clips);
 }
 static void Blend(Raster dst,Raster src,int x,int y){for(int sy=0;sy<src.Height;sy++)for(int sx=0;sx<src.Width;sx++){int si=(sy*src.Width+sx)*4,di=((y+sy)*dst.Width+x+sx)*4;int a=src.Rgba[si+3],b=dst.Rgba[di+3]*(255-a)/255,total=a+b;if(total==0)continue;for(int k=0;k<3;k++)dst.Rgba[di+k]=(byte)((src.Rgba[si+k]*a+dst.Rgba[di+k]*b)/total);dst.Rgba[di+3]=(byte)total;}}
}
