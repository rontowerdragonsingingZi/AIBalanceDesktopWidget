using System;
using System.IO;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

static class GrokData {
 static object latest; static readonly object gate=new object(); public static string Status="未授权：右键 → 登录 Grok";
 public static void Set(object value) {lock(gate){latest=value;}Status="已连接 grok.com";}
 public static void Clear(){lock(gate){latest=null;}}
 public static Meter Fetch(int id) {
  var m=new Meter(id==1?"SuperGrok":"Grok Bot");object s;lock(gate){s=latest;}
  if(s==null){m.Error=Status;return m;}
  object row=Data.At(s,id==1?"supergrok":"grokbot");
  if(row==null){m.Error="用量页面未显示此项额度";return m;}
  double? used=Data.Number(Data.At(row,"used"));if(used==null || used<0 || used>100){m.Error="页面用量格式无法识别";return m;}
  DateTimeOffset time;if(!DateTimeOffset.TryParse(Data.Str(Data.At(s,"capturedAt")),out time)){m.Error="数据时间未知";return m;}
  m.Remaining=100-used.Value;m.Updated=time.LocalDateTime;
  string reset=Data.Str(Data.At(row,"reset"));
  var resetMatch=System.Text.RegularExpressions.Regex.Match(reset,@"^\d{4}年(\d{1,2})月(\d{1,2})日\s*(\d{1,2}:\d{2})");
  m.ResetAt=resetMatch.Success?int.Parse(resetMatch.Groups[1].Value).ToString("00")+"-"+int.Parse(resetMatch.Groups[2].Value).ToString("00")+" "+resetMatch.Groups[3].Value:(reset==""?"未知":reset);
  m.Detail="本周已用 "+used.Value.ToString("0.#")+"% · 剩余 "+m.Remaining.Value.ToString("0.#")+"%\n重置 "+(reset==""?"未知":reset);
  if(id==1){string extra=Data.Str(Data.At(s,"extra"));if(extra!="")m.Detail+="\n额外使用积分 "+extra;}
  if(DateTimeOffset.UtcNow-time>TimeSpan.FromMinutes(3))m.Error="数据已过期，请打开 Grok 登录窗口";
  return m;
 }
}
class GrokSession:Form {
 const string Usage="https://grok.com/?_s=usage";
 WebView2 web=new WebView2(); Label address=new Label(),status=new Label(); Timer poll=new Timer();
 bool ready,busy,closing,successfulPage; DateTime loadedAt,lastReload; string parser;
 static string EnabledPath {get{return Path.Combine(Data.Dir,"grok-enabled");}}
 public static bool HasSavedSession {get{return File.Exists(EnabledPath);}}
 public GrokSession() {
  Text="登录 Grok · AI 额度挂件";Size=new Size(1100,800);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",9);
  var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=42};
  var usage=new Button{Text="打开用量页",Width=110,Height=32};usage.Click+=delegate{if(ready)web.CoreWebView2.Navigate(Usage);};bar.Controls.Add(usage);
  var done=new Button{Text="完成并连接",Width=115,Height=32};done.Click+=async delegate{
   if(!ready)return;
   done.Enabled=false;
   try {
    if(GrokData.Fetch(1).Error!=null || GrokData.Fetch(2).Error!=null){web.CoreWebView2.Navigate(Usage);status.Text="正在打开用量页并验证连接，请稍候…";}
    for(int n=0;n<20;n++){await Task.Delay(1000);await Tick();if(GrokData.Fetch(1).Error==null && GrokData.Fetch(2).Error==null){Hide();return;}}
    status.Text="尚未读取到两项额度。请检查下方页面是否显示每周用量；窗口会保持打开。";
   }finally{done.Enabled=true;}
  };bar.Controls.Add(done);
  var clear=new Button{Text="取消授权",Width=100,Height=32};clear.Click+=async delegate{await Disconnect();};bar.Controls.Add(clear);
  address.AutoSize=false;address.Width=700;address.Height=32;address.TextAlign=ContentAlignment.MiddleLeft;bar.Controls.Add(address);
  status.Dock=DockStyle.Bottom;status.Height=42;status.Text="请在官方页面自行登录。账号会话仅保存在本机。";
  web.Dock=DockStyle.Fill;Controls.Add(web);Controls.Add(bar);Controls.Add(status);
  FormClosing+=delegate(object s,FormClosingEventArgs e){if(!closing){e.Cancel=true;Hide();}};
  poll.Interval=5000;poll.Tick+=async delegate{await Tick();};
 }
 public async Task Start(bool showLogin) {
  Show();if(!showLogin){Opacity=0;ShowInTaskbar=false;}
  try {
   parser=File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"grok-parser.js"));
   var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Data.Dir,"GrokWebProfile"));
   await web.EnsureCoreWebView2Async(env);
   web.CoreWebView2.Settings.AreDevToolsEnabled=false;
   web.CoreWebView2.Settings.IsStatusBarEnabled=true;
   web.CoreWebView2.Settings.IsPasswordAutosaveEnabled=false;
   web.CoreWebView2.NewWindowRequested+=delegate(object s,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;if(e.Uri.StartsWith("https://",StringComparison.OrdinalIgnoreCase))web.CoreWebView2.Navigate(e.Uri);};
   web.CoreWebView2.NavigationStarting+=delegate {successfulPage=false;};
   web.CoreWebView2.NavigationCompleted+=delegate(object s,CoreWebView2NavigationCompletedEventArgs e){successfulPage=e.IsSuccess;loadedAt=DateTime.UtcNow;};
   web.CoreWebView2.SourceChanged+=delegate{Uri u;if(Uri.TryCreate(web.Source.ToString(),UriKind.Absolute,out u))address.Text="当前网站：https://"+u.Host;};
   ready=true;web.CoreWebView2.Navigate(Usage);lastReload=DateTime.UtcNow;poll.Start();
   WriteStatus("browser-ready");
  }catch(Exception e){status.Text="登录窗口启动失败："+e.GetType().Name;GrokData.Status=status.Text;WriteStatus("browser-error: "+e.GetType().Name);}
  if(!showLogin){Hide();Opacity=1;ShowInTaskbar=true;}
 }
 public void OpenLogin(){Opacity=1;ShowInTaskbar=true;Show();WindowState=FormWindowState.Normal;Activate();}
 async Task Tick() {
  if(!ready || busy || closing)return;busy=true;
  try {
   Uri u=web.Source;
   if(u!=null && u.Scheme=="https" && u.Host=="grok.com" && u.Query.Contains("_s=usage") && successfulPage) {
    string json=await web.CoreWebView2.ExecuteScriptAsync(parser+";JSON.stringify(parseGrokDocument());");
    var jsonText=Data.Parse(json) as string;
    if(jsonText!=null) {
     object value=Data.Parse(jsonText);
     if(Data.At(value,"supergrok")!=null || Data.At(value,"grokbot")!=null) {
      ((System.Collections.Generic.Dictionary<string,object>)value)["capturedAt"]=loadedAt.ToString("o");
      GrokData.Set(value);Directory.CreateDirectory(Data.Dir);File.WriteAllText(EnabledPath,"enabled");
      status.Text="已连接：每 60 秒刷新。可以点击“完成并连接”，后台会继续同步。";
      WriteStatus("usage-connected; SuperGrok used="+Data.Str(Data.At(value,"supergrok","used"))+"%; GrokBot used="+Data.Str(Data.At(value,"grokbot","used"))+"%; window-visible="+Visible);
      File.WriteAllText(Path.Combine(Data.Dir,"grok-usage.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(value));
     }else{
      GrokData.Status="已打开网页，尚未识别用量";
      status.Text="等待官方用量页面：请完成登录，并打开用量页。";WriteStatus("awaiting-usage-page");
     }
    }
    // Never reload while the user is completing sign-in or a challenge.
    if(HasSavedSession && GrokData.Status=="已连接 grok.com" && DateTime.UtcNow-lastReload>TimeSpan.FromSeconds(60)) {lastReload=DateTime.UtcNow;web.CoreWebView2.Reload();}
   }
  }catch {status.Text="暂时无法读取用量页，将自动重试。";}
  finally{busy=false;}
 }
 async Task Disconnect() {
  poll.Stop();GrokData.Clear();GrokData.Status="已取消授权：右键 → 登录 Grok";
  if(File.Exists(EnabledPath))File.Delete(EnabledPath);
  if(ready){web.CoreWebView2.Stop();await web.CoreWebView2.Profile.ClearBrowsingDataAsync();web.CoreWebView2.Navigate(Usage);}
  status.Text="已清除挂件专用浏览器的登录会话。可重新登录。";poll.Start();WriteStatus("signed-out");
 }
 void WriteStatus(string phase){try{File.WriteAllText(Path.Combine(Data.Dir,"grok-status.txt"),DateTime.Now.ToString("s")+" "+phase);}catch{}}
 public void Shutdown(){closing=true;poll.Stop();web.Dispose();Close();}
}
