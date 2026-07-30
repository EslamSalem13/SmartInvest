namespace SmartInvest.Application.DTOs;

public class ExecutiveAgencyDto
{
    public int Id { get; set; }
    public string AgencyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<AssignedSubProjectDto> AssignedSubProjects { get; set; } = new();
}

public class CreateExecutiveAgencyDto
{
    public string AgencyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

public class UpdateExecutiveAgencyDto
{
    public string AgencyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
