const string SHIP_TAG="[MB1]";
const string SIO_TAG="[SIO-PB1]";
const int RESCAN_EVERY=5;

string[] PAGES={"OVERVIEW","REACTORS","JUMP","BATTERIES","ENGINES","GYROS","TANKS","ENVIRONMENT","ASSEMBLERS","REFINERS","WEAPONS","CONVEYOR"};
List<IMyTerminalBlock> blocks=new List<IMyTerminalBlock>();
List<IMyShipController> ctrls=new List<IMyShipController>();
List<IMyTextPanel> panels=new List<IMyTextPanel>();
List<Item> items=new List<Item>();
List<Sys> wpnSys=new List<Sys>();
List<Cat> cats=new List<Cat>();
List<Ext> exts=new List<Ext>();
IMyShipController anchor=null,console=null;
int tick=0,seq=0,lastScan=-99,focus=0,consolePage=0,helpPage=0,sel=0,total=0,bad=0;
bool anchorOk=false;
Vector3D hMin,hMax; bool hReady=false;
StringBuilder sb=new StringBuilder(12000);

class Item{public string Name="",Type="",Cat="",Key="",Status="READY",Reason="",Problems="",Detail="";public Vector3D P;public bool Bad=false,Off=false,Alert=false,HasPos=true;}
class Sys{public string Key="",Status="READY",Reason="",Problems="";public int Blocks=0,Bad=0,Off=0,Deg=0,Welders=0,PosN=0;public Vector3D Pos;}
class Cat{public string Name="",Status="READY",Reason="";public int Total=0,Bad=0,Off=0,Deg=0;}
class Ext{public int Num=0,Page=0,Sel=0;public IMyTextPanel Panel=null;public string Name="";}

public Program(){Runtime.UpdateFrequency=UpdateFrequency.Update100;Scan();}
public void Save(){}
public void Main(string arg,UpdateType src){tick++;string a=(arg??"").Trim().ToUpperInvariant();
 if(a=="SCAN"||tick-lastScan>=RESCAN_EVERY)Scan();
 if(a=="FOCUS")NextFocus();
 else if(a=="PAGE NEXT"||a=="PAGENEXT")Page(1);
 else if(a=="PAGE PREV"||a=="PAGEPREV")Page(-1);
 else if(a=="NEXT")Select(1);
 else if(a=="PREV")Select(-1);
 else if(a=="INC"||a=="DEC"||a=="STEP"||a=="SAFE"){} // shared bridge hotbar words reserved/inert here
 DrawAll();EchoStatus();}

void Scan(){lastScan=tick;seq++;FindAnchorAndConsole();ScanDisplays();ScanBlocks();WritePacket();}

void FindAnchorAndConsole(){anchor=null;console=null;anchorOk=false;ctrls.Clear();GridTerminalSystem.GetBlocksOfType<IMyShipController>(ctrls);
 for(int i=0;i<ctrls.Count;i++){var c=ctrls[i];if(c!=null&&HasTag(c.CustomName,SHIP_TAG)&&HasTag(c.CustomName,"[SIO]"))console=c;}
 for(int i=0;i<ctrls.Count;i++){var c=ctrls[i];if(c!=null&&HasTag(c.CustomName,SHIP_TAG)&&HasTag(c.CustomName,"[FC]")){anchor=c;anchorOk=true;return;}}
 for(int i=0;i<ctrls.Count;i++){var c=ctrls[i];if(c!=null&&HasTag(c.CustomName,SHIP_TAG)&&HasTag(c.CustomName,"[WSO]")){anchor=c;anchorOk=true;return;}}
 for(int i=0;i<ctrls.Count;i++){var c=ctrls[i];if(c!=null&&HasTag(c.CustomName,SHIP_TAG)&&HasTag(c.CustomName,"[IMS]")){anchor=c;anchorOk=true;return;}}
}

void ScanDisplays(){int[] on=new int[exts.Count],op=new int[exts.Count],os=new int[exts.Count];for(int i=0;i<exts.Count;i++){on[i]=exts[i].Num;op[i]=exts[i].Page;os[i]=exts[i].Sel;}exts.Clear();panels.Clear();GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(panels);
 for(int i=0;i<panels.Count;i++){var p=panels[i];if(p==null||!HasTag(p.CustomName,SHIP_TAG))continue;int n=NumberTag(p.CustomName,"[SIO");if(n<=0)continue;Ext e=new Ext();e.Num=n;e.Panel=p;e.Name="SIO"+n;e.Page=DefaultPage(n);e.Sel=0;for(int j=0;j<on.Length;j++)if(on[j]==n){e.Page=ClampI(op[j],0,PAGES.Length-1);e.Sel=os[j];break;}exts.Add(e);}exts.Sort((a,b)=>a.Num.CompareTo(b.Num));if(focus>exts.Count+1)focus=0;}
int DefaultPage(int n){return (n-1)%PAGES.Length;}

void ScanBlocks(){items.Clear();wpnSys.Clear();cats.Clear();blocks.Clear();hReady=false;total=0;bad=0;GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);
 for(int i=0;i<blocks.Count;i++){var b=blocks[i];if(!IncludeBlock(b))continue;total++;Vector3D p=anchorOk?Local(b.GetPosition()):new Vector3D();AddHull(p);string wk=WpnTag(b.CustomName);if(wk.Length>0)AddWpnSys(wk,b,p);string cat=Category(b);if(cat.Length>0){if(!(cat=="WEAPONS"&&wk.Length>0))AddItem(b,cat,p);};if(IsAlertBlock(b))bad++;}
 FinalizeWpn();FinalizeCats();if(!hReady){hReady=true;hMin=new Vector3D(-20,-8,-35);hMax=new Vector3D(20,8,35);}NormalizeSelections();}

void AddItem(IMyTerminalBlock b,string cat,Vector3D p){Item it=new Item();it.Name=CleanName(b.CustomName);it.Type=PartName(b);it.Cat=cat;it.Key=WpnTag(b.CustomName);it.P=p;ClassifyItem(b,it);it.Detail=BuildDetail(b,it);items.Add(it);AddCatItem(it);} 
void ClassifyItem(IMyTerminalBlock b,Item it){it.Bad=IsBad(b);it.Off=IsOffline(b);it.Alert=IsAlertBlock(b);it.Reason=ReasonFor(b);it.Status=it.Off?"OFFLINE":(it.Bad?"DEG":"READY");if(!it.Alert&&it.Off)it.Status="DOWN";IMyAirVent v=b as IMyAirVent;if(v!=null)ClassifyVent(v,it);} 
void ClassifyVent(IMyAirVent v,Item it){it.Type="VENT";float o=v.GetOxygenLevel();bool air=v.CanPressurize;IMyFunctionalBlock f=v as IMyFunctionalBlock;if(!v.IsFunctional){it.Status="OFFLINE";it.Bad=true;it.Off=true;it.Alert=true;it.Reason="VENT DAMAGED";return;}if(f!=null&&!f.Enabled){it.Status="DEPRES";it.Bad=false;it.Off=true;it.Alert=false;it.Reason="VENT POWERED DOWN";return;}if(!v.IsWorking){it.Status="DEPRES";it.Bad=false;it.Off=true;it.Alert=false;it.Reason="VENT NOT WORKING";return;}if(v.Depressurize){it.Status="DEPRES";it.Bad=false;it.Off=false;it.Alert=false;it.Reason="DEPRESSURIZED";return;}if(!air){it.Status="DEG";it.Bad=true;it.Off=false;it.Alert=true;it.Reason="NOT AIRTIGHT";return;}if(o<0.95f){it.Status="DEG";it.Bad=true;it.Off=false;it.Alert=true;it.Reason="LOW PRESSURE "+((int)(o*100)).ToString()+"%";return;}it.Status="READY";it.Bad=false;it.Off=false;it.Alert=false;it.Reason="PRESSURIZED";} 
void AddCatItem(Item it){Cat c=FindCat(it.Cat);c.Total++;if(it.Alert||it.Status=="DEG"||it.Status=="OFFLINE"){c.Bad++;if(it.Status=="OFFLINE")c.Off++;else c.Deg++;if(c.Reason.Length==0)c.Reason=it.Reason;}}
Cat FindCat(string n){for(int i=0;i<cats.Count;i++)if(cats[i].Name==n)return cats[i];Cat c=new Cat();c.Name=n;cats.Add(c);return c;}
void FinalizeCats(){for(int i=0;i<cats.Count;i++){var c=cats[i];c.Status=c.Off>0?"OFFLINE":(c.Deg>0?"DEG":"READY");}}

void AddWpnSys(string key,IMyTerminalBlock b,Vector3D p){Sys s=FindSys(key);s.Blocks++;if(b is IMyShipWelder)s.Welders++;if(WpnPosSource(b)){s.Pos+=p;s.PosN++;}if(IsBad(b)){string st=IsOffline(b)?"OFFLINE":"DEG";s.Bad++;if(st=="OFFLINE")s.Off++;else s.Deg++;string r=ReasonFor(b);if(s.Reason.Length==0)s.Reason=r;AddProblem(s,st,PartName(b),r);}}
void AddProblem(Sys s,string st,string part,string reason){if(s.Problems.Length<220)s.Problems+=st+":"+part+":"+reason+";";}
Sys FindSys(string key){for(int i=0;i<wpnSys.Count;i++)if(wpnSys[i].Key==key)return wpnSys[i];Sys s=new Sys();s.Key=key;wpnSys.Add(s);return s;}
void FinalizeWpn(){for(int i=0;i<wpnSys.Count;i++){Sys s=wpnSys[i];s.Status=s.Off>0?"OFFLINE":(s.Deg>0?"DEG":"READY");Item it=new Item();it.Cat="WEAPONS";it.Key=s.Key;it.Name=s.Key;it.Type="MODULE";it.Status=s.Status;it.Reason=s.Reason;it.Problems=s.Problems;it.Detail="PARTS "+s.Blocks+"|BAD "+s.Bad+"|WELDERS "+s.Welders;it.Bad=s.Bad>0;it.Off=s.Off>0;it.Alert=s.Bad>0;it.P=s.PosN>0?s.Pos*(1.0/s.PosN):new Vector3D();it.HasPos=s.PosN>0;items.Add(it);AddCatItem(it);}}
bool WpnPosSource(IMyTerminalBlock b){if(b is IMyTurretControlBlock)return false;return b is IMyLargeTurretBase||b is IMyUserControllableGun||b is IMyMotorStator||b is IMyCameraBlock||b is IMyShipWelder;}

void NormalizeSelections(){int n=FilteredCount(CurrentPage());if(sel>=n)sel=Math.Max(0,n-1);for(int i=0;i<exts.Count;i++){int c=FilteredCount(exts[i].Page);if(exts[i].Sel>=c)exts[i].Sel=Math.Max(0,c-1);}}

MatrixD Am(){return anchor.WorldMatrix;}
Vector3D Local(Vector3D w){MatrixD m=Am();Vector3D d=w-m.Translation;return new Vector3D(Vector3D.Dot(d,m.Right),Vector3D.Dot(d,m.Up),Vector3D.Dot(d,m.Forward));}
void AddHull(Vector3D p){if(!anchorOk)return;if(!hReady){hReady=true;hMin=p;hMax=p;}else{if(p.X<hMin.X)hMin.X=p.X;if(p.Y<hMin.Y)hMin.Y=p.Y;if(p.Z<hMin.Z)hMin.Z=p.Z;if(p.X>hMax.X)hMax.X=p.X;if(p.Y>hMax.Y)hMax.Y=p.Y;if(p.Z>hMax.Z)hMax.Z=p.Z;}}

void NextFocus(){focus++;if(focus>exts.Count+1)focus=0;}
int CurrentPage(){if(focus==0)return consolePage;if(focus>=1&&focus<=exts.Count)return exts[focus-1].Page;return helpPage;}
void Page(int d){if(focus==0)consolePage=Wrap(consolePage+d,PAGES.Length);else if(focus>=1&&focus<=exts.Count){Ext e=exts[focus-1];e.Page=Wrap(e.Page+d,PAGES.Length);e.Sel=0;}else helpPage=Wrap(helpPage+d,2);}
void Select(int d){if(focus==0){int n=FilteredCount(consolePage);if(n>0)sel=Wrap(sel+d,n);}else if(focus>=1&&focus<=exts.Count){Ext e=exts[focus-1];int n=FilteredCount(e.Page);if(n>0)e.Sel=Wrap(e.Sel+d,n);}}
int Wrap(int v,int n){if(n<=0)return 0;while(v<0)v+=n;while(v>=n)v-=n;return v;}

void DrawAll(){if(console!=null){var sp=console as IMyTextSurfaceProvider;if(sp!=null){DrawMain(sp.GetSurface(0),consolePage,sel,focus==0,true);DrawRail(sp.GetSurface(1));if(sp.SurfaceCount>2)Blank(sp.GetSurface(2));if(sp.SurfaceCount>3)Blank(sp.GetSurface(3));if(sp.SurfaceCount>4)DrawHelp(sp.GetSurface(4));}}
 for(int i=0;i<exts.Count;i++){bool foc=(focus==i+1);DrawMain(exts[i].Panel,exts[i].Page,exts[i].Sel,foc,false);}}

void DrawMain(IMyTextSurface s,int page,int selected,bool focused,bool consoleMain){if(s==null)return;Prep(s);Vector2 sz=s.TextureSize;float sc=Math.Min(sz.X,sz.Y)/512f;var f=s.DrawFrame();DrawBg(ref f,sz,sc);DrawFocus(ref f,sz,sc,focused);if(!anchorOk){DrawFault(ref f,sz,sc);f.Dispose();return;}DrawPage(ref f,sz,sc,page,selected);if(consoleMain&&!focused)DrawConsoleFocusCard(ref f,sz,sc);f.Dispose();}
void DrawPage(ref MySpriteDrawFrame f,Vector2 sz,float sc,int page,int selected){
 if(sz.X>sz.Y*1.25f){DrawWidePage(ref f,sz,sc,page,selected);return;}
 Vector2 c=sz/2f;Panel(ref f,new Vector2(c.X,58*sc),new Vector2(sz.X*0.56f,48*sc),new Color(4,18,26,205));Txt(ref f,PAGES[page],new Vector2(c.X,45*sc),1.12f*sc,Cyan(),TextAlignment.CENTER,"Monospace");
 if(PAGES[page]=="CONVEYOR")DrawConveyor(ref f,sz,sc,page,selected);else DrawMapPage(ref f,sz,sc,page,selected);}

void DrawWidePage(ref MySpriteDrawFrame f,Vector2 sz,float sc,int page,int selected){
 Vector2 c=sz/2f;Panel(ref f,new Vector2(c.X,44*sc),new Vector2(sz.X*0.44f,42*sc),new Color(4,18,26,205));Txt(ref f,PAGES[page],new Vector2(c.X,32*sc),1.05f*sc,Cyan(),TextAlignment.CENTER,"Monospace");
 if(PAGES[page]=="CONVEYOR")DrawWideConveyor(ref f,sz,sc,page,selected);else DrawWideMapPage(ref f,sz,sc,page,selected);
}

void DrawMapPage(ref MySpriteDrawFrame f,Vector2 sz,float sc,int page,int selected){
 int vA=0,vB=1;PickViews(page,out vA,out vB);
 Vector2 left=new Vector2(sz.X*0.36f,sz.Y*0.57f);Vector2 aC=new Vector2(left.X,sz.Y*0.36f);Vector2 bC=new Vector2(left.X,sz.Y*0.68f);Vector2 aS=new Vector2(sz.X*0.54f,sz.Y*0.22f);Vector2 bS=new Vector2(sz.X*0.54f,sz.Y*0.22f);
 DrawView(ref f,aC,aS,sc,vA);DrawView(ref f,bC,bS,sc,vB);
 int idx=0;Item picked=null;for(int i=0;i<items.Count;i++){Item it=items[i];if(!InPage(it,page))continue;bool selItem=idx==selected;if(selItem)picked=it;DrawItemDotView(ref f,it,aC,aS,sc,vA,selItem);DrawItemDotView(ref f,it,bC,bS,sc,vB,selItem);idx++;}
 DrawStatusCard(ref f,new Vector2(sz.X*0.80f,sz.Y*0.56f),new Vector2(sz.X*0.31f,sz.Y*0.66f),sc,page,picked,idx);Txt(ref f,ViewName(vA),new Vector2(aC.X-aS.X/2+12*sc,aC.Y-aS.Y/2+8*sc),0.64f*sc,Cyan(),TextAlignment.LEFT,"Monospace");Txt(ref f,ViewName(vB),new Vector2(bC.X-bS.X/2+12*sc,bC.Y-bS.Y/2+8*sc),0.64f*sc,Cyan(),TextAlignment.LEFT,"Monospace");}

void DrawStatusCard(ref MySpriteDrawFrame f,Vector2 p,Vector2 s,float sc,int page,Item pick,int count){Panel(ref f,p,s,new Color(3,15,22,210));Cat c=FindCat(PAGES[page]);float y=p.Y-s.Y/2+18*sc;Txt(ref f,"STATUS",new Vector2(p.X,y),0.60f*sc,DimC(),TextAlignment.CENTER,"Monospace");y+=25*sc;Txt(ref f,c.Status,new Vector2(p.X,y),0.88f*sc,StatusColor(c.Status),TextAlignment.CENTER,"Monospace");y+=31*sc;Txt(ref f,"TOTAL "+count,new Vector2(p.X,y),0.55f*sc,White(),TextAlignment.CENTER,"Monospace");if(c.Bad>0){y+=20*sc;Txt(ref f,"ATTN  "+c.Bad,new Vector2(p.X,y),0.54f*sc,Warn(),TextAlignment.CENTER,"Monospace");}y=p.Y-s.Y*.03f;DrawSelectedBlock(ref f,p,s,sc,page,pick,y,0.47f);}

void DrawConveyor(ref MySpriteDrawFrame f,Vector2 sz,float sc,int page,int selected){Vector2 c=new Vector2(sz.X*0.43f,sz.Y*0.57f);Vector2 area=new Vector2(sz.X*0.66f,sz.Y*0.58f);DrawView(ref f,c,area,sc,2);Txt(ref f,"CONVEYOR PROXY",new Vector2(c.X,c.Y-area.Y/2+30*sc),1.0f*sc,Cyan(),TextAlignment.CENTER,"Monospace");Txt(ref f,"TOP VIEW / INVENTORY NODES",new Vector2(c.X,c.Y-area.Y/2+58*sc),0.65f*sc,DimC(),TextAlignment.CENTER,"Monospace");int idx=0;Item picked=null;Vector2 last=new Vector2();bool hasLast=false;for(int i=0;i<items.Count;i++){Item it=items[i];if(!InPage(it,page))continue;Vector2 p=MapPosView(it.P,c,area,2);if(hasLast&&idx<26)Line(ref f,last,p,0.6f*sc,new Color(56,150,170,70));if(idx==selected)picked=it;DrawCircle(ref f,p,4.5f*sc,StatusColor(it.Status));last=p;hasLast=true;idx++;}DrawStatusCard(ref f,new Vector2(sz.X*0.80f,sz.Y*0.56f),new Vector2(sz.X*0.31f,sz.Y*0.66f),sc,page,picked,idx);}

void PickViews(int page,out int a,out int b){double s0=ViewScore(page,0),s1=ViewScore(page,1),s2=ViewScore(page,2);a=0;b=1;if(s1>s0&&s1>=s2){a=1;b=s0>=s2?0:2;}else if(s2>s0&&s2>s1){a=2;b=s0>=s1?0:1;}else{a=0;b=s1>=s2?1:2;}if(ViewCount(page)<2){a=0;b=1;}}
int ViewCount(int page){int n=0;for(int i=0;i<items.Count;i++)if(InPage(items[i],page))n++;return n;}
double ViewScore(int page,int view){bool any=false;double minx=9,maxx=-9,miny=9,maxy=-9;for(int i=0;i<items.Count;i++){Item it=items[i];if(!InPage(it,page))continue;if(!it.HasPos)continue;Vector2 n=Norm2(it.P,view);if(n.X<minx)minx=n.X;if(n.X>maxx)maxx=n.X;if(n.Y<miny)miny=n.Y;if(n.Y>maxy)maxy=n.Y;any=true;}if(!any)return 0;double rx=maxx-minx,ry=maxy-miny;return rx*0.65+ry*0.65+Math.Min(rx,ry)*0.35;}
string ViewName(int v){return v==0?"SIDE":(v==1?"FRONT":"TOP");}
Vector2 Norm2(Vector3D p,int view){double dx=Math.Max(1,hMax.X-hMin.X),dy=Math.Max(1,hMax.Y-hMin.Y),dz=Math.Max(1,hMax.Z-hMin.Z);double nx=(p.X-hMin.X)/dx,ny=(p.Y-hMin.Y)/dy,nz=(p.Z-hMin.Z)/dz;if(view==0)return new Vector2((float)nz,(float)ny);if(view==1)return new Vector2((float)(1-nx),(float)ny);return new Vector2((float)nz,(float)(1-nx));} 
Vector2 FitViewSize(Vector2 s,int view){double dx=Math.Max(1,hMax.X-hMin.X),dy=Math.Max(1,hMax.Y-hMin.Y),dz=Math.Max(1,hMax.Z-hMin.Z);double w=view==1?dx:dz;double h=view==2?dx:dy;double r=w/Math.Max(1,h);float aw=s.X*.90f,ah=s.Y*.86f;if(view==1){aw=s.X*.88f;ah=s.Y*.88f;}float fw=aw,fh=(float)(aw/r);if(fh>ah){fh=ah;fw=(float)(ah*r);}if(view==0||view==2){fw=aw;fh=(float)(aw/r);float minH=s.Y*.30f,maxH=s.Y*.70f;if(fh<minH)fh=minH;if(fh>maxH)fh=maxH;}else{float minH=s.Y*.42f;if(fh<minH){fh=minH;if(fw>aw)fw=aw;}}return new Vector2(fw,fh);}
void DrawView(ref MySpriteDrawFrame f,Vector2 c,Vector2 s,float sc,int view){DrawViewPlate(ref f,c,s,sc);DrawHullView(ref f,c,s,sc,view);}
void DrawHullView(ref MySpriteDrawFrame f,Vector2 c,Vector2 s,float sc,int view){Color fill=new Color(22,105,120,54),line=new Color(142,246,255,220),inner=new Color(72,190,210,58),back=new Color(0,0,0,165);Vector2 d=FitViewSize(s,view);float w=d.X,h=d.Y;if(view==0||view==2){float nose=w*.10f;Vector2[] p={new Vector2(c.X-w/2,c.Y-h/2),new Vector2(c.X+w/2-nose,c.Y-h/2),new Vector2(c.X+w/2,c.Y),new Vector2(c.X+w/2-nose,c.Y+h/2),new Vector2(c.X-w/2,c.Y+h/2)};PolyFillApprox(ref f,p,fill);Poly(ref f,p,2.2f*sc,back);Poly(ref f,p,1.15f*sc,line);float iw=w*.90f,ih=h*.70f,n2=nose*.75f;Vector2[] q={new Vector2(c.X-iw/2,c.Y-ih/2),new Vector2(c.X+iw/2-n2,c.Y-ih/2),new Vector2(c.X+iw/2,c.Y),new Vector2(c.X+iw/2-n2,c.Y+ih/2),new Vector2(c.X-iw/2,c.Y+ih/2)};Poly(ref f,q,0.55f*sc,inner);ScanFillPoly(ref f,q,sc);}
 else{Vector2[] p={new Vector2(c.X-w/2,c.Y+h/2),new Vector2(c.X+w/2,c.Y+h/2),new Vector2(c.X+w*.46f,c.Y-h*.20f),new Vector2(c.X,c.Y-h/2),new Vector2(c.X-w*.46f,c.Y-h*.20f)};PolyFillApprox(ref f,p,fill);Poly(ref f,p,1.8f*sc,back);Poly(ref f,p,0.95f*sc,line);Vector2[] q={new Vector2(c.X-w*.42f,c.Y+h*.38f),new Vector2(c.X+w*.42f,c.Y+h*.38f),new Vector2(c.X+w*.37f,c.Y-h*.14f),new Vector2(c.X,c.Y-h*.39f),new Vector2(c.X-w*.37f,c.Y-h*.14f)};Poly(ref f,q,0.55f*sc,inner);ScanFillPoly(ref f,q,sc);}}
void ScanFill(ref MySpriteDrawFrame f,Vector2 c,Vector2 d,float sc,int view){float y0=c.Y-d.Y/2+4*sc,y1=c.Y+d.Y/2-4*sc;for(float y=y0;y<y1;y+=4.2f*sc)Line(ref f,new Vector2(c.X-d.X*.44f,y),new Vector2(c.X+d.X*.44f,y),0.35f*sc,new Color(70,180,200,45));}
void ScanFillPoly(ref MySpriteDrawFrame f,Vector2[] p,float sc){float miny=p[0].Y,maxy=p[0].Y;for(int i=1;i<p.Length;i++){if(p[i].Y<miny)miny=p[i].Y;if(p[i].Y>maxy)maxy=p[i].Y;}float step=4.2f*sc;for(float y=miny+3*sc;y<=maxy-3*sc;y+=step){float x1=0,x2=0;int hits=0;for(int i=0;i<p.Length;i++){Vector2 a=p[i],b=p[(i+1)%p.Length];if((a.Y<=y&&b.Y>y)||(b.Y<=y&&a.Y>y)){float x=a.X+(y-a.Y)*(b.X-a.X)/(b.Y-a.Y);if(hits==0)x1=x;else x2=x;hits++;}}if(hits>=2){if(x2<x1){float t=x1;x1=x2;x2=t;}float pad=4*sc;if(x2-x1>pad*2)Line(ref f,new Vector2(x1+pad,y),new Vector2(x2-pad,y),0.35f*sc,new Color(70,180,200,45));}}}
void DrawItemDotView(ref MySpriteDrawFrame f,Item it,Vector2 c,Vector2 s,float sc,int view,bool selected){if(!it.HasPos)return;Vector2 p=MapPosView(it.P,c,s,view);float r=(it.Status=="OFFLINE")?6.2f*sc:((it.Status=="DEG")?5.4f*sc:4.8f*sc);if(selected)Ring(ref f,p,r+5*sc,1.4f*sc,White());DrawCircle(ref f,p,r,StatusColor(it.Status));}
Vector2 MapPosView(Vector3D p,Vector2 c,Vector2 s,int view){Vector2 n=Norm2(p,view);Vector2 d=FitViewSize(s,view);float x=(n.X-.5f)*d.X*.92f;float y=(.5f-n.Y)*d.Y*.92f;return c+new Vector2(x,y);}
// Legacy names retained for older helper calls.
void DrawHull(ref MySpriteDrawFrame f,Vector2 c,Vector2 s,float sc,bool front){DrawHullView(ref f,c,s,sc,front?1:0);} 
void DrawItemDot(ref MySpriteDrawFrame f,Item it,Vector2 c,Vector2 s,float sc,bool front,bool selected){DrawItemDotView(ref f,it,c,s,sc,front?1:0,selected);} 
Vector2 MapPos(Vector3D p,Vector2 c,Vector2 s,bool front){return MapPosView(p,c,s,front?1:0);} 

void DrawWideMapPage(ref MySpriteDrawFrame f,Vector2 sz,float sc,int page,int selected){
 Vector2 statusS=new Vector2(sz.X*0.235f,sz.Y*0.72f);
 Vector2 statusC=new Vector2(sz.X-statusS.X/2-30*sc,sz.Y*0.55f);
 float left=44*sc,right=statusC.X-statusS.X/2-10*sc,top=116*sc,bottom=sz.Y-64*sc;
 float profW=right-left,profH=bottom-top;
 float frontW=profW*0.31f,longW=profW-frontW;
 Vector2 topS=new Vector2(longW,profH*0.50f);
 Vector2 sideS=topS;
 Vector2 frontS=new Vector2(frontW,profH);
 Vector2 topC=new Vector2(left+longW/2,top+topS.Y/2);
 Vector2 sideC=new Vector2(left+longW/2,top+topS.Y+sideS.Y/2);
 Vector2 frontC=new Vector2(left+longW+frontW/2,top+profH/2);
 DrawView(ref f,topC,topS,sc,2);DrawView(ref f,sideC,sideS,sc,0);DrawView(ref f,frontC,frontS,sc,1);
 int idx=0;Item picked=null;for(int i=0;i<items.Count;i++){Item it=items[i];if(!InPage(it,page))continue;bool selItem=idx==selected;if(selItem)picked=it;DrawItemDotView(ref f,it,topC,topS,sc,2,selItem);DrawItemDotView(ref f,it,sideC,sideS,sc,0,selItem);DrawItemDotView(ref f,it,frontC,frontS,sc,1,selItem);idx++;}
 Txt(ref f,"TOP",new Vector2(topC.X-topS.X/2+18*sc,topC.Y-topS.Y/2+2*sc),0.58f*sc,Cyan(),TextAlignment.LEFT,"Monospace");
 Txt(ref f,"SIDE",new Vector2(sideC.X-sideS.X/2+18*sc,sideC.Y-sideS.Y/2+2*sc),0.58f*sc,Cyan(),TextAlignment.LEFT,"Monospace");
 Txt(ref f,"FRONT",new Vector2(frontC.X-frontS.X/2+10*sc,frontC.Y-frontS.Y/2+8*sc),0.56f*sc,Cyan(),TextAlignment.LEFT,"Monospace");
 DrawStatusCardWide(ref f,statusC,statusS,sc,page,picked,idx);
}

void DrawStatusCardWide(ref MySpriteDrawFrame f,Vector2 p,Vector2 s,float sc,int page,Item pick,int count){
 Panel(ref f,p,s,new Color(3,15,22,210));Cat c=FindCat(PAGES[page]);float y=p.Y-s.Y/2+22*sc;
 Txt(ref f,"STATUS",new Vector2(p.X,y),0.58f*sc,DimC(),TextAlignment.CENTER,"Monospace");y+=25*sc;
 Txt(ref f,c.Status,new Vector2(p.X,y),0.90f*sc,StatusColor(c.Status),TextAlignment.CENTER,"Monospace");y+=34*sc;
 Txt(ref f,"TOTAL  "+count,new Vector2(p.X,y),0.55f*sc,White(),TextAlignment.CENTER,"Monospace");if(c.Bad>0){y+=22*sc;Txt(ref f,"ATTN   "+c.Bad,new Vector2(p.X,y),0.54f*sc,Warn(),TextAlignment.CENTER,"Monospace");}
 y=p.Y-s.Y*.04f;DrawSelectedBlock(ref f,p,s,sc,page,pick,y,0.49f);
}
string CleanReason(Item it){string r=it.Reason??"";string t=it.Type??"";if(r==it.Status||r==t+" "+it.Status)return"";if(t.Length>0&&r.StartsWith(t+" "))r=r.Substring(t.Length+1);return r;}
void DrawSelectedBlock(ref MySpriteDrawFrame f,Vector2 p,Vector2 s,float sc,int page,Item pick,float y,float fs){float left=p.X-s.X*.41f;float maxY=p.Y+s.Y*.47f;Txt(ref f,"SELECT",new Vector2(p.X,y),0.58f*sc,DimC(),TextAlignment.CENTER,"Monospace");y+=24*sc;if(pick==null){Txt(ref f,"NONE",new Vector2(p.X,y),0.62f*sc,DimC(),TextAlignment.CENTER,"Monospace");return;}Txt(ref f,ShortName(pick),new Vector2(p.X,y),0.72f*sc,StatusColor(pick.Status),TextAlignment.CENTER,"Monospace");y+=26*sc;Txt(ref f,pick.Status,new Vector2(p.X,y),0.60f*sc,StatusColor(pick.Status),TextAlignment.CENTER,"Monospace");string r=CleanReason(pick);if(r.Length>0&&r!="READY"&&r!="PRESSURIZED"&&r!="DEPRESSURIZED"){y+=21*sc;Txt(ref f,Fit(r,18),new Vector2(p.X,y),0.48f*sc,StatusColor(pick.Status),TextAlignment.CENTER,"Monospace");}y+=28*sc;if(pick.Detail.Length>0&&y<maxY-44*sc){Txt(ref f,"DETAIL",new Vector2(left,y),fs*sc,DimC(),TextAlignment.LEFT,"Monospace");y+=18*sc;y=DrawPipedRows(ref f,pick.Detail,left,y,maxY,sc,fs,White(),4);}if(y<maxY-24*sc)DrawIssues(ref f,p,s,sc,page,pick,y+4*sc,fs);}
float DrawPipedRows(ref MySpriteDrawFrame f,string rows,float left,float y,float maxY,float sc,float fs,Color col,int lim){int p=0,n=0;while(p<rows.Length&&y<maxY&&n<lim){int e=rows.IndexOf('|',p);if(e<0)e=rows.Length;string row=rows.Substring(p,e-p);if(row.Length>0){Txt(ref f,Fit(row,22),new Vector2(left,y),fs*sc,col,TextAlignment.LEFT,"Monospace");y+=17*sc;n++;}p=e+1;}return y;}

void DrawIssues(ref MySpriteDrawFrame f,Vector2 p,Vector2 s,float sc,int page,Item pick,float y,float fs){float left=p.X-s.X*.41f;float maxY=p.Y+s.Y*.47f;int any=CountIssues(page,pick);if(any<=0)return;Txt(ref f,"ISSUES",new Vector2(left,y),fs*sc,DimC(),TextAlignment.LEFT,"Monospace");y+=18*sc;int n=0;if(PAGES[page]=="WEAPONS"&&pick!=null&&pick.Problems.Length>0){DrawProblemText(ref f,pick.Problems,left,y,maxY,sc,fs);return;}for(int i=0;i<items.Count&&y<maxY;i++){Item it=items[i];if(!InPage(it,page))continue;if(!(it.Alert||it.Status=="DEG"||it.Status=="OFFLINE"))continue;string r=ShortName(it)+" "+CleanReason(it);Txt(ref f,Fit(r,22),new Vector2(left,y),fs*sc,StatusColor(it.Status),TextAlignment.LEFT,"Monospace");y+=18*sc;n++;if(n>=6)break;}}
int CountIssues(int page,Item pick){if(PAGES[page]=="WEAPONS"&&pick!=null&&pick.Problems.Length>0)return 1;int n=0;for(int i=0;i<items.Count;i++){Item it=items[i];if(!InPage(it,page))continue;if(it.Alert||it.Status=="DEG"||it.Status=="OFFLINE")n++;}return n;}
void DrawProblemText(ref MySpriteDrawFrame f,string probs,float left,float y,float maxY,float sc,float fs){int p=0,n=0;while(p<probs.Length&&y<maxY&&n<7){int e=probs.IndexOf(';',p);if(e<0)e=probs.Length;string row=probs.Substring(p,e-p);int a=row.IndexOf(':'),b=a>=0?row.IndexOf(':',a+1):-1;if(a>0&&b>a){string st=row.Substring(0,a),part=row.Substring(a+1,b-a-1),rea=row.Substring(b+1);Txt(ref f,Fit(part+" "+CleanProblemReason(part,rea),20),new Vector2(left,y),fs*sc,StatusColor(st),TextAlignment.LEFT,"Monospace");y+=18*sc;n++;}p=e+1;}if(n==0)Txt(ref f,"NONE",new Vector2(left,y),fs*sc,DimC(),TextAlignment.LEFT,"Monospace");}
string CleanProblemReason(string part,string r){if(r==null)return"";if(part!=null&&part.Length>0&&r.StartsWith(part+" "))r=r.Substring(part.Length+1);return r;}


void DrawWideConveyor(ref MySpriteDrawFrame f,Vector2 sz,float sc,int page,int selected){
 Vector2 c=new Vector2(sz.X*0.42f,sz.Y*0.58f);Vector2 area=new Vector2(sz.X*0.66f,sz.Y*0.60f);DrawView(ref f,c,area,sc,0);
 Txt(ref f,"CONVEYOR PROXY",new Vector2(c.X,c.Y-area.Y/2+30*sc),0.92f*sc,Cyan(),TextAlignment.CENTER,"Monospace");Txt(ref f,"TOP VIEW",new Vector2(c.X,c.Y-area.Y/2+55*sc),0.55f*sc,DimC(),TextAlignment.CENTER,"Monospace");
 int idx=0;Item picked=null;Vector2 last=new Vector2();bool hasLast=false;for(int i=0;i<items.Count;i++){Item it=items[i];if(!InPage(it,page))continue;Vector2 p=MapPosView(it.P,c,area,2);if(hasLast&&idx<32)Line(ref f,last,p,0.6f*sc,new Color(56,150,170,70));if(idx==selected)picked=it;DrawCircle(ref f,p,4.5f*sc,StatusColor(it.Status));last=p;hasLast=true;idx++;}
 DrawStatusCardWide(ref f,new Vector2(sz.X*0.79f,sz.Y*0.55f),new Vector2(sz.X*0.27f,sz.Y*0.72f),sc,page,picked,idx);
}

void DrawRail(IMyTextSurface s){if(s==null)return;Prep(s);Vector2 sz=s.TextureSize;float sc=Math.Min(sz.X,sz.Y)/512f;var f=s.DrawFrame();DrawBg(ref f,sz,sc);Vector2 c=sz/2f;Panel(ref f,c,new Vector2(sz.X*0.84f,sz.Y*0.66f),new Color(3,15,22,215));Txt(ref f,"PAGE",new Vector2(c.X,c.Y-122*sc),0.68f*sc,DimC(),TextAlignment.CENTER,"Monospace");Txt(ref f,PAGES[consolePage],new Vector2(c.X,c.Y-76*sc),1.35f*sc,Cyan(),TextAlignment.CENTER,"Monospace");string foc=focus==0?"CONSOLE":(focus<=exts.Count?exts[focus-1].Name:"HELP");Txt(ref f,"FOCUS: "+foc,new Vector2(c.X,c.Y-22*sc),0.70f*sc,White(),TextAlignment.CENTER,"Monospace");Txt(ref f,"TOTAL "+total+"   BAD "+bad,new Vector2(c.X,c.Y+34*sc),0.76f*sc,bad>0?Warn():White(),TextAlignment.CENTER,"Monospace");Txt(ref f,"ANCHOR "+(anchorOk?"OK":"LOST"),new Vector2(c.X,c.Y+82*sc),0.68f*sc,anchorOk?Good():Bad(),TextAlignment.CENTER,"Monospace");f.Dispose();}
void DrawHelp(IMyTextSurface s){if(s==null)return;Prep(s);Vector2 sz=s.TextureSize;float sc=Math.Min(sz.X,sz.Y)/512f;var f=s.DrawFrame();DrawBg(ref f,sz,sc);Vector2 c=sz/2f;Panel(ref f,c,new Vector2(sz.X*0.86f,sz.Y*0.72f),new Color(3,15,22,220));Txt(ref f,"SIO HELP",new Vector2(c.X,c.Y-130*sc),1.05f*sc,Cyan(),TextAlignment.CENTER,"Monospace");float y=c.Y-98*sc;string[] l={"1 PAGE PREV","2 PAGE NEXT","3 PREV","4 NEXT","5 DEC --","6 INC --","7 STEP --","8 FOCUS","9 SAFE --","ARG SCAN"};for(int i=0;i<l.Length;i++){Txt(ref f,l[i],new Vector2(c.X-125*sc,y+i*24*sc),0.72f*sc,White(),TextAlignment.LEFT,"Monospace");}f.Dispose();}
void DrawFault(ref MySpriteDrawFrame f,Vector2 sz,float sc){Vector2 c=sz/2f;Panel(ref f,c,new Vector2(sz.X*0.76f,170*sc),new Color(40,16,0,215));Txt(ref f,"ANCHOR LOST",new Vector2(c.X,c.Y-38*sc),1.25f*sc,Bad(),TextAlignment.CENTER,"Monospace");Txt(ref f,"NEED [FC] [WSO] OR [IMS]",new Vector2(c.X,c.Y+22*sc),0.68f*sc,White(),TextAlignment.CENTER,"Monospace");}

void WritePacket(){sb.Clear();sb.AppendLine("[SIO-PB1 V14E]");sb.AppendLine("STATE|"+(anchorOk?"OK":"ANCHOR LOST")+"|"+seq);sb.AppendLine("ANCHOR|"+(anchor!=null?Safe(anchor.CustomName):"NONE"));sb.AppendLine("COUNTS|"+total+"|"+bad+"|"+cats.Count);for(int i=0;i<cats.Count;i++){Cat c=cats[i];sb.AppendLine("CAT|"+c.Name+"|"+c.Status+"|"+c.Total+"|"+c.Bad+"|"+c.Off+"|"+Safe(c.Reason));}for(int i=0;i<wpnSys.Count;i++){Sys s=wpnSys[i];sb.AppendLine("SYS|"+s.Key+"|"+s.Status+"|"+s.Bad+"|"+s.Welders+"|"+Safe(s.Reason));}sb.AppendLine("END");Me.CustomData=sb.ToString();}
void EchoStatus(){Echo("SIO PB1 V14F");Echo("Anchor: "+(anchorOk?anchor.CustomName:"LOST"));Echo("Focus: "+(focus==0?"CONSOLE":(focus<=exts.Count?exts[focus-1].Name:"HELP")));Echo("Page: "+PAGES[CurrentPage()]);Echo("Blocks "+total+" Bad "+bad+" LCD "+exts.Count);}

bool IncludeBlock(IMyTerminalBlock b){if(b==null)return false;return HasTag(b.CustomName,SHIP_TAG);}

string Category(IMyTerminalBlock b){if(b is IMyReactor)return"REACTORS";if(b is IMyJumpDrive)return"JUMP";if(b is IMyBatteryBlock)return"BATTERIES";if(b is IMyThrust)return"ENGINES";if(b is IMyGyro)return"GYROS";if(b is IMyGasTank)return"TANKS";if(b is IMyAirVent)return"ENVIRONMENT";if(b is IMyAssembler)return"ASSEMBLERS";if(b is IMyRefinery)return"REFINERS";if(WpnTag(b.CustomName).Length>0||b is IMyLargeTurretBase||b is IMyUserControllableGun||b is IMyTurretControlBlock)return"WEAPONS";if(b.HasInventory&&!(b is IMyAssembler)&&!(b is IMyRefinery))return"CONVEYOR";return"";}
bool InPage(Item it,int page){string p=PAGES[page];if(p=="OVERVIEW")return it.Alert;return it.Cat==p;}
int FilteredCount(int page){int n=0;for(int i=0;i<items.Count;i++)if(InPage(items[i],page))n++;return n;}
bool IsBad(IMyTerminalBlock b){IMyFunctionalBlock f=b as IMyFunctionalBlock;return b==null||!b.IsFunctional||!b.IsWorking||(f!=null&&!f.Enabled);}
bool IsOffline(IMyTerminalBlock b){IMyFunctionalBlock f=b as IMyFunctionalBlock;if(b==null||!b.IsFunctional)return true;if(f!=null&&!f.Enabled)return true;return !b.IsWorking;}
bool IsAlertBlock(IMyTerminalBlock b){IMyAirVent v=b as IMyAirVent;if(v!=null){if(!v.IsFunctional)return true;IMyFunctionalBlock vf=v as IMyFunctionalBlock;if(vf!=null&&!vf.Enabled)return false;if(!v.IsWorking)return false;if(v.Depressurize)return false;return !v.CanPressurize||v.GetOxygenLevel()<0.95f;}if(b==null||!b.IsFunctional)return true;IMyFunctionalBlock f=b as IMyFunctionalBlock;if(f!=null&&!f.Enabled)return IsWeaponLike(b);if(!b.IsWorking)return true;return false;}
bool IsWeaponLike(IMyTerminalBlock b){return b is IMyLargeTurretBase||b is IMyUserControllableGun||b is IMyTurretControlBlock||WpnTag(b.CustomName).Length>0;}
string ReasonFor(IMyTerminalBlock b){if(b==null)return"MISSING";string t=PartName(b);if(!b.IsFunctional)return t+" DAMAGED";IMyFunctionalBlock f=b as IMyFunctionalBlock;if(f!=null&&!f.Enabled)return t+" OFFLINE";if(!b.IsWorking)return t+" NOT WORKING";return"READY";}
string BuildDetail(IMyTerminalBlock b,Item it){if(b==null)return"";IMyAirVent v=b as IMyAirVent;if(v!=null){int o=(int)(v.GetOxygenLevel()*100+0.5f);string m=v.Depressurize?"DEPRESS":"PRESS";return"O2 "+o+"%|SEALED "+(v.CanPressurize?"YES":"NO")+"|MODE "+m+"|POWER "+(IsEnabled(b)?"ON":"OFF");}IMyGasTank gt=b as IMyGasTank;if(gt!=null)return"FILL "+Pct(gt.FilledRatio)+"|STOCK "+(gt.Stockpile?"ON":"OFF")+"|POWER "+(IsEnabled(b)?"ON":"OFF");IMyGyro gy=b as IMyGyro;if(gy!=null)return"POWER "+(IsEnabled(b)?"ON":"OFF")+"|WORK "+(b.IsWorking?"YES":"NO")+"|FUNC "+(b.IsFunctional?"YES":"NO");IMyBatteryBlock ba=b as IMyBatteryBlock;if(ba!=null)return"CHARGE "+Pct(ba.CurrentStoredPower/Math.Max(0.001f,ba.MaxStoredPower))+"|MODE "+ba.ChargeMode.ToString().ToUpperInvariant()+"|POWER "+(IsEnabled(b)?"ON":"OFF");IMyJumpDrive jd=b as IMyJumpDrive;if(jd!=null)return"CHARGE "+Pct(jd.CurrentStoredPower/Math.Max(0.001f,jd.MaxStoredPower))+"|POWER "+(IsEnabled(b)?"ON":"OFF")+"|WORK "+(b.IsWorking?"YES":"NO");IMyPowerProducer pp=b as IMyPowerProducer;if(pp!=null)return"OUTPUT "+pp.CurrentOutput.ToString("0.0")+"MW|MAX "+pp.MaxOutput.ToString("0.0")+"MW|POWER "+(IsEnabled(b)?"ON":"OFF");IMyProductionBlock pr=b as IMyProductionBlock;if(pr!=null)return"PRODUCE "+(pr.IsProducing?"YES":"NO")+"|POWER "+(IsEnabled(b)?"ON":"OFF")+"|WORK "+(b.IsWorking?"YES":"NO");if(b.HasInventory)return"INV YES|POWER "+(IsEnabled(b)?"ON":"OFF")+"|WORK "+(b.IsWorking?"YES":"NO");return"POWER "+(IsEnabled(b)?"ON":"OFF")+"|WORK "+(b.IsWorking?"YES":"NO")+"|FUNC "+(b.IsFunctional?"YES":"NO");}
bool IsEnabled(IMyTerminalBlock b){IMyFunctionalBlock f=b as IMyFunctionalBlock;return f==null||f.Enabled;}
string Pct(double v){if(v<0)v=0;if(v>1)v=1;return ((int)(v*100+0.5)).ToString()+"%";}

string PartName(IMyTerminalBlock b){if(b is IMyReactor)return"REACTOR";if(b is IMyJumpDrive)return"JUMP";if(b is IMyBatteryBlock)return"BATTERY";if(b is IMyThrust)return"ENGINE";if(b is IMyGyro)return"GYRO";if(b is IMyGasTank)return"TANK";if(b is IMyAirVent)return"VENT";if(b is IMyAssembler)return"ASSEMBLER";if(b is IMyRefinery)return"REFINER";if(b is IMyMotorStator){string d=b.BlockDefinition.ToString();return d.IndexOf("Hinge",StringComparison.OrdinalIgnoreCase)>=0?"HINGE":"ROTOR";}if(b is IMyTurretControlBlock)return"CTC";if(b is IMyLargeTurretBase||b is IMyUserControllableGun)return"WEAPON";if(b is IMyCameraBlock)return"CAMERA";if(b is IMyShipWelder)return"WELDER";return"BLOCK";}

void Prep(IMyTextSurface s){s.ContentType=ContentType.SCRIPT;s.Script="";s.ScriptBackgroundColor=Bg();}
void Blank(IMyTextSurface s){if(s==null)return;s.ContentType=ContentType.SCRIPT;s.Script="";s.ScriptBackgroundColor=Bg();}
void DrawBg(ref MySpriteDrawFrame f,Vector2 sz,float sc){
 f.Add(new MySprite(SpriteType.TEXTURE,"SquareSimple",sz/2f,sz,Bg()));
 float major=128*sc,t=1.0f*sc;
 float[] vx={.34f,.57f,.43f,.71f,.49f,.62f};
 float[,] hy={{.27f,.61f},{.36f,.73f},{.24f,.52f},{.43f,.69f},{.31f,.58f}};
 int cols=(int)Math.Ceiling(sz.X/major)+1,rows=(int)Math.Ceiling(sz.Y/major)+1;
 for(int c=0;c<cols;c++){float x=c*major+major*vx[c%vx.Length];if(x>1&&x<sz.X-1)Line(ref f,new Vector2(x,0),new Vector2(x,sz.Y),t,Grid());}
 for(int r=0;r<rows;r++){int k=r%hy.GetLength(0);float y0=r*major;float y=y0+major*hy[k,0];if(y>1&&y<sz.Y-1)Line(ref f,new Vector2(0,y),new Vector2(sz.X,y),t,Grid());y=y0+major*hy[k,1];if(y>1&&y<sz.Y-1)Line(ref f,new Vector2(0,y),new Vector2(sz.X,y),t,Grid());}
 for(float x=0;x<=sz.X;x+=major)Line(ref f,new Vector2(x,0),new Vector2(x,sz.Y),t,Grid2());
 for(float y=0;y<=sz.Y;y+=major)Line(ref f,new Vector2(0,y),new Vector2(sz.X,y),t,Grid2());
}
void DrawFocus(ref MySpriteDrawFrame f,Vector2 sz,float sc,bool on){if(!on)return;float w=sz.X*.65f,x1=(sz.X-w)/2f,x2=x1+w,y1=12*sc,y2=sz.Y-12*sc;Line(ref f,new Vector2(x1,y1),new Vector2(x2,y1),2.2f*sc,White());Line(ref f,new Vector2(x1,y2),new Vector2(x2,y2),2.2f*sc,White());Line(ref f,new Vector2(x1,y1),new Vector2(x1-22*sc,y1-16*sc),2.2f*sc,White());Line(ref f,new Vector2(x2,y1),new Vector2(x2+22*sc,y1-16*sc),2.2f*sc,White());Line(ref f,new Vector2(x1,y2),new Vector2(x1-22*sc,y2+16*sc),2.2f*sc,White());Line(ref f,new Vector2(x2,y2),new Vector2(x2+22*sc,y2+16*sc),2.2f*sc,White());}

void DrawConsoleFocusCard(ref MySpriteDrawFrame f,Vector2 sz,float sc){Vector2 c=new Vector2(sz.X*.50f,sz.Y*.50f);Vector2 box=new Vector2(sz.X*.54f,sz.Y*.30f);Panel(ref f,c,box,new Color(3,15,22,230));Line(ref f,new Vector2(c.X-box.X*.42f,c.Y-box.Y*.34f),new Vector2(c.X+box.X*.42f,c.Y-box.Y*.34f),2.0f*sc,White());Line(ref f,new Vector2(c.X-box.X*.42f,c.Y+box.Y*.34f),new Vector2(c.X+box.X*.42f,c.Y+box.Y*.34f),2.0f*sc,White());string foc=(focus<=exts.Count?exts[focus-1].Name:"HELP");Txt(ref f,"COMMAND FOCUS",new Vector2(c.X,c.Y-48*sc),0.72f*sc,DimC(),TextAlignment.CENTER,"Monospace");Txt(ref f,foc,new Vector2(c.X,c.Y-6*sc),1.10f*sc,White(),TextAlignment.CENTER,"Monospace");Txt(ref f,"FOCUS TO RETURN",new Vector2(c.X,c.Y+42*sc),0.56f*sc,Cyan(),TextAlignment.CENTER,"Monospace");}
void Dim(ref MySpriteDrawFrame f,Vector2 p,Vector2 s,float sc){Box(ref f,p,s,new Color(0,8,12,118));}
void DrawViewPlate(ref MySpriteDrawFrame f,Vector2 p,Vector2 s,float sc){
 Box(ref f,p,s,new Color(0,8,13,150));
 Rect(ref f,p,s,1.15f*sc,new Color(84,205,225,155));
 Vector2 h=s/2f;float k=13*sc;
 Line(ref f,new Vector2(p.X-h.X,p.Y-h.Y+k),new Vector2(p.X-h.X+k,p.Y-h.Y),1.0f*sc,new Color(150,245,255,125));
 Line(ref f,new Vector2(p.X+h.X-k,p.Y-h.Y),new Vector2(p.X+h.X,p.Y-h.Y+k),1.0f*sc,new Color(150,245,255,125));
 Line(ref f,new Vector2(p.X-h.X,p.Y+h.Y-k),new Vector2(p.X-h.X+k,p.Y+h.Y),1.0f*sc,new Color(150,245,255,125));
 Line(ref f,new Vector2(p.X+h.X-k,p.Y+h.Y),new Vector2(p.X+h.X,p.Y+h.Y-k),1.0f*sc,new Color(150,245,255,125));
}
void Panel(ref MySpriteDrawFrame f,Vector2 p,Vector2 s,Color col){f.Add(new MySprite(SpriteType.TEXTURE,"SquareSimple",p,s,col));Rect(ref f,p,s,0.8f,DimC());}
void Box(ref MySpriteDrawFrame f,Vector2 p,Vector2 s,Color col){f.Add(new MySprite(SpriteType.TEXTURE,"SquareSimple",p,s,col));}
void Rect(ref MySpriteDrawFrame f,Vector2 p,Vector2 s,float t,Color c){Vector2 h=s/2f;Line(ref f,new Vector2(p.X-h.X,p.Y-h.Y),new Vector2(p.X+h.X,p.Y-h.Y),t,c);Line(ref f,new Vector2(p.X+h.X,p.Y-h.Y),new Vector2(p.X+h.X,p.Y+h.Y),t,c);Line(ref f,new Vector2(p.X+h.X,p.Y+h.Y),new Vector2(p.X-h.X,p.Y+h.Y),t,c);Line(ref f,new Vector2(p.X-h.X,p.Y+h.Y),new Vector2(p.X-h.X,p.Y-h.Y),t,c);}
void Poly(ref MySpriteDrawFrame f,Vector2[] p,float t,Color c){for(int i=0;i<p.Length;i++)Line(ref f,p[i],p[(i+1)%p.Length],t,c);}
void PolyFillApprox(ref MySpriteDrawFrame f,Vector2[] p,Color c){float miny=p[0].Y,maxy=p[0].Y;for(int i=1;i<p.Length;i++){if(p[i].Y<miny)miny=p[i].Y;if(p[i].Y>maxy)maxy=p[i].Y;}float step=Math.Max(1.5f,(maxy-miny)/28f);for(float y=miny;y<=maxy;y+=step){float x1=0,x2=0;int hits=0;for(int i=0;i<p.Length;i++){Vector2 a=p[i],b=p[(i+1)%p.Length];if((a.Y<=y&&b.Y>y)||(b.Y<=y&&a.Y>y)){float x=a.X+(y-a.Y)*(b.X-a.X)/(b.Y-a.Y);if(hits==0)x1=x;else x2=x;hits++;}}if(hits>=2){if(x2<x1){float t=x1;x1=x2;x2=t;}f.Add(new MySprite(SpriteType.TEXTURE,"SquareSimple",new Vector2((x1+x2)/2f,y),new Vector2(x2-x1,step+0.4f),c));}}}
void Ring(ref MySpriteDrawFrame f,Vector2 p,float r,float t,Color c){f.Add(new MySprite(SpriteType.TEXTURE,"Circle",p,new Vector2(r*2,r*2),c));f.Add(new MySprite(SpriteType.TEXTURE,"Circle",p,new Vector2((r-t)*2,(r-t)*2),Bg()));}
void DrawCircle(ref MySpriteDrawFrame f,Vector2 p,float r,Color c){f.Add(new MySprite(SpriteType.TEXTURE,"Circle",p,new Vector2(r*2,r*2),c));}
void Line(ref MySpriteDrawFrame f,Vector2 a,Vector2 b,float t,Color c){Vector2 d=b-a;float len=d.Length();if(len<=0)return;var sp=new MySprite(SpriteType.TEXTURE,"SquareSimple",a+d/2f,new Vector2(len,t),c);sp.RotationOrScale=(float)Math.Atan2(d.Y,d.X);f.Add(sp);}
void Txt(ref MySpriteDrawFrame f,string txt,Vector2 p,float sc,Color c,TextAlignment al,string font){var s=MySprite.CreateText(txt,font,c,sc,al);s.Position=p;f.Add(s);}
Color Bg(){return new Color(1,8,13,255);}Color Grid(){return new Color(18,70,92,68);}Color Grid2(){return new Color(42,130,148,96);}Color Cyan(){return new Color(105,225,245,230);}Color DimC(){return new Color(60,150,170,150);}Color White(){return new Color(230,245,250,240);}Color Good(){return new Color(120,255,210,240);}Color Warn(){return new Color(255,180,75,240);}Color Bad(){return new Color(255,70,50,240);}Color Gray(){return new Color(72,82,88,235);}Color StatusColor(string s){return s=="OFFLINE"?Bad():(s=="DEG"?Warn():((s=="DOWN"||s=="DEPRES")?Gray():White()));}

bool HasTag(string n,string t){return n!=null&&t!=null&&n.IndexOf(t,StringComparison.OrdinalIgnoreCase)>=0;}
int NumberTag(string n,string prefix){if(n==null)return 0;int p=n.IndexOf(prefix,StringComparison.OrdinalIgnoreCase);while(p>=0){int i=p+prefix.Length,v=0;bool any=false;while(i<n.Length&&n[i]>='0'&&n[i]<='9'){any=true;v=v*10+n[i]-'0';i++;}if(any&&i<n.Length&&n[i]==']')return v;p=n.IndexOf(prefix,p+1,StringComparison.OrdinalIgnoreCase);}return 0;}
string WpnTag(string n){if(n==null)return"";int p=n.IndexOf("[WPN",StringComparison.OrdinalIgnoreCase);while(p>=0){int i=p+4,v=0;bool any=false;while(i<n.Length&&n[i]>='0'&&n[i]<='9'){any=true;v=v*10+(n[i]-'0');i++;}if(any&&i<n.Length&&n[i]==']')return"WPN"+v;p=n.IndexOf("[WPN",p+1,StringComparison.OrdinalIgnoreCase);}return"";}
string CleanName(string n){if(n==null)return"BLOCK";string s=n;while(true){int a=s.IndexOf('[');int z=a>=0?s.IndexOf(']',a):-1;if(a<0||z<0)break;s=s.Remove(a,z-a+1).Trim();}return s.Length>0?s:"BLOCK";}
string ShortName(Item it){if(it.Key.Length>0)return it.Key;string t=it.Type.Length>0?it.Type:it.Cat;return Fit(t.ToUpperInvariant(),14);}
string Fit(string s,int n){if(s==null)return"";return s.Length<=n?s:s.Substring(0,Math.Max(0,n-1))+"...";}
string Safe(string s){if(s==null)return"";return s.Replace("|","/").Replace("\n"," ").Replace("\r","");}
int ClampI(int v,int a,int b){return v<a?a:(v>b?b:v);} 
