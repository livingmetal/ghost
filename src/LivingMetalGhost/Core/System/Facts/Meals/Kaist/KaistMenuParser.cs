using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using LivingMetalGhost.Core.Facts.Meals;

namespace LivingMetalGhost.Core.Facts.Meals.Kaist;

public sealed class KaistMenuParser
{
    private static readonly Regex TitleDateRegex = new(@"오늘의\s*메뉴\s*안내\s*-\s*(?<month>\d{1,2})/(?<day>\d{1,2})", RegexOptions.Compiled);
    private static readonly Regex MealTimeRegex = new(@"(?<label>조식|중식|석식)\s+(?<time>\d{1,2}:\d{2}\s*~\s*\d{1,2}:\d{2})", RegexOptions.Compiled);
    private static readonly Regex PriceRegex = new(@"(?<label>조식|중식|석식)\s*:\s*(?<price>[\d,]+)\s*원", RegexOptions.Compiled);
    private static readonly Regex CaloriesRegex = new(@"칼로리\s*:\s*(?<calories>[\d,]+)\s*kcal", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    public string ParserId => "kaist-menu-v1";

    public KaistMenuDocument Parse(string html, DateOnly requestedDate, DateTimeOffset observedAt, string sourceUrl)
    {
        var lines = NormalizeLines(html);
        if (lines.Count == 0)
        {
            throw new InvalidOperationException("KAIST 메뉴 페이지에서 텍스트를 읽지 못했습니다.");
        }

        var menuDate = ParseMenuDate(lines, requestedDate);
        var serviceTimes = ParseServiceTimes(lines);
        var prices = ParsePrices(lines);
        var blocks = SplitMealBlocks(lines);

        var breakfast = ParseMeal(MealSlot.Breakfast, blocks.ElementAtOrDefault(0), serviceTimes, prices);
        var lunch = ParseMeal(MealSlot.Lunch, blocks.ElementAtOrDefault(1), serviceTimes, prices);
        var dinner = ParseMeal(MealSlot.Dinner, blocks.ElementAtOrDefault(2), serviceTimes, prices);

        if (breakfast is null && lunch is null && dinner is null)
        {
            throw new InvalidOperationException("조식, 중식, 석식 메뉴를 찾지 못했습니다.");
        }

        return new KaistMenuDocument(
            CampusCode: "icc",
            CampusName: "문지캠퍼스",
            MenuDate: menuDate,
            ObservedAt: observedAt,
            SourceUrl: sourceUrl,
            Breakfast: breakfast,
            Lunch: lunch,
            Dinner: dinner,
            Notices: ExtractNotices(lines));
    }

    private static IReadOnlyList<string> NormalizeLines(string html)
    {
        var text = Regex.Replace(html, @"<script[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<style[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<(br|/p|/div|/tr|/li|/td|/th)[^>]*>", "\n", RegexOptions.IgnoreCase);
        text = HtmlTagRegex.Replace(text, " ");
        text = WebUtility.HtmlDecode(text);

        return text
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private static DateOnly ParseMenuDate(IReadOnlyList<string> lines, DateOnly requestedDate)
    {
        foreach (var line in lines)
        {
            var match = TitleDateRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
            var candidate = new DateOnly(requestedDate.Year, month, day);
            if (candidate > requestedDate.AddDays(7))
            {
                candidate = candidate.AddYears(-1);
            }
            else if (candidate < requestedDate.AddDays(-7))
            {
                candidate = candidate.AddYears(1);
            }

            return candidate;
        }

        return requestedDate;
    }

    private static Dictionary<MealSlot, string> ParseServiceTimes(IReadOnlyList<string> lines)
    {
        var result = new Dictionary<MealSlot, string>();
        foreach (Match match in MealTimeRegex.Matches(string.Join(" ", lines)))
        {
            if (TryParseSlot(match.Groups["label"].Value, out var slot))
            {
                result[slot] = Regex.Replace(match.Groups["time"].Value, @"\s+", string.Empty);
            }
        }

        return result;
    }

    private static Dictionary<MealSlot, int> ParsePrices(IReadOnlyList<string> lines)
    {
        var result = new Dictionary<MealSlot, int>();
        foreach (Match match in PriceRegex.Matches(string.Join(" ", lines)))
        {
            if (TryParseSlot(match.Groups["label"].Value, out var slot) &&
                int.TryParse(match.Groups["price"].Value.Replace(",", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var price))
            {
                result[slot] = price;
            }
        }

        return result;
    }

    private static IReadOnlyList<IReadOnlyList<string>> SplitMealBlocks(IReadOnlyList<string> lines)
    {
        var startIndex = lines.ToList().FindIndex(line =>
            line.Contains("오늘의 메뉴 안내", StringComparison.Ordinal) &&
            line.Contains("조식", StringComparison.Ordinal) &&
            line.Contains("중식", StringComparison.Ordinal) &&
            line.Contains("석식", StringComparison.Ordinal));

        if (startIndex < 0)
        {
            throw new InvalidOperationException("오늘의 메뉴 안내 구간을 찾지 못했습니다.");
        }

        var blocks = new List<List<string>>();
        var current = new List<string>();
        foreach (var line in lines.Skip(startIndex + 1))
        {
            if (IsFooterStart(line))
            {
                break;
            }

            if (IsNoiseLine(line))
            {
                continue;
            }

            current.Add(line);
            if (CaloriesRegex.IsMatch(line))
            {
                blocks.Add(current);
                current = [];
                if (blocks.Count == 3)
                {
                    break;
                }
            }
        }

        return blocks;
    }

    private static MealMenu? ParseMeal(MealSlot slot, IReadOnlyList<string>? block, IReadOnlyDictionary<MealSlot, string> times, IReadOnlyDictionary<MealSlot, int> prices)
    {
        if (block is null || block.Count == 0)
        {
            return null;
        }

        int? calories = null;
        var items = new List<DiningItem>();
        foreach (var line in block)
        {
            var caloriesMatch = CaloriesRegex.Match(line);
            if (caloriesMatch.Success)
            {
                if (int.TryParse(caloriesMatch.Groups["calories"].Value.Replace(",", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCalories))
                {
                    calories = parsedCalories;
                }
                continue;
            }

            var item = ParseItem(line);
            if (!string.IsNullOrWhiteSpace(item.Name))
            {
                items.Add(item);
            }
        }

        if (items.Count == 0)
        {
            return null;
        }

        return new MealMenu(
            Slot: slot,
            Label: ToLabel(slot),
            ServiceTime: times.TryGetValue(slot, out var time) ? time : null,
            PriceWon: prices.TryGetValue(slot, out var price) ? price : null,
            Calories: calories,
            Items: items);
    }

    private static DiningItem ParseItem(string line)
    {
        var raw = line.Trim();
        var name = Regex.Replace(raw, @"\s*[,\s]*(?:\d{1,2}|[*])(?:\s*,\s*(?:\d{1,2}|[*]))*[,]?\s*$", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = raw;
        }

        return new DiningItem(name, raw);
    }

    private static IReadOnlyList<string> ExtractNotices(IReadOnlyList<string> lines)
    {
        return lines
            .Where(line => line.Contains("알레르기", StringComparison.Ordinal) || line.Contains("원산지", StringComparison.Ordinal))
            .Take(5)
            .ToArray();
    }

    private static bool TryParseSlot(string label, out MealSlot slot)
    {
        slot = label switch
        {
            "조식" => MealSlot.Breakfast,
            "중식" => MealSlot.Lunch,
            "석식" => MealSlot.Dinner,
            _ => MealSlot.Lunch
        };
        return label is "조식" or "중식" or "석식";
    }

    private static string ToLabel(MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => "조식",
        MealSlot.Lunch => "중식",
        MealSlot.Dinner => "석식",
        _ => "식사"
    };

    private static bool IsNoiseLine(string line)
    {
        return line is "조식" or "중식" or "석식" ||
               line.Contains("오늘의 메뉴 안내", StringComparison.Ordinal) ||
               line.Contains("정보제공", StringComparison.Ordinal);
    }

    private static bool IsFooterStart(string line)
    {
        return line.Contains("콘텐츠담당", StringComparison.Ordinal) ||
               line.Contains("담당부서", StringComparison.Ordinal) ||
               line.Contains("상세보기", StringComparison.Ordinal);
    }
}
