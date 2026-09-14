using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Shici;

public static class Paths {
    public static string Home = AppContext.BaseDirectory;
    public static string Data = Path.Combine(Home, "data");
    public static void Log(Exception e) { try { Directory.CreateDirectory(Data); File.AppendAllText(Path.Combine(Data,"errors.log"),DateTime.Now.ToString("s")+" "+e.GetType().Name+": "+e.Message+Environment.NewLine); } catch {} }
}
public sealed class Sqlite : IDisposable {
    IntPtr db;
    const string Lib="winsqlite3.dll";
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path,out IntPtr db,int flags,IntPtr vfs);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)] string sql,int len,out IntPtr stmt,IntPtr tail);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_text(IntPtr stmt,int index,[MarshalAs(UnmanagedType.LPUTF8Str)] string text,int length,IntPtr destructor);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr stmt);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr stmt);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr stmt,int index);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_count(IntPtr stmt);
    public Sqlite(string path) { if(sqlite3_open_v2(path,out db,1,IntPtr.Zero)!=0) throw new IOException("无法打开本地词典："+path); }
    public List<string[]> Query(string sql,params string[] args) {
        lock(this) {
            if(sqlite3_prepare_v2(db,sql,-1,out var stmt,IntPtr.Zero)!=0) throw new IOException("词典查询失败。");
            try {
                for(int i=0;i<args.Length;i++) if(sqlite3_bind_text(stmt,i+1,args[i],-1,new IntPtr(-1))!=0) throw new IOException("词典参数错误。");
                var rows=new List<string[]>(); int result;
                while((result=sqlite3_step(stmt))==100) { var row=new string[sqlite3_column_count(stmt)]; for(int i=0;i<row.Length;i++) row[i]=Marshal.PtrToStringUTF8(sqlite3_column_text(stmt,i))??""; rows.Add(row); }
                if(result!=101) throw new IOException("词典读取中断。");
                return rows;
            } finally { sqlite3_finalize(stmt); }
        }
    }
    public void Dispose() { if(db!=IntPtr.Zero) sqlite3_close(db); db=IntPtr.Zero; }
}
public record RootInfo(string Root,string Meaning,string Kind,string Origin,string Examples);
public class Entry {
    public string Word {get;set;}=""; public string Phonetic {get;set;}=""; public string Chinese {get;set;}=""; public string English {get;set;}=""; public string Exchange {get;set;}=""; public string Tags {get;set;}="";
}
public class Lookup {
    public string Query {get;set;}=""; public string Lemma {get;set;}=""; public string Target {get;set;}="zh-CN"; public string Translation {get;set;}=""; public string Source {get;set;}=""; public string LemmaNote {get;set;}="";
    public Entry? Entry {get;set;} public List<RootInfo> Roots {get;set;}=new(); public bool IsWord {get;set;} public string RootNote {get;set;}="";
    public string Key => (Regex.IsMatch(Lemma,@"^[a-zA-Z][a-zA-Z'’-]*$")?"en:":"zh:")+Lemma.ToLowerInvariant();
}
public sealed class Lexicon : IDisposable {
    readonly Sqlite db;
    public Lexicon(string file) { db=new Sqlite(file); }
    public static bool HasChinese(string s)=>Regex.IsMatch(s,@"[\u3400-\u9fff]");
    public static bool SingleEnglish(string s)=>Regex.IsMatch(s,@"^[a-zA-Z]+(?:['’-][a-zA-Z]+)*$");
    public static string Clean(string s)=>Regex.Replace(s.Trim().Trim('"','“','”','‘','’','.',',','!','?',':',';','(',')','[',']'),@"\s+"," ");
    public Entry? Find(string word) {
        var r=db.Query("SELECT word,phonetic,translation,definition,exchange,tags FROM words WHERE word=? LIMIT 1",word.ToLowerInvariant()).FirstOrDefault();
        return r==null?null:new Entry{Word=r[0],Phonetic=r[1],Chinese=r[2],English=r[3],Exchange=r[4],Tags=r[5]};
    }
    public static Dictionary<string,string> Forms(string exchange) {
        var d=new Dictionary<string,string>(); foreach(var part in exchange.Split('/')) { var i=part.IndexOf(':'); if(i>0)d[part[..i]]=part[(i+1)..]; } return d;
    }
    public (Entry? Entry,string Lemma,string Note) Resolve(string query) {
        var word=query.ToLowerInvariant(); var exact=Find(word); var forms=Forms(exact?.Exchange??"");
        if(forms.TryGetValue("0",out var baseWord)) { var b=Find(baseWord.Split(',')[0]); if(b!=null)return(b,b.Word,"词形还原："+word+" → "+b.Word); }
        var candidates=db.Query("SELECT lemma,kind FROM forms WHERE form=? ORDER BY rank LIMIT 8",word);
        // Only merge inflections evidenced by the dictionary. Ambiguous standalone words retain their own entry.
        bool inflected=exact==null || Regex.IsMatch(exact.Chinese,@"(过去式|过去分词|现在分词|第三人称|复数形式|的复数|比较级|最高级)");
        if(inflected && candidates.Count>0) { var b=Find(candidates[0][0]); if(b!=null) return(b,b.Word,"词形还原："+word+" → "+b.Word+(candidates.Select(x=>x[0]).Distinct().Count()>1?"（另有候选："+string.Join(" / ",candidates.Select(x=>x[0]).Distinct().Skip(1))+"；按词频选用）":"")); }
        string note=candidates.Count>0?"该拼写也可能是 "+string.Join(" / ",candidates.Select(x=>x[0]).Distinct())+" 的词形；当前保留独立词条。":"原形："+word;
        return(exact,exact?.Word??word,note);
    }
    public List<RootInfo> GetRoots(string word) => db.Query("SELECT root,meaning,class,origin,examples FROM roots WHERE word=?",word).Select(r=>new RootInfo(r[0],r[1],KindZh(r[2]),r[3],r[4])).Distinct().ToList();
    public static string KindZh(string s)=>s.Contains("prefix")?"前缀":s.Contains("suffix")?"后缀":"词根";
    public static string RootChinese(string root,string meaning) {
        var map=new Dictionary<string,string>{{"without","没有；缺少"},{"again, back","再次；向后"},{"not","不；非"},{"life, living, living things","生命；生物"},{"man, human","人；人类"},{"with, together with","共同；一起"},{"study of, science of; written work; structure or principle","……的学科；著作；结构或原理"},{"before","在……之前"},{"after","在……之后"},{"against","反对；相反"},{"many","许多"},{"one","一；单一"},{"two","二；双"},{"water","水"},{"earth","地球；土地"},{"small","小；微"},{"large","大"},{"self","自己"},{"far","远"},{"sound","声音"},{"write","书写"},{"see","看"},{"carry","携带"}};
        return map.TryGetValue(meaning.Trim(),out var zh)?zh+" · "+meaning:meaning;
    }
    public void Dispose()=>db.Dispose();
}
public interface ITranslator { Task<string> Translate(string text,string target,CancellationToken ct); }
public sealed class MyMemory : ITranslator {
    readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(18)};
    public MyMemory(){http.DefaultRequestHeaders.UserAgent.ParseAdd("Shici/1.0 (personal desktop translation)");}
    public static List<string> Chunks(string text) {
        var result=new List<string>(); var b=new StringBuilder(); int bytes=0;
        foreach(var rune in text.EnumerateRunes()) { int n=rune.Utf8SequenceLength; if(bytes+n>480) { result.Add(b.ToString());b.Clear();bytes=0; } b.Append(rune.ToString());bytes+=n; }
        if(b.Length>0)result.Add(b.ToString());return result;
    }
    public async Task<string> Translate(string text,string target,CancellationToken ct) {
        string source=Lexicon.HasChinese(text)?"zh-CN":"en";
        if(source==target)return text;
        var result=new List<string>();
        foreach(var chunk in Chunks(text)) {
            string url="https://api.mymemory.translated.net/get?q="+Uri.EscapeDataString(chunk)+"&langpair="+Uri.EscapeDataString(source+"|"+target);
            using var response=await http.GetAsync(url,ct);response.EnsureSuccessStatusCode();
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var r=json.RootElement;
            if((r.TryGetProperty("quotaFinished",out var q)&&q.ValueKind==JsonValueKind.True)||r.GetProperty("responseStatus").ToString()!="200")throw new IOException("在线翻译暂不可用或今日免费额度已用完。本地英汉词典仍可用，请稍后重试。");
            var s=WebUtility.HtmlDecode(r.GetProperty("responseData").GetProperty("translatedText").GetString()??"");
            if(string.IsNullOrWhiteSpace(s))throw new IOException("翻译服务没有返回结果，请重试。");result.Add(s);
        }
        return string.Join(target=="en"?" ":"",result);
    }
}
public sealed class LookupService {
    readonly Lexicon lexicon;readonly ITranslator online;readonly string cacheFile; readonly Dictionary<string,Lookup> cache;
    public LookupService(Lexicon lexicon,ITranslator online,string folder) {
        this.lexicon=lexicon;this.online=online;Directory.CreateDirectory(folder);cacheFile=Path.Combine(folder,"translation-cache.json");
        try {cache=JsonSerializer.Deserialize<Dictionary<string,Lookup>>(File.ReadAllText(cacheFile))??new();}catch{cache=new();}
    }
    public async Task<Lookup> Query(string text,string target,CancellationToken ct) {
        text=Lexicon.Clean(text);if(text.Length==0)throw new IOException("请先选择或输入文字。");if(text.Length>1200)throw new IOException("每次最多查询 1200 个字符，请缩小选区。");
        string cacheKey=target+"|"+text;
        if(cache.TryGetValue(cacheKey,out var cached))return cached;
        var result=new Lookup{Query=text,Target=target};
        if(!Lexicon.HasChinese(text)) {
            var resolved=lexicon.Resolve(text);result.Entry=resolved.Entry;result.Lemma=resolved.Lemma;result.LemmaNote=resolved.Note;result.IsWord=Lexicon.SingleEnglish(text);
            if(resolved.Entry!=null && !string.IsNullOrWhiteSpace(resolved.Entry.Chinese)) {
                result.Translation=target=="zh-CN"?resolved.Entry.Chinese:(!string.IsNullOrWhiteSpace(resolved.Entry.English)?resolved.Entry.English:resolved.Entry.Word);
                result.Source="ECDICT · 本地词典";
            } else {result.Translation=await online.Translate(text,target,ct);result.Source="MyMemory · 在线翻译";}
        } else {
            result.Translation=await online.Translate(text,target,ct);result.Source="MyMemory · 在线翻译";
            var translated=Lexicon.Clean(result.Translation);
            if(target=="en"&&Lexicon.SingleEnglish(translated)) {var resolved=lexicon.Resolve(translated);result.Entry=resolved.Entry;result.Lemma=resolved.Lemma;result.LemmaNote="中文选词："+text+" · "+resolved.Note;result.IsWord=true;}
            else {result.Lemma=text;result.IsWord=text.Length<=12&&!Regex.IsMatch(text,@"[，。！？；\s]");result.LemmaNote=result.IsWord?"中文词条（不适用英文词形还原）":"句子 / 短语翻译";}
        }
        if(result.Entry!=null)result.Roots=lexicon.GetRoots(result.Lemma);
        result.RootNote=result.Roots.Count>0?"依据 ECDICT 词根词缀例词表；仅展示该词有资料支持的部分。":"词根资料未收录此词；不按字母强行拆解。原形与词根是不同概念。";
        ct.ThrowIfCancellationRequested();cache[cacheKey]=result;
        if(cache.Count>3000)cache.Remove(cache.Keys.First());
        Atomic.Write(cacheFile,JsonSerializer.Serialize(cache,Atomic.Json));return result;
    }
}
public static class Atomic {
    public static JsonSerializerOptions Json=new(){WriteIndented=true};
    public static void Write(string path,string value) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);var tmp=path+".tmp";
        File.WriteAllText(tmp,value,new UTF8Encoding(false));
        if(File.Exists(path))File.Replace(tmp,path,path+".bak",true);else File.Move(tmp,path);
    }
}
public class WordRecord {
    public string Key {get;set;}="";public string Word {get;set;}="";public string Meaning {get;set;}="";public string Notes {get;set;}="";
    public int Count {get;set;} public bool InBook {get;set;} public int Stage {get;set;} public DateTime Due {get;set;}=DateTime.Now;
    public DateTime FirstSeen {get;set;}=DateTime.Now;public DateTime LastSeen {get;set;}=DateTime.Now;
    public List<string> SeenForms {get;set;}=new();public Lookup? Detail {get;set;}
}
public sealed class WordStore {
    readonly string path;public Dictionary<string,WordRecord> Records {get;private set;}=new();
    public IEnumerable<WordRecord> Book=>Records.Values.Where(r=>r.InBook);
    public WordStore(string folder) {
        Directory.CreateDirectory(folder);path=Path.Combine(folder,"vocabulary.json");
        if(File.Exists(path)) {
            try {Records=JsonSerializer.Deserialize<Dictionary<string,WordRecord>>(File.ReadAllText(path))??throw new IOException("生词本为空文件。");}
            catch(Exception e) {if(File.Exists(path+".bak")) {Records=JsonSerializer.Deserialize<Dictionary<string,WordRecord>>(File.ReadAllText(path+".bak"))??throw new IOException("备份损坏。");File.Copy(path,path+".corrupt-"+DateTime.Now.ToString("yyyyMMddHHmmss"));}else throw new IOException("生词本无法读取，已保留原文件，请从备份恢复。",e);}
        }
    }
    public WordRecord? Record(Lookup result) {
        if(!result.IsWord)return null;
        if(!Records.TryGetValue(result.Key,out var r))Records[result.Key]=r=new WordRecord{Key=result.Key,Word=result.Lemma};
        r.Count++;r.LastSeen=DateTime.Now;r.Detail=result;r.Meaning=result.Entry?.Chinese??result.Translation;
        if(!r.SeenForms.Contains(result.Query))r.SeenForms.Add(result.Query);
        if(r.Count>=4&&!r.InBook){r.InBook=true;r.Due=DateTime.Now;}
        Save();return r;
    }
    public WordRecord Add(Lookup result) {
        if(!Records.TryGetValue(result.Key,out var r))Records[result.Key]=r=new WordRecord{Key=result.Key,Word=result.Lemma,Meaning=result.Entry?.Chinese??result.Translation,Detail=result};
        r.InBook=true;r.Due=DateTime.Now;Save();return r;
    }
    public static DateTime ReviewDue(int stage,DateTime now)=>now.AddDays(new[]{1,3,7,14,30,60}[Math.Clamp(stage,0,5)]);
    public void Grade(WordRecord r,bool remembered) {if(remembered){r.Due=ReviewDue(r.Stage,DateTime.Now);r.Stage=Math.Min(5,r.Stage+1);}else{r.Stage=0;r.Due=DateTime.Now.AddMinutes(10);}Save();}
    public void Save()=>Atomic.Write(path,JsonSerializer.Serialize(Records,Atomic.Json));
    static string SafeCell(string s){s=s.Replace("\r"," ").Replace("\n"," / ").Replace("\t"," ");return s.Length>0&&"=+-@".Contains(s[0])?"'"+s:s;}
    public string Export(string folder) {
        Directory.CreateDirectory(folder);var stem=Path.Combine(folder,"生词本-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        var list=Book.OrderByDescending(r=>r.Count).ToList();
        File.WriteAllText(stem+".tsv","单词\t中文释义\t查询次数\t见过的词形\t下次复习\t个人笔记\n"+string.Join("\n",list.Select(r=>string.Join("\t",new[]{r.Word,r.Meaning,r.Count.ToString(),string.Join(", ",r.SeenForms),r.Due.ToString("yyyy-MM-dd HH:mm"),r.Notes}.Select(SafeCell)))),new UTF8Encoding(true));
        // Two fields, compatible with Anki's Basic note type, with HTML disabled on import.
        File.WriteAllText(stem+"-Anki.tsv",string.Join("\n",list.Select(r=>SafeCell(r.Word)+"\t"+SafeCell(r.Meaning+"\n"+r.Notes))),new UTF8Encoding(true));
        File.WriteAllText(stem+".md","# 我的生词本\n\n"+string.Join("\n\n",list.Select(r=>"## "+r.Word+"\n\n"+r.Meaning+"\n\n查询 "+r.Count+" 次 · 下次复习 "+r.Due.ToString("yyyy-MM-dd HH:mm")+"\n\n"+r.Notes)),new UTF8Encoding(false));return stem+".tsv";
    }
}
