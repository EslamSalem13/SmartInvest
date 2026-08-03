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
                opt => opt.MapFrom(src => src.MeasurementSubPrograms.Select(x => x.SubProgram.SubProgramName).ToList()))
            .ForMember(
                dest => dest.UnitIds,
                opt => opt.MapFrom(src => src.MeasurementUnits.Select(x => x.UnitId).ToList()))
            .ForMember(
                dest => dest.UnitNames,
                opt => opt.MapFrom(src => src.MeasurementUnits.Select(x => x.Unit.Name).ToList()));
    }
}
