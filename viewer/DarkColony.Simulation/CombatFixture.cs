using System.Text.Json;
namespace DarkColony.Simulation;
public sealed record CombatPlacement(int Id,int Team,Point Position,string Definition,bool Stationary=false);
public sealed record CombatFixture(CombatantDefinition[] Definitions,CombatPlacement[] Spawns,Point Camera) {
 public EngagementSettings? Engagement {get;init;}
 public bool FogOfWar {get;init;}
 public GuardSettings Guard {get;init;}=new();
 public PatrolDefinition[] Patrols {get;init;}=Array.Empty<PatrolDefinition>();
 public EncounterRules Rules {get;init;}=new();
 public string Objective {get;init;}="Eliminate the opposing force";
 public static CombatFixture Parse(string json)=>JsonSerializer.Deserialize<CombatFixture>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new ArgumentException("Missing encounter");
 public CombatSimulation Create(NavigationGrid grid,MovementSettings settings){var simulation=new CombatSimulation(grid,Spawns.Select(s=>new CombatSpawn(s.Id,s.Team,s.Position,Definitions.Single(d=>d.Id==s.Definition),s.Stationary)),settings,tactics:Engagement,patrols:Patrols,rules:Rules);simulation.ConfigureGuard(Guard);if(FogOfWar)simulation.EnableVisibility();return simulation;}
}
