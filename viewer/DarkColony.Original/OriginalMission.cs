using DarkColony.Assets;
using System.Text.RegularExpressions;
namespace DarkColony.Original;

public sealed record TroAction(string Name,int[] Raw,int[] Arguments,int SourceLine){public string SourceText {get;init;}="";}
public sealed class TroBlock {
 public int Id,Remaining,SourceLine;public bool Trip;public string Condition="";public List<TroAction> Actions=new();
}
public sealed record MissionEvent(long Tick,long Milliseconds,string Kind,int Block,int Subject,string Detail);
public sealed record NativeMessage(int Speaker,int Type,int TextId,int Flags,int Duration,long Timestamp,bool Expired=false);
public sealed record OriginalOutcome(int Winner,int Result,long Deadline,bool Success,string TextPath,int NextIndex,int Rank,int PromotionScore,int[] Medals);

// Selected original TRO language subset. Input is actual source, never fixture event JSON.
public sealed partial class OriginalMission {
 public OriginalWorld World {get;}
 public TroBlock?[] Blocks=>World.Session.ScriptBlocks;
 public List<MissionEvent> Events {get;}=new();
 public List<NativeMessage> Messages {get;}=new();
 public IReadOnlyDictionary<int,string> Texts {get;}
 public int MessageIndex {get;private set;}
 public long Milliseconds {get;private set;}
 public OriginalOutcome? Outcome {get;private set;}
 public OriginalProfileContext? ResultProfile {get;private set;}
 bool outcomeFinalized;
 public bool AtOutcome=>Outcome!=null&&Milliseconds>Outcome.Deadline;
 public NativeMessage? CurrentMessage=>MessageIndex<Messages.Count?Messages[MessageIndex]:null;
 public int CampaignIndex {get;}
 public string CampaignId {get;}
 public OriginalMission(OriginalDefinitions data,int seed,OriginalProfileContext? profile=null,bool restoring=false,WarConfiguration? war=null,int? viewSlot=null) {
  if(war!=null){var map=war.Validate(data.DataRoot);if(map.Scenario!=data.Scenario.Scenario||seed!=war.Seed)throw new InvalidDataException("War setup does not match its content");War=new(){Configuration=war.Copy(),Starts=war.AssignStarts(data.Reference,map.Capacity)};}
  CampaignId=war!=null?"war":OriginalCampaign.Identity(data.Scenario.Scenario);
  CampaignIndex=war!=null?-1:OriginalCampaign.For(data).Single(m=>m.Scenario.Equals(data.Scenario.Scenario,StringComparison.OrdinalIgnoreCase)).Index;
  if(profile!=null&&(profile.CampaignId!=CampaignId||profile.CampaignIndex!=CampaignIndex))throw new InvalidDataException("Campaign entry does not match this mission");
  World=new(data,seed,viewSlot.HasValue?Array.IndexOf(War!.Starts,viewSlot.Value):War?.LocalTeam??0,profile,restoring,war?.Options.CommanderRank);
  if(war!=null)World.PlayerOrdersAllowed=team=>!War!.Eliminated[team];
  if(war!=null&&!restoring)InitializeWar();
  var deferred=Blocks.Where(b=>b!=null).SelectMany(b=>b!.Actions).FirstOrDefault(a=>!(data.ContentId==OriginalContent.Council&&a.Name=="funkytower")&&a.Name is not ("reinforce" or "reinforce2" or "abduct" or "bail" or "msg" or "ai" or "aimsg" or "waypoint" or "newrate" or "setarray" or "setlifes" or "exomoney" or "vision" or "ally" or "dfiddle" or "nopickup" or "newtype" or "newrate2" or "setmoney" or "artifact" or "noundeploy"));
  if(deferred!=null)throw new NotSupportedException("Mission execution unavailable: "+deferred.SourceText+"; use initialization inspection");
  string stem=Path.ChangeExtension(data.Scenario.Scenario,null)!.Replace('\\','/');

  var texts=new Dictionary<int,string>();int id=-1;
  foreach(var line in war!=null?Array.Empty<ScenarioLine>():OriginalScenarioImport.Lines(data.Read(stem+".MSG"),"MSG")) {
   if(line.Text.StartsWith("text ")){
    // Native44da40 ->4067a0 reads the numeric prefix with strtol(base10),
    // checks that digits were consumed and bounds the ID to0..29. Human05's
    // original header is "text 5."; the trailing punctuation is not content.
    var number=Regex.Match(line.Text[5..],@"^\s*([+-]?\d+)");
    if(!number.Success||!int.TryParse(number.Groups[1].Value,out id)||id is <0 or >=30)throw new InvalidDataException("Invalid original message ID");
    texts[id]="";
   }
   else {if(id<0)throw new InvalidDataException("MSG without text ID");texts[id]+=(texts[id].Length==0?"":"\n")+line.Text;}
  }
  Texts=texts;World.NormalScan+=_=>Scan();World.TripReserved+=(_,e,id)=>Trip(id,e);World.CommanderRecovery+=(_,e)=>World.RecoverCommander(e);
 }
 public static TroBlock[] Parse(byte[] source,bool retainDeferred=false) {
  var result=new List<TroBlock>();TroBlock? block=null;
  var counts=new Dictionary<string,int>{{"reinforce",13},{"reinforce2",13},{"abduct",2},{"bail",2},{"msg",5}};
  foreach(var line in OriginalScenarioImport.Lines(source,"TRO")) {
   var h=Regex.Match(line.Text,@"^(\d+)\s+(norm|trip)\s+(.*)$");
   if(h.Success) {
    if(block!=null)throw new InvalidDataException("Nested TRO block");
    var count=Regex.Match(h.Groups[3].Value,@"^([+-]?\d+)\s*(.*)$");
    block=new(){Id=int.Parse(h.Groups[1].Value),Trip=h.Groups[2].Value=="trip",Remaining=count.Success?unchecked((byte)int.Parse(count.Groups[1].Value)):0,Condition=count.Success?count.Groups[2].Value:h.Groups[3].Value,SourceLine=line.Number};
    if(block.Id>=128||result.Any(b=>b.Id==block.Id))throw new InvalidDataException("Invalid/duplicate TRO slot");
   }else if(line.Text=="end") {if(block==null)throw new InvalidDataException("Unmatched TRO end");result.Add(block);block=null;}
   else {
    if(block==null)throw new InvalidDataException("TRO action outside block");var words=line.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
    if(!counts.TryGetValue(words[0],out int n)) {
     if(!retainDeferred)throw new NotSupportedException("Unestablished TRO action "+words[0]);
     // Preserve unsupported source actions/expressions at load; execution remains gated.
     block.Actions.Insert(0,new(words[0],Array.Empty<int>(),Array.Empty<int>(),line.Number){SourceText=line.Text});continue;
    }
    int[] raw=words.Skip(1).Select(int.Parse).ToArray();
    // 43e9a8 consumes exactly three header bytes plus five reinforcement pairs,
    // then reads the next line. ALIEN12 contains an extra zero pair. Preserve
    // the raw source for provenance but do not invent a sixth reinforcement.
    if(raw.Length>n&&words[0] is not ("reinforce" or "reinforce2"))throw new InvalidDataException("Too many TRO action fields");
    // 43e9a8's repeated strtol yields zero for exhausted short suffixes, then byte stores.
    var effective=Enumerable.Range(0,n).Select(i=>i<raw.Length?(int)unchecked((byte)raw[i]):0).ToArray();
    block.Actions.Insert(0,new(words[0],raw,effective,line.Number){SourceText=line.Text});
   }
  }
  if(block!=null)throw new InvalidDataException("Truncated TRO block");return result.ToArray();
 }
 void Record(string kind,int block,int subject,string detail="")=>Events.Add(new(World.Tick,Milliseconds,kind,block,subject,detail));
 public void Step(){if(World.Faulted)throw new InvalidOperationException("Mission halted after simulation fault; restart required.");if(!AtOutcome){World.Step();UpdateWarOutcome();}}
 public void SetClock(long milliseconds) {
  if(World.Faulted)throw new InvalidOperationException("Mission clock halted after simulation fault; restart required.");
  if(milliseconds<Milliseconds)throw new ArgumentOutOfRangeException(nameof(milliseconds));Milliseconds=milliseconds;
  if(AtOutcome&&!outcomeFinalized) {
   // 403a48 collects statistics on entry to the result frontend, after the bail deadline.
   var prior=Outcome!;var profile=World.Session.Profile;
   int score=(profile?.PromotionScore??0)+(prior.Success?World.NearbyCommanderKills[0]:0),rank=profile?.Rank??0;int[] medals=OriginalProfileContext.CompleteMedals(OriginalCampaign.IsHuman(CampaignId)?Enumerable.Repeat(-1,4).ToArray():new int[4]);
   if(profile!=null)Array.Copy(profile.Medals,medals,Math.Min(6,profile.Medals.Length));
   int kills=Enumerable.Range(OriginalCampaign.IsHuman(CampaignId)?69:73,4).Sum(t=>World.Kills[0,t]),totalKills=(profile?.CommanderKills??0)+(prior.Success?kills:0);
   int awardBase=OriginalCampaign.IsHuman(CampaignId)?0:6;
   if(prior.Success&&!OriginalCampaign.IsTraining(CampaignId)){medals[0]=awardBase;if(kills>=15&&medals[1]<awardBase)medals[1]=awardBase+1;else if(kills>=30&&medals[2]<awardBase)medals[2]=awardBase+2;if(totalKills>=80)medals[3]=awardBase+3;
    //403d63..403e99: campaign-specific milestone and final awards, six slots.
    if(CampaignIndex==(OriginalCampaign.IsCouncil(CampaignId)?7:OriginalCampaign.IsHuman(CampaignId)?5:4))medals[4]=awardBase+4;
    if(CampaignIndex==14)medals[5]=awardBase+5;
    //404652..4046af: one promotion on campaign continuation, even if several thresholds are crossed.
    int[] thresholds={10,100,400},missions={5,10,15};if(rank<3&&(score>=thresholds[rank]||prior.NextIndex>=missions[rank])){rank++;score=0;}
   }
   if(OriginalCampaign.IsTraining(CampaignId)){rank=score=totalKills=0;medals=Enumerable.Repeat(-1,6).ToArray();}
   Outcome=prior with{Rank=rank,PromotionScore=score,Medals=medals};outcomeFinalized=true;Record("outcome-frontend",-1,prior.Winner);
   ResultProfile=new(prior.NextIndex,rank,score,medals,"Original mission result; continuation pending"){CommanderKills=totalKills,CampaignId=CampaignId};
  }
  if(CurrentMessage is not {} m)return;
  if(!m.Expired&&Milliseconds-m.Timestamp>m.Duration*1000L) {
   Messages[MessageIndex]=m with{Expired=true,Flags=m.Flags>1?0:m.Flags};Record("message-expired",-1,m.TextId);
  }
  if(Messages[MessageIndex].Expired&&MessageIndex+1<Messages.Count&&!Messages[MessageIndex+1].Expired) {
   MessageIndex++;Messages[MessageIndex]=Messages[MessageIndex] with{Timestamp=Milliseconds};Record("message-visible",-1,Messages[MessageIndex].TextId);
  }
 }
 void Scan(){Record("normal-scan",-1,-1);foreach(var block in Blocks)if(block is {Trip:false})TryFire(block,null);}
 void Trip(int id,NativeEntity unit){if(id>0&&id<128&&Blocks[id] is {Trip:true} block)TryFire(block,unit);}
 void TryFire(TroBlock block,NativeEntity? unit) {
  if(block.Remaining==0||Evaluate(block.Condition,unit)==0)return;
  Record("trigger",block.Id,unit?.Slot??-1,$"owner={unit?.Owner};x={unit?.X};z={unit?.Z}");
  foreach(var action in block.Actions)Execute(block.Id,action);
  block.Remaining=unchecked((byte)(block.Remaining-1)); // 43e84c/43e851: actions precede consumption.
 }
 public short Evaluate(string expression,NativeEntity? unit=null)=>new Condition(expression,(name,args)=>name switch {
  "c"=>unchecked((short)(World.Tick>>4)),"S"=>unchecked((short)(unit?.Owner??0)),"t"=>unchecked((short)(unit?.Type??0)),
  "r"=>unchecked((short)World.Random.Next()),
  "m" when args.Length==2=>(short)(World.ResourceSiteExists(new(args[0],args[1]))?1:0),
  "s" when War!=null&&args.Length==2&&args[1]==0=>WarOption(args[0]),
  // 43d27c opcode10 -> 41a568; native stat3 is total deaths (441e69..441e90).
  "s" when args.Length==2&&args[1]==2=>unchecked((short)Enumerable.Range(0,World.Kills.GetLength(1)).Sum(t=>World.Kills[args[0],t])),
  "s" when args.Length==2&&args[1]==3=>unchecked((short)Enumerable.Range(0,World.Live.GetLength(1)).Sum(t=>World.Deaths[args[0],t])),
  // DC16 41be11..41be2d credits initial SCN money to statistic1, then
  // 419ba6/413bcd add income. Keep saved earned[] as post-start income so
  // supported saves need no counter rewrite or guessed migration.
  "s" when args.Length==2&&args[1]==1=>unchecked((short)(World.Data.Scenario.Teams[args[0]].Money+World.EarnedResources[args[0]])),
  "s" when args.Length==2&&args[1]==9=>unchecked((short)World.HealedHealth[args[0]]),
  "s" when args.Length==2&&args[1]==6=>unchecked((short)Enumerable.Range(0,World.Live.GetLength(1)).Sum(t=>World.Live[args[0],t])),
  "s" when args.Length==2&&args[1]==10&&args[0]==0=>(short)(1-World.Phase),
  "s" when args.Length==2&&args[1]==10&&args[0]==1=>unchecked((short)(World.DayCounter*256/World.Data.Scenario.Header.Period)),
  "s" when args.Length==3=>unchecked((short)(args[1]==0?World.Deaths[args[0],args[2]]:args[1]==1?World.Live[args[0],args[2]]:args[1]==2?World.Session.ScriptValues[args[0],args[2]]:throw new NotSupportedException("Statistic subtype"))),
  "b" when args.Length==2=>unchecked((short)World.Teams[args[0]].Cities[args[1]].Health),
  _=>throw new NotSupportedException("Unestablished condition symbol "+name)}).Evaluate();
 void Execute(int block,TroAction action) {
  var p=action.Arguments;Record("action",block,action.SourceLine,action.SourceText);
  var pairs=action.Name is "reinforce" or "reinforce2"?Enumerable.Range(0,5).Select(i=>(Type:p[3+i*2],Count:p[4+i*2])).ToArray():Array.Empty<(int Type,int Count)>();
  switch(action.Name) {
   case "funkytower":
    p=ActionIntegers(action);
    if(World.Data.ContentId!=OriginalContent.Council||p.Length!=1||p[0] is <0 or >7)throw new InvalidDataException("Invalid Council palace action");
    World.CouncilPalaces[p[0]]=true;break; // CW43dbd3..43dbe9; conversion occurs next world update.
   case "noundeploy":World.Session.PreventHarvesterRetraction=true;break; //43c850 gs+948; mutable definitions are not shared.
   case "artifact":p=ActionIntegers(action);World.AppendArtifact(new(p[0],p[1]),World.Random.Next()%5+63);break;
   case "newrate2":case "setmoney":
    var resourceFields=action.SourceText.Split((char[]?)null,4,StringSplitOptions.RemoveEmptyEntries);
    World.ScriptVent(new(int.Parse(resourceFields[1]),int.Parse(resourceFields[2])),Evaluate(resourceFields[3]),action.Name=="setmoney",War==null?256:action.Name=="setmoney"?War.Configuration.Options.Quantity*64:War.Configuration.Options.Flow*64);break;
   case "newtype":
    p=ActionIntegers(action);var cell=new Cell(unchecked((byte)p[0]),unchecked((byte)p[1]));
    //43db64 opcode16: first cell-matching ground slot, type byte only. No respawn/reset.
    var changed=World.Slots.FirstOrDefault(e=>e!=null&&e.Cell==cell&&World.Data.Scenario.Types[e.Type].Flying==0);
    if(changed!=null)changed.Type=unchecked((byte)p[2]);break;
   case "nopickup":p=ActionIntegers(action);World.Teams[unchecked((byte)p[0])].CommanderRecoveryDisabled=1;break; //43ddbf: recovery flag only; generic extraction is unaffected.
   case "ally":p=ActionIntegers(action);World.SetAlliance(unchecked((byte)p[0]),unchecked((byte)p[1]),unchecked((byte)p[2])==1);break;
   case "dfiddle":p=ActionIntegers(action);World.SetDependencyDisabled(unchecked((byte)p[0]),unchecked((byte)p[1]),unchecked((byte)p[2])!=0);break;
   case "vision":p=ActionIntegers(action);World.SetVision(unchecked((byte)p[0]),unchecked((byte)p[1]),unchecked((byte)p[2])==1);break;
   case "exomoney":p=ActionIntegers(action);World.Teams[unchecked((byte)p[0])].AutomaticIncome=unchecked((byte)p[1]);break; //43db64 opcode12, not a transfer.
   case "ai":p=ActionIntegers(action);World.Teams[p[0]].Ai=p[1];break;
   case "aimsg":
    p=ActionIntegers(action);int count=unchecked((byte)p[1]);
    if(p.Length!=count+2)throw new InvalidDataException("AI message parameter count");
    World.StrategyMessage(unchecked((byte)p[0]),p.Skip(2).Select(v=>(int)unchecked((short)v)).ToArray());break;
   case "waypoint":
    p=ActionIntegers(action);var subject=World.Entities.FirstOrDefault(e=>e.Living&&e.Cell==new Cell(p[0],p[1]));
    if(subject!=null)World.AssignPatrol(subject,Enumerable.Range(0,p[2]).Select(i=>new Cell(p[3+2*i],p[4+2*i])).ToArray());break;
   case "newrate":
    p=ActionIntegers(action);var vent=World.Entities.FirstOrDefault(e=>e.Living&&e.Type==40&&e.Cell==new Cell(p[1],p[2]));
    if(vent!=null)vent.ResourceFlow=p[0];break;
   case "setlifes":p=ActionIntegers(action);if(Blocks[p[0]] is {} armed)armed.Remaining=unchecked((byte)p[1]);break;
   case "setarray":
    var fields=action.SourceText.Split((char[]?)null,3,StringSplitOptions.RemoveEmptyEntries);
    World.Session.ScriptValues[0,int.Parse(fields[1])]=Evaluate(fields[2]);break;
   case "reinforce":World.Reinforce(p[0],new(p[1],p[2]),pairs,"TRO/"+block+"/"+action.SourceLine);break;
   case "reinforce2":foreach(var pair in pairs)for(int i=0;i<pair.Count;i++)if(!World.AppendArtifact(new(p[1],p[2]),pair.Type))World.Spawn(pair.Type,p[0],new(p[1],p[2]),"TRO/"+block+"/"+action.SourceLine+"/"+i);break;
   case "abduct":World.ExtractCommander(p[0],p[1],"TRO/"+block+"/"+action.SourceLine);break;
   case "msg":Enqueue(p);break;
   case "bail":
    bool won=p[0]==0;outcomeFinalized=false;
    // CW native 404248..404257 formats the TRO result number as %3.3d.
    // Council-loss results use .003 independently of the winning team.
    Outcome=new(p[0],p[1],Milliseconds+10000,won,Path.ChangeExtension(World.Data.Scenario.Scenario,p[1].ToString("000",System.Globalization.CultureInfo.InvariantCulture)),CampaignIndex+(won?1:0),0,0,OriginalProfileContext.CompleteMedals(new int[4]));
    Record("bail",block,p[0],$"result={p[1]};deadline={Outcome.Deadline}");break;
   default:throw new NotSupportedException("TRO execution not established: "+action.SourceText);
  }
 }
 static int[] ActionIntegers(TroAction a)=>a.SourceText.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(int.Parse).ToArray();
 void Enqueue(int[] p) {
  if(p[1]!=0||!Texts.ContainsKey(p[2]))throw new InvalidDataException("Unestablished message type/text");
  // 44dc68's overflow shifts text pointers only; selected A01 never reaches16.
  if(Messages.Count==16){for(int i=0;i<15;i++)Messages[i]=Messages[i] with{TextId=Messages[i+1].TextId};Messages.RemoveAt(15);MessageIndex=Math.Max(0,MessageIndex-1);}
  Messages.Add(new(p[0],p[1],p[2],p[3],p[4],Milliseconds));
  if(Messages[MessageIndex].Expired)MessageIndex=Messages.Count-1;
  Record("message-queued",-1,p[2]);if(MessageIndex==Messages.Count-1)Record("message-visible",-1,p[2]);
 }
 sealed class Condition {
  readonly string text;readonly Func<string,int[],short> symbol;int at;
  public Condition(string text,Func<string,int[],short> symbol){this.text=text;this.symbol=symbol;}
  bool Take(string token){while(at<text.Length&&char.IsWhiteSpace(text[at]))at++;if(!text.AsSpan(at).StartsWith(token))return false;at+=token.Length;return true;}
  public short Evaluate(){var value=Or();while(at<text.Length&&char.IsWhiteSpace(text[at]))at++;if(at!=text.Length)throw new InvalidDataException("Unparsed condition suffix: "+text[at..]);return value;}
  short Or(){short v=And();while(Take("||")){short r=And();v=(short)(v!=0||r!=0?1:0);}return v;}
  short And(){short v=Comparison();while(Take("&&")){short r=Comparison();v=(short)(v!=0&&r!=0?1:0);}return v;}
  short Product(){short v=Atom();while(true){if(Take("%"))v=unchecked((short)(v%Atom()));else if(Take("*"))v=unchecked((short)(v*Atom()));else if(Take("/"))v=unchecked((short)(v/Atom()));else return v;}}
  short Sum(){short v=Product();while(true){if(Take("+"))v=unchecked((short)(v+Product()));else if(Take("-"))v=unchecked((short)(v-Product()));else return v;}}
  short Comparison() {
   short left=Sum();foreach(string op in new[]{"==","!=",">=","<=",">","<"})if(Take(op)){short right=Sum();return (short)((op switch{"=="=>left==right,"!="=>left!=right,">="=>left>=right,"<="=>left<=right,">"=>left>right,_=>left<right})?1:0);}return left;
  }
  short Atom() {
   if(Take("(")){short v=Or();if(!Take(")"))throw new InvalidDataException("Unclosed condition");return v;}
   if(Take("-"))return unchecked((short)-Atom());while(at<text.Length&&char.IsWhiteSpace(text[at]))at++;
   int start=at;while(at<text.Length&&char.IsDigit(text[at]))at++;if(at>start)return unchecked((short)int.Parse(text[start..at]));
   while(at<text.Length&&char.IsLetter(text[at]))at++;if(at==start)throw new InvalidDataException("Condition token");string name=text[start..at];var args=new List<int>();
   if(Take("(")){do{args.Add(Atom());}while(Take(","));if(!Take(")"))throw new InvalidDataException("Condition arguments");}
   return symbol(name,args.ToArray());
  }
 }
}
