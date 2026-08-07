using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Services;

namespace SchoolRecommender.Api.Endpoints;

internal static class MessageEndpoints
{
    public static void MapMessageEndpoints(this WebApplication app)
    {
        var messageGroup = app.MapGroup("/api/messages")
            .WithTags("Messages")
            .RequireRateLimiting("api");

        messageGroup.MapGet("/",
            async Task<Ok<ApiResponse<IReadOnlyList<MessageThread>>>> (
                string? schoolId,
                MessageService messageService,
                CancellationToken cancellationToken) =>
            {
                var messages = await messageService.GetMessagesAsync(schoolId, cancellationToken);
                return TypedResults.Ok(ApiResponse<IReadOnlyList<MessageThread>>.Ok(messages));
            })
            .WithName("ListMessages")
            .WithSummary("获取留言列表，可按学校筛选");

        messageGroup.MapPost("/",
            async Task<Results<Ok<ApiResponse<MessageThread>>, BadRequest<ApiResponse<object>>, UnauthorizedHttpResult>> (
                HttpContext httpContext,
                [FromBody] CreateMessageRequest request,
                AuthService authService,
                MessageService messageService,
                CancellationToken cancellationToken) =>
            {
                var user = await authService.GetUserFromRequestAsync(httpContext);
                if (user is null)
                {
                    return TypedResults.Unauthorized();
                }

                try
                {
                    var message = await messageService.CreateMessageAsync(user, request, cancellationToken);
                    return TypedResults.Ok(ApiResponse<MessageThread>.Ok(message, "留言已发布"));
                }
                catch (ArgumentException exception)
                {
                    return TypedResults.BadRequest(ApiResponse<object>.Fail(exception.Message));
                }
            })
            .WithName("CreateMessage")
            .WithSummary("发布学校留言");

        messageGroup.MapPost("/{messageId:long}/replies",
            async Task<Results<Ok<ApiResponse<MessageReplyDto>>, BadRequest<ApiResponse<object>>, NotFound<ApiResponse<object>>, UnauthorizedHttpResult>> (
                HttpContext httpContext,
                long messageId,
                [FromBody] CreateReplyRequest request,
                AuthService authService,
                MessageService messageService,
                CancellationToken cancellationToken) =>
            {
                var user = await authService.GetUserFromRequestAsync(httpContext);
                if (user is null)
                {
                    return TypedResults.Unauthorized();
                }

                try
                {
                    var reply = await messageService.CreateReplyAsync(user, messageId, request, cancellationToken);
                    return reply is null
                        ? TypedResults.NotFound(ApiResponse<object>.Fail("留言不存在"))
                        : TypedResults.Ok(ApiResponse<MessageReplyDto>.Ok(reply, "回复已发布"));
                }
                catch (ArgumentException exception)
                {
                    return TypedResults.BadRequest(ApiResponse<object>.Fail(exception.Message));
                }
            })
            .WithName("CreateMessageReply")
            .WithSummary("回复留言");

        var siteMessageGroup = app.MapGroup("/api/site-messages")
            .WithTags("SiteMessages")
            .RequireRateLimiting("api");

        siteMessageGroup.MapGet("/",
            async Task<Ok<ApiResponse<IReadOnlyList<SiteMessageThread>>>> (
                MessageService messageService,
                CancellationToken cancellationToken) =>
            {
                var messages = await messageService.GetSiteMessagesAsync(cancellationToken);
                return TypedResults.Ok(ApiResponse<IReadOnlyList<SiteMessageThread>>.Ok(messages));
            })
            .WithName("ListSiteMessages")
            .WithSummary("获取系统留言列表（所有人可见）");

        siteMessageGroup.MapPost("/",
            async Task<Results<Ok<ApiResponse<SiteMessageThread>>, BadRequest<ApiResponse<object>>, UnauthorizedHttpResult>> (
                HttpContext httpContext,
                [FromBody] CreateSiteMessageRequest request,
                AuthService authService,
                MessageService messageService,
                CancellationToken cancellationToken) =>
            {
                var user = await authService.GetUserFromRequestAsync(httpContext);
                if (user is null)
                {
                    return TypedResults.Unauthorized();
                }

                try
                {
                    var message = await messageService.CreateSiteMessageAsync(user, request, cancellationToken);
                    return TypedResults.Ok(ApiResponse<SiteMessageThread>.Ok(message, "留言已发布"));
                }
                catch (ArgumentException exception)
                {
                    return TypedResults.BadRequest(ApiResponse<object>.Fail(exception.Message));
                }
            })
            .WithName("CreateSiteMessage")
            .WithSummary("发布系统留言（登录用户）");

        siteMessageGroup.MapPost("/{messageId:long}/replies",
            async Task<Results<Ok<ApiResponse<SiteMessageReplyDto>>, BadRequest<ApiResponse<object>>, NotFound<ApiResponse<object>>, UnauthorizedHttpResult, ForbidHttpResult>> (
                HttpContext httpContext,
                long messageId,
                [FromBody] CreateReplyRequest request,
                AuthService authService,
                MessageService messageService,
                CancellationToken cancellationToken) =>
            {
                var user = await authService.GetUserFromRequestAsync(httpContext);
                if (user is null)
                {
                    return TypedResults.Unauthorized();
                }

                if (!user.IsAdmin)
                {
                    return TypedResults.Forbid();
                }

                try
                {
                    var reply = await messageService.CreateSiteMessageReplyAsync(user, messageId, request, cancellationToken);
                    return reply is null
                        ? TypedResults.NotFound(ApiResponse<object>.Fail("留言不存在"))
                        : TypedResults.Ok(ApiResponse<SiteMessageReplyDto>.Ok(reply, "回复已发布"));
                }
                catch (ArgumentException exception)
                {
                    return TypedResults.BadRequest(ApiResponse<object>.Fail(exception.Message));
                }
            })
            .WithName("CreateSiteMessageReply")
            .WithSummary("管理员回复系统留言");
    }
}
