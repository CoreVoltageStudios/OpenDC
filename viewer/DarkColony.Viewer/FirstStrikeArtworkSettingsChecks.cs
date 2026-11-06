using Godot;
using DarkColony.Assets;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyArtworkSettings(){
  missionHarnessManual=true;bool reload=missionSuite=="rendering-settings-reload";
  Check(nativeSampling==(reload?0:1),reload?"fresh process restores Crisp":"missing preference defaults to Classic");
  string invalid=Path.Combine(output,"invalid-preference.json");File.WriteAllText(invalid,"invalid");Check(ArtworkPreference.Load(invalid,out var invalidDiagnostic)==ArtworkStyle.Classic&&invalidDiagnostic!=null,"invalid preference falls back to Classic with diagnostic");
  while(nativeWorld.Tick<180){mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();}
  zoom=2;await Send(Key.Tab);await UiCapture("settings-initial-world");
  string proof=VisualStateProof();var fog=(byte[])nativeFogPixels.Clone();var knowledge=(byte[])nativeFogState.Clone();
  int orders=nativeWorld.Events.Count(e=>e.Kind=="order");await Send(Key.O);
  foreach(float scale in new[]{.75f,1f,1.5f}){
   ui=scale;await UiClick("art-crisp");Check(nativeSampling==0&&nativeFineFog,"Crisp retains corrected fog at UI scale "+scale);await UiCapture("settings-crisp-"+scale);
   Check(nativeBodies.Values.SelectMany(b=>b.Parts).Select(p=>p.Texture).OfType<CanvasTexture>().All(t=>t.TextureFilter==TextureFilterEnum.Nearest),"Crisp uses nearest on world bodies and shadows");
   Check(missionHits.All(h=>GetViewportRect().Encloses(h.Rect)),"options controls remain inside viewport "+scale);
   await UiClick("art-classic");Check(nativeSampling==1&&nativeFineFog,"Classic restores smooth art with corrected fog "+scale);await UiCapture("settings-classic-"+scale);
   Check(TextureFilter==TextureFilterEnum.Nearest&&nativeBodies.Values.SelectMany(b=>b.Parts).Select(p=>p.Texture).OfType<CanvasTexture>().All(t=>t.TextureFilter==TextureFilterEnum.Linear),"Classic filters world while UI remains nearest");
  }
  Check(proof==VisualStateProof(),"artwork switching preserves complete world, animation phases, RNG, sight and mission state");
  Check(fog.SequenceEqual(nativeFogPixels)&&knowledge.SequenceEqual(nativeFogState),"both artwork modes use identical fog pixels and visibility knowledge");
  Check(nativeWorld.Events.Count(e=>e.Kind=="order")==orders,"options clicks never issue world orders");
  await UiClick(reload?"art-classic":"art-crisp");Check(ArtworkPreference.Load(artworkPreferencePath,out var error)==(reload?ArtworkStyle.Classic:ArtworkStyle.Crisp)&&error==null,"selected artwork is persisted on disk");
  await Send(Key.Escape);ui=GetViewportRect().Size.Y>1080?4/3f:1;await UiCapture("settings-final-world");
  int selected=nativeSampling;await Send(Key.R);Check(nativeSampling==selected&&nativeFineFog,"restart retains artwork preference and corrected fog");
  File.WriteAllText(Path.Combine(output,"preference-receipt.json"),JsonSerializer.Serialize(new{reload,artworkPreferencePath,nativeSampling,nativeFineFog,saved=File.ReadAllText(artworkPreferencePath)}));
 }
}
