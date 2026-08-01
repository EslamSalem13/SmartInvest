using SmartInvest.Domain.Enums;

namespace SmartInvest.Application.Common;

/// <summary>تحويل بين قيم ProcurementStage ومفاتيح المسارات (kebab-case) المستخدمة في الـ API والواجهة.</summary>
public static class ProcurementStageKeys
{
    private static readonly Dictionary<ProcurementStage, string> ToKeyMap = new()
    {
        [ProcurementStage.TenderDocument] = "tender-document",
        [ProcurementStage.Announcement] = "announcement",
        [ProcurementStage.OpeningEnvelopes] = "opening-envelopes",
        [ProcurementStage.TechnicalEvaluation] = "technical-evaluation",
        [ProcurementStage.FinancialEvaluation] = "financial-evaluation",
        [ProcurementStage.ContractAward] = "contract-award",
    };

    private static readonly Dictionary<string, ProcurementStage> FromKeyMap =
        ToKeyMap.ToDictionary(x => x.Value, x => x.Key, StringComparer.OrdinalIgnoreCase);

    public static string ToKey(ProcurementStage stage) => ToKeyMap[stage];

    public static bool TryFromKey(string key, out ProcurementStage stage) =>
        FromKeyMap.TryGetValue(key, out stage);
}
