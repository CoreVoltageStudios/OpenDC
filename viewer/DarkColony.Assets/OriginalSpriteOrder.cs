namespace DarkColony.Assets;

// DC16 439c10/43631c/436328 -> 454710. FIN word0 is a per-cell
// depth layer, NOT a whole-entity flying flag. Height changes placement and
// terrain masking, not this key. Cell ordinal runs across body/blood/overlay.
public static class OriginalSpriteOrder {
 public static int LayerBias(ushort layer) {
  int value=(byte)layer; // Native queue stores the low byte.
  return (value&1)!=0?((value>>1)+1)*3000:-(value>>1)*3000;
 }
 public static long Key(int groundZ,bool taarFrontCity,int ordinal,ushort layer,int projectedX) {
  int depth=((groundZ-(taarFrontCity?264:0))&~7)-ordinal;
  return ((long)(depth+LayerBias(layer))<<16)+(short)projectedX;
 }
}
