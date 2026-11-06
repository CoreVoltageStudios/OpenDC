namespace DarkColony.Viewer;
public partial class Viewer {
 // Retain existing diagnostic entry points, with catalog-aware late-campaign checks.
 async Task VerifyHumanCampaign()=>await VerifyLateHumanCampaign();
}
