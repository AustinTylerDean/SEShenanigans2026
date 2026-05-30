const string SHIP_TAG="[MB1]";
const string PB2_TAG="[WSO-PB2]";
const string SIO_TAG="[SIO-PB1]";
const double CONTACT_MAX_RANGE=2500.0;
const double MOVE_LABEL_MIN_RANGE=500.0;
const double MOVE_LABEL_RATE=15.0;
const string TACTCFG_BEGIN="# WSO_TACTCFG_BEGIN";
const string TACTCFG_END="# WSO_TACTCFG_END";
const string TACTCACHE_BEGIN="# WSO_TACTCACHE_BEGIN";
const string TACTCACHE_END="# WSO_TACTCACHE_END";


List<IMyTerminalBlock> blocks=new List<IMyTerminalBlock>();
List<IMyShipController> ctrls=new List<IMyShipController>();
List<MyInventoryItem> invItems=new List<MyInventoryItem>();
List<IMyProgrammableBlock> pbs=new List<IMyProgrammableBlock>();
List<ITerminalProperty> props=new List<ITerminalProperty>();
List<ITerminalAction> acts=new List<ITerminalAction>();
List<Wpn> wpns=new List<Wpn>();
List<Con> cons=new List<Con>();
List<Trk> tracks=new List<Trk>();
List<Sio> sio=new List<Sio>();
IMyShipController anchor=null;
IMyProgrammableBlock sioPb=null;
bool sioOk=false;
Vector3D hMin,hMax;
bool hReady=false;
int hCount=0,tick=0;
string tactFire="",tactTarget="",tactTurrets="",tactAiAuth="OFF",tactProfile="AI INACTIVE",tactState="NO CFG";
bool tactCfgValid=false;
int tactCtrl=0,tactSkipStatic=0,tactFail=0;
string apiDump="";
bool apiDumpReq=false;

class Wpn{
 public string Key="",Type="WEAPON",Reason="";
 public Vector3D Pos,SysPos,Min,Max;
 public int Count=0,SysCount=0;
 public string SioStatus="",SioReason="";
 public double Ammo=0,BoxAmmo=0;
 public string AmmoSub="",AmmoLabel="";
 public Vector3D BoxPos;
 public int BoxCount=0;
 public bool AmmoSeen=false,Bounds=false;
}

class Con{
 public long Eid=0;
 public string Rel="UNKNOWN",ClassName="UNKNOWN",Arc="UNK",Source="",Move="";
 public double RangeKm=0,X=0,Y=0;
 public int SizeClass=0,ElevBand=0,Targeted=1;
}
class Trk{public long Eid;public double Rate;public int Seen;}
class Sio{public string Key="",Status="",Reason="";}

public Program(){Runtime.UpdateFrequency=UpdateFrequency.Update100;ReadTactCache();Scan();}
public void Save(){}
public void Main(string arg,UpdateType src){apiDumpReq=(arg!=null&&arg.Trim().Equals("API DUMP",StringComparison.OrdinalIgnoreCase));Scan();apiDumpReq=false;}

void Scan(){
 tick++; blocks.Clear(); wpns.Clear(); cons.Clear(); hReady=false; hCount=0; FindAnchor();
 if(anchor==null){WriteAnchorFault();Echo("WSO PB2 V7B | ANCHOR LOST");return;}
 ReadSio();
 ReadTactCfg();
 GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);
 int autoW=0;
 for(int i=0;i<blocks.Count;i++){
  IMyTerminalBlock b=blocks[i]; if(b==null||!HasTag(b.CustomName,SHIP_TAG))continue;
  Vector3D p=Local(b.GetPosition()); AddHull(p);
  string key=WpnTag(b.CustomName);
  if(key.Length==0&&IsWeaponLike(b)){autoW++;key="AUTO"+autoW.ToString("00");}
  if(key.Length>0)AddWpn(key,b,p);
  IMyTurretControlBlock ctc=b as IMyTurretControlBlock;
  if(ctc!=null)AddCtcContact(ctc);
 }
 AddMagBoxes();
 FinalizeWpns(); ApplyTacticalControls(); if(apiDumpReq)BuildApiDump(); WritePacket();
 Echo("WSO PB2 V11_AI_AUTH"); Echo("Hull "+hCount+" | WPN "+wpns.Count+" | CON "+cons.Count); Echo("SIO "+(sioOk?"OK":"LOST")+" "+sio.Count); Echo("Anchor "+(anchor!=null?anchor.CustomName:"NONE"));
}

void FindAnchor(){
 anchor=null; ctrls.Clear(); GridTerminalSystem.GetBlocksOfType<IMyShipController>(ctrls);
 for(int i=0;i<ctrls.Count;i++){var c=ctrls[i]; if(c!=null&&HasTag(c.CustomName,SHIP_TAG)&&HasTag(c.CustomName,"[FC]")){anchor=c;return;}}
 for(int i=0;i<ctrls.Count;i++){var c=ctrls[i]; if(c!=null&&HasTag(c.CustomName,SHIP_TAG)&&HasTag(c.CustomName,"[WSO]")){anchor=c;return;}}
 for(int i=0;i<ctrls.Count;i++){var c=ctrls[i]; if(c!=null&&HasTag(c.CustomName,SHIP_TAG)&&HasTag(c.CustomName,"[IMS]")){anchor=c;return;}}
}

MatrixD Am(){return anchor.WorldMatrix;}
Vector3D Local(Vector3D w){MatrixD m=Am();Vector3D d=w-m.Translation;return new Vector3D(Vector3D.Dot(d,m.Right),Vector3D.Dot(d,m.Up),Vector3D.Dot(d,m.Forward));}

void AddHull(Vector3D p){
 if(!hReady){hReady=true;hMin=p;hMax=p;}else{
  if(p.X<hMin.X)hMin.X=p.X;if(p.Y<hMin.Y)hMin.Y=p.Y;if(p.Z<hMin.Z)hMin.Z=p.Z;
  if(p.X>hMax.X)hMax.X=p.X;if(p.Y>hMax.Y)hMax.Y=p.Y;if(p.Z>hMax.Z)hMax.Z=p.Z;}
 hCount++;
}

void AddWpn(string key,IMyTerminalBlock b,Vector3D p){
 Wpn m=null; for(int i=0;i<wpns.Count;i++)if(wpns[i].Key==key){m=wpns[i];break;}
 if(m==null){m=new Wpn();m.Key=key;wpns.Add(m);} 
 m.SysPos=(m.SysPos*m.SysCount+p)/(m.SysCount+1);m.SysCount++;
 if(IsWpnLocation(b)){
  m.Pos=(m.Pos*m.Count+p)/(m.Count+1);
  if(!m.Bounds){m.Min=p;m.Max=p;m.Bounds=true;}else{
   if(p.X<m.Min.X)m.Min.X=p.X;if(p.Y<m.Min.Y)m.Min.Y=p.Y;if(p.Z<m.Min.Z)m.Min.Z=p.Z;
   if(p.X>m.Max.X)m.Max.X=p.X;if(p.Y>m.Max.Y)m.Max.Y=p.Y;if(p.Z>m.Max.Z)m.Max.Z=p.Z;}
  m.Count++;
 }
 if(b is IMyLargeTurretBase||b is IMyUserControllableGun){string sub=AmmoSubForWeapon(b);string t=AmmoLabelFromSub(sub);if(t.Length>0)m.Type=t;else{t=WeaponType(b);if(t.Length>0)m.Type=t;}if(sub.Length>0){if(m.AmmoSub.Length==0){m.AmmoSub=sub;m.AmmoLabel=t;}else if(m.AmmoSub!=sub){m.AmmoSub="MIXED";m.AmmoLabel="MIXED";}AddAmmo(m,b,sub,false);}else AddAmmo(m,b,"",false);} 
}

void AddCtcContact(IMyTurretControlBlock ctc){
 MyDetectedEntityInfo info=ctc.GetTargetedEntity();
 if(info.IsEmpty())return;
 Con c=null; for(int i=0;i<cons.Count;i++)if(cons[i].Eid==info.EntityId){c=cons[i];break;}
 if(c==null){c=new Con();c.Eid=info.EntityId;cons.Add(c);}
 Vector3D lp=Local(info.Position); double horiz=Math.Sqrt(lp.X*lp.X+lp.Z*lp.Z); double r=Math.Sqrt(horiz*horiz+lp.Y*lp.Y); if(r<1)r=1;
 double frac=Math.Min(1.0,r/CONTACT_MAX_RANGE); if(horiz<1){c.X=0;c.Y=0;}else{c.X=Clamp(lp.X/horiz*frac,-1,1);c.Y=Clamp(-lp.Z/horiz*frac,-1,1);} c.RangeKm=r/1000.0;
 double pitch=Math.Atan2(lp.Y,Math.Max(1,horiz))*180.0/Math.PI; c.ElevBand=pitch>60?2:(pitch>30?1:(pitch<-60?-2:(pitch<-30?-1:0)));
 c.Rel=RelText(info.Relationship.ToString()); c.ClassName=ClassText(info); c.SizeClass=SizeClass(info,c.ClassName); c.Arc=ArcText(lp); c.Source=ctc.CustomName; c.Targeted=1; c.Move=MoveText(info.EntityId,info.Position,info.Velocity,r);
}

void FinalizeWpns(){
 for(int i=0;i<wpns.Count;i++){
  Wpn m=wpns[i]; if(m.Count==0&&m.SysCount>0){m.Pos=m.SysPos;m.Count=m.SysCount;}
  Sio hs=FindSio(m.Key);
  if(hs!=null){m.SioStatus=hs.Status;m.SioReason=hs.Reason;}
  else{m.SioStatus="DEG";m.SioReason=sioOk?"SIO NO DATA":"SIO LINK LOST";}
  if(m.AmmoSeen&&m.Ammo<=0.01){
   if(m.SioStatus!="OFFLINE")m.SioStatus="DEG";
   m.SioReason="AMMO EMPTY";
  }
 }
 if(!hReady){hReady=true;hMin=new Vector3D(-20,-8,-35);hMax=new Vector3D(20,8,35);}else{
  if(Math.Abs(hMax.X-hMin.X)<2){hMin.X-=8;hMax.X+=8;} if(Math.Abs(hMax.Y-hMin.Y)<2){hMin.Y-=4;hMax.Y+=4;} if(Math.Abs(hMax.Z-hMin.Z)<2){hMin.Z-=12;hMax.Z+=12;}
 }
}

Sio FindSio(string key){for(int i=0;i<sio.Count;i++)if(sio[i].Key==key)return sio[i];return null;}

void ReadSio(){
 sio.Clear();sioPb=null;sioOk=false;
 pbs.Clear();GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(pbs);
 for(int i=0;i<pbs.Count;i++){var p=pbs[i];if(p!=null&&p!=Me&&HasTag(p.CustomName,SHIP_TAG)&&HasTag(p.CustomName,SIO_TAG)){sioPb=p;break;}}
 if(sioPb==null)return;
 string cd=sioPb.CustomData??"";string[] lines=Lines(cd);
 for(int i=0;i<lines.Length;i++){string line=lines[i].Trim();if(line.StartsWith("STATE|OK"))sioOk=true; if(!line.StartsWith("SYS|"))continue;
  string[] a=line.Split('|'); if(a.Length<6)continue; Sio x=new Sio();x.Key=a[1];x.Status=a[2];x.Reason=a[5];sio.Add(x);}
}

void AddMagBoxes(){
 for(int i=0;i<blocks.Count;i++){
  IMyTerminalBlock b=blocks[i]; if(b==null||!HasTag(b.CustomName,SHIP_TAG))continue;
  string key=WpnTag(b.CustomName); if(key.Length==0)continue;
  if(!(b is IMyCargoContainer))continue;
  Wpn m=null; for(int k=0;k<wpns.Count;k++)if(wpns[k].Key==key){m=wpns[k];break;}
  if(m==null||m.AmmoSub.Length==0||m.AmmoSub=="MIXED")continue;
  double before=m.BoxAmmo; AddAmmo(m,b,m.AmmoSub,true);
  // Any [WPN#] cargo box is considered a local magazine candidate even if empty.
  Vector3D p=Local(b.GetPosition()); m.BoxPos=(m.BoxPos*m.BoxCount+p)/(m.BoxCount+1); m.BoxCount++;
 }
}

void AddAmmo(Wpn m,IMyTerminalBlock b,string sub,bool box){
 if(b==null||!b.HasInventory)return; bool saw=false;
 for(int i=0;i<b.InventoryCount;i++){
  IMyInventory inv=b.GetInventory(i); if(inv==null)continue; saw=true; invItems.Clear(); inv.GetItems(invItems);
  for(int j=0;j<invItems.Count;j++){
   var it=invItems[j];string tid=it.Type.TypeId.ToString();string ss=it.Type.SubtypeId.ToString();
   bool ammo=tid.IndexOf("Ammo",StringComparison.OrdinalIgnoreCase)>=0||tid.IndexOf("Magazine",StringComparison.OrdinalIgnoreCase)>=0;
   if(!ammo)continue;
   if(sub.Length>0&&ss.IndexOf(sub,StringComparison.OrdinalIgnoreCase)<0)continue;
   double a=(double)it.Amount; if(box)m.BoxAmmo+=a;else m.Ammo+=a;
  }
 }
 if(saw&&!box)m.AmmoSeen=true;
}



string MoveText(long eid,Vector3D pos,Vector3D vel,double range){
 if(range<=MOVE_LABEL_MIN_RANGE||anchor==null)return"";
 Vector3D shipVel=anchor.GetShipVelocities().LinearVelocity;
 Vector3D d=pos-anchor.GetPosition(); double len=d.Length(); if(len<1)return"";
 Vector3D n=d/len; double rate=Vector3D.Dot(vel-shipVel,n);
 Trk t=null; for(int i=0;i<tracks.Count;i++)if(tracks[i].Eid==eid){t=tracks[i];break;}
 if(t==null){t=new Trk();t.Eid=eid;t.Rate=rate;tracks.Add(t);}else t.Rate=t.Rate*0.6+rate*0.4;
 t.Seen=tick;
 if(t.Rate<-MOVE_LABEL_RATE)return"CLOSING";
 if(t.Rate>MOVE_LABEL_RATE)return"MOVING AWAY";
 return"";
}

void PruneTracks(){for(int i=tracks.Count-1;i>=0;i--)if(tick-tracks[i].Seen>20)tracks.RemoveAt(i);}

void WriteAnchorFault(){StringBuilder s=new StringBuilder(512);s.AppendLine("[WSO-PB2 V8]");s.AppendLine("STATE|ANCHOR LOST|"+tick);s.AppendLine("ANCHOR|NONE");s.AppendLine("END");Me.CustomData=s.ToString();}

void WritePacket(){
 PruneTracks();
 StringBuilder s=new StringBuilder(4096); s.AppendLine("[WSO-PB2 V10]"); s.AppendLine("STATE|OK|"+tick);
 s.AppendLine("ANCHOR|"+(anchor!=null?anchor.CustomName:"NONE"));
 s.AppendLine("SIO|"+(sioOk?"OK":"LOST")+"|"+(sioPb!=null?Safe(sioPb.CustomName):"NONE")+"|"+sio.Count);
 s.AppendLine("HULL|"+F(hMin.X)+"|"+F(hMin.Y)+"|"+F(hMin.Z)+"|"+F(hMax.X)+"|"+F(hMax.Y)+"|"+F(hMax.Z)+"|"+hCount);
 for(int i=0;i<wpns.Count;i++){
  Wpn m=wpns[i]; string st=m.SioStatus.Length>0?m.SioStatus:"DEG"; int off=st=="OFFLINE"?1:0,deg=st=="DEG"?1:0;
  s.AppendLine("WPN|"+m.Key+"|"+st+"|"+Safe(m.Type)+"|"+(m.AmmoSeen?"1":"0")+"|"+F(m.Ammo)+"|"+F(m.Pos.X)+"|"+F(m.Pos.Y)+"|"+F(m.Pos.Z)+"|"+off+"|"+deg+"|"+Safe(m.SioReason));
  // MAGSTAT is magazine-only. Weapons without a tagged cargo magazine remain on WEAPON STATUS,
  // but they do not appear on AMMO BOX QTY and do not create WSO->IMS stocking requests.
  if(m.BoxCount>0&&m.AmmoSub.Length>0&&m.AmmoSub!="MIXED"){
   Vector3D mp=m.BoxPos;
   s.AppendLine("MAGSTAT|"+m.Key+"|"+Safe(m.AmmoSub)+"|"+Safe(m.AmmoLabel.Length>0?m.AmmoLabel:m.Type)+"|BOX|"+((int)Math.Round(m.BoxAmmo)).ToString()+"|GUN|"+((int)Math.Round(m.Ammo)).ToString()+"|X|"+F(mp.X)+"|Y|"+F(mp.Y)+"|Z|"+F(mp.Z)+"|STATE|");
  }
 }
 int hc=0,nc=0;
 for(int i=0;i<cons.Count;i++){
  Con c=cons[i]; bool host=c.Rel=="HOSTILE"; string id=(host?"H":"N")+(host?++hc:++nc).ToString("00");
  s.AppendLine("CON|"+id+"|"+c.Rel+"|"+c.ClassName+"|"+c.Targeted+"|"+c.RangeKm.ToString("0.00")+"|"+c.X.ToString("0.000")+"|"+c.Y.ToString("0.000")+"|"+c.SizeClass+"|"+c.ElevBand+"|"+c.Arc+"|"+c.Eid+"|"+Safe(c.Source)+"|"+Safe(c.Move));
 }
 s.AppendLine("TACTSTAT|"+Safe(tactState)+"|FIRE|"+Safe(tactFire)+"|TARGET|"+Safe(tactTarget)+"|TURRETS|"+Safe(tactTurrets)+"|AIAUTH|"+Safe(tactAiAuth)+"|PROFILE|"+Safe(tactProfile)+"|CTRL|"+tactCtrl+"|SKIPSTATIC|"+tactSkipStatic+"|FAIL|"+tactFail);
 if(tactCfgValid){s.AppendLine(TACTCACHE_BEGIN);s.AppendLine("TACTCACHE|FIRE|"+Safe(tactFire)+"|TARGET|"+Safe(tactTarget)+"|TURRETS|"+Safe(tactTurrets)+"|AIAUTH|"+Safe(tactAiAuth)+"|PROFILE|"+Safe(tactProfile));s.AppendLine(TACTCACHE_END);}
 if(apiDump.Length>0)s.Append(apiDump);
 s.AppendLine("END"); Me.CustomData=s.ToString();
}

void ReadTactCache(){
 string block=ExtractBlock(Me.CustomData,TACTCACHE_BEGIN,TACTCACHE_END); if(block.Length==0)return;
 string[] lines=Lines(block); for(int i=0;i<lines.Length;i++){string line=lines[i].Trim(); if(!line.StartsWith("TACTCACHE|",StringComparison.OrdinalIgnoreCase))continue; string[] a=line.Split('|');
  string v=Field(a,"FIRE"); if(v.Length>0)tactFire=v; v=Field(a,"TARGET"); if(v.Length>0)tactTarget=v; v=Field(a,"TURRETS"); if(v.Length>0)tactTurrets=v; v=Field(a,"AIAUTH"); if(v.Length>0)tactAiAuth=v; v=Field(a,"PROFILE"); if(v.Length>0)tactProfile=v;
  tactCfgValid=tactFire.Length>0&&tactTarget.Length>0&&tactTurrets.Length>0; if(tactCfgValid)tactState="CACHED"; return;}
}

void ReadTactCfg(){
 bool had=tactCfgValid; tactState=had?"CACHED":"NO CFG";
 pbs.Clear(); GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(pbs); IMyProgrammableBlock src=null;
 for(int i=0;i<pbs.Count;i++){var p=pbs[i]; if(p==null||p==Me)continue; string n=p.CustomName; if(!HasTag(n,SHIP_TAG))continue; if(HasTag(n,"[WSO-PB1]")||HasTag(n,"[WSO]")){src=p;break;}}
 if(src==null)return; string block=ExtractBlock(src.CustomData,TACTCFG_BEGIN,TACTCFG_END); if(block.Length==0)return;
 string[] lines=Lines(block); for(int i=0;i<lines.Length;i++){string line=lines[i].Trim(); if(!line.StartsWith("TACTCFG|",StringComparison.OrdinalIgnoreCase))continue; string[] a=line.Split('|');
  string v=Field(a,"FIRE"); if(v.Length>0)tactFire=v; v=Field(a,"TARGET"); if(v.Length>0)tactTarget=v; v=Field(a,"TURRETS"); if(v.Length>0)tactTurrets=v; v=Field(a,"AIAUTH"); if(v.Length>0)tactAiAuth=v; v=Field(a,"PROFILE"); if(v.Length>0)tactProfile=v;
  tactCfgValid=tactFire.Length>0&&tactTarget.Length>0&&tactTurrets.Length>0; tactState=tactCfgValid?(line.IndexOf("|PERSIST|1",StringComparison.OrdinalIgnoreCase)>=0?"PB1 PERSIST":"PB1 CFG"):"NO CFG"; return;}
}

void ApplyTacticalControls(){
 tactCtrl=0;tactSkipStatic=0;tactFail=0;
 if(!tactCfgValid){tactState="NO CFG HOLD";return;}
 for(int i=0;i<blocks.Count;i++){
  IMyTerminalBlock b=blocks[i]; if(b==null)continue; if(!IsTacticalControl(b))continue;
  if(IsStationLike(b)){tactSkipStatic++;continue;}
  tactCtrl++;
  if(!ApplyTacticalBlock(b))tactFail++;
 }
 if(tactState=="DISCOVERY")tactState=tactFail>0?"APPLY FAULT":"APPLIED";
}

bool IsStationLike(IMyTerminalBlock b){
 if(b==null)return true;
 if(b.CubeGrid!=null&&b.CubeGrid.IsStatic)return true;
 string n=b.CustomName; string g=b.CubeGrid!=null?b.CubeGrid.CustomName:"";
 if(n.IndexOf("[LB1]",StringComparison.OrdinalIgnoreCase)>=0)return true;
 if(g.IndexOf("[LB1]",StringComparison.OrdinalIgnoreCase)>=0)return true;
 if(g.IndexOf("STATION",StringComparison.OrdinalIgnoreCase)>=0)return true;
 return false;
}

bool ApplyTacticalBlock(IMyTerminalBlock b){
 bool ok=true;
 bool ai=IsAiCombatBlock(b);
 bool enemies=tactFire.Equals("ENEMIES",StringComparison.OrdinalIgnoreCase)||tactFire.Equals("BOTH",StringComparison.OrdinalIgnoreCase);
 bool neutrals=tactFire.Equals("NEUTRALS",StringComparison.OrdinalIgnoreCase)||tactFire.Equals("BOTH",StringComparison.OrdinalIgnoreCase);
 ok&=SetBoolIfPresent(b,"TargetEnemies",enemies);
 ok&=SetBoolIfPresent(b,"TargetNeutrals",neutrals);
 ok&=SetBoolIfPresent(b,"TargetFriends",false);
 ok&=SetBoolIfPresent(b,"TargetMeteors",true);
 ok&=SetBoolIfPresent(b,"TargetMissiles",true);
 if(ai){
  b.ApplyAction("OnOff_On");
  ok&=SetAiBehavior(b,tactAiAuth.Equals("ON",StringComparison.OrdinalIgnoreCase));
  SetClosestPriority(b);
 }else{
  if(tactTurrets.Equals("OFF",StringComparison.OrdinalIgnoreCase))b.ApplyAction("OnOff_Off"); else b.ApplyAction("OnOff_On");
 }
 if(tactTarget.Equals("WEAPONS",StringComparison.OrdinalIgnoreCase))b.ApplyAction("TargetingGroup_Weapons");
 else if(tactTarget.Equals("POWER",StringComparison.OrdinalIgnoreCase))b.ApplyAction("TargetingGroup_PowerSystems");
 else if(tactTarget.Equals("THRUSTERS",StringComparison.OrdinalIgnoreCase))b.ApplyAction("TargetingGroup_Propulsion");
 else if(tactTarget.Equals("DEFAULT",StringComparison.OrdinalIgnoreCase))ok&=SetSelectorDefault(b);
 return ok;
}

bool HasProp(IMyTerminalBlock b,string id){props.Clear();b.GetProperties(props);for(int i=0;i<props.Count;i++)if(props[i].Id.Equals(id,StringComparison.OrdinalIgnoreCase))return true;return false;}
bool SetBoolIfPresent(IMyTerminalBlock b,string id,bool v){if(!HasProp(b,id))return true;try{b.SetValueBool(id,v);return true;}catch{return false;}}
bool SetBoolAny(IMyTerminalBlock b,string[] ids,bool v){for(int i=0;i<ids.Length;i++)if(HasProp(b,ids[i])){try{b.SetValueBool(ids[i],v);return true;}catch{return false;}}return false;}

bool SetAiBehavior(IMyTerminalBlock b,bool on){
 string[] ids=new string[]{"AIEnabled","AIBehavior","AIBehaviour","EnableAI","EnabledAI","AI Behavior","AI Behaviour"};
 if(SetBoolAny(b,ids,on))return true;
 string act=FindAction(b,on?"ON":"OFF",new string[]{"AI","Behavior"}); if(act.Length==0)act=FindAction(b,on?"ON":"OFF",new string[]{"AI","Behaviour"});
 if(act.Length>0){b.ApplyAction(act);return true;}
 return false;
}

void SetClosestPriority(IMyTerminalBlock b){string act=FindAction(b,"",new string[]{"Priority","Closest"});if(act.Length>0)b.ApplyAction(act);}

string FindAction(IMyTerminalBlock b,string suffix,string[] must){acts.Clear();b.GetActions(acts);for(int i=0;i<acts.Count;i++){string id=acts[i].Id;if(suffix.Length>0&&!id.EndsWith("_"+suffix,StringComparison.OrdinalIgnoreCase)&&id.IndexOf(suffix,StringComparison.OrdinalIgnoreCase)<0)continue;bool ok=true;for(int k=0;k<must.Length;k++)if(id.IndexOf(must[k],StringComparison.OrdinalIgnoreCase)<0){ok=false;break;}if(ok)return id;}return"";}

bool SetSelectorDefault(IMyTerminalBlock b){if(!HasProp(b,"TargetingGroup_Selector"))return true;try{b.SetValue<long>("TargetingGroup_Selector",0L);return true;}catch{return false;}}

bool IsTacticalControl(IMyTerminalBlock b){
 if(b is IMyLargeTurretBase)return true;
 if(b is IMyTurretControlBlock)return true;
 string d=b.BlockDefinition.ToString(); if(d.IndexOf("Searchlight",StringComparison.OrdinalIgnoreCase)>=0)return true;
 if(IsAiCombatBlock(b))return true;
 return false;
}

bool IsAiCombatBlock(IMyTerminalBlock b){if(b==null)return false;string d=b.BlockDefinition.ToString();string n=b.CustomName;return d.IndexOf("Offensive",StringComparison.OrdinalIgnoreCase)>=0||d.IndexOf("Defensive",StringComparison.OrdinalIgnoreCase)>=0||d.IndexOf("Combat",StringComparison.OrdinalIgnoreCase)>=0||n.IndexOf("AI Offensive",StringComparison.OrdinalIgnoreCase)>=0||n.IndexOf("AI Defensive",StringComparison.OrdinalIgnoreCase)>=0;}

void BuildApiDump(){
 StringBuilder s=new StringBuilder(4096); s.AppendLine("# WSO_API_DISCOVERY_BEGIN"); s.AppendLine("# Stage 3 active control. AI combat blocks follow profile AIAUTH; no valid config means no weapon writes.");
 int shown=0;
 for(int pass=0;pass<2;pass++)for(int i=0;i<blocks.Count&&shown<10;i++){
  IMyTerminalBlock b=blocks[i]; if(b==null||!IsTacticalControl(b))continue; if(IsStationLike(b))continue; bool ai=IsAiCombatBlock(b); if((pass==0&&!ai)||(pass==1&&ai))continue; shown++;
  s.AppendLine("BLOCK|"+Safe(b.CustomName)+"|AI|"+(ai?"1":"0")+"|GRID|"+Safe(b.CubeGrid!=null?b.CubeGrid.CustomName:"")+"|STATIC|"+(b.CubeGrid!=null&&b.CubeGrid.IsStatic?"1":"0")+"|DEF|"+Safe(b.BlockDefinition.ToString()));
  props.Clear(); b.GetProperties(props); s.Append("PROPS|"); for(int k=0;k<props.Count;k++){if(k>0)s.Append(',');s.Append(Safe(props[k].Id));} s.AppendLine();
  acts.Clear(); b.GetActions(acts); s.Append("ACTIONS|"); for(int k=0;k<acts.Count;k++){if(k>0)s.Append(',');s.Append(Safe(acts[k].Id));} s.AppendLine();
 }
 s.AppendLine("# WSO_API_DISCOVERY_END"); apiDump=s.ToString(); tactState="API DUMP";
}

string[] Lines(string s){if(s==null)s="";s=s.Replace("\r","");return s.Split(new string[]{((char)10).ToString()},StringSplitOptions.None);}
string ExtractBlock(string text,string begin,string end){if(text==null)return"";int a=text.IndexOf(begin,StringComparison.OrdinalIgnoreCase);if(a<0)return"";a+=begin.Length;int b=text.IndexOf(end,a,StringComparison.OrdinalIgnoreCase);if(b<0)return"";return text.Substring(a,b-a).Trim();}
string Field(string[] p,string k){for(int i=0;i<p.Length-1;i++)if(p[i].Equals(k,StringComparison.OrdinalIgnoreCase))return p[i+1];return"";}

string RelText(string r){if(r==null)return"UNKNOWN";return r.IndexOf("Enem",StringComparison.OrdinalIgnoreCase)>=0?"HOSTILE":"NEUTRAL";}
string ClassText(MyDetectedEntityInfo info){string t=info.Type.ToString(); if(t.IndexOf("Large",StringComparison.OrdinalIgnoreCase)>=0)return"LARGE GRID"; if(t.IndexOf("Small",StringComparison.OrdinalIgnoreCase)>=0)return"SMALL GRID"; if(t.IndexOf("Station",StringComparison.OrdinalIgnoreCase)>=0)return"BASE/MASS"; return t.ToUpperInvariant();}
int SizeClass(MyDetectedEntityInfo info,string cls){if(cls=="SMALL GRID")return 0; double sx=Math.Abs(info.BoundingBox.Max.X-info.BoundingBox.Min.X),sy=Math.Abs(info.BoundingBox.Max.Y-info.BoundingBox.Min.Y),sz=Math.Abs(info.BoundingBox.Max.Z-info.BoundingBox.Min.Z); double m=Math.Max(sx,Math.Max(sy,sz)); if(cls=="LARGE GRID"&&m<60)return 1; if(m>60||cls=="BASE/MASS")return 2; return m>12?1:0;}
string ArcText(Vector3D p){double ax=Math.Abs(p.X),az=Math.Abs(p.Z); string fb=p.Z>=0?"FWD":"AFT"; string lr=p.X>=0?"STBD":"PORT"; if(az>ax*1.7)return fb; if(ax>az*1.7)return lr; return fb+"-"+lr;}
double Clamp(double v,double a,double b){return v<a?a:(v>b?b:v);} 

bool HasTag(string n,string t){return n!=null&&t!=null&&n.IndexOf(t,StringComparison.OrdinalIgnoreCase)>=0;}
string WpnTag(string n){if(n==null)return"";int p=n.IndexOf("[WPN",StringComparison.OrdinalIgnoreCase);while(p>=0){int i=p+4,v=0;bool any=false;while(i<n.Length&&n[i]>='0'&&n[i]<='9'){any=true;v=v*10+(n[i]-'0');i++;}if(any&&i<n.Length&&n[i]==']')return"WPN"+v;p=n.IndexOf("[WPN",p+1,StringComparison.OrdinalIgnoreCase);}return"";}
bool IsWeaponLike(IMyTerminalBlock b){return b is IMyLargeTurretBase||b is IMyUserControllableGun||b is IMyTurretControlBlock;}
bool IsWpnLocation(IMyTerminalBlock b){if(b==null)return false;if(b is IMyTurretControlBlock||b is IMyProgrammableBlock||b is IMyTimerBlock||b is IMyTextPanel||b is IMyShipController||b is IMyRemoteControl||b is IMyLightingBlock)return false;if(b is IMyMotorStator||b is IMyLargeTurretBase||b is IMyUserControllableGun||b is IMyCameraBlock)return true;return b.BlockDefinition.ToString().IndexOf("Searchlight",StringComparison.OrdinalIgnoreCase)>=0;}
string AmmoSubForWeapon(IMyTerminalBlock b){
 if(b==null)return""; string d=b.BlockDefinition.ToString();
 if(d.IndexOf("Interior",StringComparison.OrdinalIgnoreCase)>=0)return"RapidFireAutomaticRifleGun_Mag_50rd";
 if(d.IndexOf("Gatling",StringComparison.OrdinalIgnoreCase)>=0)return"NATO_25x184mm";
 if(d.IndexOf("Autocannon",StringComparison.OrdinalIgnoreCase)>=0)return"AutocannonClip";
 if(d.IndexOf("Assault",StringComparison.OrdinalIgnoreCase)>=0||d.IndexOf("MediumCalibre",StringComparison.OrdinalIgnoreCase)>=0)return"MediumCalibreAmmo";
 if(d.IndexOf("Artillery",StringComparison.OrdinalIgnoreCase)>=0||d.IndexOf("LargeCalibre",StringComparison.OrdinalIgnoreCase)>=0)return"LargeCalibreAmmo";
 if(d.IndexOf("Missile",StringComparison.OrdinalIgnoreCase)>=0||d.IndexOf("Rocket",StringComparison.OrdinalIgnoreCase)>=0)return"Missile200mm";
 if(d.IndexOf("Railgun",StringComparison.OrdinalIgnoreCase)>=0)return"SmallRailgunAmmo";
 return"";
}
string AmmoLabelFromSub(string s){
 if(s==null)return"";
 if(s.IndexOf("RapidFire",StringComparison.OrdinalIgnoreCase)>=0)return"RIFLE MAG";
 if(s.IndexOf("NATO_25x184",StringComparison.OrdinalIgnoreCase)>=0)return"GATLING";
 if(s.IndexOf("Autocannon",StringComparison.OrdinalIgnoreCase)>=0)return"AUTOCANNON";
 if(s.IndexOf("MediumCalibre",StringComparison.OrdinalIgnoreCase)>=0)return"ASSAULT";
 if(s.IndexOf("LargeCalibre",StringComparison.OrdinalIgnoreCase)>=0)return"ARTILLERY";
 if(s.IndexOf("Missile",StringComparison.OrdinalIgnoreCase)>=0)return"MISSILE";
 if(s.IndexOf("Railgun",StringComparison.OrdinalIgnoreCase)>=0)return"RAILGUN";
 return"";
}
string WeaponType(IMyTerminalBlock b){string sub=AmmoSubForWeapon(b);string lab=AmmoLabelFromSub(sub);if(lab.Length>0)return lab;string n=b.CustomName??"";while(true){int a=n.IndexOf('[');int z=a>=0?n.IndexOf(']',a):-1;if(a<0||z<0)break;n=n.Remove(a,z-a+1).Trim();}if(n.Length>0)return n.ToUpperInvariant();string d=b.BlockDefinition.ToString();int x=d.LastIndexOf('/');if(x>=0&&x<d.Length-1)d=d.Substring(x+1);return d.Replace("Large","").Replace("Small","").Replace("Block","").ToUpperInvariant();}
string F(double v){return v.ToString("0.0");}
string Safe(string s){if(s==null)return"";return s.Replace("|","/").Replace("\n"," ").Replace("\r","");}
