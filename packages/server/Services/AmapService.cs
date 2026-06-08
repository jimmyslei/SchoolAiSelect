using Microsoft.Extensions.Caching.Memory;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchoolRecommender.Api.Services;

public sealed class AmapService(HttpClient httpClient, SchoolServiceOptions options, ILogger<AmapService> logger, IMemoryCache cache)
{
    private static readonly IReadOnlyDictionary<string, string> SchoolTypeCodeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [SchoolTypes.Kindergarten] = "141204",
        [SchoolTypes.Primary] = "141203",
        [SchoolTypes.JuniorHigh] = "141202",
        [SchoolTypes.SeniorHigh] = "141202",
    };

    public async Task<GeoLocationResponse> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["address"] = address,
            ["key"] = options.AmapApiKey,
        };
        parameters["sig"] = BuildSignature(parameters, options.AmapSecurityKey);

        var uri = BuildUri("v3/geocode/geo", parameters);

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

        var uri = BuildUri("v3/geocode/regeo", parameters);

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
        };

        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            parameters["ip"] = ipAddress;
        }

        parameters["sig"] = BuildSignature(parameters, options.AmapSecurityKey);

        var uri = BuildUri("v3/ip", parameters);
        using var document = await GetJsonAsync(uri, cancellationToken);
        EnsureAmapSuccess(document.RootElement, "高德 IP 定位失败");

        var rectangle = ReadAmapString(document.RootElement, "rectangle");
        if (string.IsNullOrWhiteSpace(rectangle))
        {
            // 内网 / 无法定位的 IP 时，高德会把字段返回成空数组 []，此处给出明确错误，让前端走浏览器定位兜底
            throw new InvalidOperationException("无法根据当前 IP 进行定位，请使用浏览器定位");
        }

        var points = rectangle.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var southwest = points[0].Split(',');
        var northeast = points[1].Split(',');

        var longitude = (double.Parse(southwest[0], CultureInfo.InvariantCulture) + double.Parse(northeast[0], CultureInfo.InvariantCulture)) / 2;
        var latitude = (double.Parse(southwest[1], CultureInfo.InvariantCulture) + double.Parse(northeast[1], CultureInfo.InvariantCulture)) / 2;
        var province = ReadAmapString(document.RootElement, "province");
        var city = ReadAmapString(document.RootElement, "city");
        var district = ReadAmapString(document.RootElement, "district");
        var address = string.Concat(new[] { province, city, district }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return new IpLocationResponse(latitude, longitude, province, city, district, string.IsNullOrWhiteSpace(address) ? "当前位置" : address);
    }

    /// <summary>
    /// 高德某些字段在无数据时会返回空数组 []（而不是空字符串），统一做安全读取。
    /// </summary>
    private static string ReadAmapString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return string.Empty;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.ToString(),
            _ => string.Empty,
        };
    }

    public async Task<IReadOnlyList<AmapPoi>> SearchNearbySchoolsAsync(double longitude, double latitude, string schoolType, int radius, CancellationToken cancellationToken)
    {
        if (!SchoolTypeCodeMap.TryGetValue(schoolType, out var typeCode))
        {
            throw new InvalidOperationException($"未知学校类型: {schoolType}");
        }

        // 将坐标量化到小数点后3位（约 110m 粒度）以提高缓存命中率
        var quantizedLng = Math.Round(longitude, 3);
        var quantizedLat = Math.Round(latitude, 3);
        var cacheKey = $"amap:pois:{quantizedLng}:{quantizedLat}:{schoolType}:{radius}";

        if (cache.TryGetValue(cacheKey, out IReadOnlyList<AmapPoi>? cached) && cached is not null)
        {
            logger.LogDebug("命中高德 POI 缓存: {CacheKey}", cacheKey);
            return cached;
        }

        var parameters = new Dictionary<string, string>
        {
            ["key"] = options.AmapApiKey,
            ["location"] = $"{longitude.ToString(CultureInfo.InvariantCulture)},{latitude.ToString(CultureInfo.InvariantCulture)}",
            ["types"] = typeCode,
            ["radius"] = radius.ToString(CultureInfo.InvariantCulture),
            ["page_size"] = options.AmapPageSize.ToString(CultureInfo.InvariantCulture),
            ["sortrule"] = "distance",
            ["show_fields"] = "business",
        };
        parameters["sig"] = BuildSignature(parameters, options.AmapSecurityKey);

        var uri = BuildUri("v5/place/around", parameters);

        using var document = await GetJsonAsync(uri, cancellationToken);
        EnsureAmapSuccess(document.RootElement, "高德周边学校搜索失败");

        var results = new List<AmapPoi>();
        foreach (var poi in document.RootElement.GetProperty("pois").EnumerateArray())
        {
            string? tel = null;
            if (poi.TryGetProperty("business", out var businessElement) &&
                businessElement.ValueKind == JsonValueKind.Object &&
                businessElement.TryGetProperty("tel", out var telElement) &&
                telElement.ValueKind == JsonValueKind.String)
            {
                tel = telElement.GetString();
            }

            results.Add(new AmapPoi(
                poi.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                poi.GetProperty("name").GetString() ?? string.Empty,
                poi.GetProperty("cityname").GetString() ?? string.Empty,
                poi.GetProperty("pname").GetString() ?? string.Empty,
                poi.GetProperty("adname").GetString() ?? string.Empty,
                poi.TryGetProperty("address", out var addressElement) ? ReadAmapString(poi, "address") : string.Empty,
                poi.GetProperty("location").GetString() ?? "0,0",
                poi.TryGetProperty("distance", out var distanceElement) ? distanceElement.ToString() : "0",
                tel));
        }

        // 初中/高中共用中学 POI 类别码，需按名称进一步过滤
        if (SchoolTypes.IsMiddleSchool(schoolType))
        {
            results = FilterMiddleSchoolPois(results, schoolType).ToList();
        }

        cache.Set(cacheKey, results, TimeSpan.FromMinutes(30));
        return results;
    }

    /// <summary>
    /// 对中学类 POI 结果按学校名称过滤：高中只保留含"高中"的结果，初中排除含"高中"的结果。
    /// </summary>
    private static IReadOnlyList<AmapPoi> FilterMiddleSchoolPois(IReadOnlyList<AmapPoi> pois, string schoolType)
    {
        if (string.Equals(schoolType, SchoolTypes.SeniorHigh, StringComparison.OrdinalIgnoreCase))
        {
            return pois.Where(p => p.Name.Contains("高中", StringComparison.Ordinal)
                                 || p.Name.Contains("高级中学", StringComparison.Ordinal)
                                 || p.Name.Contains("中学", StringComparison.Ordinal) && !p.Name.Contains("初中", StringComparison.Ordinal))
                       .ToList();
        }

        if (string.Equals(schoolType, SchoolTypes.JuniorHigh, StringComparison.OrdinalIgnoreCase))
        {
            return pois.Where(p => !p.Name.Contains("高中", StringComparison.Ordinal)
                                 && !p.Name.Contains("高级中学", StringComparison.Ordinal))
                       .ToList();
        }

        return pois;
    }

    /// <summary>
    /// 周边生活设施搜索（地铁站、医院、超市等）
    /// </summary>
    public async Task<IReadOnlyList<AmapPoi>> SearchNearbyFacilitiesAsync(double longitude, double latitude, string facilityType, int radius, CancellationToken cancellationToken)
    {
        // 高德 POI 类型编码映射
        var typeCode = facilityType switch
        {
            "subway" => "150500",    // 地铁站
            "hospital" => "090100",  // 医院
            "supermarket" => "060400", // 超市
            "park" => "110101",      // 公园
            "library" => "141200",   // 图书馆
            "bookstore" => "061201", // 书店
            _ => facilityType,
        };

        var cacheKey = $"amap:facilities:{Math.Round(longitude, 3)}:{Math.Round(latitude, 3)}:{facilityType}:{radius}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<AmapPoi>? cached) && cached is not null)
        {
            return cached;
        }

        var parameters = new Dictionary<string, string>
        {
            ["key"] = options.AmapApiKey,
            ["location"] = $"{longitude.ToString(CultureInfo.InvariantCulture)},{latitude.ToString(CultureInfo.InvariantCulture)}",
            ["types"] = typeCode,
            ["radius"] = radius.ToString(CultureInfo.InvariantCulture),
            ["page_size"] = "10",
            ["sortrule"] = "distance",
        };
        parameters["sig"] = BuildSignature(parameters, options.AmapSecurityKey);

        var uri = BuildUri("v5/place/around", parameters);

        using var document = await GetJsonAsync(uri, cancellationToken);
        EnsureAmapSuccess(document.RootElement, "高德周边设施搜索失败");

        var results = new List<AmapPoi>();
        foreach (var poi in document.RootElement.GetProperty("pois").EnumerateArray())
        {
            results.Add(new AmapPoi(
                poi.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                poi.GetProperty("name").GetString() ?? string.Empty,
                poi.GetProperty("cityname").GetString() ?? string.Empty,
                poi.GetProperty("pname").GetString() ?? string.Empty,
                poi.GetProperty("adname").GetString() ?? string.Empty,
                poi.TryGetProperty("address", out var addressElement) ? ReadAmapString(poi, "address") : string.Empty,
                poi.GetProperty("location").GetString() ?? "0,0",
                poi.TryGetProperty("distance", out var distanceElement) ? distanceElement.ToString() : "0",
                null));
        }

        cache.Set(cacheKey, results, TimeSpan.FromMinutes(60));
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