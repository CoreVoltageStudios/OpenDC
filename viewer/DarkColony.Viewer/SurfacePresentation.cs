using Godot;
using DarkColony.Assets;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 ImageTexture? surfaceScene;
 CanvasTexture? surfaceArtwork;
 GradientTexture2D? surfaceShade,surfaceVertical;
 void DrawSurfaceBackdrop(){
  surfaceShade??=new GradientTexture2D{Width=256,Height=1,FillFrom=Vector2.Zero,FillTo=Vector2.Right,Gradient=new Gradient{Colors=new[]{new Color(.035f,.039f,.031f,.95f),new Color(.035f,.039f,.031f,.7f),new Color(.035f,.039f,.031f,0)},Offsets=new[]{0f,.32f,.72f}}};
  surfaceVertical??=new GradientTexture2D{Width=1,Height=256,FillFrom=Vector2.Zero,FillTo=Vector2.Down,Gradient=new Gradient{Colors=new[]{new Color(0,0,0,0),new Color(0,0,0,.82f)},Offsets=new[]{.65f,1f}}};
  if(menuColony==null)DrawRect(GetViewportRect(),OdcUiTokens.Ground);
  DrawTextureRect(surfaceShade,GetViewportRect(),false);DrawTextureRect(surfaceVertical,GetViewportRect(),false);
  if(missionPanel.Length>0||warSetupVisible||MultiplayerVisible)DrawRect(GetViewportRect(),new Color(0,0,0,.55f));
 }

 static void SurfaceBlend(Raster target,Raster source,int x,int y){
  for(int sy=0;sy<source.Height;sy++)for(int sx=0;sx<source.Width;sx++){
   int dx=x+sx,dy=y+sy;if(dx<0||dy<0||dx>=target.Width||dy>=target.Height)continue;
   int si=(sy*source.Width+sx)*4,di=(dy*target.Width+dx)*4,a=source.Rgba[si+3];
   for(int c=0;c<3;c++)target.Rgba[di+c]=(byte)((source.Rgba[si+c]*a+target.Rgba[di+c]*(255-a))/255);
  }
 }
}
