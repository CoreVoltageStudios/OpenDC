using DarkColony.Assets;
namespace DarkColony.Original;

// Run-owned state. No Godot objects, wall clocks, persistence format or network policy.
// SCN definitions remain source snapshots; a retry constructs a fresh session.
public sealed class NativeTeamState {
 public int Id {get;} public int Race,Money,Ai,Color,CommanderRecoveryDisabled,AutomaticIncome;
 public int[] Rally,Base,Allies,AiAssignments,WeaponTiers,ArmorTiers,AvailabilityCache,ProductionIds,CommanderIds;
 // AvailabilityCache is derived UI eligibility, not an authoritative production queue.
 public bool[] DisabledDependencies;
 public int SpentResources;
 public int MiningMultiplier=256;
 public int ConstructionSlot=-1;
 public NativeStrategyState? Strategy;
 public NativeProductionQueue[] Production {get;}=Enumerable.Range(0,4).Select(i=>new NativeProductionQueue(i)).ToArray();
 public NativeCityState[] Cities {get;}
 public NativeTeamState(ScenarioTeam t) {
  Id=t.Id;Race=t.Race;Money=t.Money;Ai=t.Ai;Color=t.Color;
  CommanderRecoveryDisabled=t.CommanderRecoveryDisabled;AutomaticIncome=t.AutomaticIncome;
  Rally=(int[])t.Rally.Clone();Base=(int[])t.Base.Clone();Allies=(int[])t.Allies.Clone();AiAssignments=(int[])t.RawAiSlots.Clone();
  WeaponTiers=(int[])t.WeaponTiers.Clone();ArmorTiers=(int[])t.ArmorTiers.Clone();AvailabilityCache=(int[])t.DependencyStatus.Clone();
  DisabledDependencies=(bool[])t.DisabledDependencies.Clone();ProductionIds=(int[])t.ProductionIds.Clone();CommanderIds=(int[])t.CommanderIds.Clone();
  Cities=Enumerable.Range(0,15).Select(i=>new NativeCityState(t.Id,i,t.CityLevels[i],t.CityInitializationPending[i])).ToArray();
 }
}
public sealed class NativeCityState {
 public int Team {get;} public int Slot {get;} public int NativeId=>Team*15+Slot;
 public int Level; public bool InitializationPending;
 // Health/owner/lifecycle have ONE authority, the same entity used by combat and rendering.
 public NativeEntity? Entity {get;internal set;}
 public int Health=>Entity is {Living:true} ? Math.Max(0,Entity.Health):0;
 public NativeCityState(int team,int slot,int level,bool pending){Team=team;Slot=slot;Level=level;InitializationPending=pending;}
}
public sealed class OriginalProfileContext {
 public string CampaignId="taar";
 public int CampaignIndex,Rank,PromotionScore,CommanderKills; public int[] Medals; public string Provenance {get;}
 public static int[] CompleteMedals(int[] medals){if(medals.Length is not (4 or 6))throw new InvalidDataException("Invalid campaign medal inventory");return medals.Length==6?(int[])medals.Clone():medals.Concat(new[]{-1,-1}).ToArray();}
 public OriginalProfileContext(int campaignIndex,int rank,int promotionScore,int[] medals,string provenance){CampaignIndex=campaignIndex;Rank=rank;PromotionScore=promotionScore;Medals=CompleteMedals(medals);Provenance=provenance;}
 public OriginalProfileContext Copy()=>new(CampaignIndex,Rank,PromotionScore,Medals,Provenance){CommanderKills=CommanderKills,CampaignId=CampaignId};
 // Inspection has no prior campaign result. Null means unknown, never an invented rank0.
}
public sealed class NativeProductionQueue {
 public int Id {get;} public List<int> Types {get;}=new();
 public bool Building; public int Cooldown; public Cell? ReservedExit; public long StartedTick;
 public NativeProductionQueue(int id){Id=id;}
}
public sealed class NativeSourceState {
 public ScenarioSpawnSource Definition {get;} public byte Initialized;
 public int[] Members {get;}
 public NativeSourceState(ScenarioSpawnSource source){Definition=source;Members=Enumerable.Repeat(-1,source.Count).ToArray();}
}
public sealed class OriginalSession {
 public NativeTeamState[] Teams {get;}
 public TroBlock?[] ScriptBlocks {get;}=new TroBlock?[128];
 public int[,] ScriptValues {get;} // tstat subtype2; 41a3e8 stride0x6e0/team.
 public OriginalProfileContext? Profile {get;}
 public NativeSourceState[] Sources {get;}
 public int LocalTeam {get;}
 public int MaximumArmy=150; // 41b950 initializes gs+4 to 0x96; War overrides are a later gate.
 public bool PreventHarvesterRetraction; // gs+948; campaign default is clear, retained as explicit session state.
 public OriginalSession(OriginalScenario source,int localTeam=0,OriginalProfileContext? profile=null) {
  if(localTeam is <0 or >7)throw new ArgumentOutOfRangeException(nameof(localTeam));
  ScriptValues=new int[8,OriginalContent.CounterCount(source.Scenario)];
  LocalTeam=localTeam;Teams=source.Teams.Select(t=>new NativeTeamState(t)).ToArray();Profile=profile?.Copy();
  Sources=source.SpawnSources.Select(s=>new NativeSourceState(s)).ToArray();
  foreach(var block in OriginalMission.Parse(source.Sources.Single(s=>s.Path.EndsWith(".TRO")).Bytes,true))ScriptBlocks[block.Id]=block;
 }
}
