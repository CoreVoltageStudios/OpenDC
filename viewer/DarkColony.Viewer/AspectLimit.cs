using Godot;
namespace DarkColony.Viewer;
public partial class Viewer {
 int maximumAspect;
 Vector2I aspectBasis,aspectApplied;
 int appliedAspect=-1;
 // Godot's root viewport owns letterboxing and the input transform for every child,
 // including retained Relay controls and the movie player. No per-screen offsets.
 void ApplyAspectLimit(){
  if(contentSetup==null&&(!ready||!nativeMode))return;
  var window=GetWindow();
  if(capturing&&aspectBasis==Vector2I.Zero)aspectBasis=window.ContentScaleSize;
  var basis=capturing?aspectBasis:window.Size;if(basis.X<=0||basis.Y<=0)return;
  int width=maximumAspect==0?basis.X:Math.Min(basis.X,(int)Math.Floor(basis.Y*maximumAspect/9d));
  var target=new Vector2I(width,basis.Y);
  if(target==aspectApplied&&appliedAspect==maximumAspect)return;
  aspectApplied=target;appliedAspect=maximumAspect;CancelCameraGesture();
  window.ContentScaleSize=target;window.ContentScaleMode=capturing?Window.ContentScaleModeEnum.Viewport:Window.ContentScaleModeEnum.CanvasItems;
  window.ContentScaleAspect=Window.ContentScaleAspectEnum.Keep;
  RenderingServer.SetDefaultClearColor(Colors.Black);
  GD.Print($"ASPECT {maximumAspect} window={window.Size} target={target} viewport={GetViewportRect()} transform={GetViewport().GetFinalTransform()}");
  QueueRedraw();
 }
}
