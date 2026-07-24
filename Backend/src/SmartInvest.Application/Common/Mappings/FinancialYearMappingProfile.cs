using AutoMapper;
using SmartInvest.Application.DTOs;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Application.Common.Mappings;

public class FinancialYearMappingProfile : Profile
{
    public FinancialYearMappingProfile()
    {
        CreateMap<FinancialYear, FinancialYearDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.FinancialYearId));
    }
}
