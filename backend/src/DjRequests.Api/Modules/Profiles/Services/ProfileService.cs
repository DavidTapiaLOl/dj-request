using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Profiles.Dtos;
using DjRequests.Api.Modules.Profiles.Options;
using DjRequests.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace DjRequests.Api.Modules.Profiles.Services;

public sealed class ProfileValidationException : Exception
{
    public ProfileValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Los datos del perfil no son válidos.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

public sealed class ProfileNotFoundException : Exception
{
    public ProfileNotFoundException() : base("Perfil no encontrado.")
    {
    }
}

public interface IProfileService
{
    Task<ProfileDto> GetMineAsync(Guid djId, CancellationToken cancellationToken = default);

    Task<ProfileDto> GetByDjIdAsync(
        Guid djId,
        DjAccount dj,
        CancellationToken cancellationToken = default);

    Task<ProfileDto> UpdateAsync(
        Guid djId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<ProfileDto> SetPhotoAsync(
        Guid djId,
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<ProfileDto> DeletePhotoAsync(Guid djId, CancellationToken cancellationToken = default);
}

public sealed class ProfileService(
    DjRequestsDbContext context,
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> options,
    TimeProvider timeProvider) : IProfileService
{
    private const int MaxDescriptionLength = 1500;
    private const int MaxArtistNameLength = 100;
    private const int MinStartYear = 1900;
    private const int MaxLinkLength = 2048;

    private static readonly string[] InstagramHosts =
    [
        "instagram.com",
        "www.instagram.com"
    ];

    private static readonly string[] FacebookHosts =
    [
        "facebook.com",
        "www.facebook.com",
        "m.facebook.com",
        "web.facebook.com"
    ];

    private static readonly string[] TiktokHosts =
    [
        "tiktok.com",
        "www.tiktok.com",
        "m.tiktok.com",
        "vm.tiktok.com"
    ];

    private static readonly string[] YoutubeHosts =
    [
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
        "music.youtube.com",
        "youtu.be",
        "www.youtu.be"
    ];

    private static readonly string[] SoundcloudHosts =
    [
        "soundcloud.com",
        "www.soundcloud.com",
        "m.soundcloud.com",
        "on.soundcloud.com"
    ];

    private readonly FileStorageOptions _storageOptions = options.Value;

    public async Task<ProfileDto> GetMineAsync(Guid djId, CancellationToken cancellationToken = default)
    {
        var dj = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == djId, cancellationToken)
            ?? throw new ProfileNotFoundException();

        var profile = await context.PerfilesDj
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.DjId == djId, cancellationToken);

        return ToDto(profile, dj);
    }

    public async Task<ProfileDto> GetByDjIdAsync(
        Guid djId,
        DjAccount dj,
        CancellationToken cancellationToken = default)
    {
        var profile = await context.PerfilesDj
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.DjId == djId, cancellationToken);

        return ToDto(profile, dj);
    }

    public async Task<ProfileDto> UpdateAsync(
        Guid djId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM djs WHERE \"Id\" = {djId} FOR UPDATE",
            cancellationToken);
        var dj = await context.Users
            .SingleOrDefaultAsync(u => u.Id == djId, cancellationToken)
            ?? throw new ProfileNotFoundException();

        var profile = await context.PerfilesDj
            .FirstOrDefaultAsync(p => p.DjId == djId, cancellationToken);

        var errors = new Dictionary<string, string[]>();

        var artistName = dj.NombreArtistico;
        if (request.HasArtistName && request.ArtistName is not null)
        {
            var trimmed = request.ArtistName.Trim();
            if (trimmed.Length == 0)
            {
                errors["ArtistName"] = ["El nombre artístico no puede estar vacío."];
            }
            else if (trimmed.Length > MaxArtistNameLength)
            {
                errors["ArtistName"] = [$"El nombre artístico no puede superar los {MaxArtistNameLength} caracteres."];
            }
            else
            {
                artistName = trimmed;
            }
        }

        var description = profile?.Descripcion;
        if (request.HasDescription)
        {
            description = null;
            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                var trimmed = request.Description.Trim();
                if (trimmed.Length > MaxDescriptionLength)
                {
                    errors["Description"] = [$"La descripción no puede superar los {MaxDescriptionLength} caracteres."];
                }
                else
                {
                    description = trimmed;
                }
            }
        }

        var startYear = request.HasStartYear ? request.StartYear : profile?.AnioInicio;
        var currentYear = timeProvider.GetUtcNow().Year;
        if (request.HasStartYear
            && startYear is not null
            && (startYear < MinStartYear || startYear > currentYear))
        {
            errors["StartYear"] = [$"El año de inicio debe estar entre {MinStartYear} y {currentYear}."];
        }

        var links = request.SocialLinks;
        var instagram = profile?.RedInstagram;
        var facebook = profile?.RedFacebook;
        var tiktok = profile?.RedTiktok;
        var youtube = profile?.RedYoutube;
        var soundcloud = profile?.RedSoundcloud;

        if (request.HasSocialLinks)
        {
            if (links is null)
            {
                instagram = null;
                facebook = null;
                tiktok = null;
                youtube = null;
                soundcloud = null;
            }
            else
            {
                if (links.HasInstagram)
                {
                    instagram = ValidateLink(links.Instagram, "Instagram", InstagramHosts, errors);
                }

                if (links.HasFacebook)
                {
                    facebook = ValidateLink(links.Facebook, "Facebook", FacebookHosts, errors);
                }

                if (links.HasTiktok)
                {
                    tiktok = ValidateLink(links.Tiktok, "Tiktok", TiktokHosts, errors);
                }

                if (links.HasYoutube)
                {
                    youtube = ValidateLink(links.Youtube, "Youtube", YoutubeHosts, errors);
                }

                if (links.HasSoundcloud)
                {
                    soundcloud = ValidateLink(links.Soundcloud, "Soundcloud", SoundcloudHosts, errors);
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ProfileValidationException(errors);
        }

        var now = timeProvider.GetUtcNow();
        var changesProfile = request.HasDescription || request.HasStartYear || request.HasSocialLinks;
        if (changesProfile && profile is null)
        {
            profile = new DjProfile
            {
                Id = Guid.NewGuid(),
                DjId = djId,
                CreatedAt = now
            };
            context.PerfilesDj.Add(profile);
        }

        if (changesProfile)
        {
            profile!.Descripcion = description;
            profile.AnioInicio = startYear;
            profile.RedInstagram = instagram;
            profile.RedFacebook = facebook;
            profile.RedTiktok = tiktok;
            profile.RedYoutube = youtube;
            profile.RedSoundcloud = soundcloud;
            profile.UpdatedAt = now;
        }

        if (request.HasArtistName && artistName != dj.NombreArtistico)
        {
            dj.NombreArtistico = artistName;
            dj.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToDto(profile, dj);
    }

    public async Task<ProfileDto> SetPhotoAsync(
        Guid djId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var (content, extension) = await ProcessPhotoAsync(file, cancellationToken);
        var newFileName = await fileStorage.SaveAsync(content, extension, cancellationToken);

        DjProfile profile;
        DjAccount dj;
        string? previousFileName;
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM djs WHERE \"Id\" = {djId} FOR UPDATE",
                cancellationToken);
            dj = await context.Users
                .SingleOrDefaultAsync(u => u.Id == djId, cancellationToken)
                ?? throw new ProfileNotFoundException();

            var now = timeProvider.GetUtcNow();
            var existing = await context.PerfilesDj
                .FirstOrDefaultAsync(p => p.DjId == djId, cancellationToken);

            if (existing is null)
            {
                existing = new DjProfile
                {
                    Id = Guid.NewGuid(),
                    DjId = djId,
                    CreatedAt = now
                };
                context.PerfilesDj.Add(existing);
            }

            previousFileName = existing.FotoNombreArchivo;
            existing.FotoNombreArchivo = newFileName;
            existing.UpdatedAt = now;

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            profile = existing;
        }
        catch
        {
            await fileStorage.DeleteAsync(newFileName, cancellationToken);
            throw;
        }

        if (!string.IsNullOrEmpty(previousFileName))
        {
            await fileStorage.DeleteAsync(previousFileName, cancellationToken);
        }

        return ToDto(profile, dj);
    }

    public async Task<ProfileDto> DeletePhotoAsync(
        Guid djId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM djs WHERE \"Id\" = {djId} FOR UPDATE",
            cancellationToken);
        var dj = await context.Users
            .SingleOrDefaultAsync(u => u.Id == djId, cancellationToken)
            ?? throw new ProfileNotFoundException();

        var profile = await context.PerfilesDj
            .FirstOrDefaultAsync(p => p.DjId == djId, cancellationToken);

        string? previousFileName = null;
        if (profile is not null && profile.FotoNombreArchivo is not null)
        {
            previousFileName = profile.FotoNombreArchivo;
            profile.FotoNombreArchivo = null;
            profile.UpdatedAt = timeProvider.GetUtcNow();
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        if (!string.IsNullOrEmpty(previousFileName))
        {
            await fileStorage.DeleteAsync(previousFileName, cancellationToken);
        }

        return ToDto(profile, dj);
    }

    private async Task<(byte[] Content, string Extension)> ProcessPhotoAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw InvalidPhoto("Selecciona una imagen válida.");
        }

        if (file.Length > _storageOptions.MaxFileSizeBytes)
        {
            throw InvalidPhoto($"La imagen no puede superar {FormatMegabytes(_storageOptions.MaxFileSizeBytes)} MB.");
        }

        byte[] bytes;
        await using (var stream = file.OpenReadStream())
        {
            bytes = await ReadAllBytesWithLimitAsync(stream, _storageOptions.MaxFileSizeBytes, cancellationToken);
        }

        ImageInfo info;
        using (var identifyStream = new MemoryStream(bytes, writable: false))
        {
            try
            {
                info = await Image.IdentifyAsync(identifyStream, cancellationToken);
            }
            catch (UnknownImageFormatException)
            {
                throw InvalidPhoto("El archivo no es una imagen compatible. Usa JPEG o PNG.");
            }
            catch (InvalidImageContentException)
            {
                throw InvalidPhoto("El archivo no es una imagen válida.");
            }
        }

        var format = info.Metadata.DecodedImageFormat;
        var isPng = format is PngFormat;
        var isJpeg = format is JpegFormat;

        if (!isPng && !isJpeg)
        {
            throw InvalidPhoto("Formato no compatible. Usa JPEG o PNG.");
        }

        if (info.Width <= 0 || info.Height <= 0)
        {
            throw InvalidPhoto("La imagen no tiene dimensiones válidas.");
        }

        if ((long)info.Width * info.Height > _storageOptions.MaxSourcePixels)
        {
            throw InvalidPhoto("La imagen tiene demasiados píxeles. Usa una imagen más pequeña.");
        }

        Image image;
        try
        {
            image = await Image.LoadAsync(new MemoryStream(bytes, writable: false), cancellationToken);
        }
        catch (UnknownImageFormatException)
        {
            throw InvalidPhoto("El archivo no es una imagen compatible. Usa JPEG o PNG.");
        }
        catch (InvalidImageContentException)
        {
            throw InvalidPhoto("El archivo no es una imagen válida.");
        }

        using (image)
        {
            if (image.Width > _storageOptions.MaxDimensionPx || image.Height > _storageOptions.MaxDimensionPx)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(_storageOptions.MaxDimensionPx, _storageOptions.MaxDimensionPx),
                    Mode = ResizeMode.Max
                }));
            }

            StripMetadata(image);

            using var output = new MemoryStream();
            if (isPng)
            {
                await image.SaveAsPngAsync(output, cancellationToken);
            }
            else
            {
                await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 82 }, cancellationToken);
            }

            return (output.ToArray(), isPng ? ".png" : ".jpg");
        }
    }

    private static void StripMetadata(Image image)
    {
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.CicpProfile = null;
        image.Metadata.GetPngMetadata().TextData.Clear();
    }

    private static async Task<byte[]> ReadAllBytesWithLimitAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw InvalidPhoto($"La imagen no puede superar {FormatMegabytes(maxBytes)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static ProfileValidationException InvalidPhoto(string message)
    {
        return new ProfileValidationException(new Dictionary<string, string[]>
        {
            ["File"] = [message]
        });
    }

    private static string? ValidateLink(
        string? value,
        string field,
        string[] allowedHosts,
        Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLinkLength)
        {
            errors[field] = [$"El enlace no puede superar los {MaxLinkLength} caracteres."];
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort
            || !allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            errors[field] = [$"El enlace debe ser una URL HTTPS válida de {allowedHosts[0]}."];
            return null;
        }

        return uri.AbsoluteUri;
    }

    private ProfileDto ToDto(DjProfile? profile, DjAccount dj)
    {
        var photoUrl = profile?.FotoNombreArchivo is { Length: > 0 } fileName
            ? fileStorage.GetPublicUrl(fileName)
            : null;

        return new ProfileDto(
            dj.NombreArtistico,
            profile?.Descripcion,
            profile?.AnioInicio,
            photoUrl,
            new SocialLinksDto(
                profile?.RedInstagram,
                profile?.RedFacebook,
                profile?.RedTiktok,
                profile?.RedYoutube,
                profile?.RedSoundcloud));
    }

    private static string FormatMegabytes(int bytes)
    {
        var megabytes = bytes / (1024d * 1024d);
        return megabytes.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}
