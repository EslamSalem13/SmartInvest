namespace SmartInvest.Domain.Entities
{
    public class MeasurementUnit
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Measurement")]
        public int MeasurementId { get; set; }
        public virtual Measurement Measurement { get; set; }

        [ForeignKey("Unit")]
        public int UnitId { get; set; }
        public virtual Unit Unit { get; set; }
    }
}
