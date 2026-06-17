using AutoMapper;
using Core.Helpers;
using EDBDonation = Core.Infrastructure.Persistence.Models.Donation;
using DTODonation = Core.DTOs.DonationDTO;

namespace Core.Mappings;

public class DonationProfile : Profile
{
    public DonationProfile()
    {
        // EDB -> DTO
        CreateMap<EDBDonation, DTODonation>()
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => _Helpers.ParseDate(src.CreatedAt, null)))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => _Helpers.ParseDate(src.UpdatedAt, src.CreatedAt)));

        // DTO -> EDB
        CreateMap<DTODonation, EDBDonation>()
            .ForMember(dest => dest.Entity, opt => opt.MapFrom(_ => "donation"))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => _Helpers.FormatOrNow(src.CreatedAt, null)))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt == null ? null : _Helpers.FormatOrNow(src.UpdatedAt, null)))
            .AfterMap((src, dest) =>
            {
                if (!string.IsNullOrWhiteSpace(src.DisasterSlug) && !string.IsNullOrWhiteSpace(src.Id))
                {
                    var keys = new Keys();
                    var (pk, sk) = keys.GenerateDonationKeys(src.DisasterSlug, src.Id);
                    dest.Pk = pk;
                    dest.Sk = sk;
                }
            });
    }
}
