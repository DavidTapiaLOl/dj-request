using System.Text.Json.Serialization;

namespace DjRequests.Api.Modules.Profiles.Dtos;

public sealed class SocialLinksRequest
{
    private string? _instagram;
    private string? _facebook;
    private string? _tiktok;
    private string? _youtube;
    private string? _soundcloud;

    public SocialLinksRequest()
    {
    }

    public SocialLinksRequest(
        string? Instagram,
        string? Facebook,
        string? Tiktok,
        string? Youtube,
        string? Soundcloud)
    {
        this.Instagram = Instagram;
        this.Facebook = Facebook;
        this.Tiktok = Tiktok;
        this.Youtube = Youtube;
        this.Soundcloud = Soundcloud;
    }

    public string? Instagram
    {
        get => _instagram;
        init
        {
            _instagram = value;
            HasInstagram = true;
        }
    }

    public string? Facebook
    {
        get => _facebook;
        init
        {
            _facebook = value;
            HasFacebook = true;
        }
    }

    public string? Tiktok
    {
        get => _tiktok;
        init
        {
            _tiktok = value;
            HasTiktok = true;
        }
    }

    public string? Youtube
    {
        get => _youtube;
        init
        {
            _youtube = value;
            HasYoutube = true;
        }
    }

    public string? Soundcloud
    {
        get => _soundcloud;
        init
        {
            _soundcloud = value;
            HasSoundcloud = true;
        }
    }

    [JsonIgnore]
    public bool HasInstagram { get; private set; }

    [JsonIgnore]
    public bool HasFacebook { get; private set; }

    [JsonIgnore]
    public bool HasTiktok { get; private set; }

    [JsonIgnore]
    public bool HasYoutube { get; private set; }

    [JsonIgnore]
    public bool HasSoundcloud { get; private set; }
}

public sealed class UpdateProfileRequest
{
    private string? _artistName;
    private string? _description;
    private int? _startYear;
    private SocialLinksRequest? _socialLinks;

    public UpdateProfileRequest()
    {
    }

    public UpdateProfileRequest(
        string? ArtistName,
        string? Description,
        int? StartYear,
        SocialLinksRequest? SocialLinks)
    {
        this.ArtistName = ArtistName;
        this.Description = Description;
        this.StartYear = StartYear;
        this.SocialLinks = SocialLinks;
    }

    public string? ArtistName
    {
        get => _artistName;
        init
        {
            _artistName = value;
            HasArtistName = true;
        }
    }

    public string? Description
    {
        get => _description;
        init
        {
            _description = value;
            HasDescription = true;
        }
    }

    public int? StartYear
    {
        get => _startYear;
        init
        {
            _startYear = value;
            HasStartYear = true;
        }
    }

    public SocialLinksRequest? SocialLinks
    {
        get => _socialLinks;
        init
        {
            _socialLinks = value;
            HasSocialLinks = true;
        }
    }

    [JsonIgnore]
    public bool HasArtistName { get; private set; }

    [JsonIgnore]
    public bool HasDescription { get; private set; }

    [JsonIgnore]
    public bool HasStartYear { get; private set; }

    [JsonIgnore]
    public bool HasSocialLinks { get; private set; }
}
