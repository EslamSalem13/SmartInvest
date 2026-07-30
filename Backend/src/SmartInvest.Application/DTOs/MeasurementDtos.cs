namespace SmartInvest.Application.DTOs;

public class MeasurementDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public List<int> SubProgramIds { get; set; } = new();
    public List<string> SubProgramNames { get; set; } = new();
}

public class CreateMeasurementDto
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public List<int> SubProgramIds { get; set; } = new();
}

public class UpdateMeasurementDto
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public List<int> SubProgramIds { get; set; } = new();
}

public class SubProjectMeasurementValueDto
{
    public int MeasurementId { get; set; }
    public string MeasurementName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? Value { get; set; }
}

public class SetMeasurementValueDto
{
    public int MeasurementId { get; set; }
    public decimal? Value { get; set; }
}

public class SetSubProjectMeasurementValuesDto
{
    public List<SetMeasurementValueDto> Values { get; set; } = new();
}
