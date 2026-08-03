namespace SmartInvest.Domain.Entities
{
    public class MeasurementSubProgram
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Measurement")]
        public int MeasurementId { get; set; }
        public virtual Measurement Measurement { get; set; }

        [ForeignKey("SubProgram")]
        public int SubProgramId { get; set; }
        public virtual SubProgram SubProgram { get; set; }
    }
}
