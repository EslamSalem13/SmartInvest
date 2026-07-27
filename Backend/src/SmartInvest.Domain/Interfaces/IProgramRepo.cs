namespace SmartInvest.Domain.Interfaces
{
    public interface IProgramRepo : IGenericRepository<MainProgram>
    {
       Task<IEnumerable<MainProgram>> GetProgramsTreeAsync(string? planName, PlanStatus? planStatus, string? mainProgramName);
        Task<IEnumerable<MainProgram>> GetCurrentProgramsTreeAsync();
    }
}
