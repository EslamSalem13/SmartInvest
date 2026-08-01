namespace SmartInvest.Domain.Enums
{
    /// <summary>مراحل دورة التعاقدات المرتبطة بالمشروع الفرعي (بترتيب سير العمل).</summary>
    public enum ProcurementStage
    {
        TenderDocument = 1,
        Announcement = 2,
        OpeningEnvelopes = 3,
        TechnicalEvaluation = 4,
        FinancialEvaluation = 5,
        ContractAward = 6,
    }
}
