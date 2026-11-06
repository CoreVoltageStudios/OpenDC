using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyConstructionOcclusion(){
  // Explicit diagnostic funding / opening advance. Construction itself runs on
  // the normal mission clock through the ordinary production UI and operations.
  missionHarnessManual=true;AdvanceInterference(240);fastGame=false;
  nativeWorld.Teams[0].Money=12000;nativeSelection.Clear();
  camera=NativeRenderPosition(nativeWorld.Teams[0].Cities[0].Entity!).Body+new Vector2(20,-100);zoom=2;ClampMissionCamera();
  await UiClick("tab:0");await MissionCapture("01-starting-hive");
  var stages=new List<object>();
  foreach(var product in new[]{16,17}){
   var dependency=nativeData.Scenario.Dependencies.Single(d=>d.Id==product);
   await UiClick("product:"+product);long deadline=nativeWorld.Tick+360;
   missionHarnessManual=false;string previous="";
   while(nativeWorld.Tick<deadline){
    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    var city=nativeWorld.Teams[0].Cities[dependency.Subject].Entity!;
    var carrier=nativeWorld.Slots[6];
    string stage=carrier is {Living:true}?"carrier-"+carrier.Actions.FirstOrDefault(a=>a.Kind==22)?.Mode:
      city.Body.Set?.Name+"/"+city.Body.Mode;
    if(stage!=previous){previous=stage;animationStage="product-"+product+"/"+stage;
     stages.Add(new{product,stage,nativeWorld.Tick,city=city.Identity,city.Height,carrier=carrier?.Identity});
     await MissionCapture($"product-{product}-tick-{nativeWorld.Tick}");
    }
    if(!nativeWorld.Teams[0].Cities[dependency.Subject].InitializationPending&&nativeWorld.Teams[0].ConstructionSlot==-1)break;
   }
   missionHarnessManual=true;
   Check(nativeWorld.Tick<deadline,"Construction "+product+" completes through approach/build/departure");
   Check(nativeWorld.Teams[0].Cities[dependency.Subject].Entity!.Height==0,"Purchased organ retains native ground height");
  }
  await MissionCapture("02-completed-hive");
  await VerifyCityGroundOcclusion();
  File.WriteAllText(Path.Combine(output,"construction-stages.json"),JsonSerializer.Serialize(new{stages,checks,limits="Synthetic funds/opening and ground-position rack; two normal-clock construction purchases through Godot-dispatched UI. No original-runtime or perceptual playback claim."},new JsonSerializerOptions{WriteIndented=true}));
 }
 async Task VerifyCityGroundOcclusion(){
  missionHarnessManual=true;if(nativeWorld.Tick<240)AdvanceInterference(240);
  var spire=nativeWorld.Teams[0].Cities[5].Entity!; // original central tower, not front organ slot2
  camera=NativeRenderPosition(spire).Body+new Vector2(20,-100);zoom=2;ClampMissionCamera();
  var warrior=nativeWorld.Entities.First(e=>e.Owner==0&&e.Type==8&&e.Living);
  foreach(int offset in new[]{512,-512}){
   warrior.X=spire.X;warrior.Z=spire.Z+offset;warrior.Height=0;nativePrevious.Clear();
   await MissionCapture("ground-tower-depth-"+offset);
   int ground=nativeSpriteQueue.FindIndex(p=>p.Slot==warrior.Slot);
   int tower=nativeSpriteQueue.FindIndex(p=>p.Slot==spire.Slot);
   Check(ground>=0&&tower>=0&&(offset>0?ground<tower:ground>tower),"Ground warrior sorts on correct side of original tower: "+offset);
  }
 }

}

