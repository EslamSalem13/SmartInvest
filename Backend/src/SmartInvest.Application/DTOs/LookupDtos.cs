namespace SmartInvest.Application.DTOs;

public class LookupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SubProgramLookupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MainProgramId { get; set; }
}

public class MarkazLookupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int GovernorateId { get; set; }
}

public class VillageLookupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MarkazId { get; set; }
}

public class CreateNamedLookupDto
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateNamedLookupDto
{
    public string Name { get; set; } = string.Empty;
}

public class CreateSubProgramDto
{
    public string Name { get; set; } = string.Empty;
    public int MainProgramId { get; set; }
}

public class UpdateSubProgramDto
{
    public string Name { get; set; } = string.Empty;
    public int MainProgramId { get; set; }
}

public class CreateMarkazDto
{
    public string Name { get; set; } = string.Empty;
    public int GovernorateId { get; set; }
}

public class UpdateMarkazDto
{
    public string Name { get; set; } = string.Empty;
    public int GovernorateId { get; set; }
}

public class CreateVillageDto
{
    public string Name { get; set; } = string.Empty;
    public int MarkazId { get; set; }
}

public class UpdateVillageDto
{
    public string Name { get; set; } = string.Empty;
    public int MarkazId { get; set; }
}