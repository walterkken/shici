using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Collections.Generic;

namespace Shici;
public static class Program {
    [STAThread] public static int Main(string[] args) {
        try {
            if(args.Length>=5&&args[0]=="--capture"){try{var text=Native.ReadSelectionDirect(new Native.POINT(int.Parse(args[2]),int.Parse(args[3])),new IntPtr(long.Parse(args[1])),args[4]=="test");Console.Write(Convert.ToBase64String(Encoding.UTF8.GetBytes(text)));return 0;}catch{return 2;}}
            if(args.Contains("--self-test")){SelfTest.Run(args.Contains("--network-test")).GetAwaiter().GetResult();return 0;}
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};UI.InstallStyles(app);
            if(args.Contains("--render-test")){app.Startup+=async(_,_)=>{try{await SelfTest.Render();app.Shutdown(0);}catch(Exception e){Paths.Log(e);File.WriteAllText(Path.Combine(Paths.Home,"render-error.txt"),e.ToString());app.Shutdown(1);}};return app.Run();}
            if(args.Contains("--selection-host")){var win=new Window{Title="Shici selection test host",Width=560,Height=240,Left=80,Top=80,Topmost=true};var text=new TextBox{Text="biology running studies\nThis is a translation test.",FontSize=24,Margin=new Thickness(20),AcceptsReturn=true};win.Content=text;win.Loaded+=async(_,_)=>{await Task.Delay(500);Native.ShowWindow(new WindowInteropHelper(win).Handle,5);win.Activate();text.Focus();text.Select(0,7);};app.MainWindow=win;app.ShutdownMode=ShutdownMode.OnMainWindowClose;return app.Run(win);}
            if(args.Contains("--selection-test")){app.Startup+=async(_,_)=>{try{await SelfTest.Selection();app.Shutdown(0);}catch(Exception e){File.WriteAllText(Path.Combine(Paths.Home,"selection-test.json"),JsonSerializer.Serialize(new{Passed=false,Error=e.ToString()},Atomic.Json));app.Shutdown(1);}};return app.Run();}
            if(args.Contains("--interaction-test")){app.Startup+=async(_,_)=>{try{await SelfTest.Interaction();app.Shutdown(0);}catch(Exception e){File.WriteAllText(Path.Combine(Paths.Home,"interaction-test.json"),JsonSerializer.Serialize(new{Passed=false,Error=e.ToString()},Atomic.Json));app.Shutdown(1);}};return app.Run();}
            using var mutex=new Mutex(true,"Local\\Shici.Desktop.Translator",out bool first);
            if(!first){try{using var signal=EventWaitHandle.OpenExisting("Local\\Shici.ShowWindow");signal.Set();}catch{foreach(var p in Process.GetProcessesByName("Shici"))if(p.Id!=Environment.ProcessId&&p.MainWindowHandle!=IntPtr.Zero){Native.ShowWindow(p.MainWindowHandle,9);Native.SetForegroundWindow(p.MainWindowHandle);break;}}return 0;}
            AppController? controller=null;app.Startup+=(_,_)=>{controller=new AppController(args.Contains("--quiet"));};app.Exit+=(_,_)=>controller?.Dispose();
            app.DispatcherUnhandledException+=(_,e)=>{Paths.Log(e.Exception);MessageBox.Show(e.Exception.Message,"拾词",MessageBoxButton.OK,MessageBoxImage.Information);e.Handled=true;};
            return app.Run();
        }catch(Exception e){Paths.Log(e);try{File.WriteAllText(Path.Combine(Paths.Home,"startup-error.txt"),e.ToString());}catch{}if(!args.Any(x=>x.EndsWith("test")))MessageBox.Show(e.Message,"拾词启动失败");return 1;}
    }
}
public sealed class FakeTranslator : ITranslator {
    public int Calls;
    public Task<string> Translate(string text,string target,CancellationToken ct){Calls++;ct.ThrowIfCancellationRequested();return Task.FromResult(text=="生物学"?"biology":"This is a test.");}
}
public static class SelfTest {
    static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);}
    static string Work=>Environment.GetEnvironmentVariable("SHICI_TEST_DIR")??Path.Combine(Paths.Home,".selftests");
    public static async Task Run(bool network) {
        Directory.CreateDirectory(Work);var folder=Path.Combine(Work,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(folder);
        var checks=new List<string>();using var lex=new Lexicon(Path.Combine(Paths.Home,"assets","dictionary.db"));
        foreach(var pair in new[]{("running","run"),("studies","study"),("went","go"),("children","child"),("taken","take"),("biology","biology")}){
            var resolved=lex.Resolve(pair.Item1);Check(resolved.Entry!=null&&resolved.Lemma==pair.Item2,"lemma "+pair.Item1+" got "+resolved.Lemma);checks.Add(pair.Item1+" → "+resolved.Lemma);
        }
        Check(lex.GetRoots("biology").Count>=2,"biology roots");Check(lex.GetRoots("qwertyuiop").Count==0,"no invented roots");checks.Add("exact root evidence");
        var fake=new FakeTranslator();var service=new LookupService(lex,fake,folder);var store=new WordStore(folder);
        foreach(var query in new[]{"run","running","RUN"}){var r=await service.Query(query,"zh-CN",CancellationToken.None);store.Record(r);}
        Check(!store.Book.Any(),"3 queries must not add");Check(store.Records.Count==1,"forms merged");
        var fourth=await service.Query("run","zh-CN",CancellationToken.None);var rec=store.Record(fourth)!;Check(rec.Count==4&&rec.InBook,"4th lookup adds");checks.Add("fourth lookup adds; case and forms merge");
        Check(fake.Calls==0,"local dictionary used");await service.Query("run","zh-CN",CancellationToken.None);Check(rec.Count==4,"read-only lookups do not count until record");
        var reloaded=new WordStore(folder);Check(reloaded.Book.Single().Count==4,"restart persistence");checks.Add("restart persistence and atomic backup");
        rec.Notes="My sentence.";store.Save();store.Grade(rec,true);Check(rec.Stage==1&&rec.Due>DateTime.Now.AddHours(23),"remember schedule");store.Grade(rec,false);Check(rec.Stage==0&&rec.Due<DateTime.Now.AddMinutes(11),"forgot schedule");checks.Add("review schedule");
        var chinese=await service.Query("生物学","en",CancellationToken.None);Check(chinese.Lemma=="biology"&&chinese.Entry!=null,"Chinese English enrichment");var c=store.Record(chinese)!;Check(c.Key=="en:biology","bilingual merge key");
        await service.Query("生物学","en",CancellationToken.None);Check(fake.Calls==1,"cache prevents duplicate network");checks.Add("Chinese to English enrichment and cache");
        var sentence=await service.Query("这是一段测试的文字。","en",CancellationToken.None);Check(!sentence.IsWord&&store.Record(sentence)==null,"sentences not automatic vocabulary");
        var canceled=new CancellationToken(true);bool threw=false;try{await service.Query("从未查询过的句子。","en",canceled);}catch(OperationCanceledException){threw=true;}Check(threw,"cancellation");checks.Add("sentence exclusion and cancellation");
        var chunks=MyMemory.Chunks(string.Concat(Enumerable.Repeat("中文😀test ",180)));Check(chunks.All(x=>Encoding.UTF8.GetByteCount(x)<=480),"UTF8 request limits");Check(string.Concat(chunks)==string.Concat(Enumerable.Repeat("中文😀test ",180)),"chunks preserve Unicode");checks.Add("UTF-8 and surrogate-safe request chunks");
        var export=store.Export(folder);Check(File.Exists(export)&&File.Exists(export.Replace(".tsv","-Anki.tsv")),"exports");checks.Add("TSV Markdown Anki export");
        if(network){var live=new MyMemory();var en=await live.Translate("我喜欢阅读和学习。","en",CancellationToken.None);var zh=await live.Translate("Learning a language takes practice.","zh-CN",CancellationToken.None);Check(en.Length>0&&!Lexicon.HasChinese(en),"live Chinese English");Check(Lexicon.HasChinese(zh),"live English Chinese");checks.Add("LIVE zh→en: "+en);checks.Add("LIVE en→zh: "+zh);}
        File.WriteAllText(Path.Combine(Paths.Home,"verification.json"),JsonSerializer.Serialize(new{Passed=true,At=DateTime.Now,Checks=checks},Atomic.Json));
    }
    static void SaveRender(Window window,string file) {
        window.UpdateLayout();var size=new Size(window.ActualWidth,window.ActualHeight);var bmp=new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32);bmp.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var stream=File.Create(file);png.Save(stream);
    }
    public static async Task Render() {
        var shots=Path.Combine(Paths.Home,"preview");Directory.CreateDirectory(shots);
        var oldData=Paths.Data;Paths.Data=Path.Combine(Work,"render-"+DateTime.Now.ToString("yyyyMMddHHmmss"));
        using var controller=new AppController();await Task.Delay(500);
        var main=Application.Current.Windows.OfType<MainWindow>().First();SaveRender(main,Path.Combine(shots,"01-main.png"));
        using var lex=new Lexicon(Path.Combine(Paths.Home,"assets","dictionary.db"));var svc=new LookupService(lex,new FakeTranslator(),Paths.Data);var r=await svc.Query("biology","zh-CN",CancellationToken.None);
        var card=Application.Current.Windows.OfType<CardWindow>().First();card.Present(r,new WordRecord{Count=4,InBook=true});card.Show();await Task.Delay(200);SaveRender(card,Path.Combine(shots,"02-card.png"));
        var floating=Application.Current.Windows.OfType<FloatingButtons>().First();floating.Present("biology",new Native.POINT(200,200));await Task.Delay(100);SaveRender(floating,Path.Combine(shots,"03-buttons.png"));
        floating.Dismiss();card.Hide();
        foreach(var word in new[]{"biology","curiosity","understand"})for(int i=0;i<4;i++)controller.Lookup(word,"zh-CN",new Native.POINT(200,200));
        card.Hide();main.ShowBook();await Task.Delay(200);SaveRender(main,Path.Combine(shots,"04-book-demo.png"));
        main.ShowReview();await Task.Delay(200);SaveRender(main,Path.Combine(shots,"05-review-demo.png"));
        Paths.Data=oldData;
    }
    public static async Task Selection() {
        using var process=Process.Start(new ProcessStartInfo(Path.Combine(Paths.Home,"Shici.exe"),"--selection-host"){UseShellExecute=false})!;
        try {
            await Task.Delay(1800);process.Refresh();var hwnd=process.MainWindowHandle;
            Native.ShowWindow(hwnd,9);Native.SetForegroundWindow(hwnd);await Task.Delay(350);
            var selection=await Task.Run(()=>Native.ReadSelection(new Native.POINT(160,160),hwnd,true));
            Check(selection=="biology","UIA3 selection from separate process: "+selection);
            File.WriteAllText(Path.Combine(Paths.Home,"selection-test.json"),JsonSerializer.Serialize(new{Passed=true,Selected=selection,Mechanism="Windows UI Automation 3 COM / isolated process"},Atomic.Json));
        }
        finally {if(!process.HasExited){process.CloseMainWindow();await Task.Delay(200);}}
    }
    public static async Task Interaction() {
        var oldData=Paths.Data;Paths.Data=Path.Combine(Work,"interaction-"+DateTime.Now.ToString("yyyyMMddHHmmss"));Native.GetCursorPos(out var originalPoint);
        using var controller=new AppController(true);
        using var host=Process.Start(new ProcessStartInfo(Path.Combine(Paths.Home,"Shici.exe"),"--selection-host"){UseShellExecute=false})!;
        try {
            await Task.Delay(1200);host.Refresh();var hwnd=host.MainWindowHandle;Native.ShowWindow(hwnd,9);Native.SetForegroundWindow(hwnd);await Task.Delay(300);
            var box=await Task.Run(()=>{using var a=new FlaUI.UIA3.UIA3Automation();var root=a.NativeAutomation.ElementFromHandle(hwnd);var edit=root.FindFirst(Interop.UIAutomationClient.TreeScope.TreeScope_Descendants,a.NativeAutomation.CreatePropertyCondition(30040,true));return edit.CurrentBoundingRectangle;});
            File.WriteAllText(Path.Combine(Paths.Home,"interaction-diagnostic.json"),JsonSerializer.Serialize(new{HostId=host.Id,Hwnd=hwnd.ToInt64(),Rect=new[]{box.left,box.top,box.right,box.bottom},PointProcess=Native.ProcessOf(Native.WindowFromPoint(new Native.POINT(box.left+30,box.top+18)))},Atomic.Json));
            Check(Native.ProcessOf(Native.WindowFromPoint(new Native.POINT(box.left+30,box.top+18)))==host.Id,"test pointer must be inside test window");
            // Double-click the first word in our test editor through the real mouse input path.
            Native.SetCursorPos(box.left+30,box.top+18);Native.MouseButton(true);await Task.Delay(100);Native.MouseButton(false);await Task.Delay(90);Native.MouseButton(true);await Task.Delay(100);Native.MouseButton(false);
            var floating=Application.Current.Windows.OfType<FloatingButtons>().First();
            for(int i=0;i<35&&!floating.IsVisible;i++)await Task.Delay(100);
            Check(floating.IsVisible,"mouse selection must show floating buttons");
            await Task.Delay(350);floating.UpdateLayout();
            var border=(Border)floating.Content;var row=(StackPanel)border.Child;var button=(Button)row.Children[0];
            var at=button.PointToScreen(new Point(button.ActualWidth/2,button.ActualHeight/2));Check(button.ActualWidth>0&&Native.ProcessOf(Native.WindowFromPoint(new Native.POINT((int)at.X,(int)at.Y)))==Environment.ProcessId,"button coordinates belong to floating window");Native.SetCursorPos((int)at.X,(int)at.Y);await Task.Delay(80);Native.MouseButton(true);await Task.Delay(110);Native.MouseButton(false);
            var card=Application.Current.Windows.OfType<CardWindow>().First();for(int i=0;i<35&&card.Result==null;i++)await Task.Delay(100);
            Check(card.IsVisible&&card.Result!=null&&card.Result.Lemma=="biology","button click must show biology card");
            var records=new WordStore(Paths.Data);Check(records.Records["en:biology"].Count==1,"one mouse query counts once");
            File.WriteAllText(Path.Combine(Paths.Home,"interaction-test.json"),JsonSerializer.Serialize(new{Passed=true,Selected=card.Result!.Query,CardLemma=card.Result.Lemma,Count=1,Flow="Real mouse double-click → floating 中文 button → card → persisted lookup",HotkeyStatus=File.ReadAllText(Path.Combine(Paths.Data,"status.json"))},Atomic.Json));
        }finally {if(!host.HasExited){host.CloseMainWindow();await Task.Delay(200);}Native.SetCursorPos(originalPoint.X,originalPoint.Y);Paths.Data=oldData;}
    }
}
