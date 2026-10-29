using DjRequests.Api.Modules.Rooms.Options;
using DjRequests.Api.Modules.Rooms.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DjRequests.IntegrationTests.Modules.Rooms;

public class RoomOptionsValidationTests
{
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static FrontendUrlOptionsValidator CreateValidator(string environmentName)
        => new(new TestHostEnvironment { EnvironmentName = environmentName });

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://example.com/")]
    [InlineData("https://sub.example.com/app")]
    public void FrontendUrl_ValidHttps_SucceedsInProduction(string url)
    {
        var result = CreateValidator("Production").Validate(Options.DefaultName, new FrontendUrlOptions { BaseUrl = url });
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.com")]
    public void FrontendUrl_Invalid_Fails(string url)
    {
        var result = CreateValidator("Production").Validate(Options.DefaultName, new FrontendUrlOptions { BaseUrl = url });
        Assert.True(result.Failed);
    }

    [Fact]
    public void FrontendUrl_WithCredentials_Fails()
    {
        var result = CreateValidator("Production").Validate(Options.DefaultName, new FrontendUrlOptions { BaseUrl = "https://user:pass@example.com" });
        Assert.True(result.Failed);
    }

    [Fact]
    public void FrontendUrl_WithQueryString_Fails()
    {
        var result = CreateValidator("Production").Validate(Options.DefaultName, new FrontendUrlOptions { BaseUrl = "https://example.com?key=value" });
        Assert.True(result.Failed);
    }

    [Fact]
    public void FrontendUrl_WithFragment_Fails()
    {
        var result = CreateValidator("Production").Validate(Options.DefaultName, new FrontendUrlOptions { BaseUrl = "https://example.com#section" });
        Assert.True(result.Failed);
    }

    [Fact]
    public void FrontendUrl_Http_FailsOutsideDevelopment()
    {
        var result = CreateValidator("Production").Validate(Options.DefaultName, new FrontendUrlOptions { BaseUrl = "http://localhost:3000" });
        Assert.True(result.Failed);
    }

    [Fact]
    public void FrontendUrl_HttpNonLoopback_FailsInDevelopment()
    {
        var result = CreateValidator("Development").Validate(Options.DefaultName, new FrontendUrlOptions { BaseUrl = "http://example.com" });
        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///var/www/app")]
    [InlineData("ws://example.com")]
    public void FrontendUrl_InvalidScheme_FailsInDevelopment(string url)
    {
        var result = CreateValidator("Development").Validate(Options.DefaultName, new FrontendUrlOptions { BaseUrl = url });
        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1:3000")]
    public void FrontendUrl_HttpLoopback_SucceedsInDevelopment(string url)
    {
        var result = CreateValidator("Development").Validate(Options.DefaultName, new FrontendUrlOptions { BaseUrl = url });
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("http://192.168.68.107:4200")]
    [InlineData("http://10.0.0.12:3000")]
    [InlineData("http://172.16.0.1:4200")]
    [InlineData("http://172.31.255.254:4200")]
    public void FrontendUrl_HttpPrivateLan_WithAllowHttpLan_SucceedsInDevelopment(string url)
    {
        var result = CreateValidator("Development").Validate(
            Options.DefaultName,
            new FrontendUrlOptions { BaseUrl = url, AllowHttpLan = true });
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void FrontendUrl_HttpLanWithoutAllowHttpLan_FailsInDevelopment()
    {
        var result = CreateValidator("Development").Validate(
            Options.DefaultName,
            new FrontendUrlOptions { BaseUrl = "http://192.168.68.107:4200" });
        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("http://8.8.8.8:4200")]
    [InlineData("http://172.32.0.1:4200")]
    [InlineData("http://172.15.255.254:4200")]
    [InlineData("http://[fd00::1]:4200")]
    [InlineData("http://mi-pc.local:4200")]
    public void FrontendUrl_HttpNonPrivateLan_WithAllowHttpLan_FailsInDevelopment(string url)
    {
        var result = CreateValidator("Development").Validate(
            Options.DefaultName,
            new FrontendUrlOptions { BaseUrl = url, AllowHttpLan = true });
        Assert.True(result.Failed);
    }

    [Fact]
    public void FrontendUrl_HttpPrivateLanWithAllowHttpLan_FailsOutsideDevelopment()
    {
        var result = CreateValidator("Production").Validate(
            Options.DefaultName,
            new FrontendUrlOptions { BaseUrl = "http://192.168.68.107:4200", AllowHttpLan = true });
        Assert.True(result.Failed);
    }

    [Fact]
    public void TimeZone_InvalidIana_ThrowsValidationException()
    {
        var ex = Assert.Throws<TimeZoneValidator.ValidationException>(
            () => TimeZoneValidator.ValidateAndResolve("Not/AZone"));
        Assert.Contains("IANA", ex.Message);
    }

    [Fact]
    public void TimeZone_WindowsId_ThrowsValidationException()
    {
        var ex = Assert.Throws<TimeZoneValidator.ValidationException>(
            () => TimeZoneValidator.ValidateAndResolve("Pacific Standard Time"));
        Assert.Contains("IANA", ex.Message);
    }

    [Fact]
    public void TimeZone_ValidIana_Resolves()
    {
        var info = TimeZoneValidator.ValidateAndResolve("America/Mexico_City");
        Assert.NotNull(info);
    }

    [Fact]
    public void TimeZone_Null_ResolvesDefault()
    {
        var info = TimeZoneValidator.ValidateAndResolve(null);
        Assert.NotNull(info);
    }

    [Fact]
    public void TimeZone_TryValidate_InvalidIana_ReturnsFalseWithUserMessage()
    {
        var ok = TimeZoneValidator.TryValidateAndResolve("Not/AZone", out var info, out var error);
        Assert.False(ok);
        Assert.Null(info);
        Assert.NotNull(error);
        Assert.DoesNotContain("Exception", error);
    }

    [Fact]
    public void TimeZone_SystemTimeZoneException_IsNotValidationException()
    {
        // TryValidateAndResolve solo captura ValidationException; SystemTimeZoneException debe propagarse como error de servidor
        Assert.False(typeof(TimeZoneValidator.SystemTimeZoneException).IsSubclassOf(typeof(TimeZoneValidator.ValidationException)));
    }
}
