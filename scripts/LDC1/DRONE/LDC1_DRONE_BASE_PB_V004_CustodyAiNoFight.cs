// LDC1 DRONE BASE PB V004 - CustodyAiNoFight
// Temporary/base newborn drone PB for DCS birth registration testing.
// Owns post-heartbeat drone-internal custody. DCS may perform one birth firebreak,
// but this PB owns ongoing captive safe state while assigned.

const string VERSION="LDC1_DRONE_BASE_PB_V004_CustodyAiNoFight";
const string SEC_ASSIGN="[DRONE_ASSIGN]";
const string SEC_STATUS="[DRONE_STATUS]";

string Origin="",Serial="",Role="",Short="",Wave="",Bay="",Slot="",Departure="";
bool Assigned=false;
bool SelfNamed=false;
string State="IDLE";
string Fault="";
int Tick=0;
int ScanTick=0;
int HiInstr=0;
string HiPhase="";

List<IMyBatteryBlock> Bats=new List<IMyBatteryBlock>();
List<IMyGasTank> Tanks=new List<IMyGasTank>();
List<IMyThrust> Thr=new List<IMyThrust>();
List<IMyGyro> Gyros=new List<IMyGyro>();
List<IMyFunctionalBlock> Ais=new List<IMyFunctionalBlock>();
List<IMyRadioAntenna> Ants=new List<IMyRadioAntenna>();
List<IMyShipMergeBlock> Merges=new List<IMyShipMergeBlock>();
List<IMyShipConnector> Cons=new List<IMyShipConnector>();

public Program(){Runtime.UpdateFrequency=UpdateFrequency.Update100;LoadAssign();ScanLocal();}

public void Main(string arg,UpdateType src){
    Tick++;
    string a=(arg??"").Trim().ToUpperInvariant();
    if(a=="BOOT"){LoadAssign();ScanLocal();ApplyCaptiveSafe();WriteStatus();EchoStatus("BOOT");return;}
    if(a=="SCAN"){LoadAssign();ScanLocal();WriteStatus();EchoStatus("SCAN");return;}
    if(a=="STATUS"||a==""){Auto();return;}
    if(a=="CLEAR"){ClearAssign();ScanLocal();WriteStatus();EchoStatus("CLEAR");return;}
    Auto();
}

void Auto(){
    LoadAssign();
    if((ScanTick++%2)==0)ScanLocal();
    UpdateState();
    if(Assigned)ApplyCaptiveSafe();
    WriteStatus();
    EchoStatus("AUTO");
}

void LoadAssign(){
    string cd=Me.CustomData??"";
    Origin=Val(cd,"Origin=");
    Serial=Val(cd,"Serial=");
    Role=Val(cd,"Role=");
    Short=Val(cd,"Short=");
    Wave=Val(cd,"Wave=");
    Bay=Val(cd,"Bay=");
    Slot=Val(cd,"Slot=");
    Departure=Val(cd,"Departure=");
    Assigned=Serial.Length>0;
    if(Assigned&&!SelfNamed)TrySelfName();
}

void ClearAssign(){
    Origin="";Serial="";Role="";Short="";Wave="";Bay="";Slot="";Departure="";Assigned=false;SelfNamed=false;State="IDLE";Fault="";
    Me.CustomData=ReplaceSection(Me.CustomData??"",SEC_ASSIGN,"",false);
}

void TrySelfName(){
    if(Serial.Length<1)return;
    string tag="["+Origin+"-"+Serial+"]";
    if(Me.CustomName.IndexOf(tag,StringComparison.OrdinalIgnoreCase)<0)Me.CustomName=tag+" PB";
    for(int i=0;i<Bats.Count;i++)NameOne(Bats[i],tag,"BAT "+(i+1).ToString("00"));
    for(int i=0;i<Tanks.Count;i++)NameOne(Tanks[i],tag,"H2 "+(i+1).ToString("00"));
    for(int i=0;i<Thr.Count;i++)NameOne(Thr[i],tag,"THR "+(i+1).ToString("00"));
    for(int i=0;i<Gyros.Count;i++)NameOne(Gyros[i],tag,"GYRO "+(i+1).ToString("00"));
    for(int i=0;i<Ais.Count;i++)NameOne(Ais[i],tag,"AI "+(i+1).ToString("00"));
    for(int i=0;i<Ants.Count;i++)NameOne(Ants[i],tag,"ANT "+(i+1).ToString("00"));
    for(int i=0;i<Merges.Count;i++)NameOne(Merges[i],tag,"MERGE "+(i+1).ToString("00"));
    for(int i=0;i<Cons.Count;i++)NameOne(Cons[i],tag,"CONN "+(i+1).ToString("00"));
    SelfNamed=true;
}

void NameOne(IMyTerminalBlock b,string tag,string suffix){
    if(b==null)return;
    if(b.CustomName.IndexOf(tag,StringComparison.OrdinalIgnoreCase)>=0)return;
    if(HasLeadingTag(b.CustomName))return;
    b.CustomName=tag+" "+suffix;
}

bool HasLeadingTag(string s){
    s=s??"";
    s=s.TrimStart();
    return s.StartsWith("[")&&s.IndexOf(']')>1;
}

void ScanLocal(){
    Bats.Clear();Tanks.Clear();Thr.Clear();Gyros.Clear();Ais.Clear();Ants.Clear();Merges.Clear();Cons.Clear();
    var all=new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocks(all);
    for(int i=0;i<all.Count;i++){
        var b=all[i];
        if(b==null||b.CubeGrid!=Me.CubeGrid)continue;
        var bat=b as IMyBatteryBlock;if(bat!=null){Bats.Add(bat);continue;}
        var tank=b as IMyGasTank;if(tank!=null){Tanks.Add(tank);continue;}
        var th=b as IMyThrust;if(th!=null){Thr.Add(th);continue;}
        var gy=b as IMyGyro;if(gy!=null){Gyros.Add(gy);continue;}
        var ant=b as IMyRadioAntenna;if(ant!=null){Ants.Add(ant);continue;}
        var m=b as IMyShipMergeBlock;if(m!=null){Merges.Add(m);continue;}
        var c=b as IMyShipConnector;if(c!=null){Cons.Add(c);continue;}
        var f=b as IMyFunctionalBlock;
        if(f!=null&&IsAiBlock(b))Ais.Add(f);
    }
    if(Assigned&&!SelfNamed)TrySelfName();
}

bool IsAiBlock(IMyTerminalBlock b){
    string d=(b.BlockDefinition.TypeIdString+"/"+b.BlockDefinition.SubtypeId+"/"+b.DefinitionDisplayNameText+"/"+b.CustomName).ToLowerInvariant();
    return d.IndexOf("offensive")>=0||d.IndexOf("defensive")>=0||d.IndexOf("ai")>=0&&d.IndexOf("flight")>=0;
}

void UpdateState(){
    Fault="";
    if(!Assigned){State="IDLE";return;}
    bool mergeConnected=false;
    for(int i=0;i<Merges.Count;i++)if(Merges[i]!=null&&Merges[i].IsConnected)mergeConnected=true;
    bool connectorLocked=false;
    for(int i=0;i<Cons.Count;i++)if(Cons[i]!=null&&Cons[i].Status==MyShipConnectorStatus.Connected)connectorLocked=true;
    if(mergeConnected)State="MERGED_SAFE";
    else if(connectorLocked)State="SERVICE_LOCKED";
    else State="FREE";
}

void ApplyCaptiveSafe(){
    StartPhase("SAFE");
    for(int i=0;i<Bats.Count;i++)SafeBat(Bats[i]);
    for(int i=0;i<Tanks.Count;i++)SafeTank(Tanks[i]);
    for(int i=0;i<Thr.Count;i++)SafeThrust(Thr[i]);
    for(int i=0;i<Gyros.Count;i++)SafeGyro(Gyros[i]);
    for(int i=0;i<Ais.Count;i++)SafeAi(Ais[i]);
    for(int i=0;i<Ants.Count;i++)SafeAnt(Ants[i]);
    EndPhase("SAFE");
}

void SafeBat(IMyBatteryBlock b){if(b==null)return;if(b.ChargeMode!=ChargeMode.Recharge)b.ChargeMode=ChargeMode.Recharge;}
void SafeTank(IMyGasTank t){if(t==null)return;if(!t.Stockpile)t.Stockpile=true;}
void SafeThrust(IMyThrust t){if(t==null)return;if(t.ThrustOverridePercentage!=0)t.ThrustOverridePercentage=0;if(t.Enabled)t.Enabled=false;}
void SafeGyro(IMyGyro g){if(g==null)return;if(g.GyroOverride)g.GyroOverride=false;if(g.Enabled)g.Enabled=false;}
void SafeAi(IMyFunctionalBlock f){if(f==null)return;if(f.Enabled)f.Enabled=false;}
void SafeAnt(IMyRadioAntenna a){if(a==null)return;if(!a.Enabled)a.Enabled=true;if(!a.EnableBroadcasting)a.EnableBroadcasting=true;}

void WriteStatus(){
    StartPhase("WRITE");
    var s=new StringBuilder();
    s.AppendLine(SEC_STATUS);
    s.AppendLine("Version="+VERSION);
    s.AppendLine("Origin="+Origin);
    s.AppendLine("Serial="+Serial);
    s.AppendLine("Role="+Role);
    s.AppendLine("Short="+Short);
    s.AppendLine("Wave="+Wave);
    s.AppendLine("Bay="+Bay);
    s.AppendLine("Slot="+Slot);
    s.AppendLine("Departure="+Departure);
    s.AppendLine("State="+State);
    s.AppendLine("Assigned="+(Assigned?"YES":"NO"));
    s.AppendLine("SelfNamed="+(SelfNamed?"YES":"NO"));
    s.AppendLine("BatteryPct="+F(BatPct()));
    s.AppendLine("H2Pct="+F(H2Pct()));
    s.AppendLine("Blocks="+(Bats.Count+Tanks.Count+Thr.Count+Gyros.Count+Ais.Count+Ants.Count+Merges.Count+Cons.Count)+" M="+Merges.Count+" C="+Cons.Count+" BAT="+Bats.Count+" H2="+Tanks.Count+" THR="+Thr.Count+" GYRO="+Gyros.Count+" AI="+Ais.Count);
    s.AppendLine("Fault="+Fault);
    s.AppendLine("PerfLast="+Runtime.CurrentInstructionCount+"/50000 "+HiPhase);
    s.AppendLine("PerfMax="+HiInstr+"/50000 "+HiPhase);
    string next=ReplaceSection(Me.CustomData??"",SEC_STATUS,s.ToString(),true);
    if(next!=Me.CustomData)Me.CustomData=next;
    EndPhase("WRITE");
}

string ReplaceSection(string cd,string header,string body,bool keepOther){
    int p=cd.IndexOf(header,StringComparison.OrdinalIgnoreCase);
    if(p<0)return keepOther?(cd.TrimEnd()+"\n\n"+body).TrimStart():body;
    int e=cd.IndexOf("\n[",p+header.Length,StringComparison.Ordinal);
    if(e<0)e=cd.Length;
    string pre=cd.Substring(0,p).TrimEnd();
    string post=cd.Substring(e).TrimStart();
    var s=new StringBuilder();
    if(pre.Length>0)s.AppendLine(pre).AppendLine();
    s.Append(body.TrimEnd()).AppendLine();
    if(post.Length>0)s.AppendLine().Append(post);
    return s.ToString();
}

string Val(string cd,string key){
    int p=cd.IndexOf(key,StringComparison.OrdinalIgnoreCase);
    if(p<0)return "";
    p+=key.Length;
    int e=cd.IndexOf('\n',p);
    if(e<0)e=cd.Length;
    return cd.Substring(p,e-p).Trim().Trim('\r');
}

float BatPct(){
    double cur=0,max=0;
    for(int i=0;i<Bats.Count;i++){var b=Bats[i];if(b==null)continue;cur+=b.CurrentStoredPower;max+=b.MaxStoredPower;}
    if(max<=0)return 0;
    return (float)(cur/max*100.0);
}

float H2Pct(){
    double cur=0,max=0;
    for(int i=0;i<Tanks.Count;i++){var t=Tanks[i];if(t==null)continue;cur+=t.FilledRatio;max+=1;}
    if(max<=0)return 0;
    return (float)(cur/max*100.0);
}

void EchoStatus(string phase){
    Echo("DRONE BASE PB V004");
    Echo("Serial "+(Serial.Length>0?Serial:"-"));
    Echo("State "+State+" Assigned "+(Assigned?"YES":"NO"));
    Echo("BAT "+F(BatPct())+" H2 "+F(H2Pct()));
    Echo("M"+Merges.Count+" C"+Cons.Count+" T"+Thr.Count+" G"+Gyros.Count+" AI"+Ais.Count);
    Echo("Instr "+Runtime.CurrentInstructionCount+" hi "+HiInstr+" "+HiPhase);
}

string F(double v){return v.ToString("0.#");}
void StartPhase(string p){}
void EndPhase(string p){int c=Runtime.CurrentInstructionCount;if(c>HiInstr){HiInstr=c;HiPhase=p;}}
