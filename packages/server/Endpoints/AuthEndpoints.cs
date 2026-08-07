using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Services;

namespace SchoolRecommender.Api.Endpoints;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var authGroup = app.MapGroup("/api/auth")
            .WithTags("Auth")
            .RequireRateLimiting("api");

        authGroup.MapPost("/register/code",
            async Task<Results<Ok<ApiResponse<object>>, BadRequest<ApiResponse<object>>>> (
                [FromBody] SendRegisterCodeRequest request,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                var result = await authService.SendRegisterCodeAsync(request, cancellationToken);
                return result.Success
                    ? TypedResults.Ok(ApiResponse<object>.Ok(new { expiresInSeconds = 600 }, "验证码已发送，请查收邮箱"))
                    : TypedResults.BadRequest(ApiResponse<object>.Fail(result.Message ?? "验证码发送失败"));
            })
            .WithName("SendRegisterCode")
            .WithSummary("发送邮箱注册验证码");

        authGroup.MapPost("/register",
            async Task<Results<Ok<ApiResponse<AuthPayload>>, BadRequest<ApiResponse<object>>>> (
                [FromBody] RegisterRequest request,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                var result = await authService.RegisterAsync(request, cancellationToken);
                return result.Success && result.Payload is not null
                    ? TypedResults.Ok(ApiResponse<AuthPayload>.Ok(result.Payload, "注册成功"))
                    : TypedResults.BadRequest(ApiResponse<object>.Fail(result.Message ?? "注册失败"));
            })
            .WithName("Register")
            .WithSummary("使用邮箱注册");

        authGroup.MapPost("/login",
            async Task<Results<Ok<ApiResponse<AuthPayload>>, BadRequest<ApiResponse<object>>>> (
                [FromBody] LoginRequest request,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                var result = await authService.LoginAsync(request, cancellationToken);
                return result.Success && result.Payload is not null
                    ? TypedResults.Ok(ApiResponse<AuthPayload>.Ok(result.Payload, "登录成功"))
                    : TypedResults.BadRequest(ApiResponse<object>.Fail(result.Message ?? "登录失败"));
            })
            .WithName("Login")
            .WithSummary("使用邮箱登录");

        authGroup.MapGet("/me",
            async Task<Results<Ok<ApiResponse<SessionUser>>, UnauthorizedHttpResult>> (HttpContext httpContext, AuthService authService) =>
            {
                var user = await authService.GetUserFromRequestAsync(httpContext);
                return user is null
                    ? TypedResults.Unauthorized()
                    : TypedResults.Ok(ApiResponse<SessionUser>.Ok(user));
            })
            .WithName("GetCurrentUser")
            .WithSummary("获取当前登录用户");
    }
}
