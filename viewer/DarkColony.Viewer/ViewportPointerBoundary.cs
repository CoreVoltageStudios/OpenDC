using Godot;
using System.Runtime.InteropServices;
namespace DarkColony.Viewer;
public partial class Viewer {
 // Windows confinement, not a per-frame warp: motion cannot overshoot into bars.
 // Only own the clip while this game's fullscreen battlefield has foreground focus.
 [StructLayout(LayoutKind.Sequential)] struct PointerRect {
  public int Left,Top,Right,Bottom;
  public readonly bool Matches(PointerRect other)=>Left==other.Left&&Top==other.Top&&Right==other.Right&&Bottom==other.Bottom;
 }
 [StructLayout(LayoutKind.Sequential)] struct PointerPoint {public int X,Y;}
 [DllImport("user32.dll",EntryPoint="ClipCursor")] static extern bool SetPointerClip(ref PointerRect rect);
 [DllImport("user32.dll",EntryPoint="ClipCursor")] static extern bool ClearPointerClip(IntPtr unused);
 [DllImport("user32.dll")] static extern bool GetClipCursor(out PointerRect rect);
 [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window,ref PointerPoint point);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 bool ownsPointerClip;PointerRect pointerClip;Input.MouseModeEnum pointerClipMode;
 void ReleasePointerBoundary(){
  if(!ownsPointerClip)return;
  if(GetClipCursor(out var current)&&current.Matches(pointerClip))ClearPointerClip(IntPtr.Zero);
  ownsPointerClip=false;
 }
 void UpdatePointerBoundary(){
  if(!OperatingSystem.IsWindows())return;
  var window=GetWindow();var hwnd=(IntPtr)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle,window.GetWindowId());
  bool fullscreen=window.Mode is Window.ModeEnum.Fullscreen or Window.ModeEnum.ExclusiveFullscreen;
  var active=GetViewport().GetFinalTransform()*GetViewportRect();
  bool limited=active.Position.X>=1&&active.End.X<window.Size.X;
  if(capturing||!ready||!edgeScrolling||MissionModal||!fullscreen||!limited||!window.HasFocus()||GetForegroundWindow()!=hwnd){ReleasePointerBoundary();return;}
  var origin=new PointerPoint();if(!ClientToScreen(hwnd,ref origin)){ReleasePointerBoundary();return;}
  var rect=new PointerRect{Left=origin.X+(int)Math.Ceiling(active.Position.X),Top=origin.Y+(int)Math.Ceiling(active.Position.Y),Right=origin.X+(int)Math.Floor(active.End.X),Bottom=origin.Y+(int)Math.Floor(active.End.Y)};
  if(rect.Right<=rect.Left||rect.Bottom<=rect.Top){ReleasePointerBoundary();return;}
  if(!ownsPointerClip||!rect.Matches(pointerClip)||pointerClipMode!=Input.MouseMode){
   if(SetPointerClip(ref rect)){pointerClip=rect;pointerClipMode=Input.MouseMode;ownsPointerClip=true;if(edgeTrace)GD.Print($"POINTER_BOUNDARY {rect.Left},{rect.Top}..{rect.Right},{rect.Bottom}");}
  }
 }
}
