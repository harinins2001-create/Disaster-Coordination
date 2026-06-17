using AutoMapper;
using Core.Helpers;
using EDBAssignment = Core.Infrastructure.Persistence.Models.Assignment;
using DTOAssignment = Core.DTOs.AssignmentDTO;

namespace Core.Mappings;

public class AssignmentProfile : Profile
{
    public AssignmentProfile()
    {
        // EDB -> DTO
        CreateMap<EDBAssignment, DTOAssignment>()
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => _Helpers.ParseDate(src.CreatedAt, null)))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => _Helpers.ParseDate(src.UpdatedAt, src.CreatedAt)));

        // DTO -> EDB
        CreateMap<DTOAssignment, EDBAssignment>()
            .ForMember(dest => dest.Entity, opt => opt.MapFrom(_ => "assignment"))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => _Helpers.FormatOrNow(src.CreatedAt, null)))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt == null ? null : _Helpers.FormatOrNow(src.UpdatedAt, null)))
            .AfterMap((src, dest) =>
            {
                if (!string.IsNullOrWhiteSpace(src.DisasterSlug) && !string.IsNullOrWhiteSpace(src.UserSub))
                {
                    var keys = new Keys();
                    var (pk, sk) = keys.GenerateAssignmentKeys(src.DisasterSlug, src.UserSub);
                    dest.Pk = pk;
                    dest.Sk = sk;
                }
            });
    }
}
