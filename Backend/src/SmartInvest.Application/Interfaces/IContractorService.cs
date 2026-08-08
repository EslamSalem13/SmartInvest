using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IContractorService
{
    Task<IReadOnlyList<ContractorDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ContractorDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ContractorDto> CreateAsync(CreateContractorDto dto, CancellationToken cancellationToken = default);

    Task<ContractorDto> UpdateAsync(int id, UpdateContractorDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<ContractorDto> SetWillWorkAgainAsync(int id, SetWillWorkAgainDto dto, CancellationToken cancellationToken = default);

    Task<ContractorNoteDto> AddNoteAsync(int id, CreateContractorNoteDto dto, CancellationToken cancellationToken = default);
}
