using SmartInvest.Application.DTOs.Program;

namespace SmartInvest.Application.Interfaces
{
    public interface IProgramService
    {
        Task<IEnumerable<MainProgram>> GetProgramsTreeAsync(string? planName, PlanStatus? planStatus, string? mainProgramName);
        Task<IEnumerable<MainProgramDto>> GetCurrentProgramsTreeAsync();
    }
}
