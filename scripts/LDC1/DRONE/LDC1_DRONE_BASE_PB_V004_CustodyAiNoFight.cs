// LDC1 DRONE BASE PB V004 CUSTODY AI NO-FIGHT
// Base onboard drone infrastructure: DCS assignment read, heartbeat, captive/service safe enforcement, and post-merge self-naming scaffold.
// Scope: no combat, no launch flight, no AI engagement, no connector release authority.

const string VERSION="LDC1_DRONE_BASE_PB_V004_CustodyAiNoFight";
const string DEFAULT_IGC_TAG="DRONE";
const string ASSIGN_HDR="[DRONE_ASSIGN]";
const string STATUS_HDR="[DRONE_STATUS]";

string Serial="",Origin="",Role="DRONE",Short="D",Slot="",Wave="A",Vector="",Departure="";
int Bay=0,Tick=0,Seq=0,Hi=0,HiCap=0; string State="ASSIGN_WAIT",Fault="",HiPhase="INIT",HiCapPhase="INIT",LastCmd="";
bool Assigned=false,SelfNamed=false;

List<IMyTerminalBlock> Blocks=new List<IMyTerminalBlock>();
List<IMyShipMergeBlock> Merges=new List<IMyShipMergeBlock>();
List<IMyShipConnector> Conns=new List<IMyShipConnector>();
List<IMyBatteryBlock> Batteries=new List<IMyBatteryBlock>();
List<IMyGasTank> Tanks=new List<IMyGasTank>();
List<IMyThrust> Thrusters=new List<IMyThrust>();
List<IMyGyro> Gyros=new List<IMyGyro>();
List<IMyRadioAntenna> Ants=new List<IMyRadioAntenna>();
List<IMyCameraBlock> Cams=new List<IMyCameraBlock>();
List<IMyRemoteControl> Remotes=new List<IMyRemoteControl>();
List<IMyFunctionalBlock> AiBlocks=new List<IMyFunctionalBlock>();

public Program(){Runtime.UpdateFrequency=UpdateFrequency.Update100;LoadAssign();ScanLocal();ApplyCaptiveSafe();WriteStatus();Heartbeat();}
public void Save(){WriteStatus();}
public void Main(string arg,UpdateType src){Upd("START");if(arg!=null&&arg.Trim().Length>0)Cmd(arg.Trim());else Auto();EchoOut();Upd("END");}

void Cmd(string a){LastCmd=a;string u=a.ToUpper().Trim();if(u=="STATUS"){LoadAssign();ScanLocal();UpdateState();WriteStatus();Heartbeat();return;}if(u=="BOOT"||u=="SAFE"){LoadAssign();ScanLocal();UpdateState();ApplyCaptiveSafe();WriteStatus();Heartbeat();return;}if(u=="SELFNAME"){LoadAssign();ScanLocal();UpdateState();if(CanSelfName())SelfName();WriteStatus();Heartbeat();return;}Fault="UNKNOWN_CMD";WriteStatus();Heartbeat();}
void Auto(){Tick++;LoadAssign();if(Tick%2==0)ScanLocal();UpdateState();if(Assigned)ApplyCaptiveSafe();if(State=="SERVICE_LOCKED"&&!SelfNamed)SelfName();WriteStatus();Heartbeat();}

void LoadAssign(){string cd=Section(Me.CustomData,ASSIGN_HDR);Serial=Val(cd,"Serial=");Origin=Val(cd,"Origin=");Role=Val(cd,"Role=");Short=Val(cd,"Short=");Slot=Val(cd,"Slot=");Wave=Val(cd,"Wave=");Vector=Val(cd,"Vector=");Departure=Val(cd,"Departure=");Bay=ToInt(Val(cd,"Bay="));if(Role=="")Role="DRONE";if(Short=="")Short="D";if(Wave=="")Wave="A";Assigned=Serial.Length>0&&Bay>0;if(!Assigned&&Fault=="")Fault="NO_ASSIGNMENT";if(Assigned&&Fault=="NO_ASSIGNMENT")Fault="";}

void ScanLocal(){Blocks.Clear();Merges.Clear();Conns.Clear();Batteries.Clear();Tanks.Clear();Thrusters.Clear();Gyros.Clear();Ants.Clear();Cams.Clear();Remotes.Clear();AiBlocks.Clear();GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(Blocks,SameGrid);for(int i=0;i<Blocks.Count;i++){IMyTerminalBlock b=Blocks[i];IMyShipMergeBlock m=b as IMyShipMergeBlock;if(m!=null){Merges.Add(m);continue;}IMyShipConnector c=b as IMyShipConnector;if(c!=null){Conns.Add(c);continue;}IMyBatteryBlock bat=b as IMyBatteryBlock;if(bat!=null){Batteries.Add(bat);continue;}IMyGasTank t=b as IMyGasTank;if(t!=null){Tanks.Add(t);continue;}IMyThrust th=b as IMyThrust;if(th!=null){Thrusters.Add(th);continue;}IMyGyro g=b as IMyGyro;if(g!=null){Gyros.Add(g);continue;}IMyRadioAntenna an=b as IMyRadioAntenna;if(an!=null){Ants.Add(an);continue;}IMyCameraBlock cam=b as IMyCameraBlock;if(cam!=null){Cams.Add(cam);continue;}IMyRemoteControl r=b as IMyRemoteControl;if(r!=null){Remotes.Add(r);continue;}if(IsAiBlock(b))AiBlocks.Add((IMyFunctionalBlock)b);}}
bool SameGrid(IMyTerminalBlock b){return b!=null&&b.CubeGrid==Me.CubeGrid;}

void UpdateState(){bool mergeConnected=false;for(int i=0;i<Merges.Count;i++){try{if(Merges[i].IsConnected)mergeConnected=true;}catch{}}bool connLocked=false;for(int i=0;i<Conns.Count;i++){try{if(Conns[i].Status==MyShipConnectorStatus.Connected)connLocked=true;}catch{}}if(!Assigned){State="ASSIGN_WAIT";return;}if(mergeConnected){State="MERGED_SAFE";return;}if(connLocked){State="SERVICE_LOCKED";return;}State="FREE_SAFE";}
bool CanSelfName(){UpdateState();return Assigned&&(State=="SERVICE_LOCKED"||State=="FREE_SAFE");}

void ApplyCaptiveSafe(){for(int i=0;i<Batteries.Count;i++){try{if(Batteries[i].ChargeMode!=ChargeMode.Recharge)Batteries[i].ChargeMode=ChargeMode.Recharge;}catch{}}for(int i=0;i<Tanks.Count;i++){try{if(!Tanks[i].Stockpile)Tanks[i].Stockpile=true;}catch{}}for(int i=0;i<Thrusters.Count;i++){try{if(Thrusters[i].ThrustOverridePercentage!=0f)Thrusters[i].ThrustOverridePercentage=0f;if(Thrusters[i].Enabled)Thrusters[i].Enabled=false;}catch{}}for(int i=0;i<Gyros.Count;i++){try{if(Gyros[i].GyroOverride)Gyros[i].GyroOverride=false;if(Gyros[i].Enabled)Gyros[i].Enabled=false;}catch{}}for(int i=0;i<AiBlocks.Count;i++)SafeAi(AiBlocks[i]);for(int i=0;i<Ants.Count;i++){try{if(!Ants[i].Enabled)Ants[i].Enabled=true;if(!Ants[i].EnableBroadcasting)Ants[i].EnableBroadcasting=true;}catch{}}}
void SafeAi(IMyFunctionalBlock f){if(f==null)return;try{if(f.Enabled)f.Enabled=false;}catch{}}
void TryAct(IMyTerminalBlock b,string a){try{b.ApplyAction(a);}catch{}}

void SelfName(){if(!Assigned)return;string p="["+(Origin!=""?Origin+"-":"")+Serial+"] ";NameFirst(Me,p+"PB");NameList(Ants,p+"ANT");NameList(Remotes,p+"RC");NameList(Cams,p+"CAM");NameList(Conns,p+"CONN");NameList(Merges,p+"MERGE");NameList(Gyros,p+"GYRO");NameList(Thrusters,p+"THR");NameList(Batteries,p+"BAT");NameList(Tanks,p+"H2");NameAi(p);SelfNamed=true;}
void NameAi(string p){int n=1;for(int i=0;i<AiBlocks.Count;i++){IMyTerminalBlock b=AiBlocks[i] as IMyTerminalBlock;if(b==null)continue;string t=b.BlockDefinition.TypeIdString+"/"+b.BlockDefinition.SubtypeName;string role=t.ToUpper().Contains("OFFENSIVE")?"AIO":(t.ToUpper().Contains("FLIGHT")?"AIF":"AI");b.CustomName=p+role+(n<10?" 0":" ")+n;n++;}}
void NameFirst(IMyTerminalBlock b,string n){if(b!=null)try{b.CustomName=n;}catch{}}
void NameList<T>(List<T> l,string label) where T:class,IMyTerminalBlock{for(int i=0;i<l.Count;i++){try{l[i].CustomName=label+(i+1<10?" 0":" ")+(i+1);}catch{}}}

bool IsAiBlock(IMyTerminalBlock b){if(!(b is IMyFunctionalBlock))return false;string s=(b.BlockDefinition.TypeIdString+"/"+b.BlockDefinition.SubtypeName).ToUpper();return s.Contains("OFFENSIVE")||s.Contains("FLIGHTMOVEMENT")||s.Contains("FLIGHT");}

void Heartbeat(){Seq++;StringBuilder sb=new StringBuilder();sb.Append("Serial=").Append(Serial).Append(";Origin=").Append(Origin).Append(";Bay=").Append(Bay).Append(";Role=").Append(Role).Append(";Short=").Append(Short).Append(";Wave=").Append(Wave).Append(";State=").Append(State).Append(";Seq=").Append(Seq).Append(";Safe=YES;Fault=").Append(Fault).Append(";BAT=").Append(BatPct()).Append(";H2=").Append(H2Pct());IGC.SendBroadcastMessage(DEFAULT_IGC_TAG,sb.ToString(),TransmissionDistance.TransmissionDistanceMax);if(Serial.Length>0)IGC.SendBroadcastMessage("DRONE:"+Serial,sb.ToString(),TransmissionDistance.TransmissionDistanceMax);}
int BatPct(){double cur=0,max=0;for(int i=0;i<Batteries.Count;i++){try{cur+=Batteries[i].CurrentStoredPower;max+=Batteries[i].MaxStoredPower;}catch{}}return max>0?(int)Math.Round(cur/max*100):0;}
int H2Pct(){double cur=0,max=0;for(int i=0;i<Tanks.Count;i++){try{cur+=Tanks[i].FilledRatio;max+=1;}catch{}}return max>0?(int)Math.Round(cur/max*100):0;}

void WriteStatus(){StringBuilder st=new StringBuilder();st.Append(STATUS_HDR).Append('\n');st.Append("Version=").Append(VERSION).Append('\n');st.Append("Origin=").Append(Origin).Append('\n');st.Append("Serial=").Append(Serial).Append('\n');st.Append("Role=").Append(Role).Append('\n');st.Append("Short=").Append(Short).Append('\n');st.Append("Wave=").Append(Wave).Append('\n');st.Append("Bay=").Append(Bay).Append('\n');st.Append("Slot=").Append(Slot).Append('\n');st.Append("Departure=").Append(Departure).Append('\n');st.Append("State=").Append(State).Append('\n');st.Append("Assigned=").Append(Assigned?"YES":"NO").Append('\n');st.Append("SelfNamed=").Append(SelfNamed?"YES":"NO").Append('\n');st.Append("BatteryPct=").Append(BatPct()).Append('\n');st.Append("H2Pct=").Append(H2Pct()).Append('\n');st.Append("Blocks=").Append(Blocks.Count).Append(" M=").Append(Merges.Count).Append(" C=").Append(Conns.Count).Append(" BAT=").Append(Batteries.Count).Append(" H2=").Append(Tanks.Count).Append(" THR=").Append(Thrusters.Count).Append(" GYRO=").Append(Gyros.Count).Append(" AI=").Append(AiBlocks.Count).Append('\n');st.Append("Fault=").Append(Fault).Append('\n');st.Append("PerfLast=").Append(Hi).Append('/').Append(50000).Append(' ').Append(HiPhase).Append('\n');st.Append("PerfMax=").Append(HiCap).Append('/').Append(50000).Append(' ').Append(HiCapPhase).Append('\n');Me.CustomData=RenderCustomData(Section(Me.CustomData,ASSIGN_HDR),st.ToString());}
string RenderCustomData(string assign,string status){StringBuilder s=new StringBuilder();if(assign!=null&&assign.Trim().Length>0)s.Append(assign.Trim()).Append("\n\n");s.Append(status.Trim()).Append("\n");return s.ToString();}
string Section(string cd,string header){if(cd==null)return"";int a=cd.IndexOf(header);if(a<0)return"";int n=cd.IndexOf("\n[",a+header.Length);if(n<0)n=cd.Length;return cd.Substring(a,n-a).Trim();}

void EchoOut(){Echo("DRONE V004");Echo("SER "+(Serial==""?"-":Serial)+" BAY "+Bay+" W "+Wave);Echo("STATE "+State+" FAULT "+(Fault==""?"-":Fault));Echo("BAT "+BatPct()+" H2 "+H2Pct()+" BLK "+Blocks.Count);Echo("PERF "+(Hi/1000.0).ToString("0.0")+"K max "+(HiCap/1000.0).ToString("0.0")+"K "+HiCapPhase);}
void Upd(string p){int n=Runtime.CurrentInstructionCount;if(n>Hi){Hi=n;HiPhase=p;}if(n>HiCap){HiCap=n;HiCapPhase=p;}}
string Val(string cd,string key){int i=cd.IndexOf(key);if(i<0)return"";i+=key.Length;int e=cd.IndexOf('\n',i);if(e<0)e=cd.Length;return cd.Substring(i,e-i).Trim();}
int ToInt(string s){int v=0;int.TryParse(s,out v);return v;}
