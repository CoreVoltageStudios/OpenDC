namespace DarkColony.Assets;

// Modern presentation adapter, not a claim about the original fog algorithm.
// Knowledge0/1/2 is authoritative input only. Feather inward, never reveal a hidden body.
public static class SoftFog {
 public const int SamplesPerCell=8,PixelsPerCell=32;
 static readonly (int X,int Y)[] Neighbors=Enumerable.Range(-1,3).SelectMany(y=>Enumerable.Range(-1,3).Select(x=>(x,y))).Where(p=>p!=(0,0)).ToArray();
 static readonly float[,,] Feather=MakeFeather();
 static float[,,] MakeFeather() {
  var table=new float[8,8,8];
  for(int y=0;y<8;y++)for(int x=0;x<8;x++)for(int n=0;n<8;n++) {
   var d=Neighbors[n];float px=x*4+2,py=y*4+2;
   float dx=Math.Max(Math.Max(d.X*32-px,px-(d.X+1)*32),0),dy=Math.Max(Math.Max(d.Y*32-py,py-(d.Y+1)*32),0);
   // Fully opaque first3px keeps bilinear sampling from exposing unknown adjacent cells.
   float t=Math.Clamp(((float)Math.Sqrt(dx*dx+dy*dy)-3)/11,0,1);table[x,y,n]=1-t*t*(3-2*t);
  }
  return table;
 }
 // Input rows are screen-down, already separate from map lighting and collision.
 public static Raster Build(int width,int height,ReadOnlySpan<byte> knowledge) {
  if(width<1||height<1||knowledge.Length!=width*height)throw new ArgumentException("Fog dimensions");
  var result=new Raster(width*8,height*8,new byte[width*height*64*4]);
  Span<byte> near=stackalloc byte[8];
  for(int cy=0;cy<height;cy++)for(int cx=0;cx<width;cx++) {
   int state=knowledge[cy*width+cx];if(state>2)throw new ArgumentException("Fog knowledge state");
   for(int n=0;n<8;n++){var d=Neighbors[n];int x=cx+d.X,y=cy+d.Y;near[n]=x<0||y<0||x>=width||y>=height?(byte)0:knowledge[y*width+x];}
   for(int sy=0;sy<8;sy++)for(int sx=0;sx<8;sx++) {
    float alpha=state==0?1:state==1?.68f:0;
    if(state!=0)for(int n=0;n<8;n++) {
     if(near[n]>=state)continue;
     float target=near[n]==0?1:.68f,baseAlpha=state==1?.68f:0;
     alpha=Math.Max(alpha,baseAlpha+(target-baseAlpha)*Feather[sx,sy,n]);
    }
    int at=((cy*8+sy)*result.Width+cx*8+sx)*4;
    result.Rgba[at]=8;result.Rgba[at+1]=14;result.Rgba[at+2]=12;result.Rgba[at+3]=(byte)Math.Round(alpha*255);
   }
  }
  return result;
 }
}
