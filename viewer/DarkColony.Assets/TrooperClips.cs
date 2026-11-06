namespace DarkColony.Assets;
public sealed record TrooperPose(VisualAsset Visual,int FinFrame,int SpriteCell,int BottomOffset,string Sequence);
// Asset adapter: eight original, unmirrored directions. Facing order N,NE,E,SE,S,SW,W,NW.
// Mapping was visually inspected from original STAND/MOVE art; no runtime heading claim.
public sealed class TrooperClips {
 readonly TrooperPose[][] move=new TrooperPose[8][],stand=new TrooperPose[8][];
 static readonly int[] Suffix={8,10,12,14,0,2,4,6};
 public TrooperClips(AnimationAsset fin,SpriteAsset spr,GifPalette palette,int team=0,string family="TRSC") {
  if(family is not ("TRSC" or "GRAY"))throw new NotSupportedException("Unverified infantry family: "+family);
  string bank=family.ToLowerInvariant(),identity=family=="TRSC"?"human.security":"martian.gray";
  var colors=GameColors.Table(palette,team,16,ColorBank.Body);
  for(int facing=0;facing<8;facing++)foreach(bool moving in new[]{false,true}) {
   string name=family+(moving?"MOVE":"STAND")+Suffix[facing];
   var sequence=fin.Animations.Single(a=>a.Name==name);int count=sequence.Last-sequence.First+1;
   int expected=moving?(family=="GRAY"&&Suffix[facing] is 0 or 2?7:8):1;
   if(count!=expected)throw new NotSupportedException("Unexpected infantry sequence length: "+name);
   var poses=new TrooperPose[count];
   for(int i=0;i<count;i++) {
    int frame=sequence.First+i;var f=fin.Frames[frame];Presentation.RequireSelectedFrame(f);
    if(f.Cells.Length!=1||f.Cells[0].Bank!=bank)throw new NotSupportedException("Unsupported infantry composition: "+name);
    var c=f.Cells[0];var s=spr.Cells[c.CellId];var placement=Presentation.AnimationPlacement(c,s);
    string id=$"{identity}/{(moving?"move":"stand")}/{facing}/{i}"+(team==0?"":$"/team{team}");
    var visual=new VisualAsset(id,GameColors.Body(s,colors),s.Width,s.Height,s.Width,s.Height,-placement.X,-placement.Y);
    poses[i]=new(visual,frame,c.CellId,c.Y,name);
   }
   (moving?move:stand)[facing]=poses;
  }
 }
 public TrooperPose Resolve(int facing,bool moving,long animationFrame) {
  if(facing<0||facing>7||animationFrame<0)throw new ArgumentOutOfRangeException();var poses=(moving?move:stand)[facing];return poses[(int)(animationFrame%poses.Length)];
 }
}
