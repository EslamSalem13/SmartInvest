namespace SmartInvest.Domain.Entities
{
    public class AccountingUnit
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
