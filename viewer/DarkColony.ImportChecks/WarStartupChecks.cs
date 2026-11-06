using DarkColony.ContentImport;
using DarkColony.Original;

public static class WarStartupChecks {
 public static void Run(string storePath) {
  string active = new ContentStore(storePath).ActiveRoot
   ?? throw new InvalidDataException("Import supported base-game content before running this check.");
  string root = Path.Combine(active, "private/DC");
  bool shuffled = false;
  foreach (int seed in new[] { 0, 7 }) {
   foreach (bool sameTeam in new[] { true, false }) {
    var setup = WarConfiguration.Default("D4PLAY04", seed) with { Multiplayer = true };
    setup.Slots[0] = setup.Slots[0] with { Team = 3 };
    setup.Slots[1] = setup.Slots[1] with { Controller = WarController.Remote, Team = sameTeam ? 3 : 4 };
    setup.Slots[2] = setup.Slots[2] with { Controller = WarController.Ai, Team = 5 };

    // Independent local perspectives must resolve lobby slots through shuffled starts.
    var host = new SynchronizedWar(root, setup, 0);
    var client = new SynchronizedWar(root, setup, 1);
    int[] starts = host.Mission.War!.Starts;
    shuffled |= Array.IndexOf(starts, 0) != 0 || Array.IndexOf(starts, 1) != 1;
    Assert(client.Mission.War!.Starts.SequenceEqual(starts), "Peer starting assignments differ");

    // These checks deliberately precede Step(): checking only tick1 hid the defect.
    CheckRelations(host, sameTeam, 0);
    CheckRelations(client, sameTeam, 0);
    Assert(host.Hash() == client.Hash(), "Peers disagree before the first tick");
    host.Step();
    client.Step();
    CheckRelations(host, sameTeam, 1);
    CheckRelations(client, sameTeam, 1);
    Assert(host.Hash() == client.Hash(), "Peers disagree after the first tick");
    Console.WriteLine($"WAR_STARTUP_PASS seed={seed} sameTeam={sameTeam} starts={string.Join(',', starts)} ticks=0,1");
   }
  }
  Assert(shuffled, "Regression must exercise an owner index different from its lobby slot");
 }

 static void CheckRelations(SynchronizedWar match, bool sameTeam, long tick) {
  var world = match.Mission.World;
  var war = match.Mission.War!;
  int host = Array.IndexOf(war.Starts, 0), client = Array.IndexOf(war.Starts, 1);
  int opponent = Array.IndexOf(war.Starts, 2);
  Assert(host >= 0 && client >= 0 && opponent >= 0, "Missing active owner");
  Assert(world.Tick == tick, "Check ran at the wrong tick");
  Assert((world.Teams[host].Allies[client] != 0) == sameTeam &&
   (world.Teams[client].Allies[host] != 0) == sameTeam, "Incorrect reciprocal alliance offers");
  Assert(world.Relations[host, client] == sameTeam && world.Relations[client, host] == sameTeam,
   $"Effective teammate relations incorrect at tick {tick}");
  foreach (int owner in new[] { host, client }) {
   Assert(!world.Relations[owner, opponent] && !world.Relations[opponent, owner],
    "Opposing AI must remain hostile in both directions");
   Assert(war.VisionOffers[owner] == 1 << owner && world.VisionBits[owner] == 1 << owner,
    "Starting alliance must not implicitly grant shared vision between humans");
  }
 }

 static void Assert(bool condition, string message) {
  if (!condition) throw new InvalidOperationException(message);
 }
}
