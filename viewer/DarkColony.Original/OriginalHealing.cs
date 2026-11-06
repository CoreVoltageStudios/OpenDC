namespace DarkColony.Original;

public sealed partial class OriginalWorld {
 // 413c50..41410b: automatic idle healing, not an attack/targeted ability.
 // Expanding inclusive squares, X then Z, airborne before ground. Only ownership
 // matters; alliance and render visibility neither grant nor prevent healing.
 void HealIdle(NativeEntity healer) {
  if(healer.Energy>=4)for(int radius=0;radius<8;radius++)
   for(int x=healer.Cell.X-radius;x<=healer.Cell.X+radius;x++)
    for(int z=healer.Cell.Z-radius;z<=healer.Cell.Z+radius;z++) {
     var cell=new Cell(x,z);if(!Navigation.Inside(cell))continue;
     for(int layer=0;layer<2;layer++) {
      int slot=(layer==0?Air:Ground)[At(cell)];
      if(slot<0||Slots[slot] is not {Living:true} target||target.Owner!=healer.Owner)continue;
      var type=Data.Scenario.Types[target.Type];int missing=type.MaximumHealth-target.Health;
      if(missing<=0)continue;
      int amount=Math.Min(missing,36*Data.DamageFactors[7][type.Fields[10]]/256);
      target.Health+=amount;HealedHealth[target.Owner]+=amount;
      if(target.Overlay.Mode!=1)target.Overlay.Start(Data.Clips[healer.Type].Fire[0],1);
      // Both branches explicitly zero byte+0a (413f77 / 413fdd), even when a
      // partial repair's earlier proportional subtraction left some energy.
      healer.Energy=0;healer.Body.Start(Data.Clips[healer.Type].Special,1);
      healer.Actions.Add(new(13){Word=50});Emit("heal",healer,target.Slot,amount);
      AudioCue("heal",healer);return;
     }
    }
  ResetOrders(healer);
 }

 public void SetAlliance(int first,int second,bool enabled) {
  if(first is <0 or >7||second is <0 or >7)throw new ArgumentOutOfRangeException();
  // 43dd20: immediate directed cache store, then symmetric alliance bits.
  // StepCore derives the full cache on the next tick. Vision is independent;
  // no ownership transfer, order cancellation, or retroactive projectile edit.
  Relations[first,second]=enabled;
  Teams[first].Allies[second]=Teams[second].Allies[first]=enabled?1:0;
  Events.Add(new(Tick,"alliance",first,second,0,0,enabled?1:0));
 }
 public void SetDependencyDisabled(int team,int dependency,bool disabled) {
  if(team is <0 or >7||dependency<0||dependency>=Teams[team].DisabledDependencies.Length)throw new ArgumentOutOfRangeException();
  Teams[team].DisabledDependencies[dependency]=disabled;
  if(team==Session.LocalTeam)RefreshDependencies(team); // 43dddd..43de17.
  Events.Add(new(Tick,"dependency-disabled",team,dependency,0,0,disabled?1:0));
 }
}
