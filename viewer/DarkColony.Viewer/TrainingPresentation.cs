using Godot;
using DarkColony.Original;
using System.Text.RegularExpressions;
namespace DarkColony.Viewer;
// Explicit presentation adaptation. Original TXT/MSG and script IDs stay immutable.
static class TrainingInstructions {
 public static string MapBriefing(string text)=>Regex.Replace(text,"ENTER", "D");
 public static string MapMessage(int lesson,int id,string text)=> (lesson,id) switch {
  (0,2)=>"Click its production tile to purchase it.",
  (1,10)=>"Click the infantry tile once per unit to queue three.",
  (1,11)=>"Queued production is shown on the tile. Buy only three troops for this lesson.",
  (3,4)=>"Select your mech and choose Waypoints [W].",
  (3,5)=>"Click the markers in order to place up to eight waypoints.",
  (3,6)=>"Click the last waypoint again to start the route. Stop [S] ends patrolling.",
  _=>Regex.Replace(text,"ENTER", "D",RegexOptions.IgnoreCase)
 };
}
public partial class Viewer {
 bool IsTraining=>OriginalCampaign.IsTraining(mainMenu?SelectedCampaign:mission?.CampaignId??campaignMission?.CampaignId??"");
 static readonly string[] lessonNames={"Petra-7 harvesting","Move only and escort","Mine deployment","Waypoints","Artillery and reinforcements","Scouting","Healing"};
 string TrainingObjective=>lessonNames[campaignMission.Index];
 string TrainingStatus=>(IsHuman?"Finish before dusk":"Finish before dawn")+" · Instructions in history";
 string InstructionText(int id)=>IsTraining?TrainingInstructions.MapMessage(campaignMission.Index,id,mission.Texts[id]):mission.Texts[id];
 void DrawTrainingObjectives(float x,float y){
  UiWrap(TrainingObjective,x,y,620,26,uiGreen);
  UiWrap("Follow the original instructions in the message bar. Open message history to review earlier instructions. "+TrainingStatus+".",x,y+62,620,20,uiText);
  UiWrap("OpenDC controls: click a production tile to purchase one unit or building. M selects Move only; A selects Move & attack; D deploys eligible units. W places waypoints; click the final point again to begin. S stops. Training control wording is adapted to these bindings.",x,y+182,620,18,uiDim);
  UiButton("history","Instruction history",new(x,y+330,300,44),"Review the lesson messages while paused.");
 }
}
