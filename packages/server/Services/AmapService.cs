using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Options;
using System.Globalization;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SchoolRecommender.Api.Services;

public sealed class AmapService(HttpClient httpClient, SchoolServiceOptions options, ILogger<AmapService> logger)
{
    private static readonly IReadOnlyDictionary<string, string> SchoolTypeCodeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [SchoolTypes.Kindergarten] = "141204",
        [SchoolTypes.Primary] = "141203",
        [SchoolTypes.Middle] = "141202",
    };

    public async Task<GeoLocationResponse> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["address"] = address,
            ["key"] = options.AmapApiKey,
        };
        parameters["sig"] = BuildSignature(parameters, options.AmapSecurityKey);

        var uri = BuildUri("geocode/geo", parameters);

        using var document = await GetJsonAsync(uri, cancellationToken);
        EnsureAmapSuccess(document.RootElement, "高德地理编码失败");

        var first = document.RootElement.GetProperty("geocodes")[0];
        var location = first.GetProperty("location").GetString() ?? "0,0";
        var parts = location.Split(',');
        var longitude = parts.Length > 0 ? double.Parse(parts[0], CultureInfo.InvariantCulture) : 0;
        var latitude = parts.Length > 1 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : 0;

        return new GeoLocationResponse(latitude, longitude, first.GetProperty("formatted_address").GetString() ?? address);
    }

    public async Task<ReverseGeocodeResponse> ReverseGeocodeAsync(double longitude, double latitude, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["key"] = options.AmapApiKey,
            ["location"] = $"{longitude.ToString(CultureInfo.InvariantCulture)},{latitude.ToString(CultureInfo.InvariantCulture)}",
        };
        parameters["sig"] = BuildSignature(parameters, options.AmapSecurityKey);

        var uri = BuildUri("geocode/regeo", parameters);

        using var document = await GetJsonAsync(uri, cancellationToken);
        EnsureAmapSuccess(document.RootElement, "高德逆地理编码失败");

        var regeocode = document.RootElement.GetProperty("regeocode");
        var component = regeocode.GetProperty("addressComponent");

        return new ReverseGeocodeResponse(
            regeocode.GetProperty("formatted_address").GetString() ?? string.Empty,
            component.GetProperty("province").GetString() ?? string.Empty,
            component.TryGetProperty("city", out var cityElement) ? cityElement.ToString() : string.Empty,
            component.GetProperty("district").GetString() ?? string.Empty);
    }

    public async Task<IpLocationResponse> IpLocateAsync(string? ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.AmapApiKey) || string.IsNullOrWhiteSpace(options.AmapSecurityKey))
        {
            throw new InvalidOperationException("高德地图 IP 定位所需的 KEY 或 SK 未配置");
        }

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["key"] = options.AmapApiKey,
            ["output"] = "json",
        };

        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            parameters["ip"] = ipAddress;
        }

        parameters["sig"] = BuildSignature(parameters, options.AmapSecurityKey);

        var uri = BuildUri("ip", parameters);
        using var document = await GetJsonAsync(uri, cancellationToken);
        EnsureAmapSuccess(document.RootElement, "高德 IP 定位失败");

        var rectangle = document.RootElement.GetProperty("rectangle").GetString();
        if (string.IsNullOrWhiteSpace(rectangle))
        {
            throw new InvalidOperationException("高德 IP 定位未返回有效坐标范围");
        }

        var points = rectangle.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var southwest = points[0].Split(',');
        var northeast = points[1].Split(',');

        var longitude = (double.Parse(southwest[0], CultureInfo.InvariantCulture) + double.Parse(northeast[0], CultureInfo.InvariantCulture)) / 2;
        var latitude = (double.Parse(southwest[1], CultureInfo.InvariantCulture) + double.Parse(northeast[1], CultureInfo.InvariantCulture)) / 2;
        var province = document.RootElement.GetProperty("province").GetString() ?? string.Empty;
        var city = document.RootElement.TryGetProperty("city", out var cityElement) ? cityElement.ToString() : string.Empty;
        var district = document.RootElement.GetProperty("district").GetString() ?? string.Empty;
        var address = string.Concat(new[] { province, city, district }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return new IpLocationResponse(latitude, longitude, province, city, district, string.IsNullOrWhiteSpace(address) ? "当前位置" : address);
    }

    public async Task<IReadOnlyList<AmapPoi>> SearchNearbySchoolsAsync(double longitude, double latitude, string schoolType, int radius, CancellationToken cancellationToken)
    {
        if (!SchoolTypeCodeMap.TryGetValue(schoolType, out var typeCode))
        {
            throw new InvalidOperationException($"未知学校类型: {schoolType}");
        }
        var parameters = new Dictionary<string, string>
        {
            ["key"] = options.AmapApiKey,
            ["location"] = $"{longitude.ToString(CultureInfo.InvariantCulture)},{latitude.ToString(CultureInfo.InvariantCulture)}",
            ["types"] = typeCode,
            ["radius"] = radius.ToString(CultureInfo.InvariantCulture),
            ["offset"] = "20",
            ["sortrule"] = "distance",
            ["extensions"] = "all",
        };
        parameters["sig"] = BuildSignature(parameters, options.AmapSecurityKey);

        var uri = BuildUri("place/around", parameters);

        using var document = await GetJsonAsync(uri, cancellationToken);
        EnsureAmapSuccess(document.RootElement, "高德周边学校搜索失败");

        var results = new List<AmapPoi>();
        foreach (var poi in document.RootElement.GetProperty("pois").EnumerateArray())
        {
            results.Add(new AmapPoi(
                poi.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                poi.GetProperty("name").GetString() ?? string.Empty,
                poi.TryGetProperty("address", out var addressElement) ? addressElement.ToString() : string.Empty,
                poi.GetProperty("location").GetString() ?? "0,0",
                poi.TryGetProperty("distance", out var distanceElement) ? distanceElement.ToString() : "0",
                poi.TryGetProperty("tel", out var telElement) ? telElement.ToString() : null));
        }

        return results;
    }

    private async Task<JsonDocument> GetJsonAsync(string uri, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(uri, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("AMap request failed: {StatusCode} {Content}", response.StatusCode, content);
            throw new InvalidOperationException($"高德接口请求失败: {(int)response.StatusCode}");
        }

        return JsonDocument.Parse(content);
    }

    private static string BuildUri(string relativePath, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var query = string.Join("&", parameters.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return $"{relativePath}?{query}";
    }

    private static void EnsureAmapSuccess(JsonElement root, string prefix)
    {
        var status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
        if (status == "1")
        {
            return;
        }

        var info = root.TryGetProperty("info", out var infoElement) ? infoElement.GetString() : "未知错误";
        throw new InvalidOperationException($"{prefix}: {info}");
    }

    private static string BuildSignature(IEnumerable<KeyValuePair<string, string>> parameters, string secret)
    {
        var source = string.Concat(string.Join("&", parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}")), secret);
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}