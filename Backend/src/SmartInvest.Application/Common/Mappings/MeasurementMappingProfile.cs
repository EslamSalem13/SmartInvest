using AutoMapper;
using SmartInvest.Application.DTOs;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Application.Common.Mappings;

public class MeasurementMappingProfile : Profile
{
    public MeasurementMappingProfile()
    {
        CreateMap<Measurement, MeasurementDto>()
            .ForMember(
                dest => dest.SubProgramIds,
                opt => opt.MapFrom(src => src.MeasurementSubPrograms.Select(x => x.SubProgramId).ToList()))
            .ForMember(
                dest => dest.SubProgramNames,
                opt => opt.MapFrom(src => src.MeasurementSubPrograms.Select(x => x.SubProgram.SubProgramName).ToList()));
    }
}
