namespace SmartInvest.Application.DTOs;

public class ContractorDto
{
    public int Id { get; set; }
    public string ContractorName { get; set; } = string.Empty;
    public string CompanyType { get; set; } = string.Empty;
    public string NationalIdOrCommercialRegister { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<AssignedSubProjectDto> AssignedSubProjects { get; set; } = new();
}

public class CreateContractorDto
{
    public string ContractorName { get; set; } = string.Empty;
    public string CompanyType { get; set; } = string.Empty;
    public string NationalIdOrCommercialRegister { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class UpdateContractorDto
{
    public string ContractorName { get; set; } = string.Empty;
    public string CompanyType { get; set; } = string.Empty;
    public string NationalIdOrCommercialRegister { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
