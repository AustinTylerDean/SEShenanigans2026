// OB1_IMS_PB3_V015_BuildPlannerQueueRebalance.cs
// Production/assembly worker for OB1 IMS. PB1 owns UI/targets; PB2 owns logistics/refinery/gas/distribution.
// V015 adds active-tier Build Planner queue rebalance: existing queues that Keen lands on one assembler are offloaded across qAsm.
// Rebalance is bounded, uses existing active-tier/least-loaded queue target logic, and never touches offline/non-active-tier assemblers.
// V013 adds Build Queue Source cooperative guard on top of local assembler-bank enforcement.
// V010 reads PB1 Build Queue Source config and reports read-only awareness. No assembler-bank enforcement yet.
// V009 simplifies production accounting: trust PB3 queue spread/output clearing and use intent ledger to suppress repeat requests.

const string INSTALL_TAG = "[OB1]";
const string PB3_TAG = "[PB3]";
const string PROD_TAG = "[IMSPROD]";
const string PB1_EXPORT_BEGIN = "# IMS_PB1_EXPORT_BEGIN";
const string PB1_EXPORT_END = "# IMS_PB1_EXPORT_END";
const string PROD_STATUS_BEGIN = "# IMS_PB3_STATUS_BEGIN";
const string PROD_STATUS_END = "# IMS_PB3_STATUS_END";
const string BQS_DISABLED_BEGIN = "# IMS_PB3_BQS_DISABLED_BEGIN";
const string BQS_DISABLED_END = "# IMS_PB3_BQS_DISABLED_END";
const string BQS_COOP_BEGIN = "# IMS_PB3_BQS_COOP_GUARD_BEGIN";
const string BQS_COOP_END = "# IMS_PB3_BQS_COOP_GUARD_END";
const double MANAGED_CARGO_MIN_L = 100000.0;
const int SCAN_EVERY = 60;
const int OUTPUT_MOVES_PER_PASS = 16;
const int INPUT_MOVES_PER_PASS = 8;
const int QUEUE_ADD_LIMIT = 2500;
const int QUEUE_DIAG_MAX_ASSEMBLERS = 16;
const int REBALANCE_MAX_MOVE = 300;
const double REBALANCE_MIN_DIFF = 8.0;
const int INTENT_STALE_TICKS = 180;
const int INTENT_SETTLE_TICKS = 18;

List<IMyTerminalBlock> all = new List<IMyTerminalBlock>();
List<IMyAssembler> assemblers = new List<IMyAssembler>();
List<IMyAssembler> allAssemblers = new List<IMyAssembler>();
List<IMyAssembler> qAsm = new List<IMyAssembler>();
List<IMyCargoContainer> cargoCand = new List<IMyCargoContainer>();
List<IMyCargoContainer> managed = new List<IMyCargoContainer>();
List<MyInventoryItem> items = new List<MyInventoryItem>();
List<MyProductionItem> queue = new List<MyProductionItem>();
Dictionary<string,string> pkt = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
List<Led> ledgers = new List<Led>();
List<QSlot> slots = new List<QSlot>();
List<long> bqsDisabled = new List<long>();
List<long> bqsCoopGuard = new List<long>();

int tick = 0, phase = 0, seq = 0, pb1Seq = -1, pb1Age = 999999, asmCursor = 0, outCursor = 0, inCursor = 0, activeTier = 0, fullTier = 0, basicTier = 0, survTier = 0;
string state = "INIT", fault = "NONE", lastQueue = "NONE", plan = "NONE", lastOut = "NONE", lastIn = "NONE", pb1State = "NO PACKET", instrAt = "INIT";
string queueDiag = "QDIAG not run";
string lastRebal = "IDLE";
string imsEntityTag = "OB1", imsEntityLabel = "OB1", bqsSource = "AUTO", bqsSourceTag = "AUTO", bqsClusterTag = "AUTO", bqsClusterSrc = "OB1", bqsLocalState = "ACTIVE", bqsEnforce = "NONE";
int instrLast = 0, instrHigh = 0;
bool discovered = false, bqsLoaded = false, bqsCoopLoaded = false;

string[] compKeys = { "STEEL_PLATE", "INT._PLATE", "CONST._COMP", "MOTOR", "COMPUTER", "DISPLAY", "GIRDER", "SMALL_TUBE", "LARGE_TUBE", "METAL_GRID", "B._GLASS", "MEDICAL", "DETECTOR", "RADIO_COMM", "POWER_CELL", "REACTOR_COMP", "SUPERCOND.", "THRUSTER_COMP" };
string[] compLabels = { "Steel", "Int Plate", "Const", "Motor", "Computer", "Display", "Girder", "S Tube", "L Tube", "Grid", "Glass", "Medical", "Detector", "Radio", "Power", "Reactor", "Super", "Thrust" };
string[] compTokens = { "SteelPlate", "InteriorPlate", "Construction", "Motor", "Computer", "Display", "Girder", "SmallTube", "LargeTube", "MetalGrid", "BulletproofGlass", "Medical", "Detector", "RadioCommunication", "PowerCell", "Reactor", "Superconductor", "Thrust" };
string[] compBp = { "SteelPlate", "InteriorPlate", "ConstructionComponent", "MotorComponent", "ComputerComponent", "Display", "GirderComponent", "SmallTube", "LargeTube", "MetalGrid", "BulletproofGlass", "MedicalComponent", "DetectorComponent", "RadioCommunicationComponent", "PowerCell", "ReactorComponent", "Superconductor", "ThrustComponent" };
string[] ammoKeys = { "GATLING_BOX", "RIFLE_MAG", "MISSILES", "ARTILLERY", "ASSAULT", "AUTOCANNON", "RAILGUN" };
string[] ammoLabels = { "Gatling", "Rifle", "Missile", "Artillery", "Assault", "Auto", "Rail" };
string[] ammoTokens = { "NATO_25x184mm", "RapidFireAutomaticRifleGun_Mag_50rd", "Missile", "LargeCalibre", "MediumCalibre", "Autocannon", "Railgun" };
string[] ammoBp = { "Position0080_NATO_25x184mmMagazine", "Position0050_RapidFireAutomaticRifleGun_Mag_50rd", "Position0100_Missile200mm", "Position0120_LargeCalibreAmmo", "Position0110_MediumCalibreAmmo", "Position0090_AutocannonClip", "Position0130_SmallRailgunAmmo" };

public Program(){Runtime.UpdateFrequency=UpdateFrequency.Update10;}

public void Main(string arg, UpdateType src)
{
    tick++; string ph="MAIN";
    if(!discovered || tick%SCAN_EVERY==1 || Eq(arg,"RESCAN")) { ph="SCAN"; Discover(); }
    ReadPacket();
    EnforceBuildQueueSource();
    if(Eq(arg,"CLEAR STALE")){ ClearStale(); ph="LEDGER"; UpdateInstr(ph); WriteStatus(); return; }
    if(Eq(arg,"CLEAR LEDGER")){ ledgers.Clear(); lastQueue="LEDGER CLEARED"; ph="LEDGER"; UpdateInstr(ph); WriteStatus(); return; }
    if(Eq(arg,"QUEUE ONCE")){ ph="QUEUE"; QueueOnce(true); UpdateInstr(ph); WriteStatus(); return; }
    if(Eq(arg,"QDIAG") || Eq(arg,"QUEUE DIAG")){ ph="QDIAG"; QueueDiag(); UpdateInstr(ph); WriteStatus(); return; }
    if(Eq(arg,"REBAL ONCE") || Eq(arg,"REBALANCE ONCE")){ ph="REBAL"; RebalanceQueues(true); UpdateInstr(ph); WriteStatus(); return; }
    if(Eq(arg,"CLEAR OUTPUTS")){ ph="OUT"; ClearOutputs(true); UpdateInstr(ph); WriteStatus(); return; }
    if(Eq(arg,"CLEAR INPUTS")){ ph="IN"; ClearInputs(true); UpdateInstr(ph); WriteStatus(); return; }
    if(phase==0){ph="ASM"; ClassifyAssemblers();}
    else if(phase==1){ph="OUT"; ClearOutputs(false);}
    else if(phase==2){ph="IN"; ClearInputs(false);}
    else if(phase==3){ph="REBAL"; RebalanceQueues(false);}
    else if(phase==4){ph="QUEUE"; QueueOnce(false);}
    else if(phase==5){ph="STATUS";}
    phase=(phase+1)%6;
    UpdateInstr(ph);
    WriteStatus();
}

void Discover()
{
    discovered=true; all.Clear(); assemblers.Clear(); allAssemblers.Clear(); qAsm.Clear(); cargoCand.Clear(); managed.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyAssembler>(allAssemblers);
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(all,b=>b!=null && HasTag(b,INSTALL_TAG));
    for(int i=0;i<all.Count;i++){
        IMyTerminalBlock b=all[i];
        IMyAssembler a=b as IMyAssembler; if(a!=null) assemblers.Add(a);
        IMyCargoContainer c=b as IMyCargoContainer; if(c!=null) cargoCand.Add(c);
    }
    SelectManagedCargo();
    state="DISCOVERED";
}

void SelectManagedCargo()
{
    managed.Clear();
    if(cargoCand.Count==0) return;
    double bestCap=0,bestTot=0;
    for(int i=0;i<cargoCand.Count;i++){
        double cap=CapL(cargoCand[i]); if(cap<=0) continue;
        double tot=0;
        for(int j=0;j<cargoCand.Count;j++){ double c=CapL(cargoCand[j]); if(SameTier(cap,c))tot+=c; }
        if(tot>bestTot || (Math.Abs(tot-bestTot)<1 && cap>bestCap)){bestTot=tot;bestCap=cap;}
    }
    for(int i=0;i<cargoCand.Count;i++){ double cap=CapL(cargoCand[i]); if((bestCap>=MANAGED_CARGO_MIN_L && SameTier(bestCap,cap)) || (bestCap<MANAGED_CARGO_MIN_L && cap>=MANAGED_CARGO_MIN_L)) managed.Add(cargoCand[i]); }
}

void ClassifyAssemblers()
{
    qAsm.Clear();
    if(Eq(bqsLocalState,"OFFLINE")){ activeTier=0; fullTier=0; basicTier=0; survTier=0; state="ASM "+assemblers.Count+" BQS OFFLINE"; return; } int coop=0, off=0, dead=0; activeTier=0; fullTier=0; basicTier=0; survTier=0;
    for(int pass=0;pass<2;pass++){
        if(pass==1 && activeTier<1){ if(fullTier>0)activeTier=3; else if(basicTier>0)activeTier=2; else if(survTier>0)activeTier=1; }
        for(int i=0;i<assemblers.Count;i++){
            IMyAssembler a=assemblers[i]; if(a==null)continue;
            int tier=AsmTier(a); if(!a.IsFunctional){if(pass==0)dead++; continue;}
            IMyFunctionalBlock fb=a as IMyFunctionalBlock; if(fb!=null && !fb.Enabled){if(pass==0)off++; continue;}
            if(a.CooperativeMode){if(pass==0)coop++; continue;}
            if(pass==0){ if(tier==3)fullTier++; else if(tier==2)basicTier++; else survTier++; }
            else if(tier==activeTier){ try{ if(!a.UseConveyorSystem)a.UseConveyorSystem=true; if(a.Mode!=MyAssemblerMode.Assembly)a.Mode=MyAssemblerMode.Assembly; }catch{} qAsm.Add(a); }
        }
    }
    state="ASM "+assemblers.Count+" T"+TierName(activeTier)+" q "+qAsm.Count+" F"+fullTier+" B"+basicTier+" S"+survTier+" coop "+coop+" off "+off+" dead "+dead;
}

int AsmTier(IMyAssembler a)
{
    string n=Safe(a.CustomName).ToUpperInvariant(), sub=Safe(a.BlockDefinition.SubtypeName).ToUpperInvariant(), type=Safe(a.BlockDefinition.TypeIdString).ToUpperInvariant();
    if(n.IndexOf("SURVIVAL")>=0 || sub.IndexOf("SURVIVAL")>=0 || type.IndexOf("SURVIVAL")>=0)return 1;
    if(n.IndexOf("BASIC")>=0 || sub.IndexOf("BASIC")>=0)return 2;
    return 3;
}
string TierName(int t){return t==3?"FULL":t==2?"BASIC":t==1?"SURV":"NONE";}

void ReadPacket()
{
    string block=Extract(Me.CustomData,PB1_EXPORT_BEGIN,PB1_EXPORT_END);
    if(block.Length<1){pb1State="NO PACKET"; pb1Age++; return;}
    pkt.Clear(); string[] lines=block.Split('\n');
    for(int i=0;i<lines.Length;i++){
        string l=lines[i].Trim(); if(l.Length==0 || l[0]=='#') continue;
        int p=l.IndexOf('='); if(p<1) continue;
        pkt[l.Substring(0,p).Trim()]=l.Substring(p+1).Trim();
    }
    int s;if(int.TryParse(Get("Seq","-1"),out s)){ if(s!=pb1Seq){pb1Seq=s;pb1Age=0;} else pb1Age++; }
    pb1State="OK";
    imsEntityTag=Get("IMS.EntityTag","OB1");
    imsEntityLabel=Get("IMS.EntityLabel",imsEntityTag);
    bqsSource=Get("BuildQueue.Source","AUTO");
    bqsSourceTag=Get("BuildQueue.SourceTag",bqsSource);
    bqsClusterTag=Get("BuildQueue.ClusterTag",bqsSourceTag);
    bqsClusterSrc=Get("BuildQueue.ClusterSource",imsEntityTag);
    UpdateBuildQueueState();
}

void UpdateBuildQueueState()
{
    string s=Safe(bqsSource).ToUpperInvariant();
    string t=Safe(bqsClusterTag).ToUpperInvariant();
    string me=Safe(imsEntityTag).ToUpperInvariant();
    if(s.Length==0 || s=="AUTO") bqsLocalState="ACTIVE";
    else if(s=="LOCAL" || t==me || s==me) bqsLocalState="ACTIVE";
    else bqsLocalState="OFFLINE";
}


void LoadBqsDisabled()
{
    if(bqsLoaded)return; bqsLoaded=true; bqsDisabled.Clear();
    string block=Extract(Me.CustomData,BQS_DISABLED_BEGIN,BQS_DISABLED_END); if(block.Length<1)return;
    string[] lines=block.Split('\n');
    for(int i=0;i<lines.Length;i++){ long id; string l=lines[i].Trim(); if(long.TryParse(l,out id) && !HasId(bqsDisabled,id)) bqsDisabled.Add(id); }
}
void SaveBqsDisabled()
{
    string block=BQS_DISABLED_BEGIN+"\n"; for(int i=0;i<bqsDisabled.Count;i++) block+=bqsDisabled[i].ToString()+"\n"; block+=BQS_DISABLED_END+"\n";
    Me.CustomData=ReplaceBlock(Me.CustomData,BQS_DISABLED_BEGIN,BQS_DISABLED_END,block);
}

void LoadBqsCoopGuard()
{
    if(bqsCoopLoaded)return; bqsCoopLoaded=true; bqsCoopGuard.Clear();
    string block=Extract(Me.CustomData,BQS_COOP_BEGIN,BQS_COOP_END); if(block.Length<1)return;
    string[] lines=block.Split('\n');
    for(int i=0;i<lines.Length;i++){ long id; string l=lines[i].Trim(); if(long.TryParse(l,out id) && !HasId(bqsCoopGuard,id)) bqsCoopGuard.Add(id); }
}
void SaveBqsCoopGuard()
{
    string block=BQS_COOP_BEGIN+"\n"; for(int i=0;i<bqsCoopGuard.Count;i++) block+=bqsCoopGuard[i].ToString()+"\n"; block+=BQS_COOP_END+"\n";
    Me.CustomData=ReplaceBlock(Me.CustomData,BQS_COOP_BEGIN,BQS_COOP_END,block);
}
IMyAssembler FindAnyAssemblerById(long id){ for(int i=0;i<allAssemblers.Count;i++) if(allAssemblers[i]!=null && allAssemblers[i].EntityId==id)return allAssemblers[i]; return null; }
bool BqsProtected(){ string t=Safe(bqsClusterTag).ToUpperInvariant(); return t.Length>0 && t!="AUTO"; }
bool IsSelectedSourceAsm(IMyAssembler a){ string t=Safe(bqsClusterTag).ToUpperInvariant(); if(t.Length<1||t=="AUTO")return false; return HasTag(a,"["+t+"]"); }
int ApplyCoopGuard()
{
    LoadBqsCoopGuard(); int changed=0;
    for(int i=0;i<allAssemblers.Count;i++){
        IMyAssembler a=allAssemblers[i]; if(a==null)continue;
        if(HasTag(a,INSTALL_TAG) || IsSelectedSourceAsm(a))continue;
        if(a.CooperativeMode){ if(!HasId(bqsCoopGuard,a.EntityId)) bqsCoopGuard.Add(a.EntityId); try{a.CooperativeMode=false; changed++;}catch{} }
    }
    if(changed>0)SaveBqsCoopGuard(); return changed;
}
int RestoreCoopGuard()
{
    LoadBqsCoopGuard(); int restored=0;
    if(bqsCoopGuard.Count<1)return 0;
    for(int i=bqsCoopGuard.Count-1;i>=0;i--){
        IMyAssembler a=FindAnyAssemblerById(bqsCoopGuard[i]);
        if(a==null)continue; // keep entry; restore when seen again
        try{ if(!a.CooperativeMode){a.CooperativeMode=true; restored++;} bqsCoopGuard.RemoveAt(i); }catch{}
    }
    SaveBqsCoopGuard(); return restored;
}
bool HasId(List<long> list,long id){ for(int i=0;i<list.Count;i++) if(list[i]==id)return true; return false; }
void EnforceBuildQueueSource()
{
    LoadBqsDisabled(); LoadBqsCoopGuard(); int changed=0, restored=0, coopChanged=0, coopRestored=0;
    if(BqsProtected()) coopChanged=ApplyCoopGuard(); else coopRestored=RestoreCoopGuard();
    if(Eq(bqsLocalState,"OFFLINE")){
        for(int i=0;i<assemblers.Count;i++){ IMyFunctionalBlock fb=assemblers[i] as IMyFunctionalBlock; if(fb!=null && fb.Enabled){ if(!HasId(bqsDisabled,assemblers[i].EntityId)) bqsDisabled.Add(assemblers[i].EntityId); try{fb.Enabled=false; changed++;}catch{} } }
        if(changed>0)SaveBqsDisabled(); bqsEnforce="OFFLINE disabled "+changed+" held "+bqsDisabled.Count+" coopOff "+coopChanged; return;
    }
    if(bqsDisabled.Count>0){
        for(int i=bqsDisabled.Count-1;i>=0;i--){ IMyAssembler a=FindAssemblerById(bqsDisabled[i]); IMyFunctionalBlock fb=a as IMyFunctionalBlock; if(fb!=null){ try{ if(!fb.Enabled){fb.Enabled=true; restored++;} }catch{} } bqsDisabled.RemoveAt(i); }
        SaveBqsDisabled();
    }
    bqsEnforce="ACTIVE restored "+restored+" coopOff "+coopChanged+" coopRest "+coopRestored+" coopHeld "+bqsCoopGuard.Count;
}
IMyAssembler FindAssemblerById(long id){ for(int i=0;i<assemblers.Count;i++) if(assemblers[i]!=null && assemblers[i].EntityId==id)return assemblers[i]; return null; }

bool AssemblyAllowed()
{
    int pause; if(int.TryParse(Get("Mode.SYSTEM.WORKER_MODE","0"),out pause) && pause>0) return false;
    // PB1 labels ASSEMBLY=0 as AUTO and >0 as ON. Both allow PB3 to work; category switches decide what to produce.
    return true;
}

bool EditHoldActive(){ return IsOn(Get("Console.EditHold","0")); }

void QueueOnce(bool force)
{
    if(!force && EditHoldActive()){plan="EDIT HOLD"; lastQueue="SKIP EDIT HOLD"; return;}
    if(Eq(bqsLocalState,"OFFLINE")){plan="OFFLINE BY "+bqsClusterTag; lastQueue="SKIP BQS OFFLINE"; return;}
    if(!AssemblyAllowed()){lastQueue="SKIP PAUSE"; return;}
    ClassifyAssemblers();
    UpdateLedgers();
    if(qAsm.Count<1){lastQueue="SKIP NO ASM"; return;}
    Candidate c=FindCandidate(force);
    if(c==null){lastQueue="SKIP "+plan; return;}
    MyDefinitionId bp; try{bp=MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/"+c.bp);}catch{lastQueue="BAD BP "+c.bp; return;}
    int need=(int)Math.Floor(c.need); if(need<1){lastQueue="SKIP SMALL"; return;}
    if(need>QUEUE_ADD_LIMIT) need=QUEUE_ADD_LIMIT;
    double qBefore=QueuedBp(c.bp);
    int added=AddQueueSpread(bp,need), qAfter=(int)Math.Floor(QueuedBp(c.bp)), delta=qAfter-(int)Math.Floor(qBefore);
    if(added>0) StartLedger(c,added);
    lastQueue=added>0 ? "OK "+c.label+" +"+added+"/"+need+" d"+delta : "FAIL "+c.label;
}

int AddQueueSpread(MyDefinitionId bp,int need)
{
    return AddQueueSpreadEx(bp,need,null);
}

int AddQueueSpreadEx(MyDefinitionId bp,int need,IMyAssembler exclude)
{
    slots.Clear();
    for(int i=0;i<qAsm.Count;i++){ IMyAssembler a=qAsm[i]; if(a==null)continue; if(exclude!=null && a.EntityId==exclude.EntityId)continue; QSlot s=new QSlot(); s.a=a; s.load=QueueLoad(a); slots.Add(s); }
    int added=0, guard=0;
    while(added<need && slots.Count>0 && guard<slots.Count*4){
        guard++; int bi=0; double bl=slots[0].load;
        for(int i=1;i<slots.Count;i++) if(slots[i].load<bl){bi=i; bl=slots[i].load;}
        int left=need-added, amt=(int)Math.Ceiling(left/(double)slots.Count); if(amt<1)amt=1;
        IMyAssembler a=slots[bi].a;
        try{ if(a.Mode!=MyAssemblerMode.Assembly)a.Mode=MyAssemblerMode.Assembly; a.AddQueueItem(bp,(MyFixedPoint)amt); added+=amt; slots[bi].load+=amt; }
        catch{ slots.RemoveAt(bi); }
    }
    return added;
}

double QueueLoad(IMyAssembler a)
{
    double t=0; if(a==null)return 999999; queue.Clear();
    try{a.GetQueue(queue); for(int i=0;i<queue.Count;i++)t+=(double)queue[i].Amount;}catch{return 999999;}
    return t;
}

void RebalanceQueues(bool force)
{
    if(Eq(bqsLocalState,"OFFLINE")){ lastRebal="SKIP BQS OFFLINE"; return; }
    if(qAsm.Count<2) ClassifyAssemblers();
    if(qAsm.Count<2){ lastRebal="SKIP ASM "+qAsm.Count; return; }
    double minLoad=999999999, maxLoad=-1; int minI=-1, maxI=-1;
    for(int i=0;i<qAsm.Count;i++){
        double l=QueueLoad(qAsm[i]);
        if(l<minLoad){minLoad=l; minI=i;}
        if(l>maxLoad && l<999999){maxLoad=l; maxI=i;}
    }
    double diff=maxLoad-minLoad;
    if(maxI<0 || minI<0 || diff<REBALANCE_MIN_DIFF){ lastRebal="IDLE diff "+diff.ToString("0"); return; }
    IMyAssembler src=qAsm[maxI]; if(src==null){lastRebal="SKIP SRC"; return;}
    queue.Clear(); try{src.GetQueue(queue);}catch{lastRebal="SKIP GETQ"; return;}
    if(queue.Count<1){lastRebal="IDLE EMPTY"; return;}
    int qi=-1; double itemAmt=0;
    for(int i=0;i<queue.Count;i++){ itemAmt=(double)queue[i].Amount; if(itemAmt>=1){qi=i; break;} }
    if(qi<0){lastRebal="IDLE SMALL"; return;}
    MyProductionItem it=queue[qi];
    int move=(int)Math.Floor(Math.Min(itemAmt, Math.Ceiling(diff/2.0)));
    if(move>REBALANCE_MAX_MOVE) move=REBALANCE_MAX_MOVE;
    if(move<1){lastRebal="IDLE MOVE0"; return;}
    MyDefinitionId bp=it.BlueprintId;
    try{src.RemoveQueueItem(qi,(MyFixedPoint)move);}catch{lastRebal="FAIL REMOVE "+Short(src); return;}
    int added=AddQueueSpreadEx(bp,move,src);
    if(added<move){
        int back=move-added;
        try{src.AddQueueItem(bp,(MyFixedPoint)back);}catch{}
    }
    lastRebal="MOVE "+ShortBp(bp.SubtypeName)+" "+added+"/"+move+" from "+Short(src)+" diff "+diff.ToString("0");
}

void QueueDiag()
{
    ClassifyAssemblers();
    string r="QUEUE DIAG V015\n";
    r += "ActiveTier="+TierName(activeTier)+" qAsm="+qAsm.Count+" allAsm="+assemblers.Count+" BQS="+bqsLocalState+"\n";
    if(qAsm.Count<1){ queueDiag=r+"No active queue assemblers.\n"; lastQueue="QDIAG NO ASM"; return; }
    double minLoad=999999999, maxLoad=-1; int minI=-1, maxI=-1;
    int totalItems=0; double totalAmt=0;
    for(int i=0;i<qAsm.Count;i++)
    {
        IMyAssembler a=qAsm[i]; if(a==null)continue;
        queue.Clear(); double load=0; int count=0; string first="EMPTY";
        try{
            a.GetQueue(queue); count=queue.Count; totalItems+=count;
            for(int j=0;j<queue.Count;j++){ double amt=(double)queue[j].Amount; load+=amt; totalAmt+=amt; if(j==0) first=ShortBp(queue[j].BlueprintId.SubtypeName)+" x"+amt.ToString("0.##"); }
        }catch{ load=999999; first="GETQUEUE ERR"; }
        if(load<minLoad){minLoad=load; minI=i;}
        if(load>maxLoad && load<999999){maxLoad=load; maxI=i;}
        if(i<QUEUE_DIAG_MAX_ASSEMBLERS) r += Pad2(i)+" "+Short(a)+" load="+load.ToString("0.##")+" items="+count+" first="+first+"\n";
    }
    r += "Total queue items="+totalItems+" amount="+totalAmt.ToString("0.##")+"\n";
    if(maxI>=0 && minI>=0)
    {
        r += "High="+Short(qAsm[maxI])+" "+maxLoad.ToString("0.##")+" Low="+Short(qAsm[minI])+" "+minLoad.ToString("0.##")+" diff="+(maxLoad-minLoad).ToString("0.##")+"\n";
        if(maxLoad>minLoad+1 && qAsm.Count>1)
        {
            IMyAssembler src=qAsm[maxI]; queue.Clear(); src.GetQueue(queue);
            if(queue.Count>0)
            {
                MyProductionItem it=queue[0]; double move=Math.Max(1, Math.Floor(((double)it.Amount)/2.0));
                r += "Rebal candidate: idx0 "+ShortBp(it.BlueprintId.SubtypeName)+" amount="+((double)it.Amount).ToString("0.##")+" move~"+move.ToString("0")+" from "+Short(src)+"\n";
                r += "Next patch can RemoveQueueItem(index, amount) then AddQueueSpread excluding source.\n";
            }
        }
        else r += "Rebal candidate: none; queue loads already close or empty.\n";
    }
    queueDiag=r; lastQueue="QDIAG";
}

string ShortBp(string s){ if(s==null)return"?"; int p=s.LastIndexOf('/'); if(p>=0 && p<s.Length-1)s=s.Substring(p+1); return s.Length>22?s.Substring(0,22):s; }
string Pad2(int n){ return n<10?"0"+n.ToString():n.ToString(); }

class Candidate{public string cat,label,token,bp,key; public double need,cur,target,q,outp;}
class Led{public string key,label,token,bp; public double req,target,lastCur; public int lastActiveTick; public bool stale,settling;}
class QSlot{public IMyAssembler a; public double load;}
Candidate FindCandidate(bool force)
{
    plan="NONE";
    Candidate best=null;
    int start=asmCursor%2;
    for(int p=0;p<2;p++){
        int ci=(start+p)%2;
        if(ci==0) CheckCategory("COMPONENTS",compKeys,compLabels,compTokens,compBp,ref best);
        else CheckCategory("AMMO",ammoKeys,ammoLabels,ammoTokens,ammoBp,ref best);
        if(best!=null){asmCursor=(ci+1)%2; plan=best.cat+" "+best.label+" +"+best.need.ToString("0"); return best;}
    }
    if(plan=="NONE") plan="NO NEED"; return null;
}

void CheckCategory(string cat,string[] keys,string[] labs,string[] toks,string[] bps,ref Candidate best)
{
    int mode; bool on=int.TryParse(Get("Mode.ASSEMBLY."+cat,"0"),out mode) && mode>0;
    if(!on){
        for(int i=0;i<keys.Length;i++){
            string bk=cat+"."+keys[i]; double cur=D("Current."+bk), min=D("Target."+bk+".Min");
            if(min>0 && cur<min && plan=="NONE") plan=cat+" OFF "+labs[i]+" "+cur.ToString("0")+"/"+min.ToString("0");
        }
        return;
    }
    for(int i=0;i<keys.Length;i++){
        string baseKey=cat+"."+keys[i];
        double cur=D("Current."+baseKey), target=D("Target."+baseKey+(mode==1?".Min":".Max"));
        double q=QueuedBp(bps[i]), outp=PendingOutput(toks[i]), raw=target-cur;
        Led led=FindLed(baseKey);
        if(led!=null){
            if(plan=="NONE") plan=(led.stale?"STALE ":(led.settling?"SETTLE ":"WAIT "))+labs[i]+" "+cur.ToString("0")+"/"+target.ToString("0")+" q"+q.ToString("0")+" o"+outp.ToString("0")+" i"+led.req.ToString("0");
            continue;
        }
        double accounted=q+outp, need=Math.Floor(raw-accounted);
        if(need<1){ if(raw>=1 && accounted>=1 && plan=="NONE") plan="WAIT "+labs[i]+" "+cur.ToString("0")+"/"+target.ToString("0")+" q"+q.ToString("0")+" o"+outp.ToString("0"); continue; }
        if(best==null || need>best.need){ best=new Candidate(); best.cat=cat; best.label=labs[i]; best.token=toks[i]; best.bp=bps[i]; best.key=baseKey; best.need=need; best.cur=cur; best.target=target; best.q=q; best.outp=outp; }
    }
}

void ClearOutputs(bool force)
{
    int moves=0; if(assemblers.Count<1)return;
    int budget=force?OUTPUT_MOVES_PER_PASS*4:OUTPUT_MOVES_PER_PASS;
    for(int pass=0;pass<assemblers.Count && moves<budget;pass++){
        int ai=(outCursor+pass)%assemblers.Count; IMyAssembler a=assemblers[ai]; if(a==null||a.InventoryCount<2)continue;
        IMyInventory inv=a.GetInventory(1); items.Clear(); inv.GetItems(items); if(items.Count<1)continue;
        for(int k=items.Count-1;k>=0 && moves<budget;k--){ if(MoveToManaged(inv,k,null)){moves++; lastOut="OUT "+ShortItem(items[k])+" "+Short(a);} }
        outCursor=(ai+1)%assemblers.Count;
    }
    if(moves==0)lastOut="IDLE";
}

void ClearInputs(bool force)
{
    int moves=0; if(assemblers.Count<1)return;
    int budget=force?INPUT_MOVES_PER_PASS*4:INPUT_MOVES_PER_PASS;
    for(int pass=0;pass<assemblers.Count && moves<budget;pass++){
        int ai=(inCursor+pass)%assemblers.Count; IMyAssembler a=assemblers[ai]; if(a==null||a.InventoryCount<1)continue;
        IMyInventory inv=a.GetInventory(0); items.Clear(); inv.GetItems(items); if(items.Count<1)continue;
        bool hasQ=false; queue.Clear(); a.GetQueue(queue); if(queue.Count>0)hasQ=true;
        for(int k=items.Count-1;k>=0 && moves<budget;k--){
            string type=items[k].Type.TypeId.ToString();
            bool ingot=type.IndexOf("Ingot",StringComparison.OrdinalIgnoreCase)>=0;
            if(hasQ && ingot && !force) continue; // keep useful feedstock while this assembler has a queue
            if(MoveToManaged(inv,k,null)){moves++; lastIn="IN "+ShortItem(items[k])+" "+Short(a);} 
        }
        inCursor=(ai+1)%assemblers.Count;
    }
    if(moves==0)lastIn="IDLE";
}

bool MoveToManaged(IMyInventory src,int itemIndex,MyFixedPoint? amount)
{
    if(src==null||managed.Count<1)return false;
    int start=tick%managed.Count;
    for(int i=0;i<managed.Count;i++){
        IMyInventory dst=managed[(start+i)%managed.Count].GetInventory(0);
        try{ if(src.TransferItemTo(dst,itemIndex,null,true,amount)) return true; }catch{}
    }
    return false;
}


Led FindLed(string key){ for(int i=0;i<ledgers.Count;i++) if(Eq(ledgers[i].key,key)) return ledgers[i]; return null; }
void StartLedger(Candidate c,int added)
{
    Led l=FindLed(c.key); if(l==null){l=new Led(); ledgers.Add(l);} 
    l.key=c.key; l.label=c.label; l.token=c.token; l.bp=c.bp; l.req=added; l.target=c.target; l.lastCur=c.cur; l.lastActiveTick=tick; l.stale=false; l.settling=false;
}
void UpdateLedgers()
{
    for(int i=ledgers.Count-1;i>=0;i--){
        Led l=ledgers[i]; double cur=D("Current."+l.key), q=QueuedBp(l.bp), outp=PendingOutput(l.token);
        if(cur>=l.target-0.001){ledgers.RemoveAt(i); continue;}
        bool active=q>=1 || outp>=1;
        if(cur>l.lastCur+0.001 || active){l.lastCur=cur; l.lastActiveTick=tick; l.stale=false; l.settling=false; continue;}
        int idle=tick-l.lastActiveTick;
        if(idle>INTENT_STALE_TICKS){l.stale=true; l.settling=false;}
        else if(idle>INTENT_SETTLE_TICKS) ledgers.RemoveAt(i); // trust output clearing/PB1 lag briefly, then allow exact remaining shortage
        else l.settling=true;
    }
}
void ClearStale(){ int n=0; for(int i=ledgers.Count-1;i>=0;i--) if(ledgers[i].stale){ledgers.RemoveAt(i); n++;} lastQueue="CLEAR STALE "+n; }
string LedgerSummary(){ int st=0,se=0; double r=0; for(int i=0;i<ledgers.Count;i++){if(ledgers[i].stale)st++; if(ledgers[i].settling)se++; r+=ledgers[i].req;} return ledgers.Count+"/"+st+" r"+r.ToString("0")+" s"+se; }
void UpdateInstr(string ph){ instrLast=Runtime.CurrentInstructionCount; if(instrLast>instrHigh){instrHigh=instrLast; instrAt=ph;} }

double QueuedBp(string bp){ double t=0; for(int i=0;i<qAsm.Count;i++){ IMyAssembler a=qAsm[i]; if(a==null)continue; queue.Clear(); a.GetQueue(queue); for(int q=0;q<queue.Count;q++){ string s=queue[q].BlueprintId.SubtypeName; if(Eq(s,bp)||s.EndsWith("/"+bp,StringComparison.OrdinalIgnoreCase)) t+=(double)queue[q].Amount; } } return t; }
double PendingOutput(string token){ double t=0; for(int i=0;i<assemblers.Count;i++){ IMyAssembler a=assemblers[i]; if(a==null||a.InventoryCount<2)continue; t+=CountInvToken(a.GetInventory(1),token); } return t; }
double CountInvToken(IMyInventory inv,string token){ double t=0; items.Clear(); inv.GetItems(items); for(int i=0;i<items.Count;i++) if(items[i].Type.SubtypeId.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0) t+=(double)items[i].Amount; return t; }

double D(string k){ double v; return double.TryParse(Get(k,"0"),out v)?v:0; }
string Get(string k,string d){ string v; return pkt.TryGetValue(k,out v)?v:d; }
bool IsOn(string v){ if(v==null)return false; v=v.Trim(); return v=="1"||Eq(v,"ON")||Eq(v,"YES")||Eq(v,"TRUE"); }
bool Eq(string a,string b){return a!=null&&a.Equals(b,StringComparison.OrdinalIgnoreCase);} 
string Safe(string s){return s==null?"":s;}
bool HasTag(IMyTerminalBlock b,string tag){return b!=null&&b.CustomName!=null&&b.CustomName.IndexOf(tag,StringComparison.OrdinalIgnoreCase)>=0;}
double CapL(IMyTerminalBlock b){ if(b==null||!b.HasInventory)return 0; return (double)b.GetInventory(0).MaxVolume*1000.0; }
bool SameTier(double a,double b){ if(a<=0||b<=0)return false; double m=Math.Max(a,b); return Math.Abs(a-b)/m<=0.02; }
string Short(IMyTerminalBlock b){ if(b==null)return"?"; string n=b.CustomName.Replace(INSTALL_TAG,"").Replace(PB3_TAG,"").Replace(PROD_TAG,"").Trim(); return n.Length>18?n.Substring(0,18):n; }
string ShortItem(MyInventoryItem it){ string s=it.Type.SubtypeId; return s.Length>16?s.Substring(0,16):s; }

string BuildText(){ return "OB1 IMS PB3 PROD V015 QREBAL\n"+state+" Fault "+fault+"\nPB1 "+pb1State+" seq "+pb1Seq+" age "+pb1Age+"\nBQS "+bqsSource+" -> "+bqsClusterTag+" src "+bqsClusterSrc+" LocalASM "+bqsLocalState+"\nBQS ENF "+bqsEnforce+"\nASM "+assemblers.Count+" T"+TierName(activeTier)+" Q "+qAsm.Count+" Cargo "+managed.Count+"\nTiers F"+fullTier+" B"+basicTier+" S"+survTier+"\nModes C"+Get("Mode.ASSEMBLY.COMPONENTS","?")+" A"+Get("Mode.ASSEMBLY.AMMO","?")+" W"+Get("Mode.SYSTEM.WORKER_MODE","?")+" E"+Get("Console.EditHold","0")+"\nPlan "+plan+"\nQueue "+lastQueue+"\nLedger "+LedgerSummary()+"\nRebal "+lastRebal+"\nOut "+lastOut+"\nIn "+lastIn+"\nInstr "+instrLast+"/50000 High "+instrHigh+" "+instrAt+"\n"+queueDiag+"Cmds: RESCAN | QUEUE ONCE | REBAL ONCE | QDIAG | CLEAR OUTPUTS | CLEAR INPUTS | CLEAR STALE | CLEAR LEDGER\n"; }
void WriteStatus(){ Echo(BuildText()); seq++; string sb=PROD_STATUS_BEGIN+"\nWorkerVersion=OB1_PB3_V015_BUILD_PLANNER_QUEUE_REBALANCE\nSeq="+seq+"\nState="+state+"\nFault="+fault+"\nPB1="+pb1State+"\nPB1Seq="+pb1Seq+"\nPB1AgeTicks="+pb1Age+"\nAssemblers="+assemblers.Count+"\nActiveTier="+TierName(activeTier)+"\nQueueTargets="+qAsm.Count+"\nTierFull="+fullTier+"\nTierBasic="+basicTier+"\nTierSurvival="+survTier+"\nManagedCargo="+managed.Count+"\nBuildQueueSource="+bqsSource+"\nBuildQueueSourceTag="+bqsSourceTag+"\nBuildQueueLocalState="+bqsLocalState+"\nBuildQueueEnforcement="+bqsEnforce+"\nCoopGuardHeld="+bqsCoopGuard.Count+"\nEditHold="+Get("Console.EditHold","0")+"\nPlan="+plan+"\nLastQueue="+lastQueue+"\nLastRebalance="+lastRebal+"\nLedger="+LedgerSummary()+"\nLastOutputClear="+lastOut+"\nLastInputClear="+lastIn+"\nInstrLast="+instrLast+"\nInstrHigh="+instrHigh+"\nInstrHighAt="+instrAt+"\n"+PROD_STATUS_END+"\n"; Me.CustomData=ReplaceBlock(Me.CustomData,PROD_STATUS_BEGIN,PROD_STATUS_END,sb); }
string Extract(string text,string begin,string end){ if(text==null)return""; int a=text.IndexOf(begin,StringComparison.OrdinalIgnoreCase); if(a<0)return""; int b=text.IndexOf(end,a,StringComparison.OrdinalIgnoreCase); if(b<0)return""; return text.Substring(a+begin.Length,b-a-begin.Length); }
string ReplaceBlock(string text,string begin,string end,string block){ if(text==null)text=""; int a=text.IndexOf(begin,StringComparison.OrdinalIgnoreCase); if(a<0)return text.TrimEnd()+"\n\n"+block; int b=text.IndexOf(end,a,StringComparison.OrdinalIgnoreCase); if(b<0)return text.TrimEnd()+"\n\n"+block; b+=end.Length; return text.Substring(0,a).TrimEnd()+"\n\n"+block+text.Substring(b); }
