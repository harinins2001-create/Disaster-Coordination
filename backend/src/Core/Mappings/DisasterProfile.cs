using AutoMapper;
using Core.Helpers;
using EDBDisaster = Core.Infrastructure.Persistence.Models.Disaster;
using EDBRequiredResource = Core.Infrastructure.Persistence.Models.RequiredResource;
using DTODisaster = Core.DTOs.DisasterDTO;
using DTORequiredResource = Core.DTOs.RequiredResourceDTO;

namespace Core.Mappings;

public class DisasterProfile : Profile
{
    public DisasterProfile()
    {
        CreateMap<EDBRequiredResource, DTORequiredResource>();
        CreateMap<DTORequiredResource, EDBRequiredResource>();

        // EDB -> DTO
        CreateMap<EDBDisaster, DTODisaster>()
            .ForMember(dest => dest.RequiredResources, opt => opt.MapFrom(src => src.RequiredResources ?? new List<EDBRequiredResource>()))
            .ForMember(dest => dest.PhotoKeys, opt => opt.MapFrom(src => src.PhotoKeys ?? new List<string>()))
            .ForMember(dest => dest.PhotoUrls, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => _Helpers.ParseDate(src.CreatedAt, null)))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => _Helpers.ParseDate(src.UpdatedAt, src.CreatedAt)));

        // DTO -> EDB
        CreateMap<DTODisaster, EDBDisaster>()
            .ForMember(dest => dest.Entity, opt => opt.MapFrom(_ => "disaster"))
            .ForMember(dest => dest.RequiredResources, opt => opt.MapFrom(src => src.RequiredResources ?? new List<DTORequiredResource>()))
            .ForMember(dest => dest.PhotoKeys, opt => opt.MapFrom(src => src.PhotoKeys ?? new List<string>()))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => _Helpers.FormatOrNow(src.CreatedAt, null)))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt == null ? null : _Helpers.FormatOrNow(src.UpdatedAt, null)))
            .AfterMap((src, dest) =>
            {
                if (!string.IsNullOrWhiteSpace(src.Slug))
                {
                    var keys = new Keys();
                    var (pk, sk) = keys.GenerateDisasterKeys(src.Slug);
                    dest.Pk = pk;
                    dest.Sk = sk;
                }
            });
    }
}
