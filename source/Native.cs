using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Text;
using System.IO;
using UIA = Interop.UIAutomationClient;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Shici;

public static class Native {
    [StructLayout(LayoutKind.Sequential)] public struct POINT {public int X,Y;public POINT(int x,int y){X=x;Y=y;}}
    [DllImport("user32.dll")]public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]public static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")]public static extern uint GetDoubleClickTime();
    [DllImport("user32.dll",SetLastError=true)]public static extern bool RegisterHotKey(IntPtr window,int id,uint mods,uint key);
    [DllImport("user32.dll")]public static extern bool UnregisterHotKey(IntPtr window,int id);
    [DllImport("user32.dll")]public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")]public static extern int GetWindowLong(IntPtr hwnd,int index);
    [DllImport("user32.dll")]public static extern int SetWindowLong(IntPtr hwnd,int index,int value);
    [DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")]public static extern bool ShowWindow(IntPtr hwnd,int cmd);
    [DllImport("user32.dll")]public static extern uint SendInput(uint n,INPUT[] inputs,int size);
    [StructLayout(LayoutKind.Sequential)]public struct INPUT {public uint type; public INPUTUNION data;}
    [StructLayout(LayoutKind.Explicit)]public struct INPUTUNION {[FieldOffset(0)]public KEYBDINPUT keyboard;[FieldOffset(0)]public MOUSEINPUT mouse;}
    [StructLayout(LayoutKind.Sequential)]public struct KEYBDINPUT {public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
    [StructLayout(LayoutKind.Sequential)]public struct MOUSEINPUT {public int dx,dy;public uint mouseData,flags,time;public UIntPtr extra;}
    public static bool CopyShortcut() {
        INPUT K(ushort key,bool up)=>new INPUT{type=1,data=new INPUTUNION{keyboard=new KEYBDINPUT{vk=key,flags=up?2u:0u}}};
        var inputs=new[]{K(0x11,false),K(0x43,false),K(0x43,true),K(0x11,true)};
        return SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<INPUT>())==inputs.Length;
    }
    public static void MouseButton(bool down) {var inputs=new[]{new INPUT{type=0,data=new INPUTUNION{mouse=new MOUSEINPUT{flags=down?2u:4u}}}};SendInput(1,inputs,Marshal.SizeOf<INPUT>());}
    public static uint ProcessOf(IntPtr h){GetWindowThreadProcessId(h,out var p);return p;}
    public static string SelectedText(UIA.IUIAutomationElement? el,UIA.IUIAutomationTreeWalker walker) {
        if(el==null)return "";
        for(int i=0;i<7&&el!=null;i++) {
            if(el.CurrentIsPassword!=0)return "\0PASSWORD";
            try {
                var pattern=(UIA.IUIAutomationTextPattern)el.GetCurrentPattern(10014);
                if(pattern!=null) {var ranges=pattern.GetSelection();var parts=new System.Collections.Generic.List<string>();for(int j=0;j<ranges.Length;j++)parts.Add(ranges.GetElement(j).GetText(1201));var text=string.Join(" ",parts).Trim();if(text.Length>0&&text.Length<=1200)return text;}
            } catch(COMException) {
            }
            el=walker.GetParentElement(el);
        }
        return "";
    }
    public static string ReadSelectionDirect(POINT point,IntPtr foreground,bool test=false) {
        if(foreground==IntPtr.Zero||(!test&&GetForegroundWindow()!=foreground))return "";
        using var automation=new FlaUI.UIA3.UIA3Automation();var native=automation.NativeAutomation;var walker=native.ControlViewWalker;
        var focused=native.GetFocusedElement();
        if(focused!=null&&focused.CurrentIsPassword!=0)return "\0PASSWORD";
        string text="";
        if(focused!=null&&focused.CurrentProcessId==ProcessOf(foreground))text=SelectedText(focused,walker);
        if(text.Length==0) {
            var under=native.ElementFromPoint(new UIA.tagPOINT{x=point.X,y=point.Y});
            if(under!=null&&under.CurrentProcessId==ProcessOf(foreground))text=SelectedText(under,walker);
        }
        if(test&&text.Length==0){var root=native.ElementFromHandle(foreground);var edit=root.FindFirst(UIA.TreeScope.TreeScope_Descendants,native.CreatePropertyCondition(30040,true));if(edit!=null)text=SelectedText(edit,walker);}
        return text;
    }
    public static string ReadSelection(POINT point,IntPtr foreground,bool test=false) {
        var info=new ProcessStartInfo(Path.Combine(Paths.Home,"Shici.exe")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var s in new[]{"--capture",foreground.ToInt64().ToString(),point.X.ToString(),point.Y.ToString(),test?"test":"normal"})info.ArgumentList.Add(s);
        using var helper=Process.Start(info)!;var output=helper.StandardOutput.ReadToEndAsync();
        if(!helper.WaitForExit(1800)){try{helper.Kill();}catch{}return "";}
        if(helper.ExitCode!=0)return "";
        try{return Encoding.UTF8.GetString(Convert.FromBase64String(output.GetAwaiter().GetResult().Trim()));}catch{return "";}
    }
    public static Point PopupPoint(Window window,POINT point,double width,double height) {
        // Screen coordinates are physical pixels; WPF window dimensions are DIPs.
        var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(point.X,point.Y));
        var work=screen.WorkingArea;
        var source=PresentationSource.FromVisual(window);var m=source?.CompositionTarget?.TransformFromDevice??System.Windows.Media.Matrix.Identity;
        var p=m.Transform(new Point(point.X+12,point.Y+20));var a=m.Transform(new Point(work.Left,work.Top));var b=m.Transform(new Point(work.Right,work.Bottom));
        return new Point(Math.Clamp(p.X,a.X+6,Math.Max(a.X+6,b.X-width-6)),Math.Clamp(p.Y,a.Y+6,Math.Max(a.Y+6,b.Y-height-6)));
    }
}

public sealed class SelectionWatcher : IDisposable {
    readonly DispatcherTimer timer;readonly Action<string,Native.POINT> selected;readonly Action outside;
    bool wasDown;Native.POINT down,lastUp;DateTime lastUpAt=DateTime.MinValue;int busy,generation;bool paused;
    public bool Paused {get=>paused;set{paused=value;generation++;}}
    public SelectionWatcher(Action<string,Native.POINT> selected,Action outside) {
        this.selected=selected;this.outside=outside;
        timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(45)};timer.Tick+=Tick;timer.Start();
    }
    async void Tick(object? sender,EventArgs e) {
        bool pressed=(Native.GetAsyncKeyState(1)&0x8000)!=0;
        if(pressed&&!wasDown) {Native.GetCursorPos(out down);generation++;if(Native.ProcessOf(Native.WindowFromPoint(down))!=Environment.ProcessId)outside();}
        bool released=!pressed&&wasDown;wasDown=pressed;
        if(!released||paused)return;
        Native.GetCursorPos(out var point);var now=DateTime.Now;
        if(Native.ProcessOf(Native.WindowFromPoint(point))==Environment.ProcessId)return;
        bool drag=Math.Abs(point.X-down.X)+Math.Abs(point.Y-down.Y)>5;
        bool doubleClick=(now-lastUpAt).TotalMilliseconds<=Native.GetDoubleClickTime()+70 && Math.Abs(point.X-lastUp.X)+Math.Abs(point.Y-lastUp.Y)<12;
        lastUp=point;lastUpAt=now;
        if(!drag&&!doubleClick)return;
        int token=generation;var foreground=Native.GetForegroundWindow();
        if(Native.ProcessOf(foreground)==Environment.ProcessId||Interlocked.CompareExchange(ref busy,1,0)!=0)return;
        bool releaseBusy=true;
        try {
            await Task.Delay(180);
            if(token!=generation||paused)return;
            var task=Task.Run(()=>{try{return Native.ReadSelection(point,foreground);}catch{return "";}});
            // If a provider stalls, do not create additional workers. The completion releases the guard.
            if(await Task.WhenAny(task,Task.Delay(1800))!=task) {releaseBusy=false;_=task.ContinueWith(_=>Interlocked.Exchange(ref busy,0));return;}
            var text=await task;
            if(token==generation&&!paused&&Native.GetForegroundWindow()==foreground&&!string.IsNullOrWhiteSpace(text)&&text!="\0PASSWORD")selected(text,point);
        } finally {
            // Typical providers complete within a few milliseconds. Stalled task guard is handled above.
            if(releaseBusy)Interlocked.Exchange(ref busy,0);
        }
    }
    public void Dispose(){timer.Stop();generation++;}
}

public sealed class Hotkeys : IDisposable {
    readonly HwndSource source;readonly Action<string,Native.POINT> selected;readonly Action show;readonly Action<string> message;
    bool copying;public bool Registered {get;} public string Shortcut {get;}="Ctrl+Alt+T";
    public bool BookRegistered {get;} public string BookShortcut {get;}="Ctrl+Alt+B";
    public Hotkeys(Action<string,Native.POINT> selected,Action show,Action<string> message) {
        this.selected=selected;this.show=show;this.message=message;
        source=new HwndSource(new HwndSourceParameters("ShiciHotkeys"){Width=0,Height=0,WindowStyle=0,ParentWindow=new IntPtr(-3)});
        source.AddHook(Hook);Registered=Native.RegisterHotKey(source.Handle,1,0x4000|0x0001|0x0002,0x54);
        if(!Registered){Registered=Native.RegisterHotKey(source.Handle,1,0x4000|0x0001|0x0002|0x0004,0x54);Shortcut="Ctrl+Alt+Shift+T";}
        BookRegistered=Native.RegisterHotKey(source.Handle,2,0x4000|0x0001|0x0002,0x42);
        if(!BookRegistered){BookRegistered=Native.RegisterHotKey(source.Handle,2,0x4000|0x0001|0x0002|0x0004,0x42);BookShortcut="Ctrl+Alt+Shift+B";}
    }
    IntPtr Hook(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled) {
        if(msg==0x0312){handled=true;if(w.ToInt32()==1)_=Capture();else show();}return IntPtr.Zero;
    }
    async Task Capture() {
        if(copying)return;copying=true;
        try {
            var foreground=Native.GetForegroundWindow();Native.GetCursorPos(out var point);
            if(Native.ProcessOf(foreground)==Environment.ProcessId){message("请先在其他软件里选中文字，再按 Ctrl+Alt+T。");return;}
            // Read accessible selection first; no clipboard interaction when supported.
            var read=Task.Run(()=>{try {return Native.ReadSelection(point,foreground);}catch{return "";}});
            if(await Task.WhenAny(read,Task.Delay(2300))!=read){message("当前软件响应较慢。请复制选中文字，再到拾词输入框粘贴。");return;}
            var text=await read;if(text=="\0PASSWORD")return;
            if(text.Length>0){selected(text,point);return;}
            // Explicit shortcut fallback only. Snapshot every clipboard format before synthetic Ctrl+C.
            var saved=new DataObject();bool wasEmpty=true;
            try {var old=Clipboard.GetDataObject();if(old!=null)foreach(var format in old.GetFormats(false)){var data=old.GetData(format,false);if(data!=null){saved.SetData(format,data);wasEmpty=false;}}}
            catch {message("剪贴板正忙，请稍后再试。");return;}
            for(int i=0;i<30&&((Native.GetAsyncKeyState(0x11)&0x8000)!=0||(Native.GetAsyncKeyState(0x12)&0x8000)!=0);i++)await Task.Delay(30);
            if(Native.GetForegroundWindow()!=foreground)return;
            uint before=Native.GetClipboardSequenceNumber();if(!Native.CopyShortcut()){message("无法从当前窗口取词，可在拾词中手动粘贴。");return;}
            for(int i=0;i<25&&Native.GetClipboardSequenceNumber()==before;i++)await Task.Delay(25);
            uint captured=Native.GetClipboardSequenceNumber();
            if(captured==before){message("没有读到选中文字，请复制后到拾词输入框粘贴。");return;}
            try {text=Clipboard.ContainsText()?Clipboard.GetText():"";}
            finally {if(Native.GetClipboardSequenceNumber()==captured){if(wasEmpty)Clipboard.Clear();else Clipboard.SetDataObject(saved,true);}}
            if(!string.IsNullOrWhiteSpace(text)&&text.Length<=1200)selected(text,point);else message("请选择不超过 1200 字符的可复制文字。");
        }catch(Exception e){Paths.Log(e);message("取词未成功，请重试或在主窗口粘贴文字。");}finally{copying=false;}
    }
    public void Dispose(){Native.UnregisterHotKey(source.Handle,1);Native.UnregisterHotKey(source.Handle,2);source.Dispose();}
}
