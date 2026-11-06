namespace DarkColony.Assets;
// Bounded original-art presentation. Original FIN records remain untouched in the
// importer. Only [0,16,1,0] body cells are supported here. No guessed blends/mirroring.
public sealed class TrooperCombatClips {
 readonly TrooperPose[][] fire=new TrooperPose[8][];readonly TrooperPose[] death;
 public TrooperClips Locomotion {get;}
 public string[] Limitations {get;}
 public TrooperCombatClips(AnimationAsset fin,SpriteAsset spr,GifPalette palette,int team,string family="TRSC") {
  Locomotion=new(fin,spr,palette,team,family);string bank=family.ToLowerInvariant(),identity=family=="TRSC"?"human.security":"martian.gray";int deathCount=family=="TRSC"?6:9;
  Limitations=new[]{"FIREA normal body here; mode5 original overlays handled by CombatEffects. FIREA versus FIREB selection provisional.",$"{family}DIEA14 first{deathCount} normal frames only, fixed SE fallback; remaining variants/timing unresolved. Host fades then removes.","No original hit association established; health-bar/damage feedback only.8FPS is prototype."};
  var colors=GameColors.Table(palette,team,16,ColorBank.Body);int[] suffix={8,10,12,14,0,2,4,6};
  TrooperPose Pose(int frame,string name,int index,bool bodyOnly) {
   var f=fin.Frames[frame];if(f.Parts.Any(p=>p.Name!="NONAME"&&p.Name!=""))throw new NotSupportedException("Combat nested FIN");
   var bodies=f.Cells.Where(c=>c.Bank==bank&&c.UnknownWords.SequenceEqual(new ushort[]{0,16,1,0})).ToArray();
   if(bodies.Length!=1||(!bodyOnly&&f.Cells.Length!=1))throw new NotSupportedException("Unsupported combat body composition: "+name);
   var c=bodies[0];var cell=spr.Cells[c.CellId];var p=Presentation.AnimationPlacement(c,cell);
   return new(new VisualAsset($"{identity}/{name}/{index}/team{team}",GameColors.Body(cell,colors),cell.Width,cell.Height,cell.Width,cell.Height,-p.X,-p.Y),frame,c.CellId,c.Y,name);
  }
  for(int facing=0;facing<8;facing++) {string name=family+"FIREA"+suffix[facing];var a=fin.Animations.Single(a=>a.Name==name);if(a.Last-a.First+1!=8)throw new NotSupportedException("Combat sequence length");fire[facing]=Enumerable.Range(0,8).Select(i=>Pose(a.First+i,name,i,true)).ToArray();}
  var die=fin.Animations.Single(a=>a.Name==family+"DIEA14");death=Enumerable.Range(0,deathCount).Select(i=>Pose(die.First+i,die.Name,i,false)).ToArray();
 }
 public TrooperPose Fire(int facing,long frame){if(facing<0||facing>7||frame<0)throw new ArgumentOutOfRangeException();return fire[facing][Math.Min(7,(int)Math.Min(frame,7))];}
 public TrooperPose Death(long frame){if(frame<0)throw new ArgumentOutOfRangeException();return death[(int)Math.Min(frame,death.Length-1)];}
}
