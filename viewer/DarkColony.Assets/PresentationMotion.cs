namespace DarkColony.Assets;

public sealed record PresentationPose(int Incarnation,int Type,int Owner,int State,int Flag,int X,int Z,int Height,bool Visible,bool Active);
public static class PresentationMotion {
 // Modern one-update display interpolation. No extrapolation or authoritative writes.
 public static (double X,double Z,double Height) Sample(PresentationPose? previous,PresentationPose current,double fraction) {
  if(previous==null||previous.Incarnation!=current.Incarnation||previous.Type!=current.Type||previous.Owner!=current.Owner||previous.State!=current.State||previous.Flag!=current.Flag||
   !previous.Visible||!current.Visible||!previous.Active||!current.Active||Math.Abs(previous.X-current.X)>256||Math.Abs(previous.Z-current.Z)>256||Math.Abs(previous.Height-current.Height)>512)
   return(current.X,current.Z,current.Height);
  double t=Math.Max(0,Math.Min(1,fraction));
  return(previous.X+(current.X-previous.X)*t,previous.Z+(current.Z-previous.Z)*t,previous.Height+(current.Height-previous.Height)*t);
 }
}
