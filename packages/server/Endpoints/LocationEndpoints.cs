using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Services;

namespace SchoolRecommender.Api.Endpoints;

internal static class LocationEndpoints
{
    public static void MapLocationEndpoints(this WebApplication app)
    {
        var locationGroup = app.MapGroup("/api/location")
            .WithTags("Location")
            .RequireRateLimiting("api");

        locationGroup.MapPost("/geocode",
            async Task<Results<Ok<ApiResponse<GeoLocationResponse>>, BadRequest<ApiResponse<object>>, ProblemHttpResult>> (
                [FromBody] GeocodeRequest request,
                AmapService amapService,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Address))
                {
                    return TypedResults.BadRequest(ApiResponse<object>.Fail("请提供有效的地址"));
                }

                var result = await amapService.GeocodeAsync(request.Address, cancellationToken);
                return TypedResults.Ok(ApiResponse<GeoLocationResponse>.Ok(result));
            })
            .WithName("GeocodeAddress")
            .WithSummary("地址转坐标");

        locationGroup.MapPost("/reverse",
            async Task<Results<Ok<ApiResponse<ReverseGeocodeResponse>>, BadRequest<ApiResponse<object>>, ProblemHttpResult>> (
                [FromBody] ReverseGeocodeRequest request,
                AmapService amapService,
                CancellationToken cancellationToken) =>
            {
                if (!GeoValidator.IsValidLatitude(request.Latitude) || !GeoValidator.IsValidLongitude(request.Longitude))
                {
                    return TypedResults.BadRequest(ApiResponse<object>.Fail("请提供有效的经纬度"));
                }

                var result = await amapService.ReverseGeocodeAsync(request.Longitude, request.Latitude, cancellationToken);
                return TypedResults.Ok(ApiResponse<ReverseGeocodeResponse>.Ok(result));
            })
            .WithName("ReverseGeocode")
            .WithSummary("坐标转地址");

        locationGroup.MapGet("/ip",
            async Task<Ok<ApiResponse<IpLocationResponse>>> (
                HttpContext httpContext,
                AmapService amapService,
                CancellationToken cancellationToken) =>
            {
                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
                if (clientIp == "::1") clientIp = "127.0.0.1";
                try
                {
                    var result = await amapService.IpLocateAsync(clientIp, cancellationToken);
                    return TypedResults.Ok(ApiResponse<IpLocationResponse>.Ok(result));
                }
                catch (Exception exception)
                {
                    return TypedResults.Ok(ApiResponse<IpLocationResponse>.Fail(exception.Message, fallbackToBrowser: true));
                }
            })
            .WithName("LocateByIp")
            .WithSummary("根据访问者 IP 自动定位");
    }
}
