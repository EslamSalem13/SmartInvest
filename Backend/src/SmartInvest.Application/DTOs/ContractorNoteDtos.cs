namespace SmartInvest.Application.DTOs;

public class ContractorNoteDto
{
    public int Id { get; set; }
    public int? SubProjectId { get; set; }
    public string? SubProjectName { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsAiGenerated { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateContractorNoteDto
{
    public int? SubProjectId { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class SetWillWorkAgainDto
{
    public bool? WillWorkAgain { get; set; }
}
