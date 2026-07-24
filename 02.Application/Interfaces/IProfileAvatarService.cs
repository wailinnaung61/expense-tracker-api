using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Application.Interfaces;

public interface IProfileAvatarService
{
    IReadOnlyList<AvatarPresetDto> ListPresets(string publicBaseUrl);
    Task<AvatarDto> ResolveAsync(MemberProfile profile, string publicBaseUrl);
    Task<MemberProfile> SelectPresetAsync(MemberProfile profile, string presetId);
    Task<MemberProfile> UploadAsync(MemberProfile profile, Stream fileStream, string contentType, string fileName);
    Task<MemberProfile> ClearUploadAsync(MemberProfile profile);
}
