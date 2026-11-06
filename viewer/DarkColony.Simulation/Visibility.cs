namespace DarkColony.Simulation;

public enum TerrainKnowledge { Unexplored, Explored, Visible }
public sealed record SightSource(int Team,Point Position,double Radius);
// ODC adaptation: inclusive circle sampled at tile centers, unioned per team.
// Neither navigation flags, terrain lighting nor projectile geometry are inputs.
public sealed class TeamVisibility {
 readonly Dictionary<int,bool[]> visible=new(),explored=new();
 public int Width {get;} public int Height {get;}
 public TeamVisibility(int width,int height,IEnumerable<int> teams) {
  if(width<1||height<1)throw new ArgumentException("Visibility dimensions");
  Width=width;Height=height;
  foreach(int team in teams.Distinct()){visible.Add(team,new bool[checked(width*height)]);explored.Add(team,new bool[width*height]);}
 }
 public TerrainKnowledge At(int team,Cell cell) {
  if(cell.X<0||cell.Z<0||cell.X>=Width||cell.Z>=Height||!visible.ContainsKey(team))return TerrainKnowledge.Unexplored;
  int i=cell.Z*Width+cell.X;return visible[team][i]?TerrainKnowledge.Visible:explored[team][i]?TerrainKnowledge.Explored:TerrainKnowledge.Unexplored;
 }
 public bool CanSee(int team,Point position)=>position.Finite&&At(team,new((int)Math.Floor(position.X),(int)Math.Floor(position.Z)))==TerrainKnowledge.Visible;
 public TerrainKnowledge[] Snapshot(int team)=>Enumerable.Range(0,Width*Height).Select(i=>At(team,new(i%Width,i/Width))).ToArray();
 public void Update(IEnumerable<SightSource> sources) {
  foreach(var field in visible.Values)Array.Clear(field);
  foreach(var source in sources) {
   if(!source.Position.Finite||!double.IsFinite(source.Radius)||source.Radius<=0||source.Radius>32||!visible.ContainsKey(source.Team))throw new ArgumentException("Sight source");
   for(int z=Math.Max(0,(int)Math.Floor(source.Position.Z-source.Radius));z<=Math.Min(Height-1,(int)Math.Floor(source.Position.Z+source.Radius));z++)
    for(int x=Math.Max(0,(int)Math.Floor(source.Position.X-source.Radius));x<=Math.Min(Width-1,(int)Math.Floor(source.Position.X+source.Radius));x++)
     if(CombatSimulation.InRange(source.Position,new(x+.5,z+.5),source.Radius)){int i=z*Width+x;visible[source.Team][i]=explored[source.Team][i]=true;}
  }
 }
}

public sealed partial class CombatSimulation {
 public TeamVisibility? Visibility {get;private set;}
 public void EnableVisibility() {
  if(Tick!=0)throw new InvalidOperationException("Configure visibility before ticking");
  Visibility=new(Movement.Grid.Width,Movement.Grid.Height,fighters.Values.Select(f=>f.Spawn.Team));RefreshVisibility();
 }
 void RefreshVisibility()=>Visibility?.Update(fighters.Values.Where(f=>f.Alive).Select(f=>new SightSource(f.Spawn.Team,Movement.Unit(f.Spawn.Id).Position,f.Spawn.Definition.SightTiles)));
 public bool CanSeeUnit(int team,int id)=>fighters.TryGetValue(id,out var f)&&f.Alive&&(team==f.Spawn.Team||Visibility==null||Visibility.CanSee(team,Movement.Unit(id).Position));
 bool Eligible(Fighter f,int target)=>Hostile(f,target)&&CanSeeUnit(f.Spawn.Team,target);
}
