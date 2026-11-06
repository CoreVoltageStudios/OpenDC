using Godot;
namespace DarkColony.Viewer;

// Native, retained Godot input/focus surfaces over the existing original-art renderer.
// Nodes are pooled; changing game state does not rebuild the scene tree.
public partial class RelayInputControl : Control {
 public Action<InputEvent>? Dispatch;
 public Action? Focused;
 public override void _GuiInput(InputEvent input) {
  if(input is InputEventMouse m) { var copy=(InputEventMouse)m.Duplicate();copy.Position+=GlobalPosition;Dispatch?.Invoke(copy);copy.Dispose(); }
  else Dispatch?.Invoke(input);
  AcceptEvent();
 }
 public override void _Ready()=>FocusEntered+=()=>Focused?.Invoke();
}

public partial class Viewer {
 public override void _Input(InputEvent input){
  if(RestorationInput(input)){GetViewport().SetInputAsHandled();return;}
  if(input is InputEventMouse mouse&&!GetViewportRect().HasPoint(mouse.Position)){missionPointer=mouse.Position;CancelCameraGesture();GetViewport().SetInputAsHandled();QueueRedraw();return;}
  if(CouncilIntroductionInput(input)){GetViewport().SetInputAsHandled();return;}
  // Route keys before Godot's built-in ui_focus_next handling. Otherwise plain
  // Tab can acquire a Control before _UnhandledInput sees commander selection,
  // depending on window focus. MissionInterfaceInput owns modal/HUD navigation.
  if(contentSetup!=null&&input is InputEventKey{Keycode:Key.Escape,Pressed:true,Echo:false}){if(importTask is not {IsCompleted:false})contentBack?.EmitSignal(Button.SignalName.Pressed);GetViewport().SetInputAsHandled();return;}
  if(!ready||!nativeMode||!(firstStrike||interferenceDev)||input is not InputEventKey)return;
  if(desktopReview&&input is InputEventKey{Keycode:Key.F12,Pressed:true,Echo:false}){CaptureDesktopReview();GetViewport().SetInputAsHandled();return;}
  if(MultiplayerVisible){if(input is InputEventKey{Keycode:Key.Escape,Pressed:true,Echo:false}){CloseNetwork();focusedUi="multiplayer";GetViewport().SetInputAsHandled();}return;}
  if(warSetupVisible){if(input is InputEventKey{Keycode:Key.Escape,Pressed:true,Echo:false}){if(network!=null){CloseNetwork();HideWarSetup();OpenMultiplayer();}else HideWarSetup();GetViewport().SetInputAsHandled();}return;}
  if(GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)return;
  NativeInput(input);GetViewport().SetInputAsHandled();
 }
 Control? relayRoot;
 readonly List<RelayInputControl> relayControls=new();
 readonly List<string> relayControlIds=new();
 PanelContainer? relayTooltip;
 Label? relayTooltipLabel;
 Rect2 relayTooltipBounds;
 string tooltipCopy="";
 bool relayPointerHelp;
 string relayReturnFocus="";
 readonly Dictionary<string,GradientTexture2D> relayGradients=new();
 void UiMetal(Rect2 r,bool active=false,bool rail=false){
  string key=rail?"rail":active?"active":"idle";
  if(!relayGradients.TryGetValue(key,out var texture)){
   texture=new GradientTexture2D{Width=rail?32:1,Height=rail?1:32,FillFrom=Vector2.Zero,FillTo=rail?Vector2.Right:Vector2.Down,
    Gradient=new Gradient{Colors=rail?new[]{new Color("2a2c2e"),new Color("7d8083"),new Color("1d1f20")}:active?new[]{new Color("a3a6a8"),new Color("76797b")}:new[]{new Color("45484a"),new Color("2e3032")},Offsets=rail?new[]{0f,.4f,1f}:new[]{0f,1f}}};relayGradients[key]=texture;
  }
  DrawTextureRect(texture,Px(r),false);DrawRect(Px(r),Colors.Black,false,ui);
  DrawLine(Px(r).Position+Vector2.One*ui,new Vector2(r.End.X-1,r.Position.Y+1)*ui,new Color("a3a6a8"),ui);
  if(active)DrawRect(Px(new(r.Position.X,r.End.Y-3,r.Size.X,3)),uiGreen);
 }
 void CloseRelayPanel(){if(missionPanel=="missions"){OpenMissionPanel("campaign");focusedUi="journey:"+(IsTraining?"training":IsCouncil?"council":"base")+":"+(IsHuman?"human":"taar");return;}if(missionPanel=="abandon"){ActivateMissionUi("abandon-cancel");return;}if(landfallNavigation.TryPop(out var back)){OpenMissionPanel(back.Panel);focusedUi=back.Focus;return;}if(missionPanel=="playtesting"||(mainMenu&&missionPanel=="controls")){OpenMissionPanel("options");return;}missionPanel="";focusedUi=mainMenu?relayReturnFocus:"";persistenceNotice="";}
 bool RelayGridKey(InputEventKey key){
  if(!focusedUi.StartsWith("product:")||key.Keycode is not (Key.Left or Key.Right or Key.Up or Key.Down))return false;
  var products=relayCategories[developmentTab];int index=Array.FindIndex(products,d=>"product:"+d.Id==focusedUi);
  index=Math.Clamp(index+(key.Keycode==Key.Left?-1:key.Keycode==Key.Right?1:key.Keycode==Key.Up?-4:4),0,products.Length-1);
  int row=index/4;if(row<developmentPage)developmentPage=row;else if(row>=developmentPage+relayVisibleRows)developmentPage=row-relayVisibleRows+1;
  focusedUi="product:"+products[index].Id;QueueRedraw();return true;
 }

 float tooltipScale;
 int relayControlCount;
 void BeginRelayControls(){
  if(relayRoot==null){relayRoot=new Control{Name="RelaySurface",MouseFilter=Control.MouseFilterEnum.Ignore,ZIndex=100};AddChild(relayRoot);}
  relayControlCount=0;relayTooltipBounds=default;if(relayTooltip!=null)relayTooltip.Visible=false;
 }
 void RelayControl(string id,Rect2 bounds,bool focusable=true,bool tooltip=false){
  int index=relayControlCount++;
  if(index==relayControls.Count){
   var c=new RelayInputControl{Name="Control"+index,MouseFilter=Control.MouseFilterEnum.Stop,MouseForcePassScrollEvents=false};
   relayControls.Add(c);relayControlIds.Add("");relayRoot!.AddChild(c);
   c.Focused=()=>{focusedUi=relayControlIds[index];QueueRedraw();};
   c.Dispatch=input=>{if(relayControlIds[index]=="tooltip")return;if(interferenceDev)DevelopmentInput(input);else MissionInterfaceInput(input);QueueRedraw();};
  }
  var node=relayControls[index];relayControlIds[index]=id;
  node.Position=bounds.Position;node.Size=bounds.Size;node.Visible=true;
  // Modal navigation is deliberately ordered by the displayed model, while plain
  // Tab retains commander selection during play. Ctrl+Tab enters the HUD.
  node.FocusMode=focusable?Control.FocusModeEnum.All:Control.FocusModeEnum.None;
  if(!MissionModal&&focusable&&focusedUi==id&&!node.HasFocus())node.GrabFocus();
 }
 void EndRelayControls(){
  string requestedFocus=focusedUi;
  if(focusedUi.Length==0)foreach(var c in relayControls)if(c.HasFocus())c.ReleaseFocus();
  // Background shields sit behind the individual controls, never over battlefield.
  if(warSetupVisible||MultiplayerVisible){foreach(var c in relayControls)c.Visible=false;return;}
  if(MissionModal)RelayControl("shield",GetViewportRect(),false);
  else {RelayControl("sidebar",Px(new(UiSize.X-336,0,336,UiSize.Y)),false);RelayControl("messages",Px(new(0,UiSize.Y-64,UiSize.X-336,64)),false);}
  foreach(var hit in missionHits)RelayControl(hit.Id,hit.Rect,hit.Enabled||hit.Id.StartsWith("product:"));
  if(relayTooltipBounds.HasArea())RelayControl("tooltip",relayTooltipBounds,false,true);
  for(int i=relayControlCount;i<relayControls.Count;i++)relayControls[i].Visible=false;
  if(MissionModal&&requestedFocus.Length>0){int focusIndex=relayControlIds.FindIndex(id=>id==requestedFocus);if(focusIndex>=0&&focusIndex<relayControlCount&&relayControls[focusIndex].FocusMode==Control.FocusModeEnum.All){focusedUi=requestedFocus;if(!relayControls[focusIndex].HasFocus())relayControls[focusIndex].GrabFocus();}}
  if(relayTooltip!=null)relayRoot!.MoveChild(relayTooltip,-1);
 }
 void ShowRelayTooltip(string text,Rect2 anchor){
  if(relayTooltip==null){
   relayTooltip=new PanelContainer{Name="Tooltip",MouseFilter=Control.MouseFilterEnum.Ignore};
   relayTooltipLabel=new Label{MouseFilter=Control.MouseFilterEnum.Ignore};
   relayTooltip.AddChild(relayTooltipLabel);relayRoot!.AddChild(relayTooltip);
  }
  if(tooltipCopy!=text||tooltipScale!=ui){
   tooltipCopy=text;tooltipScale=ui;relayTooltip.CustomMinimumSize=new(256*ui,0);
   var style=new StyleBoxFlat{BgColor=new("0f100f"),BorderColor=new("6a6d70"),ContentMarginLeft=12*ui,ContentMarginRight=12*ui,ContentMarginTop=10*ui,ContentMarginBottom=11*ui};style.SetBorderWidthAll(Math.Max(1,(int)(2*ui)));
   relayTooltip.AddThemeStyleboxOverride("panel",style);
   relayTooltipLabel!.AddThemeFontOverride("font",UiFont);relayTooltipLabel.AddThemeFontSizeOverride("font_size",(int)(14*ui));relayTooltipLabel.AddThemeColorOverride("font_color",uiText);
   relayTooltipLabel.Text=string.Join("\n",UiLines(text,256-24,14));
  }
  relayTooltip.ResetSize();var size=relayTooltip.GetCombinedMinimumSize();
  var pos=new Vector2((UiSize.X-336-12)*ui-size.X,anchor.Position.Y-4*ui);
  if(MissionModal)pos=new(anchor.End.X+12*ui,anchor.Position.Y);
  var viewport=GetViewportRect().Size;
  pos.X=Math.Clamp(pos.X,8*ui,Math.Max(8*ui,viewport.X-size.X-8*ui));pos.Y=Math.Clamp(pos.Y,8*ui,Math.Max(8*ui,viewport.Y-size.Y-8*ui));
  relayTooltip.Position=pos;relayTooltip.Size=size;relayTooltip.Visible=true;relayTooltipBounds=new(pos,size);
 }
}
