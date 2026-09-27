using System;

/// <summary>
/// 玩家 ID（「异界号」）· **A 方案：Steam 账号直接编进 ID** —— 拿到 ID 的人在本机就能解出对方 SteamID64，
/// 于是能直接加 Steam 好友 / 直连，**不需要任何后端目录**。
/// 形如 <c>P00-20260927-4-K3M79QX-8</c>（带分隔符 24 字符；剥掉分隔符 20 字符）。**创建一次、永不变**。
/// </summary>
/// <remarks>2026-09-27 定。B 方案（短 ID + 等后端目录）作废 —— 我们纯 Steam P2P，没有可查的索引。
///
/// 结构（剥掉分隔符后的 20 个字符，位置从 0 数）：
/// <code>
///   0      'P'              类型位（玩家；将来公会 / 战队留 G）
///   1..10  赛季 2 + 创建日 8  例：00 20260927
///   11     校验位 ①（11 取模）  守着上面那 10 位数字
///   12..18 Steam 账号 7 个 Base32 符号（账号 ID = SteamID64 − 76561197960265728）
///   19     校验位 ②（31 取模）  守着上面那 7 个符号
/// </code>
///
/// **为什么只编 7 个符号**：SteamID64 的高 32 位对「公开个人账号」是常量，真正因人而异的只有低 32 位账号 ID。
/// 32 位 → 7 个符号（31^7 = 275 亿，够到 340 亿个账号里的每一个）。
///
/// **字母表 31 个符号**（<see cref="Alphabet"/>）：Crockford Base32 剔掉 I / L / O / U，再剔掉 Z（易与 2 混）——
/// 归一化时 I、L 当 1、O 当 0、Z 当 2 —— 手抄 / 念给人听时最不容易错；31 个符号又正好配 31 取模。
///
/// **两个校验位都实测过**（<c>Tools/…</c> 之外的一次性测试，见项目讨论）：
///   ① 11 取模（11 是质数 ⇒ 权重永远互质）：10 位数字里**单字打错 100% 查出**，**相邻换位也 100% 查出**；
///   ② 31 取模（31 是质数 ⇒ 权重 1..7 全与 31 互质）：7 个符号里**单字打错 100% 查出**、**相邻换位也 100% 查出**；
///      实测穷举 2000 个号 × 117.8 万个单字错 + 2.68 万个换位，**0 漏检**（旧版 32 取模漏了 321 次换位）。
/// 校验值 ① 为 10 时写字母 <see cref="CheckTen"/>（X）。
///
/// **真正的唯一性**：SteamID64 本身全球唯一，所以这一版**不再需要随机段** ——
/// 早期版本那「3 个随机数」是为了在没绑 Steam 时防撞车，绑进 Steam 之后是纯冗余，删掉（ID 短 3 个字符）。</remarks>
public static class PlayerId
{
    public const char  KindPlayer    = 'P';   // 类型位
    public const int   CurrentSeason = 0;     // 创建时赛季（00 起）—— 赛季系统落地时只改这里
    public const int   RawLength     = 20;    // 去掉分隔符后的长度（Normalize 之后）
    public const int   Length        = 24;    // P00-20260927-4-K3M79QX-8 —— 给人看的那一串
    public const char  CheckTen      = 'X';   // 校验位 ① 为 10 时写这个（11 取模的标准写法）

    /// <summary>公开个人账号的 SteamID64 基址：SteamID64 = 基址 + 账号 ID。</summary>
    public const ulong SteamIdBase = 76561197960265728UL;

    /// <summary>字母表：0-9 + 21 个字母，共 <b>31</b> 个 —— 剔掉易混的 I / L / O / U / Z。</summary>
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXY";

    const int SeasonDigits = 2;
    const int DateDigits   = 8;               // yyyyMMdd
    const int DigitLen     = SeasonDigits + DateDigits;   // 10
    const int AccDigits    = 7;               // 32 位账号 → 7 个 base-31 符号
    const int LegacyRawLength = 15;           // 老格式（无 Steam 绑定）：P + 10 数字 + 3 随机 + 1 校验

    // ── 生成 ─────────────────────────────────────────────────────────────────

    /// <summary>按「创建那一刻 + 这个 Steam 账号」生成一个 ID。createdAt 非法（默认值）就用本机今天。</summary>
    public static string Create(DateTime createdAt, ulong steamId)
    {
        if (createdAt.Year < 1000 || createdAt.Year > 9999) createdAt = DateTime.Now;
        return Format(CurrentSeason, createdAt, steamId);
    }

    /// <summary>本机今天创建的号。</summary>
    public static string CreateToday(ulong steamId) { return Create(DateTime.Now, steamId); }

    /// <summary>拼 ID：P + 赛季 2 + '-' + yyyyMMdd + '-' + 校验① + '-' + 账号 7 位 + '-' + 校验②。</summary>
    public static string Format(int season, DateTime createdAt, ulong steamId)
    {
        if (!IsEncodableSteamId(steamId)) throw new ArgumentException("不是公开个人账号的 SteamID64: " + steamId, "steamId");

        int s = ((season % 100) + 100) % 100;
        int y = Math.Max(1000, Math.Min(9999, createdAt.Year));
        string digits = string.Format("{0:D2}{1:D4}{2:D2}{3:D2}", s, y, createdAt.Month, createdAt.Day);
        string acc = EncodeAccount(AccountOf(steamId));

        return string.Format("{0}{1}-{2}-{3}-{4}-{5}",
            KindPlayer,
            digits.Substring(0, SeasonDigits),
            digits.Substring(SeasonDigits, DateDigits),
            DigitCheckChar(digits),
            acc,
            AccCheckChar(acc));
    }

    // ── SteamID64 ↔ 账号 ID ──────────────────────────────────────────────────

    /// <summary>是不是能编进 ID 的 SteamID64（公开个人账号、低 32 位以内）。</summary>
    public static bool IsEncodableSteamId(ulong steamId)
    {
        return steamId >= SteamIdBase && (steamId - SteamIdBase) <= uint.MaxValue;
    }

    public static uint AccountOf(ulong steamId) { return (uint)(steamId - SteamIdBase); }
    public static ulong SteamIdOf(uint accountId) { return SteamIdBase + accountId; }

    // ── 校验 ─────────────────────────────────────────────────────────────────

    /// <summary>校验值 ①（0–10）：10 位数字按 1..10 循环加权求和，取 11 的补数。11 是质数 ⇒ 权重全与 11 互质。</summary>
    public static int DigitCheckValue(string digits) { return (11 - (WeightedSum(digits, 10) % 11)) % 11; }

    /// <summary>校验位 ① 的字符：0–9；值为 10 时写 <see cref="CheckTen"/>。</summary>
    public static char DigitCheckChar(string digits)
    {
        int v = DigitCheckValue(digits);
        return v == 10 ? CheckTen : (char)('0' + v);
    }

    /// <summary>校验值 ②（0–30）：7 个符号按其值 × 权重(1..7) 求和，取 31 的补数。
    /// **31 是质数 ⇒ 权重全与 31 互质 ⇒ 单符号打错必查出、相邻换位也必查出**
    /// （符号值差最大 30 &lt; 31，换位时两权重必不相同 ⇒ 只可能是「值差 0」＝没换）。
    /// 早先用 32 取模 + 奇数权重，实测在「两个符号值正好差 16」时会漏掉换位（2000 个号漏 321 次），故改 31。</summary>
    public static int AccCheckValue(string acc)
    {
        int sum = 0;
        for (int i = 0; i < acc.Length; i++)
        {
            int v = AlphabetIndexOf(acc[i]);
            if (v < 0) return -1;                      // 表外字符 → 调用方判退
            sum += v * (i + 1);                        // 权重 1..7，全与 31 互质
        }
        return (31 - (sum % 31)) % 31;
    }

    /// <summary>校验位 ② 的字符（同样是 <see cref="Alphabet"/> 里的符号）。</summary>
    public static char AccCheckChar(string acc)
    {
        int v = AccCheckValue(acc);
        return v < 0 ? '\0' : Alphabet[v];
    }

    static int WeightedSum(string digits, int weightCycle)
    {
        int sum = 0;
        for (int i = 0; i < digits.Length; i++)
        {
            int d = digits[i] - '0';
            if (d < 0 || d > 9) continue;              // 非数字位跳过（调用方保证只有数字）
            sum += d * ((i % weightCycle) + 1);
        }
        return sum;
    }

    public static int AlphabetIndexOf(char c)
    {
        for (int i = 0; i < Alphabet.Length; i++) if (Alphabet[i] == c) return i;
        return -1;
    }

    // ── 归一化 / 显示 ────────────────────────────────────────────────────────

    /// <summary>只留 A–Z / 0–9、转大写，并把易混的 I / L 当成 1、O 当成 0（Crockford 的规矩）——
    /// 手输时的空格 / 分隔符 / 小写 / 全角 / 看错字母都在这儿抹平。</summary>
    public static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new System.Text.StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = char.ToUpperInvariant(raw[i]);
            if (c == 'I' || c == 'L') c = '1';
            else if (c == 'O') c = '0';
            else if (c == 'Z') c = '2';   // Z 不在表里（2 看成 Z），顺手纠正
            if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z')) sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>写出来好看的那一串（P00-20260927-4-K3M79QX-8）。输入可以带空格 / 少打分隔符。</summary>
    public static string Pretty(string raw)
    {
        string n = Normalize(raw);
        if (n.Length != RawLength) return raw ?? "";
        return n.Substring(0, 3) + "-" + n.Substring(3, 8) + "-" + n.Substring(11, 1) + "-" +
               n.Substring(12, 7) + "-" + n.Substring(19, 1);
    }

    /// <summary>格式 + 两个校验位都对。输入可以带空格 / 不带分隔符。</summary>
    public static bool IsWellFormed(string raw)
    {
        int season; DateTime createdAt; ulong steamId;
        return TryParse(raw, out season, out createdAt, out steamId);
    }

    /// <summary>解析出「创建时赛季 / 创建日 / 对方 SteamID64」—— 拿到 SteamID 就能加好友、直连。</summary>
    public static bool TryParse(string raw, out int season, out DateTime createdAt, out ulong steamId)
    {
        season = -1; createdAt = default(DateTime); steamId = 0;
        string n = Normalize(raw);
        if (n.Length != RawLength) return false;
        if (n[0] != KindPlayer) return false;

        string digits = n.Substring(1, DigitLen);
        for (int i = 0; i < digits.Length; i++)
        {
            char c = digits[i];
            if (c < '0' || c > '9') return false;      // 赛季 + 日期必须是数字
        }
        if (n[1 + DigitLen] != DigitCheckChar(digits)) return false;

        string acc = n.Substring(2 + DigitLen, AccDigits);
        for (int i = 0; i < acc.Length; i++)
            if (AlphabetIndexOf(acc[i]) < 0) return false;   // Base32 段必须都在表内
        if (n[RawLength - 1] != AccCheckChar(acc)) return false;

        int y  = int.Parse(digits.Substring(2, 4));
        int mo = int.Parse(digits.Substring(6, 2));
        int d  = int.Parse(digits.Substring(8, 2));
        if (y < 2000 || y > 2100 || mo < 1 || mo > 12) return false;
        if (d < 1 || d > DateTime.DaysInMonth(y, mo)) return false;

        uint accountId;
        if (!TryDecodeAccount(acc, out accountId)) return false;

        season = int.Parse(digits.Substring(0, 2));
        createdAt = new DateTime(y, mo, d);
        steamId = SteamIdOf(accountId);
        return true;
    }

    /// <summary>只取 SteamID64（加好友 / 直连用）；格式不对返回 0。</summary>
    public static ulong ResolveSteamId(string raw)
    {
        int season; DateTime createdAt; ulong steamId;
        return TryParse(raw, out season, out createdAt, out steamId) ? steamId : 0UL;
    }

    /// <summary>创建日文本（"2026-09-27"）；格式不对就原样返回。</summary>
    public static string CreatedDateText(string raw)
    {
        int season; DateTime createdAt; ulong steamId;
        if (!TryParse(raw, out season, out createdAt, out steamId)) return raw ?? "";
        return createdAt.ToString("yyyy-MM-dd");
    }

    /// <summary>「创建时赛季 00 · 2026-09-27」—— 给 UI 用的一行说明。格式不对返回空串。</summary>
    public static string Describe(string raw)
    {
        int season; DateTime createdAt; ulong steamId;
        if (!TryParse(raw, out season, out createdAt, out steamId)) return "";
        return string.Format("创建时赛季 {0:D2} · {1:yyyy-MM-dd}", season, createdAt);
    }

    // ── 老格式（B 时代的无 Steam 绑定版）：只为迁移时把「创建日」捡回来 ──────────

    /// <summary>老格式 <c>P00-20260927-482-4</c>（15 字符，末尾是随机段 + 单个 11 取模校验位）。
    /// 现在只用来在升级到 A 格式时**保留原来的创建日**，不再生成。</summary>
    public static bool TryParseLegacy(string raw, out int season, out DateTime createdAt)
    {
        season = -1; createdAt = default(DateTime);
        string n = Normalize(raw);
        if (n.Length != LegacyRawLength || n[0] != KindPlayer) return false;

        string digits = n.Substring(1, 13);                 // 赛季 2 + 日期 8 + 随机 3
        for (int i = 0; i < digits.Length; i++)
        {
            char c = digits[i];
            if (c < '0' || c > '9') return false;
        }
        int check = n[LegacyRawLength - 1] - '0';
        if (check != (11 - (WeightedSum(digits, 10) % 11)) % 11) return false;

        int y  = int.Parse(digits.Substring(2, 4));
        int mo = int.Parse(digits.Substring(6, 2));
        int d  = int.Parse(digits.Substring(8, 2));
        if (y < 2000 || y > 2100 || mo < 1 || mo > 12) return false;
        if (d < 1 || d > DateTime.DaysInMonth(y, mo)) return false;

        season = int.Parse(digits.Substring(0, 2));
        createdAt = new DateTime(y, mo, d);
        return true;
    }

    // ── 账号 ID ↔ Base32 ─────────────────────────────────────────────────────

    /// <summary>32 位账号 ID → 7 个符号（31^7 = 275 亿 &gt; 2^32，够用）。</summary>
    public static string EncodeAccount(uint accountId)
    {
        char[] c = new char[AccDigits];
        uint v = accountId;
        for (int i = AccDigits - 1; i >= 0; i--)
        {
            c[i] = Alphabet[(int)(v % (uint)Alphabet.Length)];
            v /= (uint)Alphabet.Length;
        }
        return new string(c);
    }

    /// <summary>7 个符号 → 32 位账号 ID。表外字符、或解出来超出 32 位都失败。</summary>
    public static bool TryDecodeAccount(string acc, out uint accountId)
    {
        accountId = 0;
        if (string.IsNullOrEmpty(acc) || acc.Length != AccDigits) return false;
        ulong v = 0;
        for (int i = 0; i < acc.Length; i++)
        {
            int dg = AlphabetIndexOf(acc[i]);
            if (dg < 0) return false;
            v = v * (ulong)Alphabet.Length + (ulong)dg;
        }
        if (v > uint.MaxValue) return false;              // 31^7 里有超出 2^32 的组合 —— 不存在这种账号
        accountId = (uint)v;
        return true;
    }
}
