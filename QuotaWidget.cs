using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

class Meter {
 public string Name, Detail, Error; public double? Remaining, WeeklyRemaining; public DateTime Updated;
 public string ResetAt="未知", WeeklyResetAt="未知", Period="5小时";
 public Meter(string n) { Name=n; Detail="等待刷新"; }
}
static class Data {
 public static readonly string Dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QuotaWidget");
 public static string Home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
 public static object Parse(string s) { return new JavaScriptSerializer().DeserializeObject(s); }
 public static object At(object o, params string[] keys) { foreach(string k in keys) { var d=o as Dictionary<string,object>; if(d==null || !d.TryGetValue(k,out o)) return null; } return o; }
 public static string Str(object o) { return o==null?"":Convert.ToString(o,System.Globalization.CultureInfo.InvariantCulture); }
 public static double? Number(object o) { double n; return o!=null && double.TryParse(Str(o),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out n) && !double.IsNaN(n) && !double.IsInfinity(n)?(double?)n:null; }
 public static string Secret(string name) { string p=Path.Combine(Dir,name+".bin"); if(!File.Exists(p))return ""; return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(p),null,DataProtectionScope.CurrentUser)); }
 public static void SaveSecret(string name,string value) { Directory.CreateDirectory(Dir); File.WriteAllBytes(Path.Combine(Dir,name+".bin"),ProtectedData.Protect(Encoding.UTF8.GetBytes(value.Trim()),null,DataProtectionScope.CurrentUser)); }
 public static string Bearer(object o) { var d=o as Dictionary<string,object>; if(d!=null) { foreach(string k in new[]{"access_token","accessToken","token","bearer"}) { string s=Str(At(o,k)); if(s.StartsWith("xai-oauth") || s.Split('.').Length==3)return s; } foreach(var v in d.Values) { string s=Bearer(v); if(s!="")return s; } } return ""; }
 public static string Reset(object o) { string s=Str(o); DateTimeOffset t; double n; if(double.TryParse(s,out n)) { try { t=new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero).AddSeconds(n>100000000000?n/1000:n); return t.ToLocalTime().ToString("MM-dd HH:mm"); } catch {return "未知";} } if(DateTimeOffset.TryParse(s,out t))return t.ToLocalTime().ToString("MM-dd HH:mm"); return "未知"; }
 public static object Request(string url,string token,string cookie,string account,bool post,bool grok) {
  var r=(HttpWebRequest)WebRequest.Create(url); r.Timeout=15000; r.ReadWriteTimeout=15000; r.AllowAutoRedirect=false; r.UserAgent="QuotaWidget/1.0"; r.Accept="application/json";
  if(token!="")r.Headers["Authorization"]="Bearer "+token;
  if(cookie!="")r.Headers["Cookie"]="WorkosCursorSessionToken="+cookie;
  if(account!="")r.Headers["ChatGPT-Account-Id"]=account;
  if(grok)r.Headers["x-xai-token-auth"]="xai-grok-cli";
  if(post) { r.Method="POST";r.ContentType="application/json";r.Headers["Origin"]="https://cursor.com";byte[] b=Encoding.UTF8.GetBytes("{}");r.ContentLength=b.Length;using(var w=r.GetRequestStream())w.Write(b,0,b.Length); }
  using(var response=(HttpWebResponse)r.GetResponse()) { if((int)response.StatusCode>=300)throw new InvalidOperationException("登录重定向，请重新登录");using(var s=new StreamReader(response.GetResponseStream()))return Parse(s.ReadToEnd()); }
 }
 public static Meter Fetch(int id) {
  var m=new Meter(new[]{"Codex","Grok","Grok Bot"}[id]);
  try {
   if(id==0) {
    string root=Environment.GetEnvironmentVariable("CODEX_HOME"); if(string.IsNullOrEmpty(root))root=Path.Combine(Home,".codex");
    string p=Path.Combine(root,"auth.json");if(!File.Exists(p))throw new InvalidOperationException("请先在 Codex 登录");
    object a=Parse(File.ReadAllText(p));string token=Str(At(a,"tokens","access_token"));if(token=="")throw new InvalidOperationException("需要 Codex 的 ChatGPT 登录");
    object r=Request("https://chatgpt.com/backend-api/wham/usage",token,"",Str(At(a,"tokens","account_id")),false,false);
    object w=At(r,"rate_limit","primary_window"), week=At(r,"rate_limit","secondary_window");
    double? used=Number(At(w,"used_percent")), weekly=Number(At(week,"used_percent"));
    if(used==null && weekly==null)throw new InvalidOperationException("接口未返回额度窗口");
    m.Remaining=used==null?(double?)null:Math.Max(0,Math.Min(100,100-used.Value));
    m.WeeklyRemaining=weekly==null?(double?)null:Math.Max(0,Math.Min(100,100-weekly.Value));
    m.ResetAt=Reset(At(w,"reset_at"));m.WeeklyResetAt=Reset(At(week,"reset_at"));
    double? seconds=Number(At(w,"limit_window_seconds"));if(seconds.HasValue && seconds.Value!=18000)m.Period=(seconds.Value/3600).ToString("0.#")+"小时";
    m.Detail="5小时剩余 "+Pct(used)+" · 周剩余 "+Pct(weekly)+"\n重置 "+Reset(At(w,"reset_at"))+" / "+Reset(At(week,"reset_at"))+"\n额外积分 "+(At(r,"credits","unlimited") is bool && (bool)At(r,"credits","unlimited")?"不限":(Str(At(r,"credits","balance"))==""?"未知":Str(At(r,"credits","balance"))));
   } else { return GrokData.Fetch(id); }
   if(m.Remaining!=null)m.Remaining=Math.Max(0,Math.Min(100,m.Remaining.Value));m.Updated=DateTime.Now;
  } catch(WebException e) {var response=e.Response as HttpWebResponse;m.Error=response==null?"网络连接失败，稍后自动重试":"HTTP "+(int)response.StatusCode+((int)response.StatusCode==401?"：登录已过期":((int)response.StatusCode==429?"：查询过于频繁":"：接口拒绝请求"));if(response!=null)response.Dispose();}
  catch(InvalidOperationException e) {m.Error=e.Message;}
  catch {m.Error="读取失败，请检查登录文件或接口格式";}
  return m;
 }
 public static string Pct(double? used) {return used==null?"未知":Math.Max(0,Math.Min(100,100-used.Value)).ToString("0.#")+"%";}
 public static bool Startup {get {using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))return k!=null && k.GetValue("QuotaWidget")!=null;}set {using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(value)k.SetValue("QuotaWidget","\""+Application.ExecutablePath+"\"");else k.DeleteValue("QuotaWidget",false);}}}
}
class Widget:Form {
 GrokSession grokSession; bool notified; DateTime started=DateTime.Now; System.Windows.Forms.Timer grokTimer=new System.Windows.Forms.Timer();
 Meter[] meters={new Meter("Codex"),new Meter("SuperGrok"),new Meter("Grok Bot")};
 System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer(); NotifyIcon tray; bool busy; Point drag; bool dragging;
 ToolTip tips=new ToolTip{InitialDelay=350,ReshowDelay=100,AutoPopDelay=15000}; string lastTip="";
 Color ink=Color.FromArgb(231,237,246),muted=Color.FromArgb(151,164,184),mint=Color.FromArgb(93,224,179);
 public Widget() {
  Text="额度挂件";ClientSize=new Size(600,68);FormBorderStyle=FormBorderStyle.None;BackColor=Color.FromArgb(20,25,35);ForeColor=ink;Font=new Font("Microsoft YaHei UI",8.5f);DoubleBuffered=true;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;
  using(var outline=new System.Drawing.Drawing2D.GraphicsPath()){outline.AddArc(0,0,16,16,180,90);outline.AddArc(Width-17,0,16,16,270,90);outline.AddArc(Width-17,Height-17,16,16,0,90);outline.AddArc(0,Height-17,16,16,90,90);outline.CloseFigure();Region=new Region(outline);}
  var area=Screen.PrimaryScreen.WorkingArea;Location=new Point(area.Right-Width-24,area.Top+50);
  try {string[] p=File.ReadAllText(Path.Combine(Data.Dir,"position.txt")).Split(',');Rectangle r=new Rectangle(int.Parse(p[0]),int.Parse(p[1]),Width,Height);foreach(var s in Screen.AllScreens)if(s.WorkingArea.Contains(r)){Location=r.Location;break;}}catch{}
  var menu=new ContextMenuStrip();menu.Items.Add("立即刷新",null,async delegate{await RefreshMeters();});menu.Items.Add("登录 Grok / 管理授权…",null,delegate{Settings();});
  var top=new ToolStripMenuItem("保持置顶"){Checked=true,CheckOnClick=true};top.CheckedChanged+=delegate{TopMost=top.Checked;};menu.Items.Add(top);
  var startup=new ToolStripMenuItem("开机自启动"){Checked=Data.Startup,CheckOnClick=true};startup.CheckedChanged+=delegate{try{Data.Startup=startup.Checked;}catch{MessageBox.Show("无法更新自启动设置");}};menu.Items.Add(startup);
  menu.Items.Add("隐藏 / 显示",null,delegate{Visible=!Visible;});menu.Items.Add("退出",null,delegate{Close();});ContextMenuStrip=menu;
  tray=new NotifyIcon{Icon=SystemIcons.Information,Text="AI 额度挂件",Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=delegate{Show();Activate();};
  MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){drag=e.Location;dragging=true;Capture=true;}};
  MouseMove+=delegate(object sender,MouseEventArgs e){if(dragging){Location=new Point(Left+e.X-drag.X,Top+e.Y-drag.Y);return;}int tile=Math.Max(0,Math.Min(3,e.X/150));int i=tile<2?0:tile-1;var m=meters[i];string detail=string.Join("\n",Array.FindAll(m.Detail.Split('\n'),line=>!line.Contains("积分")));string hint=m.Name+" · 剩余额度\n"+(m.Error==null?detail:m.Error+(m.Updated==default(DateTime)?"":"\n上次更新 "+m.Updated.ToString("HH:mm:ss")))+"\n拖动移动 · 右键设置";if(hint!=lastTip){lastTip=hint;tips.SetToolTip(this,hint);}};
  MouseUp+=delegate{dragging=false;Capture=false;try{Directory.CreateDirectory(Data.Dir);File.WriteAllText(Path.Combine(Data.Dir,"position.txt"),Left+","+Top);}catch{}};
  timer.Interval=60000;timer.Tick+=async delegate{await RefreshMeters();};Shown+=async delegate{timer.Start();await RefreshMeters();};FormClosed+=delegate{timer.Stop();grokTimer.Stop();if(grokSession!=null)grokSession.Shutdown();tray.Dispose();tips.Dispose();};
  grokTimer.Interval=5000;grokTimer.Tick+=delegate{meters[1]=GrokData.Fetch(1);meters[2]=GrokData.Fetch(2);if(GrokSession.HasSavedSession && (DateTime.Now-started).TotalMinutes>2 && meters[1].Error!=null && !notified){notified=true;tray.ShowBalloonTip(10000,"Grok 用量暂不可用","登录可能过期或网络异常。右键挂件 → 登录 Grok 检查；旧数据不会显示为最新。",ToolTipIcon.Warning);}if(meters[1].Error==null)notified=false;Invalidate();};grokTimer.Start();
  Shown+=async delegate{if(GrokSession.HasSavedSession || Array.IndexOf(Environment.GetCommandLineArgs(),"--login")>=0){grokSession=new GrokSession();await grokSession.Start(!GrokSession.HasSavedSession);}};
 }
 async Task RefreshMeters() {if(busy)return;busy=true;Invalidate();try{var tasks=new Task<Meter>[3];for(int i=0;i<3;i++){int id=i;tasks[i]=Task.Run(()=>Data.Fetch(id));}Meter[] next=await Task.WhenAll(tasks);if(IsDisposed)return;for(int i=0;i<3;i++){if(i==0 && next[i].Error!=null && meters[i].Updated!=default(DateTime)){meters[i].Error=next[i].Error;}else meters[i]=next[i];} }finally{busy=false;if(!IsDisposed)Invalidate();}}
 protected override void OnPaint(PaintEventArgs e) {
  base.OnPaint(e);var g=e.Graphics;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
  for(int i=0;i<4;i++) {int x=i*150;var m=meters[i<2?0:i-1];
   double? remaining=i==1?m.WeeklyRemaining:m.Remaining;
   string label=i==0?"Codex "+m.Period:(i==1?"Codex 每周":m.Name);
   string reset=i==1?m.WeeklyResetAt:m.ResetAt;
   if(i>0)using(var pen=new Pen(Color.FromArgb(43,51,65)))g.DrawLine(pen,x,11,x,57);
   using(var f=new Font(Font.FontFamily,8.5f,FontStyle.Bold))Draw(g,label,f,ink,x+10,9,98,22);
   string amount=remaining.HasValue?remaining.Value.ToString("0.#")+"%":"—";
   if(m.Error!=null && remaining.HasValue)amount+="*";
   TextRenderer.DrawText(g,amount,Font,new Rectangle(x+103,10,37,22),m.Error==null?mint:Color.SandyBrown,TextFormatFlags.Right|TextFormatFlags.Top|TextFormatFlags.NoPadding);
   TextRenderer.DrawText(g,"重置 "+reset,Font,new Rectangle(x+10,31,132,18),muted,TextFormatFlags.Left|TextFormatFlags.Top|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
   using(var b=new SolidBrush(Color.FromArgb(48,58,74)))g.FillRectangle(b,x+10,55,130,3);
   if(remaining.HasValue)using(var b=new SolidBrush(m.Error!=null?Color.Gray:mint))g.FillRectangle(b,x+10,55,(float)(130*remaining.Value/100),3);
  }
 }
 void Draw(Graphics g,string text,Font font,Color color,int x,int y,int w,int h){using(var b=new SolidBrush(color))g.DrawString(text,font,b,new RectangleF(x,y,w,h));}
 async void Settings() {if(grokSession==null){grokSession=new GrokSession();await grokSession.Start(true);}else grokSession.OpenLogin();}
}static class Program {
 [STAThread] static void Main(string[] args) {
  ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
  if(args.Length>0 && args[0]=="--check") {var result=new List<object>();for(int i=0;i<3;i++){Meter m=Data.Fetch(i);result.Add(new{Name=m.Name,Remaining=m.Remaining,Detail=m.Detail,Error=m.Error});}Directory.CreateDirectory(Data.Dir);File.WriteAllText(Path.Combine(Data.Dir,"check.json"),new JavaScriptSerializer().Serialize(result));return;}
  bool created;using(var mutex=new Mutex(true,"Local\\AIQuotaWidget",out created)){if(!created)return;Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Widget());}
 }
}
