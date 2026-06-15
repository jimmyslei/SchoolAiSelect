using FreeSql;
using SchoolRecommender.Api.Data;
using SchoolRecommender.Api.Models;

namespace SchoolRecommender.Api.Services;

public sealed class MessageService
{
    private readonly IFreeSql _freeSql;

    public MessageService(IFreeSql freeSql)
    {
        _freeSql = freeSql;
    }

    // ───── 学校留言 ─────

    public async Task<IReadOnlyList<MessageThread>> GetMessagesAsync(string? schoolId, CancellationToken cancellationToken)
    {
        var messagesQuery = _freeSql.Select<Message, AppUser>()
            .LeftJoin((message, user) => message.UserId == user.Id);

        if (!string.IsNullOrWhiteSpace(schoolId))
        {
            var normalizedSchoolId = schoolId.Trim();
            messagesQuery = messagesQuery.Where((message, _) => message.SchoolId == normalizedSchoolId);
        }

        var rows = await messagesQuery
            .OrderByDescending((message, _) => message.CreatedAt)
            .Limit(50)
            .ToListAsync((message, user) => new MessageRow(message, user.DisplayName), cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<MessageThread>();
        }

        var messageIds = rows.Select(row => row.Message.Id).ToArray();
        var replyRows = await _freeSql.Select<MessageReply, AppUser>()
            .LeftJoin((reply, user) => reply.UserId == user.Id)
            .Where((reply, _) => messageIds.Contains(reply.MessageId))
            .OrderBy((reply, _) => reply.CreatedAt)
            .ToListAsync((reply, user) => new MessageReplyRow(
                reply.Id, reply.MessageId, reply.UserId, user.DisplayName, reply.Content, reply.CreatedAt), cancellationToken);

        var replies = replyRows.Select(r => new MessageReplyDto(
            r.Id, r.MessageId, r.UserId, r.DisplayName, r.Content, ToUtcOffset(r.CreatedAt))).ToList();

        var repliesByMessage = replies
            .GroupBy(reply => reply.MessageId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<MessageReplyDto>)group.ToArray());

        return rows.Select(row => new MessageThread(
            row.Message.Id,
            row.Message.SchoolId,
            row.Message.SchoolName,
            row.Message.UserId,
            row.AuthorName,
            row.Message.Content,
            ToUtcOffset(row.Message.CreatedAt),
            repliesByMessage.TryGetValue(row.Message.Id, out var itemReplies) ? itemReplies : Array.Empty<MessageReplyDto>()))
            .ToArray();
    }

    public async Task<MessageThread> CreateMessageAsync(SessionUser user, CreateMessageRequest request, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            UserId = user.Id,
            SchoolId = request.SchoolId.Trim(),
            SchoolName = request.SchoolName.Trim(),
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        ValidateMessage(message.SchoolId, message.SchoolName, message.Content);

        message.Id = (long)await _freeSql.Insert(message).ExecuteIdentityAsync(cancellationToken);
        return new MessageThread(message.Id, message.SchoolId, message.SchoolName, user.Id, user.DisplayName, message.Content, ToUtcOffset(message.CreatedAt), Array.Empty<MessageReplyDto>());
    }

    public async Task<MessageReplyDto?> CreateReplyAsync(SessionUser user, long messageId, CreateReplyRequest request, CancellationToken cancellationToken)
    {
        var exists = await _freeSql.Select<Message>().Where(message => message.Id == messageId).AnyAsync(cancellationToken);
        if (!exists)
        {
            return null;
        }

        var content = request.Content.Trim();
        ValidateContent(content);

        var reply = new MessageReply
        {
            MessageId = messageId,
            UserId = user.Id,
            Content = content,
            CreatedAt = DateTime.UtcNow,
        };
        reply.Id = (long)await _freeSql.Insert(reply).ExecuteIdentityAsync(cancellationToken);

        return new MessageReplyDto(reply.Id, reply.MessageId, user.Id, user.DisplayName, reply.Content, ToUtcOffset(reply.CreatedAt));
    }

    private static DateTimeOffset ToUtcOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static void ValidateMessage(string schoolId, string schoolName, string content)
    {
        if (string.IsNullOrWhiteSpace(schoolId) || schoolId.Length > 128)
        {
            throw new ArgumentException("学校编号无效");
        }

        if (string.IsNullOrWhiteSpace(schoolName) || schoolName.Length > 160)
        {
            throw new ArgumentException("学校名称无效");
        }

        ValidateContent(content);
    }

    private static void ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 1000)
        {
            throw new ArgumentException("留言内容需要在 1 到 1000 个字符之间");
        }
    }

    private sealed record MessageRow(Message Message, string AuthorName);
    private sealed record MessageReplyRow(long Id, long MessageId, long UserId, string DisplayName, string Content, DateTime CreatedAt);

    // ───── 系统留言 ─────

    public async Task<IReadOnlyList<SiteMessageThread>> GetSiteMessagesAsync(CancellationToken cancellationToken)
    {
        var rows = await _freeSql.Select<SiteMessage, AppUser>()
            .LeftJoin((msg, user) => msg.UserId == user.Id)
            .OrderByDescending((msg, _) => msg.CreatedAt)
            .Limit(100)
            .ToListAsync((msg, user) => new SiteMessageRow(msg, user.DisplayName), cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<SiteMessageThread>();
        }

        var messageIds = rows.Select(r => r.Message.Id).ToArray();
        var replyRows = await _freeSql.Select<SiteMessageReply, AppUser>()
            .LeftJoin((reply, user) => reply.UserId == user.Id)
            .Where((reply, _) => messageIds.Contains(reply.MessageId))
            .OrderBy((reply, _) => reply.CreatedAt)
            .ToListAsync((reply, user) => new SiteMessageReplyRow(
                reply.Id, reply.MessageId, reply.UserId, user.DisplayName,
                user.IsAdmin, reply.Content, reply.CreatedAt), cancellationToken);

        var replies = replyRows.Select(r => new SiteMessageReplyDto(
            r.Id, r.MessageId, r.UserId, r.DisplayName,
            r.IsAdmin, r.Content, r.CreatedAt)).ToList();

        var repliesByMessage = replies
            .GroupBy(r => r.MessageId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SiteMessageReplyDto>)g.ToArray());

        return rows.Select(r => new SiteMessageThread(
            r.Message.Id, r.Message.UserId, r.AuthorName,
            r.Message.Content, r.Message.CreatedAt,
            repliesByMessage.TryGetValue(r.Message.Id, out var list) ? list : Array.Empty<SiteMessageReplyDto>()))
            .ToArray();
    }

    public async Task<SiteMessageThread> CreateSiteMessageAsync(SessionUser user, CreateSiteMessageRequest request, CancellationToken cancellationToken)
    {
        var content = request.Content.Trim();
        ValidateContent(content);

        var msg = new SiteMessage
        {
            UserId = user.Id,
            Content = content,
            CreatedAt = DateTime.UtcNow,
        };
        msg.Id = (long)await _freeSql.Insert(msg).ExecuteIdentityAsync(cancellationToken);
        return new SiteMessageThread(msg.Id, user.Id, user.DisplayName, msg.Content, ToUtcOffset(msg.CreatedAt), Array.Empty<SiteMessageReplyDto>());
    }

    public async Task<SiteMessageReplyDto?> CreateSiteMessageReplyAsync(SessionUser adminUser, long messageId, CreateReplyRequest request, CancellationToken cancellationToken)
    {
        var exists = await _freeSql.Select<SiteMessage>().Where(m => m.Id == messageId).AnyAsync(cancellationToken);
        if (!exists)
        {
            return null;
        }

        var content = request.Content.Trim();
        ValidateContent(content);

        var reply = new SiteMessageReply
        {
            MessageId = messageId,
            UserId = adminUser.Id,
            Content = content,
            CreatedAt = DateTime.UtcNow,
        };
        reply.Id = (long)await _freeSql.Insert(reply).ExecuteIdentityAsync(cancellationToken);
        return new SiteMessageReplyDto(reply.Id, reply.MessageId, adminUser.Id, adminUser.DisplayName, adminUser.IsAdmin, reply.Content, ToUtcOffset(reply.CreatedAt));
    }

    private sealed record SiteMessageRow(SiteMessage Message, string AuthorName);
    private sealed record SiteMessageReplyRow(long Id, long MessageId, long UserId, string DisplayName, bool IsAdmin, string Content, DateTime CreatedAt);
}
