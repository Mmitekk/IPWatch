using System.Globalization;

namespace IpWatch;

internal sealed record IpInfo(
    string Ip,
    string CountryName,
    string CountryCode,
    string Region,
    string City,
    string Isp,
    string TimeZoneId,
    int UtcOffsetSeconds)
{
    public string Place
    {
        get
        {
            var parts = new[] { City, Region }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToArray();
            return parts.Length == 0 ? "—" : string.Join(", ", parts);
        }
    }

    public TimeZoneInfo? Zone => IpTimeZone.Find(TimeZoneId);

    public DateTimeOffset LocalTimeNow()
    {
        var zone = Zone;
        if (zone != null) return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        return DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromSeconds(UtcOffsetSeconds));
    }

    public TimeSpan OffsetNow()
    {
        var zone = Zone;
        return zone != null ? zone.GetUtcOffset(DateTime.UtcNow) : TimeSpan.FromSeconds(UtcOffsetSeconds);
    }
}

internal sealed record NetworkSnapshot(string? Gateway, string? LocalIp, string AdapterName)
{
    public static readonly NetworkSnapshot Empty = new(null, null, "—");
}

internal sealed record WatchSnapshot(
    IpInfo Ip,
    NetworkSnapshot Net,
    int? PingMs,
    int? GatewayPingMs,
    DateTime UpdatedAt);

internal static class IpTimeZone
{
    private static readonly Dictionary<string, TimeZoneInfo?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static TimeZoneInfo? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        lock (Cache)
        {
            if (Cache.TryGetValue(id, out var cached)) return cached;

            TimeZoneInfo? zone = null;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }

            Cache[id] = zone;
            return zone;
        }
    }

    public static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return string.Format(CultureInfo.InvariantCulture, "UTC{0}{1:00}:{2:00}", sign, (int)abs.TotalHours, abs.Minutes);
    }

    public static string DescribeDifference(TimeSpan ipOffset)
    {
        var diff = ipOffset - TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
        var hours = diff.TotalMinutes / 60.0;
        if (Math.Abs(diff.TotalMinutes) < 1) return "совпадает с местным";

        var sign = hours > 0 ? "+" : "−";
        var absHours = Math.Abs(hours);
        var text = Math.Abs(absHours - Math.Round(absHours)) < 0.01
            ? string.Format(CultureInfo.InvariantCulture, "{0}{1:0} ч", sign, absHours)
            : string.Format(CultureInfo.InvariantCulture, "{0}{1:0.#} ч", sign, absHours);

        return diff > TimeSpan.Zero ? $"{text} к местному" : $"{text} от местного";
    }

    public static string RussianCountryName(string code, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(code) && CountryNames.TryGetValue(code.ToUpperInvariant(), out var russian))
            return russian;

        return string.IsNullOrWhiteSpace(fallback) ? code.ToUpperInvariant() : fallback;
    }

    /// <summary>Русские названия стран и территорий, которые реально встречаются как выходные узлы VPN.</summary>
    private static readonly Dictionary<string, string> CountryNames = new(StringComparer.Ordinal)
    {
        ["AE"] = "ОАЭ",
        ["AL"] = "Албания",
        ["AM"] = "Армения",
        ["AR"] = "Аргентина",
        ["AT"] = "Австрия",
        ["AU"] = "Австралия",
        ["AZ"] = "Азербайджан",
        ["BA"] = "Босния и Герцеговина",
        ["BD"] = "Бангладеш",
        ["BE"] = "Бельгия",
        ["BG"] = "Болгария",
        ["BH"] = "Бахрейн",
        ["BO"] = "Боливия",
        ["BR"] = "Бразилия",
        ["BY"] = "Беларусь",
        ["CA"] = "Канада",
        ["CH"] = "Швейцария",
        ["CL"] = "Чили",
        ["CN"] = "Китай",
        ["CO"] = "Колумбия",
        ["CR"] = "Коста-Рика",
        ["CY"] = "Кипр",
        ["CZ"] = "Чехия",
        ["DE"] = "Германия",
        ["DK"] = "Дания",
        ["DO"] = "Доминикана",
        ["EC"] = "Эквадор",
        ["EE"] = "Эстония",
        ["EG"] = "Египет",
        ["ES"] = "Испания",
        ["FI"] = "Финляндия",
        ["FR"] = "Франция",
        ["GB"] = "Великобритания",
        ["GE"] = "Грузия",
        ["GR"] = "Греция",
        ["HK"] = "Гонконг",
        ["HR"] = "Хорватия",
        ["HU"] = "Венгрия",
        ["ID"] = "Индонезия",
        ["IE"] = "Ирландия",
        ["IL"] = "Израиль",
        ["IN"] = "Индия",
        ["IS"] = "Исландия",
        ["IT"] = "Италия",
        ["JP"] = "Япония",
        ["KG"] = "Киргизия",
        ["KH"] = "Камбоджа",
        ["KR"] = "Южная Корея",
        ["KZ"] = "Казахстан",
        ["LT"] = "Литва",
        ["LU"] = "Люксембург",
        ["LV"] = "Латвия",
        ["MA"] = "Марокко",
        ["MD"] = "Молдова",
        ["ME"] = "Черногория",
        ["MK"] = "Северная Македония",
        ["MT"] = "Мальта",
        ["MX"] = "Мексика",
        ["MY"] = "Малайзия",
        ["NG"] = "Нигерия",
        ["NL"] = "Нидерланды",
        ["NO"] = "Норвегия",
        ["NZ"] = "Новая Зеландия",
        ["OM"] = "Оман",
        ["PA"] = "Панама",
        ["PE"] = "Перу",
        ["PH"] = "Филиппины",
        ["PK"] = "Пакистан",
        ["PL"] = "Польша",
        ["PT"] = "Португалия",
        ["PY"] = "Парагвай",
        ["QA"] = "Катар",
        ["RO"] = "Румыния",
        ["RS"] = "Сербия",
        ["RU"] = "Россия",
        ["SA"] = "Саудовская Аравия",
        ["SC"] = "Сейшелы",
        ["SE"] = "Швеция",
        ["SG"] = "Сингапур",
        ["SI"] = "Словения",
        ["SK"] = "Словакия",
        ["TH"] = "Таиланд",
        ["TR"] = "Турция",
        ["TW"] = "Тайвань",
        ["UA"] = "Украина",
        ["US"] = "США",
        ["UY"] = "Уругвай",
        ["UZ"] = "Узбекистан",
        ["VG"] = "Британские Виргинские о-ва",
        ["VN"] = "Вьетнам",
        ["ZA"] = "ЮАР",
        ["IM"] = "Остров Мэн",
        ["GI"] = "Гибралтар",
        ["AD"] = "Андорра",
        ["MC"] = "Монако",
        ["SM"] = "Сан-Марино",
        ["LI"] = "Лихтенштейн",
        ["CW"] = "Кюрасао",
        ["KY"] = "Каймановы о-ва",
        ["BZ"] = "Белиз",
        ["HN"] = "Гондурас",
        ["GT"] = "Гватемала",
        ["SV"] = "Сальвадор",
        ["NI"] = "Никарагуа",
        ["JM"] = "Ямайка",
        ["TT"] = "Тринидад и Тобаго",
        ["BS"] = "Багамы",
        ["BB"] = "Барбадос",
        ["PR"] = "Пуэрто-Рико",
        ["MO"] = "Макао",
        ["FJ"] = "Фиджи",
        ["NP"] = "Непал",
        ["LK"] = "Шри-Ланка",
        ["MN"] = "Монголия",
        ["MM"] = "Мьянма",
        ["BN"] = "Бруней",
        ["LA"] = "Лаос",
        ["AF"] = "Афганистан",
        ["IQ"] = "Ирак",
        ["IR"] = "Иран",
        ["JO"] = "Иордания",
        ["KW"] = "Кувейт",
        ["LB"] = "Ливан",
        ["PS"] = "Палестина",
        ["SY"] = "Сирия",
        ["TJ"] = "Таджикистан",
        ["TM"] = "Туркменистан",
        ["ET"] = "Эфиопия",
        ["KE"] = "Кения",
        ["GH"] = "Гана",
        ["SN"] = "Сенегал",
        ["TN"] = "Тунис",
        ["DZ"] = "Алжир",
        ["LY"] = "Ливия",
        ["MU"] = "Маврикий",
        ["MG"] = "Мадагаскар",
        ["AO"] = "Ангола",
        ["BW"] = "Ботсвана",
        ["MZ"] = "Мозамбик",
        ["TZ"] = "Танзания",
        ["UG"] = "Уганда",
        ["ZW"] = "Зимбабве",
        ["ZM"] = "Замбия",
        ["CM"] = "Камерун",
        ["CI"] = "Кот-д’Ивуар",
        ["EH"] = "Западная Сахара",
        ["RE"] = "Реюньон",
        ["GP"] = "Гваделупа",
        ["MQ"] = "Мартиника",
        ["GF"] = "Французская Гвиана",
        ["YT"] = "Майотта",
        ["PF"] = "Французская Полинезия",
        ["NC"] = "Новая Каледония",
        ["BM"] = "Бермуды",
        ["GL"] = "Гренландия",
        ["FO"] = "Фарерские о-ва",
        ["AX"] = "Аландские о-ва",
        ["SJ"] = "Шпицберген",
        ["GG"] = "Гернси",
        ["JE"] = "Джерси",
        ["VI"] = "Виргинские о-ва (США)",
        ["TC"] = "Тёркс и Кайкос",
        ["AW"] = "Аруба",
        ["SX"] = "Синт-Мартен",
        ["BQ"] = "Карибские Нидерланды",
        ["FK"] = "Фолклендские о-ва",
        ["GU"] = "Гуам",
        ["MP"] = "Северные Марианские о-ва",
        ["AS"] = "Американское Самоа",
        ["CK"] = "Острова Кука",
        ["WS"] = "Самоа",
        ["TO"] = "Тонга",
        ["VU"] = "Вануату",
        ["SB"] = "Соломоновы о-ва",
        ["PG"] = "Папуа — Новая Гвинея",
        ["TL"] = "Восточный Тимор",
        ["MV"] = "Мальдивы",
        ["BT"] = "Бутан",
        ["KP"] = "КНДР"
    };
}
