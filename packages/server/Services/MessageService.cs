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
        var replies = await _freeSql.Select<MessageReply, AppUser>()
            .LeftJoin((reply, user) => reply.UserId == user.Id)
            .Where((reply, _) => messageIds.Contains(reply.MessageId))
            .OrderBy((reply, _) => reply.CreatedAt)
            .ToListAsync((reply, user) => new MessageReplyDto(reply.Id, reply.MessageId, reply.UserId, user.DisplayName, reply.Content, ToUtcOffset(reply.CreatedAt)), cancellationToken);

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
}
