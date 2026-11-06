using Godot;
using DarkColony.Assets;
using DarkColony.Original;
namespace DarkColony.Viewer;

// Retained native controls, sharing Surface fonts/materials. Bindings only mutate
// WarConfiguration; validation and launch rules live in DarkColony.Original.
public partial class Viewer {
 Control? warPlayersPane,warOptionsPane,warMapPane,warLeftPane;HBoxContainer? warSheets;string warSheet="players";
 bool warSetupVisible,warBinding;
 WarConfiguration warSetup=WarConfiguration.Default();
 WarMap[] warMaps=Array.Empty<WarMap>(),warFiltered=Array.Empty<WarMap>();
 Control? warRoot;VBoxContainer? warLayout;
 ItemList warMapList=null!;LineEdit warSearch=null!;OptionButton warCapacity=null!;
 Label warNotice=null!,warDetails=null!,warPreviewStatus=null!;Button warStart=null!;
 TextureRect warPreview=null!;
 readonly OptionButton[] warRoles=new OptionButton[8],warRaces=new OptionButton[8],warColors=new OptionButton[8],warTeams=new OptionButton[8];
 readonly LineEdit[] warNames=new LineEdit[8];
 readonly Dictionary<string,ImageTexture> warPreviews=new();
 Task<Raster>? warPreviewTask;string warPreviewJob="",warPreviewWanted="";
 Vector2 warLayoutSize;float warLayoutScale;
 ulong warStartAfter;OptionButton? warRank;
 int? warDiagnosticSeed; // Capture harness only; normal setup always chooses a fresh seed.
 readonly HashSet<string> failedWarPreviews=new();
 static readonly string[] WarColors={"Red","Blue","Yellow","Purple","Green","Black","Tan","Cyan"};
 static string WarTerrain(WarMap m)=>m.Terrain.StartsWith("JUNG")?"Jungle":m.Terrain is "ATLANTIS"||m.Terrain.StartsWith("ALIE")?"Underground":"Desert";
 StyleBoxFlat WarStyle(string fill,string border,int inset=10){var s=new StyleBoxFlat{BgColor=new(fill),BorderColor=new(border),ContentMarginLeft=inset,ContentMarginRight=inset,ContentMarginTop=7,ContentMarginBottom=7};s.SetBorderWidthAll(1);return s;}
 Theme WarTheme(){var t=new Theme{DefaultFont=LandfallFont,DefaultFontSize=16};
  foreach(string kind in new[]{"Button","OptionButton","LineEdit"}){
   t.SetStylebox("normal",kind,WarStyle("141612","30352a"));t.SetStylebox("hover",kind,WarStyle("252a1a","c9d86b"));
   t.SetStylebox("pressed",kind,WarStyle("2d3820","c9d86b"));t.SetStylebox("focus",kind,WarStyle("00000000","c9d86b",0));
   t.SetStylebox("disabled",kind,WarStyle("0b0e0c","242924"));
   t.SetColor("font_color",kind,uiText);t.SetColor("font_disabled_color",kind,new("747c71"));
  }
  t.SetStylebox("panel","PanelContainer",WarStyle("0f110df2","30352a",18));
  t.SetStylebox("panel","PopupMenu",WarStyle("101513","677149"));t.SetStylebox("hover","PopupMenu",WarStyle("303d21","c9d86b"));
  t.SetStylebox("panel","ItemList",WarStyle("090d0b","303831"));t.SetStylebox("selected","ItemList",WarStyle("2d391c","c9d86b"));t.SetStylebox("selected_focus","ItemList",WarStyle("2d391c","c9d86b"));
  t.SetColor("font_color","Label",uiText);t.SetColor("font_color","ItemList",uiText);t.SetConstant("v_separation","ItemList",9);
  t.SetConstant("separation","HBoxContainer",8);t.SetConstant("separation","VBoxContainer",8);return t;
 }
 Label WarLabel(string text,int size=16){var l=new Label{Text=text};l.AddThemeFontSizeOverride("font_size",size);return l;}
 OptionButton WarChoice(string[] values,int width=0){var c=new OptionButton{CustomMinimumSize=new(width,36),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};foreach(string s in values)c.AddItem(s);return c;}
 VBoxContainer WarSection(Container parent,string title,bool expand=true){var panel=new PanelContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,SizeFlagsVertical=expand?Control.SizeFlags.ExpandFill:Control.SizeFlags.Fill};parent.AddChild(panel);var box=new VBoxContainer();panel.AddChild(box);var label=WarLabel(title,21);label.AddThemeColorOverride("font_color",uiGreen);box.AddChild(label);return box;}
 void OpenWarSetup(){
  if(!mainMenu){var config=mission.War?.Configuration.Copy();ReturnMainMenu();if(config!=null)warSetup=config;}
  missionPanel="";focusedUi="";ReleasePointerBoundary();warMaps=WarCatalog.Read(Path.Combine(root,"private/DC"));
  if(warRoot!=null){warRoot.QueueFree();warRoot=null;}BuildWarSetup();warSetupVisible=true;warRoot!.Show();warStartAfter=Time.GetTicksMsec()+300;
  BindWarPlayers();FilterWarMaps();ValidateWarSetup();LayoutWarSetup();warSearch.GrabFocus();QueueRedraw();
 }
 void HideWarSetup(){warSetupVisible=false;warRoot?.Hide();if(GetViewport().GuiGetFocusOwner() is {} c&&warRoot?.IsAncestorOf(c)==true)c.ReleaseFocus();focusedUi="war-setup";QueueRedraw();}
 void BuildWarSetup(){
  warLayoutSize=Vector2.Zero;warOptionsControls.Clear();
  warRoot=new Control{Name="WarSetup",ZIndex=150,TextureFilter=TextureFilterEnum.Linear,MouseFilter=Control.MouseFilterEnum.Stop,Theme=WarTheme()};AddChild(warRoot);
  warLayout=new VBoxContainer();warRoot.AddChild(warLayout);warLayout.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
  var heading=new HBoxContainer();warLayout.AddChild(heading);heading.AddChild(WarLabel(network==null?"SINGLE-PLAYER WAR":"MULTIPLAYER LOBBY",28));var subtitle=WarLabel((network!=null?"Network battle":"Local battle")+" · Original match rules");subtitle.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;subtitle.HorizontalAlignment=HorizontalAlignment.Right;heading.AddChild(subtitle);
  if(network!=null){networkLobbyText=WarLabel("",14);networkLobbyText.AutowrapMode=TextServer.AutowrapMode.WordSmart;warLayout.AddChild(networkLobbyText);}
  warSheets=new HBoxContainer();warLayout.AddChild(warSheets);foreach(string page in new[]{"players","maps","options"}){string value=page;var tab=new Button{Text=page=="players"?"Players":page=="maps"?"Change map ›":"Match options ›",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};warSheets.AddChild(tab);tab.Pressed+=()=>{warSheet=value;warLayoutSize=Vector2.Zero;LayoutWarSetup();};}
  var columns=warColumns=new HBoxContainer{SizeFlagsVertical=Control.SizeFlags.ExpandFill};columns.AddThemeConstantOverride("separation",18);warLayout.AddChild(columns);
  var leftScroll=new ScrollContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsStretchRatio=1.55f,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,FollowFocus=true};columns.AddChild(leftScroll);warLeftPane=leftScroll;
  var left=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};leftScroll.AddChild(left);
  var players=WarSection(left,"PLAYERS",false);warPlayersPane=players.GetParent<Control>();
  var header=new HBoxContainer();players.AddChild(header);
  foreach(var col in new[]{("Controller",108),("Faction",96),("Name",100),("Colour",94),("Team",74)}){var l=WarLabel(col.Item1,14);l.CustomMinimumSize=new(col.Item2,0);if(col.Item1=="Name")l.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;header.AddChild(l);}if(network!=null)header.AddChild(new Label{Text="Status",CustomMinimumSize=new(76,0)});
  for(int i=0;i<8;i++){
   int slot=i;var row=new HBoxContainer();players.AddChild(row);
   var role=warRoles[i]=WarChoice(network==null?new[]{"AI","AI+","You","Inactive"}:new[]{"AI","AI+","Host","Inactive","Open"},108);role.SizeFlagsHorizontal=Control.SizeFlags.Fill;row.AddChild(role);
   var race=warRaces[i]=WarChoice(new[]{"Human","Taar"},96);race.SizeFlagsHorizontal=Control.SizeFlags.Fill;row.AddChild(race);
   var name=warNames[i]=new LineEdit{MaxLength=16,CustomMinimumSize=new(100,36),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};row.AddChild(name);
   var color=warColors[i]=WarChoice(WarColors,94);color.SizeFlagsHorizontal=Control.SizeFlags.Fill;row.AddChild(color);
   var team=warTeams[i]=WarChoice(Enumerable.Range(1,8).Select(n=>n.ToString()).ToArray(),74);team.SizeFlagsHorizontal=Control.SizeFlags.Fill;row.AddChild(team);
   if(network!=null){networkStates[i]=WarLabel("",12);networkStates[i].CustomMinimumSize=new(76,36);row.AddChild(networkStates[i]);role.SetItemDisabled(2,true);}
   role.TooltipText="One local player; AI and AI+ use the original opponent profiles. AI+ receives double mining income.";
   team.TooltipText="Players with the same team begin allied. Starting position is assigned separately.";
   role.ItemSelected+=n=>{if(warBinding)return;if((WarController)n==WarController.Local){int old=Array.FindIndex(warSetup.Slots,s=>s.Controller==WarController.Local);if(old>=0&&old!=slot)warSetup.Slots[old]=warSetup.Slots[old] with{Controller=WarController.Ai};}warSetup.Slots[slot]=warSetup.Slots[slot] with{Controller=(WarController)n};BindWarPlayers();ValidateWarSetup();};
   race.ItemSelected+=n=>{if(warBinding)return;warSetup.Slots[slot]=warSetup.Slots[slot] with{Race=(int)n};BindWarPlayers();ValidateWarSetup();};
   color.ItemSelected+=n=>{if(warBinding)return;warSetup.Slots[slot]=warSetup.Slots[slot] with{Color=(int)n};ValidateWarSetup();};
   team.ItemSelected+=n=>{if(warBinding)return;warSetup.Slots[slot]=warSetup.Slots[slot] with{Team=(int)n};ValidateWarSetup();};
   name.TextChanged+=s=>{if(warBinding||!name.Editable||network?.Players.Any(p=>p.Slot==slot)==true)return;if(network?.Host==true)warNameDrafts[slot]=s;warSetup.Slots[slot]=warSetup.Slots[slot] with{Name=s};ValidateWarSetup();};
  }
  var hint=WarLabel("Matching teams start allied. AI+ has twice the mining income.\nGrey rows are unavailable on this map; inactive slots are not opponents.",13);hint.AutowrapMode=TextServer.AutowrapMode.WordSmart;players.AddChild(hint);
  warNotice=WarLabel("");warNotice.AutowrapMode=TextServer.AutowrapMode.WordSmart;warNotice.AddThemeColorOverride("font_color",new Color("edbf68"));players.AddChild(warNotice);
  var options=WarSection(left,"MATCH OPTIONS",false);warOptionsPane=options.GetParent<Control>();var grid=new GridContainer{Columns=2};grid.AddThemeConstantOverride("h_separation",18);options.AddChild(grid);
  void Option(string label,string[] items,int selected,string help,Action<int> change){var line=new HBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};grid.AddChild(line);var l=WarLabel(label);l.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;l.TooltipText=help;line.AddChild(l);var choice=WarChoice(items,106);choice.SizeFlagsHorizontal=Control.SizeFlags.Fill;choice.Select(selected);choice.TooltipText=help;line.AddChild(choice);warOptionsControls.Add(choice);if(label=="Commander rank")warRank=choice;choice.ItemSelected+=n=>{if(warBinding||networkBinding)return;change((int)n);ValidateWarSetup();};}
  Option("Storage cells",new[]{"Off","Low","Medium","High"},warSetup.Options.Storage,"Scatter 0, 1, 2 or 3 collectible storage cells per active base.",n=>warSetup=warSetup with{Options=warSetup.Options with{Storage=n}});
  Option("Artifacts",new[]{"Off","Low","Medium","High"},warSetup.Options.Artifacts,"Map-authored artifact sites and contents. Detect with research; excavate with harvesters.",n=>warSetup=warSetup with{Options=warSetup.Options with{Artifacts=n}});
  Option("Erupting vents",new[]{"Off","On"},warSetup.Options.Erupting?1:0,"Allow map-scripted dormant vents to activate.",n=>warSetup=warSetup with{Options=warSetup.Options with{Erupting=n==1}});
  Option("Renewable vents",new[]{"Off","On"},warSetup.Options.Renewable?1:0,"Allow map-scripted exhausted vents to return.",n=>warSetup=warSetup with{Options=warSetup.Options with{Renewable=n==1}});
  var percents=Enumerable.Range(1,20).Select(n=>$"{n*25}%").ToArray();
  Option("P7 quantity",percents,warSetup.Options.Quantity-1,"Total Petra-7 contained in each vent, relative to original map amounts.",n=>warSetup=warSetup with{Options=warSetup.Options with{Quantity=n+1}});
  Option("P7 flow",percents,warSetup.Options.Flow-1,"Petra-7 transferred per harvesting interval, relative to original map rates.",n=>warSetup=warSetup with{Options=warSetup.Options with{Flow=n+1}});
  Option("Commander rank",new[]{"Lieutenant","Captain","Major","Colonel"},warSetup.Options.CommanderRank,"Starting commander rank for every player; independent of campaign ranks.",n=>warSetup=warSetup with{Options=warSetup.Options with{CommanderRank=n}});
  var maps=WarSection(columns,"MAPS");maps.GetParent<Control>().SizeFlagsStretchRatio=1;warMapPane=maps.GetParent<Control>();
  var filters=new HBoxContainer();maps.AddChild(filters);warSearch=new LineEdit{PlaceholderText="Search maps",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,CustomMinimumSize=new(120,36)};filters.AddChild(warSearch);warCapacity=WarChoice(new[]{"All sizes","2 players","4 players","6 players","8 players"},125);warCapacity.SizeFlagsHorizontal=Control.SizeFlags.Fill;filters.AddChild(warCapacity);
  warSearch.TextChanged+=_=>FilterWarMaps();warCapacity.ItemSelected+=_=>FilterWarMaps();
  warMapList=new ItemList{SizeFlagsVertical=Control.SizeFlags.ExpandFill,CustomMinimumSize=new(0,140),AllowReselect=true};maps.AddChild(warMapList);warMapList.ItemSelected+=n=>SelectWarMap(warFiltered[n]);
  warDetails=WarLabel("",16);warDetails.AutowrapMode=TextServer.AutowrapMode.WordSmart;maps.AddChild(warDetails);
  warPreview=new TextureRect{CustomMinimumSize=new(0,190),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered};maps.AddChild(warPreview);
  warPreviewStatus=WarLabel("",13);maps.AddChild(warPreviewStatus);
  var footer=new HBoxContainer();warLayout.AddChild(footer);var back=new Button{Text="Back   Esc",CustomMinimumSize=new(180,44)};footer.AddChild(back);back.Pressed+=()=>{if(network!=null){CloseNetwork();HideWarSetup();OpenMultiplayer();}else HideWarSetup();};
  footer.AddChild(new Control{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill});if(network!=null){networkReady=new Button{Text="Ready",CustomMinimumSize=new(170,44)};footer.AddChild(networkReady);networkReady.Pressed+=()=>network?.Ready(!(network.Players.FirstOrDefault(p=>p.Id==network.Id)?.Ready??false));}warStart=new Button{Text="Start battle",CustomMinimumSize=new(240,44)};warStart.AddThemeStyleboxOverride("normal",WarStyle("263019","c9d86b"));footer.AddChild(warStart);
  warStart.Pressed+=()=>{if(Time.GetTicksMsec()<warStartAfter)return;try{if(network!=null){network.Start();return;}StartWarMatch(warSetup with{Seed=capturing&&warDiagnosticSeed.HasValue?warDiagnosticSeed.Value:Random.Shared.Next(65536)});}catch(Exception e){warNotice.Text=e.Message;}};
 }
 // Reassigning even identical LineEdit text resets its caret. Genuine model
 // changes must also retain the user's editing position and selection.
 static void BindWarName(LineEdit field,string value){
  if(field.Text==value)return;
  bool editing=field.Editable&&field.HasFocus(),selected=field.HasSelection();
  int caret=field.CaretColumn,from=selected?field.GetSelectionFromColumn():0,to=selected?field.GetSelectionToColumn():0;
  field.Text=value;
  if(!editing)return;
  field.CaretColumn=Math.Min(caret,value.Length);
  if(selected)field.Select(Math.Min(from,value.Length),Math.Min(to,value.Length));
 }
 void BindWarPlayers(){warBinding=true;int capacity=warMaps.Single(m=>m.Id==warSetup.MapId).Capacity;
  for(int i=0;i<8;i++){var s=warSetup.Slots[i];bool unavailable=i>=capacity,off=s.Controller==WarController.None;if(network!=null)warRoles[i].SetItemText(4,network.Players.Any(p=>p.Slot==i)?"Player":"Open");warRoles[i].Select((int)s.Controller);warRoles[i].Disabled=unavailable;warRaces[i].Select(s.Race);warColors[i].Select(s.Color);warTeams[i].Select(s.Team);BindWarName(warNames[i],s.Name);warNames[i].Editable=!unavailable&&(network==null?!off:network.Host&&network.Phase=="lobby"&&!network.Players.Any(p=>p.Slot==i));warRaces[i].Disabled=warColors[i].Disabled=warTeams[i].Disabled=unavailable||off;warRoles[i].TooltipText=unavailable?$"This map supports {capacity} slots. Choose a larger map to use slot {i+1}.":"You, AI, AI+ or inactive. Exactly one local player is required.";}
  if(warRank!=null){int race=warSetup.Slots.FirstOrDefault(s=>s.Controller==WarController.Local)?.Race??0;var names=nativeData.Reference.Ints(race==0?0x488cecU:0x488cfcU,4).Select(p=>nativeData.Reference.Text((uint)p).Trim()).ToArray();for(int i=0;i<4;i++)warRank.SetItemText(i,names[i]);}
  warBinding=false;
 }
 void ValidateWarSetup(){
  if(network!=null){NetworkSetupChanged();return;}
  for(int i=0;i<8;i++){var slot=warSetup.Slots[i];int other=Array.FindIndex(warSetup.Slots,s=>!ReferenceEquals(s,slot)&&s.Controller!=WarController.None&&s.Color==slot.Color);bool conflict=slot.Controller!=WarController.None&&other>=0;
   warColors[i].TooltipText=conflict?$"This colour is also used by slot {other+1} ({warSetup.Slots[other].Name}). Choose a different colour.":"Each active player has a unique colour.";
   if(conflict)warColors[i].AddThemeStyleboxOverride("normal",WarStyle("281b13","edbf68"));else warColors[i].RemoveThemeStyleboxOverride("normal");
  }
  try{warSetup.Validate(Path.Combine(root,"private/DC"));warNotice.Text=$"{warSetup.Slots.Count(s=>s.Controller!=WarController.None)} active players · Starting positions are shuffled.";warStart.Disabled=false;}catch(InvalidDataException e){warNotice.Text=e.Message;warStart.Disabled=true;}
 }
 void FilterWarMaps(){if(warMapList==null)return;int capacity=warCapacity.Selected*2;warFiltered=warMaps.Where(m=>(capacity==0||m.Capacity==capacity)&&m.Title.Contains(warSearch.Text,StringComparison.OrdinalIgnoreCase)).ToArray();warMapList.Clear();for(int i=0;i<warFiltered.Length;i++){var m=warFiltered[i];warMapList.AddItem($"{m.Title}   ·   {m.Capacity}P {WarTerrain(m)}");if(m.Id==warSetup.MapId)warMapList.Select(i);}warDetails.Text=warFiltered.Length==0?"No maps match these filters. Clear search or change capacity.":$"{warFiltered.Length} maps · Selected: {warMaps.Single(m=>m.Id==warSetup.MapId).Title}";RequestWarPreview();}
 void SelectWarMap(WarMap map){
  if(network!=null&&(!network.Host||network.Players.Any(p=>p.Slot>=map.Capacity))){warNotice.Text="Connected players exceed this map capacity. Keep a larger map until they leave.";return;}
  for(int n=0;n<warFiltered.Length;n++)if(warFiltered[n].Id==map.Id){warMapList.Select(n);warMapList.EnsureCurrentIsVisible();}
  warSetup=warSetup with{MapId=map.Id};bool removed=false;
  for(int i=map.Capacity;i<8;i++)if(warSetup.Slots[i].Controller!=WarController.None){removed=true;if(warSetup.Slots[i].Controller==WarController.Local)warSetup.Slots[0]=warSetup.Slots[0] with{Controller=WarController.Local};warSetup.Slots[i]=warSetup.Slots[i] with{Controller=WarController.None};}
  BindWarPlayers();ValidateWarSetup();if(removed)warNotice.Text="Slots beyond this map's capacity were set inactive. Review players before starting.";
  warDetails.Text=$"{map.Title} · {map.Capacity} players · {WarTerrain(map)}"+(map.Id=="A2PLAY01"?"\nCombat arena: starting army only; no cities or resource sites.":"");RequestWarPreview();
 }
 void LayoutWarSetup(){if(!warSetupVisible||warRoot==null)return;var size=GetViewportRect().Size/ui;
  if(warLayoutSize!=size||warLayoutScale!=ui){warLayoutSize=size;warLayoutScale=ui;warRoot.Position=new Vector2(80,28)*ui;warRoot.Scale=Vector2.One*ui;warRoot.Size=new(size.X-112,size.Y-56);warPreview.CustomMinimumSize=new(0,size.Y<800?132:220);bool compact=size.X<1500||size.Y<800;warSheets!.Visible=compact;warPlayersPane!.Visible=!compact||warSheet=="players";warOptionsPane!.Visible=!compact||warSheet=="options";warMapPane!.Visible=!compact||warSheet=="maps";warLeftPane!.Visible=!compact||warSheet!="maps";}
  PollWarPreview();
 }
 void RequestWarPreview(){warPreviewWanted=warSetup.MapId;if(warPreviews.TryGetValue(warPreviewWanted,out var texture)){warPreview.Texture=texture;warPreviewStatus.Text="Original map terrain · positions assigned at launch";}else{warPreview.Texture=null;warPreviewStatus.Text="Preparing terrain preview…";}PollWarPreview();}
 void PollWarPreview(){
  if(warPreviewTask?.IsCompleted==true){try{var pixels=warPreviewTask.GetAwaiter().GetResult();var texture=OriginalTexture(pixels);if(warPreviews.Count>=8){var first=warPreviews.First();warPreviews.Remove(first.Key);if(warPreview.Texture==first.Value)warPreview.Texture=null;first.Value.Dispose();}warPreviews[warPreviewJob]=texture;if(warPreviewWanted==warPreviewJob){warPreview.Texture=texture;warPreviewStatus.Text="Original map terrain · positions assigned at launch";}}catch(Exception){failedWarPreviews.Add(warPreviewJob);warPreviewStatus.Text="Terrain preview unavailable. Map remains selectable.";}warPreviewTask=null;}
  if(warPreviewTask==null&&!warPreviews.ContainsKey(warPreviewWanted)&&warPreviewWanted.Length>0&&!failedWarPreviews.Contains(warPreviewWanted)){string path=Path.Combine(root,"private/DC"),id=warPreviewWanted;warPreviewJob=id;warPreviewTask=Task.Run(()=>BuildWarPreview(path,id));}
 }
 static Raster BuildWarPreview(string path,string id){
  var s=OriginalScenarioImport.Load(path,WarCatalog.Find(path,id).Scenario);var palette=GameColors.Table(GameColors.ReadGifPalette(File.ReadAllBytes(Path.Combine(path,s.Header.PaletteName))),5,16,ColorBank.Terrain);
  var rgba=new byte[s.Map.Width*s.Map.Height*4];var cache=new Dictionary<(int,int,int),(byte,byte,byte)>();
  for(int z=0;z<s.Map.Height;z++)for(int x=0;x<s.Map.Width;x++){var c=s.Map.At(x,z);var key=((int)c.BackgroundId,(int)c.ForegroundId,c.Attributes&0x60);if(!cache.TryGetValue(key,out var rgb)){var bg=s.Terrain.ByEditorId[c.BackgroundId].Indices;var fg=c.ForegroundId==0?null:s.Terrain.ByEditorId[c.ForegroundId].Indices;int red=0,green=0,blue=0;for(int y=2;y<32;y+=4)for(int px=2;px<32;px+=4){int color=bg[y*32+((c.Attributes&0x20)!=0?31-px:px)];if(fg!=null){int f=fg[y*32+((c.Attributes&0x40)!=0?31-px:px)];if(f!=0)color=f;}var p=palette[color];red+=p.R;green+=p.G;blue+=p.B;}cache[key]=rgb=((byte)(red/64),(byte)(green/64),(byte)(blue/64));}int at=((s.Map.Height-1-z)*s.Map.Width+x)*4;rgba[at]=rgb.Item1;rgba[at+1]=rgb.Item2;rgba[at+2]=rgb.Item3;rgba[at+3]=255;}
  return new(s.Map.Width,s.Map.Height,rgba);
 }
}
