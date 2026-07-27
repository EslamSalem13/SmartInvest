namespace SmartInvest.Application.DTOs.Program
{
    public class MainProgramDto
    {
        public string ProgramName { get; set; } = string.Empty;
        public List<SubProgramDto>? SubPrograms { get; set; }
    }
}
