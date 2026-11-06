using Godot;
using DarkColony.Assets;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 Rect2 relayGrid;
 Font? relayNumbers;
 OriginalDependency[]? relayCatalogSource;
 readonly OriginalDependency[][] relayCategories=new OriginalDependency[3][];
 readonly int[] relayPages=new int[3];
 int relayVisibleRows=1;
 long relayStateTick=-1;int relayStateMoney=-1;
 sealed class ProductView {public int State,Count;public bool Constructing,Active;public float Progress;public string Name="",Help="",Status="";}
 readonly Dictionary<int,ProductView> relayProducts=new();
 void PrepareRelayProducts(){
  if(relayCatalogSource!=developmentCatalog){
   relayCatalogSource=developmentCatalog;relayProducts.Clear();
   for(int kind=0;kind<3;kind++)relayCategories[kind]=developmentCatalog.Where(d=>d.Kind==kind).ToArray();
   foreach(var d in developmentCatalog)relayProducts[d.Id]=new(){Name=ProductName(d)};
   relayStateTick=-1;
  }
  var team=nativeWorld.Teams[LocalTeam];if(relayStateTick==nativeWorld.Tick&&relayStateMoney==team.Money)return;
  relayStateTick=nativeWorld.Tick;relayStateMoney=team.Money;
  foreach(var d in developmentCatalog){
   var v=relayProducts[d.Id];v.State=nativeWorld.DependencyState(LocalTeam,d.Id);v.Constructing=d.Kind==0&&team.Cities[d.Subject].InitializationPending&&team.Cities[d.Subject].Level==d.Level;v.Count=0;v.Active=false;v.Progress=0;
   var q=d.Kind==1?team.Production[nativeData.Scenario.Types[d.Subject].Fields[20]]:null;
   if(q!=null){v.Count=q.Types.Count(t=>t==d.Subject);v.Active=q.Building&&q.Types.Count>0&&q.Types[0]==d.Subject;}
   NativeEntity? entity=null;NativeAnimation? animation=null;
   if(v.Constructing){entity=team.Cities[d.Subject].Entity;animation=entity?.Body;}
   else if(v.Active){
    // Production queue is the native city/category contract from OriginalEconomy.
    entity=nativeWorld.ProductionSource(LocalTeam,q!.Id);animation=entity?.Overlay;
   }
   if(animation?.Set is {} set&&animation.Mode!=2){var direction=set.Direction(entity!.Angle);v.Progress=Math.Clamp((animation.Frame+1f)/(direction.Last-direction.First+1),0,1);}
   string parents=string.Join(", ",d.Parents.Where(n=>nativeWorld.DependencyState(LocalTeam,n)!=0).Select(n=>ProductName(nativeData.Scenario.Dependencies.Single(p=>p.Id==n))));
   v.Status=v.Constructing?"Construction in progress":v.Active?$"Producing · {v.Progress:P0}":v.Count>0?"Waiting for production":v.State==0?d.Kind==2?"Upgrade acquired":"Built and ready":v.State==2?parents.Length>0?"Requires "+parents:"Unavailable while construction is pending":team.Money<d.Cost?$"Need {d.Cost-team.Money:N0} more Petra-7":"Ready to purchase";
   v.Help=v.Name+(d.Subject is 49 or 50&&d.Kind==1?"\nAutomatically heals your nearby units and buildings while idle. Uses energy; recharges over time.":"")+"\n"+d.Cost.ToString("N0")+" Petra-7\n"+v.Status+(v.Count>0?$"\n{v.Count} paid order(s) of this type.":"")+(v.State==1&&team.Money>=d.Cost?d.Kind==2?"\nClick: acquire upgrade immediately.":"\nClick: purchase.":"");
  }
 }
 void DrawRelayGroup(float x,float y,float bottom){
  relayGrid=default;UiFrame(new(x+20,y,296,Math.Max(28,bottom-y)));UiLabel("SELECTED GROUP",x+30,y+22,13,uiDim);y+=42;
  UiLabel("No production this mission",x+30,y,12,uiDim);y+=12;
  var groups=NativeSelected().GroupBy(e=>e.Type);foreach(var g in groups){
   if(y+38>bottom)break;UiLabel(UnitTitle(g.First())+" ×"+g.Count(),x+30,y+16,16);
   int hp=g.Sum(e=>e.Health),max=g.Sum(e=>nativeData.Scenario.Types[e.Type].MaximumHealth);
   DrawRect(Px(new(x+30,y+24,140*Math.Clamp(hp/(float)Math.Max(1,max),0,1),4)),uiGreen);UiLabel($"{hp} / {max}",x+180,y+28,12,uiDim);y+=42;
  }
  if(!NativeSelected().Any())UiWrap("Select your units to see the group breakdown.",x+30,y+18,270,14,uiDim);
 }
 void DrawRelayProduction(float x,float y,float bottom){
  PrepareRelayProducts();var team=nativeWorld.Teams[LocalTeam];bool compact=UiSize.Y<980;float moneyHeight=compact?32:38;
  UiFrame(new(x+20,y,296,moneyHeight));UiLabel("◆ PETRA-7",x+28,y+22,13,new Color("8cc7ce"));
  relayNumbers??=ResourceLoader.Load<FontFile>("res://Fonts/ShareTechMono-Regular.ttf");
  string balance=team.Money.ToString("N0");DrawString(relayNumbers,new Vector2(x+306-relayNumbers.GetStringSize(balance,fontSize:27).X,y+26)*ui,balance,fontSize:(int)(27*ui),modulate:new Color("45d3ea"));y+=moneyHeight+6;
  foreach(var tab in new[]{(1,"FORCES"),(0,IsHuman?"BASE":"HIVE"),(2,"UPGRADES")}){
   int col=tab.Item1==1?0:tab.Item1==0?1:2;var r=new Rect2(x+20+col*100,y,96,32);
   UiButton("tab:"+tab.Item1,"",r,tab.Item2,active:developmentTab==tab.Item1);UiMetal(r,developmentTab==tab.Item1);
   UiLabel(tab.Item2,r.Position.X+9,y+21,14,developmentTab==tab.Item1?new Color("141514"):uiText);
   if(relayCategories[tab.Item1].Any(d=>relayProducts[d.Id].Count>0||relayProducts[d.Id].Constructing))DrawRect(Px(new(r.End.X-7,y+4,4,4)),uiGreen);
  }y+=38;
  var products=relayCategories[developmentTab];int total=(products.Length+3)/4;
  int rows=Math.Max(1,(int)((bottom-y)/81));bool scrolling=total>rows;if(scrolling)rows=Math.Max(1,(int)((bottom-y-24)/81));relayVisibleRows=rows;
  developmentPage=Math.Clamp(developmentPage,0,Math.Max(0,total-rows));relayPages[developmentTab]=developmentPage;
  relayGrid=Px(new(x+20,y,296,Math.Max(75,bottom-y)));UiFrame(new(x+18,y-2,300,Math.Max(79,bottom-y+2)));
  for(int i=developmentPage*4;i<Math.Min(products.Length,(developmentPage+rows)*4);i++){
   var d=products[i];var v=relayProducts[d.Id];bool affordable=team.Money>=d.Cost,built=v.State==0;
   int j=i-developmentPage*4;var r=new Rect2(x+20+j%4*75,y+j/4*81,71,75);string id="product:"+d.Id;
   missionHits.Add(new(id,Px(r),v.Help,v.State==1&&affordable));bool hover=Px(r).HasPoint(missionPointer);
   UiFrame(r);DrawRect(Px(r),v.Active||v.Constructing?uiGreen:hover?chrome:new Color("2a2c2a"),false,2*ui);
   var art=economicArt[d.Icon];UiImage("MAINBUT",pressedUi==id&&art.Pressed>=0?art.Pressed:art.Normal,new(r.Position.X+2,r.Position.Y+2,67,49),v.State==1&&affordable||built?Colors.White:new Color(.6f,.6f,.6f));
   UiLabel(built?d.Kind==2?"Done":"Built":(affordable&&v.State==1?"◆":"◇")+d.Cost,r.Position.X+4,r.End.Y-7,14,built?uiGreen:affordable&&v.State==1?new Color("45d3ea"):new Color("f2b33d"));
   if(built)UiLabel("✓",r.End.X-16,r.Position.Y+15,15,uiGreen);
   else if(v.State==2&&!v.Constructing){DrawRect(Px(new(r.Position.X+2,r.Position.Y+2,15,18)),Colors.Black);UiLabel("!",r.Position.X+6,r.Position.Y+16,15,new Color("f2b33d"));}
   if(v.Count>0||v.Constructing){
    DrawRect(Px(new(r.Position.X+2,r.Position.Y+2,67,16)),new Color(0,0,0,.85f));
    UiLabel(v.Constructing?"BUILD":v.Active?$"{v.Progress:P0}":"WAIT",r.Position.X+3,r.Position.Y+14,12,uiGreen);
    if(v.Count>1)UiLabel("×"+v.Count,r.End.X-24,r.Position.Y+14,12,uiText);
    DrawRect(Px(new(r.Position.X,r.End.Y-3,r.Size.X*v.Progress,3)),uiGreen);
   }
   if(focusedUi==id)DrawRect(Px(r.Grow(2)),uiText,false,2*ui);
  }
  if(scrolling){float navY=Math.Max(bottom-22,y+rows*81);UiButton("products-prev","↑",new(x+20,navY,36,22),"Previous production row",developmentPage>0);UiLabel($"Rows {developmentPage+1}–{Math.Min(total,developmentPage+rows)} / {total}",x+86,bottom-6,12,uiDim);UiButton("products-next","↓",new(x+280,navY,36,22),"Next production row",developmentPage+rows<total);}
 }
}
