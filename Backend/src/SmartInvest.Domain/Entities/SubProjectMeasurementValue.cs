namespace SmartInvest.Domain.Entities
{
    public class SubProjectMeasurementValue
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("SubProject")]
        public int SubProjectId { get; set; }
        public virtual SubProject SubProject { get; set; }

        [ForeignKey("Measurement")]
        public int MeasurementId { get; set; }
        public virtual Measurement Measurement { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Value { get; set; }
    }
}
