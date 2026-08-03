namespace SmartInvest.Domain.Entities
{
    public class Unit
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
