using AutoMapper;
using Core.Helpers;
using EDBResource = Core.Infrastructure.Persistence.Models.Resource;
using DTOResource = Core.DTOs.ResourceDTO;

namespace Core.Mappings;

public class ResourceProfile : Profile
{
    public ResourceProfile()
    {
        // EDB -> DTO
        CreateMap<EDBResource, DTOResource>()
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => _Helpers.ParseDate(src.CreatedAt, null)))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => _Helpers.ParseDate(src.UpdatedAt, src.CreatedAt)));

        // DTO -> EDB
        CreateMap<DTOResource, EDBResource>()
            .ForMember(dest => dest.Entity, opt => opt.MapFrom(_ => "resource"))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => _Helpers.FormatOrNow(src.CreatedAt, null)))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt == null ? null : _Helpers.FormatOrNow(src.UpdatedAt, null)))
            .AfterMap((src, dest) =>
            {
                if (!string.IsNullOrWhiteSpace(src.DisasterSlug) && !string.IsNullOrWhiteSpace(src.ItemType))
                {
                    var keys = new Keys();
                    var (pk, sk) = keys.GenerateResourceKeys(src.DisasterSlug, src.ItemType);
                    dest.Pk = pk;
                    dest.Sk = sk;
                }
            });
    }
}
