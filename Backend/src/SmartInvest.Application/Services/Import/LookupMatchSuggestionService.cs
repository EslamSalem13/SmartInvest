using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SmartInvest.Application.Common.Ai;
using SmartInvest.Application.Interfaces;

namespace SmartInvest.Application.Services.Import;

public class LookupMatchSuggestionService : ILookupMatchSuggestionService
{
    // Guards against a file with an unusually large number of distinct unresolved names
    // exhausting the shared AI gateway budget on a single preview call.
    private const int MaxUnresolvedNames = 500;

    private const string SystemPrompt = """
        أنت تُطابق أسماء عناصر غير محلولة (مثل أسماء مراكز أو مشروعات) مستخرجة من ملف استيراد بأسماء
        عناصر موجودة بالفعل في قاعدة البيانات، ضمن عدة فئات منفصلة لا علاقة بينها.
        سيُعطى لك مصفوفة JSON من الفئات، كل فئة تحتوي على مفتاح الفئة (categoryKey)، ومصفوفة
        الأسماء غير المحلولة (unresolvedNames)، ومصفوفة الأسماء الموجودة بالفعل لهذه الفئة تحديدًا
        (existingNames). قد يختلف الاسم غير المحلول عن الاسم الموجود بخطأ إملائي بسيط أو كلمة زائدة
        مثل "مركز"/"مدينة" أو تشكيل ناقص أو زائد فقط أو ترتيب مختلف قليلًا لنفس المعنى - وليس اسمًا
        لعنصر حقيقي مختلف.
        لتوفير المساحة، أعد فقط أرقام الفهارس (index بدءًا من 0) بدلًا من إعادة كتابة النصوص نفسها.
        أعد فقط مصفوفة JSON بنفس عدد الفئات وبنفس ترتيبها، كل عنصر فيها بمفتاح categoryKey ومصفوفة
        matches تحتوي فقط على الأزواج التي أنت واثق منها (احذف أي زوج غير واثق منه تمامًا - لا داعي
        لذكر كل عنصر). كل عنصر في matches كائن JSON بمفتاحين: u (رقم فهرس الاسم غير المحلول في
        unresolvedNames لنفس الفئة) وe (رقم فهرس أفضل تطابق له في existingNames لنفس الفئة).
        الشكل المطلوب بالضبط، بدون أي اختلاف ودون أي نص إضافي:
        [{"categoryKey": "...", "matches": [{"u": 0, "e": 3}]}]
        لا تخترع تطابقًا إن لم تكن واثقًا - من الأفضل حذف الزوج تمامًا بدلًا من ربط عنصرين مختلفين
        فعليًا ببعضهما. لا تُضف أي نص أو تعليق آخر ولا صيغة Markdown - أعد JSON خام فقط.
        """;

    private readonly IAiGatewayClient _aiGatewayClient;
    private readonly ILogger<LookupMatchSuggestionService> _logger;

    public LookupMatchSuggestionService(IAiGatewayClient aiGatewayClient, ILogger<LookupMatchSuggestionService> logger)
    {
        _aiGatewayClient = aiGatewayClient;
        _logger = logger;
    }

    private record CategoryInput(
        [property: JsonPropertyName("categoryKey")] string CategoryKey,
        [property: JsonPropertyName("unresolvedNames")] List<string> UnresolvedNames,
        [property: JsonPropertyName("existingNames")] List<string> ExistingNames);

    private record MatchIndexPair(
        [property: JsonPropertyName("u")] int UnresolvedIndex,
        [property: JsonPropertyName("e")] int ExistingIndex);

    private record CategoryOutput(
        [property: JsonPropertyName("categoryKey")] string CategoryKey,
        [property: JsonPropertyName("matches")] List<MatchIndexPair>? Matches);

    public async Task<Dictionary<string, Dictionary<string, string?>>> SuggestMatchesAsync(
        List<LookupMatchCategory> categories, CancellationToken cancellationToken = default)
    {
        var nonEmpty = categories.Where(c => c.UnresolvedNames.Count > 0 && c.ExistingNames.Count > 0).ToList();
        if (nonEmpty.Count == 0)
        {
            return new();
        }

        var totalUnresolved = nonEmpty.Sum(c => c.UnresolvedNames.Count);
        if (totalUnresolved > MaxUnresolvedNames)
        {
            _logger.LogWarning(
                "Skipping AI lookup-match suggestions: {Count} unresolved names exceeds the cap of {Max}",
                totalUnresolved, MaxUnresolvedNames);
            return new();
        }

        var input = nonEmpty.Select(c => new CategoryInput(c.CategoryKey, c.UnresolvedNames, c.ExistingNames)).ToList();
        var userMessage = JsonSerializer.Serialize(input);

        string outputText;
        try
        {
            outputText = await _aiGatewayClient.CompleteAsync(SystemPrompt, userMessage, 6000, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("AI lookup-match suggestion call failed; continuing without suggestions");
            return new();
        }

        List<CategoryOutput>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<CategoryOutput>>(
                AiResponseParsing.StripMarkdownFences(outputText), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI lookup-match suggestion response failed to parse; continuing without suggestions. Raw response: {Raw}", outputText);
            return new();
        }

        if (parsed == null)
        {
            return new();
        }

        var byCategory = nonEmpty.ToDictionary(c => c.CategoryKey);
        var result = new Dictionary<string, Dictionary<string, string?>>();
        foreach (var category in parsed)
        {
            if (category.Matches == null || !byCategory.TryGetValue(category.CategoryKey, out var input2))
            {
                continue;
            }

            // Index-based responses are inherently safe against invented names - the model can
            // only reference positions in the exact lists we sent, so no name-membership check is
            // needed here (unlike a text-echo response, which would have to be validated).
            var validated = new Dictionary<string, string?>();
            foreach (var pair in category.Matches)
            {
                if (pair.UnresolvedIndex < 0 || pair.UnresolvedIndex >= input2.UnresolvedNames.Count
                    || pair.ExistingIndex < 0 || pair.ExistingIndex >= input2.ExistingNames.Count)
                {
                    continue;
                }

                validated[input2.UnresolvedNames[pair.UnresolvedIndex]] = input2.ExistingNames[pair.ExistingIndex];
            }

            result[category.CategoryKey] = validated;
        }

        return result;
    }
}
