namespace SmartInvest.Domain.Entities
{
    public class Measurement
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;

        public virtual ICollection<MeasurementSubProgram> MeasurementSubPrograms { get; set; }
        public virtual ICollection<SubProjectMeasurementValue> Values { get; set; }
    }
}
