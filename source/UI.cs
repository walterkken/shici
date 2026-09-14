using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;

namespace Shici;

public static class UI {
    public static SolidColorBrush Ink=B("#193D35"),Muted=B("#7A8780"),Paper=B("#F7F7F1"),White=B("#FFFFFF"),Line=B("#E4E9E2"),Green=B("#2C6955"),Pale=B("#ECF3EB"),Orange=B("#B98239");
    public static SolidColorBrush B(string hex)=>new((Color)ColorConverter.ConvertFromString(hex));
    public static TextBlock Text(string text,double size=14,Brush? color=null,bool bold=false) => new(){Text=text,FontSize=size,Foreground=color??Ink,TextWrapping=TextWrapping.Wrap,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,Margin=new Thickness(0,0,0,8),LineHeight=size*1.55};
    public static Button Button(string text,Action click,bool primary=false) {
        var b=new Button{Content=text,Padding=new Thickness(16,9,16,9),Margin=new Thickness(0,0,8,0),Background=primary?Green:White,Foreground=primary?White:Ink,BorderBrush=primary?Green:Line,BorderThickness=new Thickness(1),Cursor=Cursors.Hand,FontSize=13,MinHeight=36};
        b.Click+=(_,_)=>click();return b;
    }
    public static Border Panel(UIElement child,Thickness? padding=null,Brush? bg=null)=>new(){Child=child,Background=bg??White,Padding=padding??new Thickness(22),CornerRadius=new CornerRadius(16),BorderBrush=Line,BorderThickness=new Thickness(1),Margin=new Thickness(0,0,0,14)};
    public static StackPanel Row(params UIElement[] elements){var p=new StackPanel{Orientation=Orientation.Horizontal};foreach(var e in elements)p.Children.Add(e);return p;}
    public static void Configure(Window w) {w.FontFamily=new FontFamily("Microsoft YaHei UI");w.FontSize=14;w.Background=Paper;w.Foreground=Ink;w.Icon=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(Path.Combine(Paths.Home,"assets","shici.ico")));}
    public static void InstallStyles(Application app) {
        var template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='{x:Type Button}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Opacity' Value='0.82'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='box' Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        var style=new Style(typeof(Button));style.Setters.Add(new Setter(Control.TemplateProperty,template));app.Resources.Add(typeof(Button),style);
        var tipStyle=new Style(typeof(ToolTip));tipStyle.Setters.Add(new Setter(Control.FontSizeProperty,12.0));app.Resources.Add(typeof(ToolTip),tipStyle);
        var tabStyle=new Style(typeof(TabItem));tabStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(18,10,18,10)));tabStyle.Setters.Add(new Setter(Control.FontSizeProperty,14.0));tabStyle.Setters.Add(new Setter(Control.ForegroundProperty,Muted));tabStyle.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));tabStyle.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type TabItem}'><Border x:Name='tab' Background='{TemplateBinding Background}' CornerRadius='9' Padding='{TemplateBinding Padding}' Margin='0,0,8,0'><ContentPresenter ContentSource='Header'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='tab' Property='Background' Value='#E5EEDF'/><Setter Property='Foreground' Value='#235844'/><Setter Property='FontWeight' Value='SemiBold'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));app.Resources.Add(typeof(TabItem),tabStyle);
    }
    public static void Open(string path)=>Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
}

public sealed class FloatingButtons : Window {
    readonly Action<string,string,Native.POINT> lookup;readonly DispatcherTimer expiry;string text="";Native.POINT at;
    public FloatingButtons(Action<string,string,Native.POINT> lookup) {
        this.lookup=lookup;UI.Configure(this);Title="拾词 · 划词按钮";Width=206;Height=60;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;
        var row=UI.Row(UI.Button("中文",()=>Choose("zh-CN"),true),UI.Button("English",()=>Choose("en")));
        var border=UI.Panel(row,new Thickness(7));border.Margin=new Thickness(3);border.CornerRadius=new CornerRadius(12);Content=border;
        SourceInitialized+=(_,_)=>{var h=new WindowInteropHelper(this).Handle;Native.SetWindowLong(h,-20,Native.GetWindowLong(h,-20)|0x08000000|0x00000080);};
        expiry=new DispatcherTimer{Interval=TimeSpan.FromSeconds(12)};expiry.Tick+=(_,_)=>Dismiss();MouseEnter+=(_,_)=>expiry.Stop();MouseLeave+=(_,_)=>expiry.Start();
    }
    void Choose(string lang){expiry.Stop();Hide();lookup(text,lang,at);}
    public void Present(string selection,Native.POINT point){text=selection;at=point;Show();var p=Native.PopupPoint(this,point,Width,Height);Left=p.X;Top=p.Y;expiry.Stop();expiry.Start();}
    public void Dismiss(){expiry.Stop();Hide();}
}

public sealed class CardWindow : Window {
    readonly StackPanel body=new();readonly TextBlock status=UI.Text("",12,UI.Muted);readonly Action<Lookup> add;readonly Action book; readonly Action<string> speak;
    public bool Pinned {get;private set;} public Lookup? Result {get;private set;}
    public CardWindow(Action<Lookup> add,Action book,Action<string> speak) {
        this.add=add;this.book=book;this.speak=speak;UI.Configure(this);Title="拾词 · 单词卡";Width=474;Height=668;MinWidth=390;MinHeight=440;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResizeWithGrip;Topmost=true;ShowInTaskbar=false;AllowsTransparency=true;Background=Brushes.Transparent;
        var outer=new Grid();outer.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});outer.RowDefinitions.Add(new RowDefinition());outer.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var pin=UI.Button("固定",()=>{Pinned=!Pinned;});pin.Click+=(_,_)=>pin.Content=Pinned?"已固定":"固定";
        var bar=new DockPanel{Margin=new Thickness(0,0,0,15),LastChildFill=true};var controls=UI.Row(pin,UI.Button("×",()=>Hide()));DockPanel.SetDock(controls,Dock.Right);bar.Children.Add(controls);bar.Children.Add(UI.Text("拾词  /  WORD CARD",12,UI.Muted,true));bar.MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource is TextBlock){try{DragMove();}catch{}}};outer.Children.Add(bar);
        var scroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(0,0,8,0)};Grid.SetRow(scroll,1);outer.Children.Add(scroll);
        var foot=new StackPanel{Margin=new Thickness(0,14,0,0)};foot.Children.Add(status);foot.Children.Add(UI.Row(UI.Button("生词本",book),UI.Button("复制释义",()=>{if(Result!=null)Clipboard.SetText(Result.Translation);})));Grid.SetRow(foot,2);outer.Children.Add(foot);
        var shell=UI.Panel(outer,new Thickness(24),UI.Paper);shell.Margin=new Thickness(5);shell.Effect=new DropShadowEffect{BlurRadius=9,Opacity=.12,ShadowDepth=2};Content=shell;
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape)Hide();};
    }
    public void Place(Native.POINT point){Show();var p=Native.PopupPoint(this,point,Width,Height);Left=p.X;Top=p.Y;}
    public void Loading(string text){Result=null;body.Children.Clear();body.Children.Add(UI.Text(text,30,null,true));body.Children.Add(UI.Text("正在查词…",15,UI.Muted));status.Text="只在点击翻译后查询";}
    public void Failure(string message,Action retry){body.Children.Clear();body.Children.Add(UI.Text("暂时没有查到",26,null,true));body.Children.Add(UI.Text(message,14,UI.Muted));body.Children.Add(UI.Button("重试",retry,true));status.Text="本次未计入查询次数";}
    public void Present(Lookup r,WordRecord? record) {
        Result=r;body.Children.Clear();
        body.Children.Add(UI.Text(r.IsWord?r.Lemma:r.Query,32,null,true));
        if(!string.IsNullOrWhiteSpace(r.Entry?.Phonetic))body.Children.Add(UI.Text("/ "+r.Entry.Phonetic+" /",14,UI.Muted));
        body.Children.Add(UI.Text(r.LemmaNote,12,UI.Muted));
        var actions=UI.Row(UI.Button("朗读",()=>speak(r.Entry?.Word??r.Translation)),UI.Button(record?.InBook==true?"已在生词本":"＋ 加入生词本",()=>{add(r);status.Text="已加入生词本，可随时复习";}));body.Children.Add(actions);
        Section(r.Target=="zh-CN"?"中文释义":"ENGLISH",r.Translation);
        if(r.Target=="en"&&r.Entry!=null&&!string.IsNullOrWhiteSpace(r.Entry.Chinese))Section("中文释义",r.Entry.Chinese);
        if(r.Entry!=null) {
            if(r.Target=="zh-CN"&&!string.IsNullOrWhiteSpace(r.Entry.English)) {
                var english=new Expander{Header="英文释义 · 双语理解",Foreground=UI.Muted,Margin=new Thickness(0,6,0,12),Content=UI.Text(r.Entry.English,13,UI.Muted)};body.Children.Add(english);
            }
            var names=new Dictionary<string,string>{{"p","过去式"},{"d","过去分词"},{"i","现在分词"},{"3","第三人称"},{"s","复数"},{"r","比较级"},{"t","最高级"}};
            var forms=Lexicon.Forms(r.Entry.Exchange).Where(x=>names.ContainsKey(x.Key)).Select(x=>names[x.Key]+"  "+x.Value).ToList();
            if(forms.Count>0)Section("词形变化",string.Join("\n",forms));
        }
        body.Children.Add(UI.Text("词根 · 词缀",12,UI.Muted,true));
        if(r.Roots.Count>0)foreach(var root in r.Roots) {
            var p=new StackPanel();p.Children.Add(UI.Text(root.Root+"  ·  "+root.Kind,17,null,true));p.Children.Add(UI.Text(Lexicon.RootChinese(root.Root,root.Meaning),13));p.Children.Add(UI.Text("来源语："+root.Origin+"\n同根词："+root.Examples,11,UI.Muted));body.Children.Add(UI.Panel(p,new Thickness(15),UI.Pale));
        }
        body.Children.Add(UI.Text(r.RootNote,11,UI.Muted));body.Children.Add(UI.Text("释义来源："+r.Source,11,UI.Muted));
        status.Text=record==null?"短语 / 句子不自动计入生词本；可手动收藏":record.InBook?"已查 "+record.Count+" 次  ·  已加入生词本":"已查 "+record.Count+" 次  ·  第 4 次自动加入生词本";
    }
    void Section(string title,string value){body.Children.Add(UI.Text(title,12,UI.Muted,true));var text=UI.Text(value,15);text.Margin=new Thickness(0,0,0,18);body.Children.Add(text);}
}

public sealed class MainWindow : Window {
    readonly AppController controller;readonly WordStore store;
    readonly TabControl tabs=new(){Background=Brushes.Transparent,BorderThickness=new Thickness(0)};
    readonly StackPanel bookPanel=new(),reviewPanel=new();readonly TextBox search=new();readonly TextBlock stats=UI.Text("",13,UI.Muted);readonly TextBlock bookStats=UI.Text("",13,UI.Muted);
    readonly TextBox filter=new(){Padding=new Thickness(12,9,12,9),Margin=new Thickness(0,0,0,12)};
    readonly DataGrid table=new(){AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,HeadersVisibility=DataGridHeadersVisibility.Column,GridLinesVisibility=DataGridGridLinesVisibility.None,RowHeight=42,Background=UI.White,BorderBrush=UI.Line,SelectionMode=DataGridSelectionMode.Single,MinHeight=190,Height=275};
    readonly TextBox notes=new(){AcceptsReturn=true,MinHeight=58,MaxHeight=90,TextWrapping=TextWrapping.Wrap,Padding=new Thickness(9),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    readonly TextBlock shortcutHint=UI.Text("",12,UI.Muted);
    readonly TextBlock notice=UI.Text("",12,UI.Muted);Button pauseButton=null!;WordRecord? reviewCurrent; bool quitting;
    public MainWindow(AppController controller,WordStore store) {
        this.controller=controller;this.store=store;UI.Configure(this);Title="拾词 · 划词翻译与生词本";Width=940;Height=790;MinWidth=800;MinHeight=700;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var main=new Grid{Margin=new Thickness(30,22,30,20)};main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition());main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        pauseButton=UI.Button("暂停划词",()=>TogglePause());var top=new DockPanel{Margin=new Thickness(0,0,0,16)};var right=UI.Row(pauseButton,UI.Button("使用说明",()=>UI.Open(Path.Combine(Paths.Home,"使用说明.md")))) ;DockPanel.SetDock(right,Dock.Right);top.Children.Add(right);top.Children.Add(UI.Text("拾词   SHICI",23,null,true));main.Children.Add(top);
        tabs.Items.Add(new TabItem{Header="  开始查词  ",Content=Welcome()});tabs.Items.Add(new TabItem{Header="  我的生词本  ",Content=BuildBook()});tabs.Items.Add(new TabItem{Header="  今日复习  ",Content=BuildReview()});tabs.SelectionChanged+=(_,e)=>{if(e.Source==tabs){Refresh();if(tabs.SelectedIndex==2)NextReview();}};Grid.SetRow(tabs,1);main.Children.Add(tabs);
        notice.Text="划词取词已开启  ·  Ctrl+Alt+T 兼容取词  ·  Ctrl+Alt+B 打开生词本";Grid.SetRow(notice,2);main.Children.Add(notice);Content=main;
        Closing+=(_,e)=>{if(!quitting){e.Cancel=true;Hide();controller.Notify("拾词仍在托盘运行，可右键托盘图标退出。");}};
        Refresh();
    }
    UIElement Welcome() {
        var p=new StackPanel{Margin=new Thickness(0,20,0,0)};p.Children.Add(UI.Text("让查过的词，\n慢慢成为你的词。",35,null,true));p.Children.Add(UI.Text("轻轻一划，中英互译。反复遇见的词，替你收进生词本。",15,UI.Muted));
        var input=new StackPanel();input.Children.Add(UI.Text("手动查词",12,UI.Muted,true));search.FontSize=20;search.Padding=new Thickness(12);search.MinHeight=58;search.MaxHeight=115;search.AcceptsReturn=true;search.TextWrapping=TextWrapping.Wrap;search.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;search.BorderBrush=UI.Line;search.Margin=new Thickness(0,0,0,14);
        search.KeyDown+=(_,e)=>{if(e.Key==Key.Enter&&Keyboard.Modifiers!=ModifierKeys.Shift){e.Handled=true;Query(Lexicon.HasChinese(search.Text)?"en":"zh-CN");}};input.Children.Add(search);input.Children.Add(UI.Row(UI.Button("译为中文",()=>Query("zh-CN"),true),UI.Button("Translate to English",()=>Query("en")),UI.Button("粘贴",()=>{if(Clipboard.ContainsText())search.Text=Clipboard.GetText();})));p.Children.Add(UI.Panel(input));
        var steps=new StackPanel();steps.Children.Add(UI.Text("01  在网页、文档或编辑器里选中文字",14,null,true));steps.Children.Add(UI.Text("02  点击选区旁的「中文」或「English」",14,null,true));steps.Children.Add(UI.Text("03  查看卡片；同词第 4 次成功查询自动入本",14,null,true));shortcutHint.Text="未出现按钮时：保持选区，按 Ctrl+Alt+T。图片或扫描 PDF 需先识别文字。";steps.Children.Add(shortcutHint);p.Children.Add(UI.Panel(steps,bg:UI.Pale));
        p.Children.Add(stats);p.Children.Add(UI.Text("词典优先本地查询。句子和中译英使用 MyMemory；只有点击翻译才联网。\n查询记录只保存在本机。免费联网翻译有每日额度，结果可能有误。",11,UI.Muted));return new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    }
    void Query(string lang){Native.GetCursorPos(out var point);controller.Lookup(search.Text,lang,point);}
    UIElement BuildBook() {
        bookPanel.Margin=new Thickness(0,20,0,0);bookPanel.Children.Add(UI.Text("那些值得再见一面的词",25,null,true));bookPanel.Children.Add(bookStats);
        filter.ToolTip="筛选单词、中文释义或笔记";filter.TextChanged+=(_,_)=>RefreshBook();bookPanel.Children.Add(filter);
        table.Columns.Add(new DataGridTextColumn{Header="单词",Binding=new Binding("Word"),Width=145});table.Columns.Add(new DataGridTextColumn{Header="中文释义",Binding=new Binding("Meaning"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});table.Columns.Add(new DataGridTextColumn{Header="次数",Binding=new Binding("Count"),Width=52});table.Columns.Add(new DataGridTextColumn{Header="下次复习",Binding=new Binding("Due"){StringFormat="MM-dd HH:mm"},Width=105});
        table.MouseDoubleClick+=(_,_)=>OpenSelected();table.SelectionChanged+=(_,_)=>{if(table.SelectedItem is WordRecord r)notes.Text=r.Notes;else notes.Text="";};bookPanel.Children.Add(table);
        var buttons=UI.Row(UI.Button("查看卡片",OpenSelected,true),UI.Button("导出生词本",()=>{try {var file=store.Export(Path.Combine(Paths.Home,"exports"));Notify("已导出 TSV、Markdown 和 Anki 文件");UI.Open(Path.GetDirectoryName(file)!);}catch(Exception e){Notify(e.Message);}}),UI.Button("打开数据目录",()=>UI.Open(Paths.Data)));buttons.Margin=new Thickness(0,12,0,12);bookPanel.Children.Add(buttons);
        bookPanel.Children.Add(UI.Text("个人笔记 · 可以记下原句、搭配或你自己的理解",12,UI.Muted));bookPanel.Children.Add(notes);var save=UI.Button("保存笔记",()=>{if(table.SelectedItem is WordRecord r){r.Notes=notes.Text;store.Save();Notify("笔记已保存");}});save.HorizontalAlignment=HorizontalAlignment.Left;save.Margin=new Thickness(0,8,0,0);bookPanel.Children.Add(save);
        return new ScrollViewer{Content=bookPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    }
    UIElement BuildReview(){reviewPanel.Margin=new Thickness(0,24,0,0);return new ScrollViewer{Content=reviewPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};}
    public void NextReview() {
        reviewPanel.Children.Clear();reviewCurrent=store.Book.Where(x=>x.Due<=DateTime.Now).OrderBy(x=>x.Due).ThenByDescending(x=>x.Count).FirstOrDefault();
        reviewPanel.Children.Add(UI.Text("给记忆一点间隔",27,null,true));
        var count=store.Book.Count(x=>x.Due<=DateTime.Now);reviewPanel.Children.Add(UI.Text("待复习 "+count+" 个  ·  先回忆，再翻面",13,UI.Muted));
        if(reviewCurrent==null) {reviewPanel.Children.Add(UI.Panel(UI.Text(store.Book.Any()?"今天的复习完成了。\n下次打开时，到期的词会在这里等你。":"生词本还是空的。\n同一个词查到第 4 次，或在卡片里手动加入，就能开始复习。",20),new Thickness(35)));return;}
        var r=reviewCurrent;var face=new StackPanel();face.Children.Add(UI.Text(r.Word,42,null,true));face.Children.Add(UI.Text("你已经遇见它 "+r.Count+" 次",13,UI.Muted));
        var answer=new StackPanel{Visibility=Visibility.Collapsed,Margin=new Thickness(0,20,0,0)};answer.Children.Add(UI.Text(r.Meaning,19));if(r.Notes.Length>0)answer.Children.Add(UI.Text("我的笔记："+r.Notes,14,UI.Muted));
        var root=r.Detail?.Roots.FirstOrDefault();if(root!=null)answer.Children.Add(UI.Text(root.Root+" · "+Lexicon.RootChinese(root.Root,root.Meaning),14));
        answer.Children.Add(UI.Row(UI.Button("还没记住 · 10 分钟后",()=>{store.Grade(r,false);NextReview();}),UI.Button("记住了 · "+(WordStore.ReviewDue(r.Stage,DateTime.Today)-DateTime.Today).Days+" 天后",()=>{store.Grade(r,true);NextReview();},true)));
        var flip=UI.Button("翻面 · 看答案",()=>answer.Visibility=Visibility.Visible,true);flip.HorizontalAlignment=HorizontalAlignment.Left;flip.Margin=new Thickness(0,18,0,0);face.Children.Add(flip);face.Children.Add(answer);reviewPanel.Children.Add(UI.Panel(face,new Thickness(34)));
        reviewPanel.Children.Add(UI.Text("复习间隔：1 → 3 → 7 → 14 → 30 → 60 天。遗忘的词会提前回来。\n这是轻量间隔复习规则；不使用 Anki 的 FSRS 模型。",12,UI.Muted));
    }
    void OpenSelected(){if(table.SelectedItem is WordRecord r && r.Detail!=null){Native.GetCursorPos(out var point);controller.ShowSaved(r,point);}}
    void TogglePause(){controller.Watcher.Paused=!controller.Watcher.Paused;pauseButton.Content=controller.Watcher.Paused?"恢复划词":"暂停划词";Notify(controller.Watcher.Paused?"划词已暂停，手动查词和快捷键仍可用":"划词已开启");}
    public void Refresh(){stats.Text="本地词典 770,611 条  ·  生词本 "+store.Book.Count()+" 个  ·  今日待复习 "+store.Book.Count(x=>x.Due<=DateTime.Now)+" 个";RefreshBook();}
    void RefreshBook(){var q=filter.Text.Trim();table.ItemsSource=store.Book.Where(r=>q.Length==0||r.Word.Contains(q,StringComparison.OrdinalIgnoreCase)||r.Meaning.Contains(q)||r.Notes.Contains(q)).OrderByDescending(r=>r.Count).ThenByDescending(r=>r.LastSeen).ToList();bookStats.Text="共 "+store.Book.Count()+" 个词 · 双击查看卡片；按列标题排序";}
    public void ShowBook(){Show();WindowState=WindowState.Normal;Activate();tabs.SelectedIndex=1;Refresh();}
    public void ShowReview(){Show();tabs.SelectedIndex=2;NextReview();}
    public void Notify(string text){notice.Text=text;}
    public void SetShortcut(string shortcut,string bookShortcut){shortcutHint.Text="未出现按钮时：保持选区，按 "+shortcut+"。图片或扫描 PDF 需先识别文字。";notice.Text="划词取词已开启  ·  "+shortcut+" 兼容取词  ·  "+bookShortcut+" 生词本";}
    public void Quit(){quitting=true;Close();}
}

public sealed class AppController : IDisposable {
    readonly Lexicon lexicon;readonly WordStore store;readonly LookupService service;readonly FloatingButtons floating;readonly CardWindow card;readonly MainWindow main;readonly Hotkeys hotkeys;readonly System.Windows.Forms.NotifyIcon tray;
    readonly EventWaitHandle reopen;readonly RegisteredWaitHandle reopenWait;
    CancellationTokenSource? request;int requestId;public SelectionWatcher Watcher {get;}
    public AppController(bool quiet=false) {
        Directory.CreateDirectory(Paths.Data);lexicon=new Lexicon(Path.Combine(Paths.Home,"assets","dictionary.db"));store=new WordStore(Paths.Data);service=new LookupService(lexicon,new MyMemory(),Paths.Data);
        floating=new FloatingButtons(Lookup);card=new CardWindow(Add,()=>main!.ShowBook(),Speak);
        Watcher=new SelectionWatcher(floating.Present,()=>{floating.Dismiss();if(!card.Pinned)card.Hide();});main=new MainWindow(this,store);
        reopen=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Shici.ShowWindow");reopenWait=ThreadPool.RegisterWaitForSingleObject(reopen,(_,_)=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>{main.Show();main.WindowState=WindowState.Normal;main.Activate();})),null,Timeout.Infinite,false);
        hotkeys=new Hotkeys(floating.Present,main.ShowBook,Notify);
        tray=new System.Windows.Forms.NotifyIcon{Icon=new System.Drawing.Icon(Path.Combine(Paths.Home,"assets","shici.ico")),Text="拾词 · 划词翻译",Visible=true};
        var menu=new System.Windows.Forms.ContextMenuStrip();menu.Items.Add("打开拾词",null,(_,_)=>{main.Show();main.Activate();});menu.Items.Add("我的生词本",null,(_,_)=>main.ShowBook());
        var pause=new System.Windows.Forms.ToolStripMenuItem("暂停划词"){CheckOnClick=true};pause.CheckedChanged+=(_,_)=>Watcher.Paused=pause.Checked;menu.Items.Add(pause);menu.Items.Add("退出",null,(_,_)=>Application.Current.Shutdown());tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>{main.Show();main.Activate();};
        main.SetShortcut(hotkeys.Shortcut,hotkeys.BookRegistered?hotkeys.BookShortcut:"托盘");
        File.WriteAllText(Path.Combine(Paths.Data,"status.json"),System.Text.Json.JsonSerializer.Serialize(new{SelectionShortcut=hotkeys.Shortcut,SelectionShortcutRegistered=hotkeys.Registered,BookShortcut=hotkeys.BookShortcut,BookShortcutRegistered=hotkeys.BookRegistered,Started=DateTime.Now},Atomic.Json));
        if(!quiet)main.Show();if(!hotkeys.Registered)Notify("取词快捷键已被其他软件占用；划词和手动查询仍可使用。");
    }
    public async void Lookup(string text,string target,Native.POINT point) {
        if(string.IsNullOrWhiteSpace(text)){Notify("请先选中或输入文字。");return;}
        int id=++requestId;request?.Cancel();request?.Dispose();request=new CancellationTokenSource();var ct=request.Token;
        floating.Dismiss();card.Loading(text);card.Place(point);
        try {var result=await service.Query(text,target,ct);if(id!=requestId||ct.IsCancellationRequested)return;var record=store.Record(result);card.Present(result,record);main.Refresh();}
        catch(OperationCanceledException){if(id==requestId)card.Failure("请求超时，请检查网络后重试。",()=>Lookup(text,target,point));}
        catch(Exception e){Paths.Log(e);if(id==requestId)card.Failure(e is HttpRequestException?"无法连接翻译服务，请检查网络。已缓存的词和本地词典仍可使用。":e.Message,()=>Lookup(text,target,point));}
    }
    void Add(Lookup result){store.Add(result);main.Refresh();}
    public void ShowSaved(WordRecord r,Native.POINT point){if(r.Detail!=null){card.Present(r.Detail,r);card.Place(point);}}
    public void Notify(string text){main?.Notify(text);if(tray!=null){tray.BalloonTipTitle="拾词";tray.BalloonTipText=text;tray.ShowBalloonTip(2500);}}
    public static void Speak(string text) {
        Task.Run(()=>{object? speaker=null;try{var type=Type.GetTypeFromProgID("SAPI.SpVoice");if(type==null)return;speaker=Activator.CreateInstance(type);type.InvokeMember("Speak",System.Reflection.BindingFlags.InvokeMethod,null,speaker,new object[]{text,0});}catch(Exception e){Paths.Log(e);}finally{if(speaker!=null)System.Runtime.InteropServices.Marshal.ReleaseComObject(speaker);}});
    }
    public void Dispose(){request?.Cancel();reopenWait.Unregister(null);reopen.Dispose();Watcher.Dispose();hotkeys.Dispose();tray.Visible=false;tray.Dispose();floating.Close();card.Close();main.Quit();lexicon.Dispose();}
}
