using AutoMapper;
using Core.Helpers;
using EDBUser = Core.Infrastructure.Persistence.Models.User;
using DTOUser = Core.DTOs.UserDTO;

namespace Core.Mappings;

public class UserProfile : Profile
{
    public UserProfile()
    {
        // EDB -> DTO
        CreateMap<EDBUser, DTOUser>()
            .ForMember(dest => dest.Skills, opt => opt.MapFrom(src => src.Skills ?? new List<string>()))
            .ForMember(dest => dest.TravelMethods, opt => opt.MapFrom(src => src.TravelMethods ?? new List<string>()))
            .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => src.Roles ?? new List<string>()))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => _Helpers.ParseDate(src.CreatedAt, null)))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => _Helpers.ParseDate(src.UpdatedAt, src.CreatedAt)));

        // DTO -> EDB
        CreateMap<DTOUser, EDBUser>()
            .ForMember(dest => dest.Entity, opt => opt.MapFrom(_ => "user"))
            .ForMember(dest => dest.Skills, opt => opt.MapFrom(src => src.Skills ?? new List<string>()))
            .ForMember(dest => dest.TravelMethods, opt => opt.MapFrom(src => src.TravelMethods ?? new List<string>()))
            .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => src.Roles ?? new List<string>()))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => _Helpers.FormatOrNow(src.CreatedAt, null)))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt == null ? null : _Helpers.FormatOrNow(src.UpdatedAt, null)))
            .AfterMap((src, dest) =>
            {
                if (!string.IsNullOrWhiteSpace(src.Sub))
                {
                    var keys = new Keys();
                    var (pk, sk) = keys.GenerateUserKeys(src.Sub);
                    dest.Pk = pk;
                    dest.Sk = sk;
                }
            });
    }
}
