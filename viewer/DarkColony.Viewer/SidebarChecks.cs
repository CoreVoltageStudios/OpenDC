using Godot;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifySidebarLayout(){
  void Layout(){
   var hits=missionHits.ToArray();
   Check(hits.All(h=>GetViewportRect().Encloses(h.Rect)),"Sidebar controls fit framebuffer");
   Check(!hits.Where((a,i)=>hits.Skip(i+1).Any(b=>a.Rect.Intersects(b.Rect))).Any(),"Sidebar controls never overlap");
  }
  foreach(float scale in new[]{.75f,1f,1.25f,1.5f}){
   ui=scale;await UiCapture("sidebar-scale-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture));Layout();
   if(economicMission){
    foreach(int tab in new[]{0,2,1}){await UiClick("tab:"+tab);await UiCapture("sidebar-tab-"+tab+"-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture));Layout();
     var next=missionHits.FirstOrDefault(h=>h.Id=="products-next"&&h.Enabled);
     if(next!=null){var old=missionHits.First(h=>h.Id.StartsWith("product:")).Id;await UiClick("products-next");await UiCapture("sidebar-page-"+tab+"-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture));Check(missionHits.First(h=>h.Id.StartsWith("product:")).Id!=old,"Purchase paging changes choices");Layout();await UiClick("products-prev");}
    }
   }
  }
  ui=Math.Clamp(GetViewportRect().Size.Y/1080,1,1.5f);
  if(economicMission){
   nativeSelection.Clear();var city=nativeWorld.Teams[0].Cities[0].Entity!;nativeSelection.Add((city.Slot,city.Incarnation));
   await UiClick("tab:0");await UiCapture("sidebar-city-hive");Layout();
   await UiClick("tab:2");await UiCapture("sidebar-city-upgrades");
   nativeWorld.Teams[0].Money=12000;await UiClick("tab:1");await UiCapture("sidebar-funded");int orders=nativeWorld.Events.Count(e=>e.Kind=="order");var product=missionHits.First(h=>h.Id.StartsWith("product:")&&h.Enabled);await UiClick(product.Id);
   Check(nativeWorld.Teams[0].Money<12000&&nativeWorld.Events.Count(e=>e.Kind=="order")==orders,"Purchase commits resources without a world order");await UiCapture("sidebar-purchase");
  }
  var previous=camera;int count=nativeWorld.Events.Count(e=>e.Kind=="order");await NativeMouse(MouseButton.Left,MiniMapRect.GetCenter());
  Check(camera!=previous&&nativeWorld.Events.Count(e=>e.Kind=="order")==count,"Minimap navigation does not issue world orders");
  await Send(Key.Tab);await UiCapture("sidebar-commander");
  SaveMissionSlot(0);LoadMissionSlot(0);missionPanel="";await UiCapture("sidebar-restored");Layout();Check(NativeSelected().Any(e=>nativeWorld.IsCommander(e)),"Loaded sidebar retains commander selection");
  ResetFirstStrike();StartFirstStrike();AdvanceInterference(240);await Send(Key.Tab);await UiCapture("sidebar-restarted");Layout();
 }
}
