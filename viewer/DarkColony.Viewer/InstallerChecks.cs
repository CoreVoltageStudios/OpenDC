using Godot;
using DarkColony.Original;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyInstaller(){
  if(IsolatedPlayerData==null&&installProfile.Length==0)throw new InvalidOperationException("Installer checks require a comparison or isolated profile.");
  Check(productVersion=="0.2.0-test.1","Installed product version");
  if(missionSuite=="installer-resume"){
   Check(Math.Abs(ui-1.25f)<.001&&Math.Abs(sfxVolume-.6f)<.001&&Math.Abs(musicVolume-.4f)<.001&&!fastGame&&nativeSampling==0&&!enhancedVideo,"Installed settings survive exit/upgrade/reinstall");
   Check(continueSlot==0,"Installed Continue finds retained save");await UiClick("resume-save");Check(mission!.CampaignIndex==4&&nativeWorld.Tick>=250,"Installed Continue restores Roswell");
   await MenuCapture("installed-continued");return;
  }
  if(missionSuite=="installer-errors"){
   string original=File.ReadAllText(SavePath(0));var wrong=JsonNode.Parse(original)!;wrong["version"]=999;string invalid=wrong.ToJsonString();File.WriteAllText(SavePath(1),invalid);
   File.WriteAllText(SavePath(2),"{unreadable fixture");RefreshPersistenceCatalog();await UiClick("load");await UiClick("load-slot:1");
   Check(missionPanel=="save-error"&&mainMenu,"Incompatible save explains failure");await MenuCapture("installed-incompatible-save");await UiClick("error-close");
   await UiClick("load-slot:2");Check(missionPanel=="save-error"&&mainMenu,"Unreadable save explains failure");await MenuCapture("installed-unreadable-save");
   Check(File.ReadAllText(SavePath(1))==invalid&&File.ReadAllText(SavePath(0))==original,"Invalid load preserves all existing saves");return;
  }
  if(missionSuite=="installer-fallback"){
   Check(!enhancedVideo,"Enhanced movie path is never selected");
   var movie=new OriginalMovie();movie.Load(Path.Combine(root,"generated/original-video/v1/avhd4.odcv"));AddChild(movie);movie.Size=GetViewportRect().Size;movie.Play();await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);Check(!movie.Enhanced&&movie.Frame>0,"Original fallback movie plays without enhancement runtime");await MenuCapture("installed-original-fallback");RemoveChild(movie);movie.Free();return;
  }
  Check(continueSlot<0&&availableInterference==null,"Fresh installed profile preserves first-mission unlock rules");
  await UiClick("campaign");await MenuCapture("installed-fresh-campaign");await UiClick("campaign-start:0");await MenuCapture("installed-first-briefing");await Send(Key.Enter);AdvanceInterference(250);
  // Isolated routing fixture only. No simulated wins and no changed mission outcomes.
  var completed=new JsonObject();var codec=new SaveCodec(nativeData);
  for(int i=0;i<4;i++)completed[i.ToString()]=new JsonObject{["runId"]=Guid.NewGuid().ToString("N"),["entry"]=codec.Node(new OriginalProfileContext(i,0,0,new int[4],"Installer routing fixture")),["profile"]=codec.Node(new OriginalProfileContext(i+1,0,0,new int[4],"Installer routing fixture"))};
  SaveCodec.Write(CampaignPath,new JsonObject{["completed"]=completed});ReturnMainMenu();
  for(int n=0;n<5;n++){
   await UiClick("campaign");await UiClick("campaign-start:"+n);Check(missionBriefing&&mission!.CampaignIndex==n,"Installed campaign enters mission "+(n+1));await MenuCapture("installed-briefing-"+(n+1));
   await Send(Key.Enter);AdvanceInterference(250);await Send(Key.Tab);await MenuCapture("installed-play-"+(n+1));Check(missionMusic!=null&&missionSoundPaths.Count>0,"Installed music/sound content bound "+(n+1));
   missionHarnessManual=false;await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);missionHarnessManual=true;Check(missionMusic!.Frame>0,"Installed music advances "+(n+1));
   await Send(Key.Escape);await UiClick("save");await UiClick("save-slot:0");if(missionPanel=="overwrite")await UiClick("overwrite-yes");Check(File.Exists(SavePath(0)),"Installed UI saves mission "+(n+1));await UiClick("close");ReturnMainMenu();
  }
  foreach(string name in new[]{"avhd4","hvad2","avhd5","atran3","jtalk3","jcrawl","escape","atran2","scrub","waste"})foreach(bool enhanced in new[]{false}){
   var movie=new OriginalMovie();movie.Load(Path.Combine(root,"generated",enhanced?"enhanced-video":"original-video","v1",name+".odcv"));AddChild(movie);movie.Size=GetViewportRect().Size;movie.Play();await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);
   Check(movie.Frame>0&&movie.Enhanced==enhanced,"Installed clip decodes/advances "+name+" "+enhanced);if(name is "avhd4" or "scrub")await MenuCapture("installed-media-"+name+"-"+enhanced);RemoveChild(movie);movie.Free();
  }
  await UiClick("options");await UiClick("art-crisp");await UiClick("speed-classic");ui=1.25f;sfxVolume=.6f;musicVolume=.4f;SavePresentationPreferences();await MenuCapture("installed-options");await UiClick("close");await MenuCapture("installed-saved-menu");
 }
}
