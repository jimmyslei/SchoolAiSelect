using FreeSql.DataAnnotations;

namespace SchoolRecommender.Api.Data;

[Table(Name = "school_analysis_logs")]
[Index("idx_analysis_session", nameof(SearchSessionId), false)]
[Index("idx_analysis_school_created", $"{nameof(SchoolId)}, {nameof(CreatedAt)}", false)]
public sealed class SchoolAnalysisLog
{
    [Column(Name = nameof(Id), IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column(Name = nameof(SearchSessionId), StringLength = 64, IsNullable = false)]
    public string SearchSessionId { get; set; } = string.Empty;

    [Column(Name = nameof(SchoolId), StringLength = 128, IsNullable = false)]
    public string SchoolId { get; set; } = string.Empty;

    [Column(Name = nameof(SchoolName), StringLength = 160, IsNullable = false)]
    public string SchoolName { get; set; } = string.Empty;

    [Column(Name = nameof(SchoolType), StringLength = 32, IsNullable = false)]
    public string SchoolType { get; set; } = string.Empty;

    [Column(Name = nameof(SearchLatitude), IsNullable = false)]
    public double SearchLatitude { get; set; }

    [Column(Name = nameof(SearchLongitude), IsNullable = false)]
    public double SearchLongitude { get; set; }

    [Column(Name = nameof(SearchRadius), IsNullable = false)]
    public int SearchRadius { get; set; }

    [Column(Name = nameof(SearchAddress), StringLength = 512, IsNullable = true)]
    public string? SearchAddress { get; set; }

    [Column(Name = nameof(SchoolDistance), IsNullable = false)]
    public int SchoolDistance { get; set; }

    [Column(Name = nameof(OverallScore), IsNullable = false)]
    public int OverallScore { get; set; }

    [Column(Name = nameof(Rank), IsNullable = false)]
    public int Rank { get; set; }

    [Column(Name = nameof(IsTopPick), IsNullable = false)]
    public bool IsTopPick { get; set; }

    [Column(Name = nameof(ActivityScore), IsNullable = false)]
    public int ActivityScore { get; set; }

    [Column(Name = nameof(ReputationScore), IsNullable = false)]
    public int ReputationScore { get; set; }

    [Column(Name = nameof(ContentQualityScore), IsNullable = false)]
    public int ContentQualityScore { get; set; }

    [Column(Name = nameof(TeachingStaffScore), IsNullable = false)]
    public int TeachingStaffScore { get; set; }

    [Column(Name = nameof(AwardsCount), IsNullable = false)]
    public int AwardsCount { get; set; }

    [Column(Name = nameof(StudentActivityScore), IsNullable = false)]
    public int StudentActivityScore { get; set; }

    [Column(Name = nameof(SourceCount), IsNullable = false)]
    public int SourceCount { get; set; }

    [Column(Name = nameof(AnalysisResultJson), StringLength = -1, IsNullable = false)]
    public string AnalysisResultJson { get; set; } = string.Empty;

    [Column(Name = nameof(ClientIp), StringLength = 64, IsNullable = true)]
    public string? ClientIp { get; set; }

    [Column(Name = nameof(UserAgent), StringLength = 512, IsNullable = true)]
    public string? UserAgent { get; set; }

    [Column(Name = nameof(CreatedAt), IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table(Name = "site_visit_stats")]
[Index("idx_visit_path", nameof(Path), true)]
public sealed class SiteVisitStat
{
    [Column(Name = nameof(Id), IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column(Name = nameof(Path), StringLength = 256, IsNullable = false)]
    public string Path { get; set; } = string.Empty;

    [Column(Name = nameof(VisitCount), IsNullable = false)]
    public long VisitCount { get; set; }

    [Column(Name = nameof(LastVisitedAt), IsNullable = false)]
    public DateTime LastVisitedAt { get; set; } = DateTime.UtcNow;

    [Column(Name = nameof(CreatedAt), IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Index("idx_user_email", nameof(Email), true)]
public sealed class AppUser
{
    [Column(Name = nameof(Id), IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column(Name = nameof(Email), StringLength = 320, IsNullable = false)]
    public string Email { get; set; } = string.Empty;

    [Column(Name = nameof(DisplayName), StringLength = 128, IsNullable = false)]
    public string DisplayName { get; set; } = string.Empty;

    [Column(Name = nameof(PasswordHash), StringLength = 512, IsNullable = false)]
    public string PasswordHash { get; set; } = string.Empty;

    [Column(Name = nameof(IsAdmin), IsNullable = false)]
    public bool IsAdmin { get; set; }

    [Column(Name = nameof(CreatedAt), IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Index("idx_email_code_email_created", $"{nameof(Email)}, {nameof(CreatedAt)}", false)]
public sealed class EmailVerificationCode
{
    [Column(Name = nameof(Id), IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column(Name = nameof(Email), StringLength = 320, IsNullable = false)]
    public string Email { get; set; } = string.Empty;

    [Column(Name = nameof(Code), StringLength = 16, IsNullable = false)]
    public string Code { get; set; } = string.Empty;

    [Column(Name = nameof(CreatedAt), IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(Name = nameof(ExpiresAt), IsNullable = false)]
    public DateTime ExpiresAt { get; set; }

    [Column(Name = nameof(UsedAt), IsNullable = true)]
    public DateTime? UsedAt { get; set; }
}

/// <summary>用户分析历史记录，支持三种类型：school / university / highschool</summary>
[Table(Name = "analysis_histories")]
[Index("idx_history_user_created", $"{nameof(UserId)}, {nameof(CreatedAt)}", false)]
[Index("idx_history_session", nameof(SessionType), false)]
public sealed class AnalysisHistory
{
    [Column(Name = nameof(Id), IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column(Name = nameof(UserId), IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>school | university | highschool</summary>
    [Column(Name = nameof(SessionType), StringLength = 32, IsNullable = false)]
    public string SessionType { get; set; } = string.Empty;

    /// <summary>展示用标题，如 "北京朝阳区·初中" 或 "清华大学 580分"</summary>
    [Column(Name = nameof(Title), StringLength = 256, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    /// <summary>请求参数 JSON（序列化原始 request）</summary>
    [Column(Name = nameof(RequestJson), StringLength = -1, IsNullable = false)]
    public string RequestJson { get; set; } = string.Empty;

    /// <summary>完整 done 事件 payload JSON</summary>
    [Column(Name = nameof(ResultJson), StringLength = -1, IsNullable = false)]
    public string ResultJson { get; set; } = string.Empty;

    /// <summary>简要摘要，如 "共分析 12 所学校" </summary>
    [Column(Name = nameof(Summary), StringLength = 512, IsNullable = true)]
    public string? Summary { get; set; }

    [Column(Name = nameof(CreatedAt), IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Index("idx_message_school_created", $"{nameof(SchoolId)}, {nameof(CreatedAt)}", false)]
public sealed class Message
{
    [Column(Name = nameof(Id), IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column(Name = nameof(UserId), IsNullable = false)]
    public long UserId { get; set; }

    [Column(Name = nameof(SchoolId), StringLength = 128, IsNullable = false)]
    public string SchoolId { get; set; } = string.Empty;

    [Column(Name = nameof(SchoolName), StringLength = 160, IsNullable = false)]
    public string SchoolName { get; set; } = string.Empty;

    [Column(Name = nameof(Content), StringLength = 1000, IsNullable = false)]
    public string Content { get; set; } = string.Empty;

    [Column(Name = nameof(CreatedAt), IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Index("idx_reply_message_created", $"{nameof(MessageId)}, {nameof(CreatedAt)}", false)]
public sealed class MessageReply
{
    [Column(Name = nameof(Id), IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column(Name = nameof(MessageId), IsNullable = false)]
    public long MessageId { get; set; }

    [Column(Name = nameof(UserId), IsNullable = false)]
    public long UserId { get; set; }

    [Column(Name = nameof(Content), StringLength = 1000, IsNullable = false)]
    public string Content { get; set; } = string.Empty;

    [Column(Name = nameof(CreatedAt), IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ───── 系统留言（不绑定学校） ─────

[Table(Name = "site_messages")]
[Index("idx_site_message_created", nameof(CreatedAt), false)]
public sealed class SiteMessage
{
    [Column(Name = nameof(Id), IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column(Name = nameof(UserId), IsNullable = false)]
    public long UserId { get; set; }

    [Column(Name = nameof(Content), StringLength = 1000, IsNullable = false)]
    public string Content { get; set; } = string.Empty;

    [Column(Name = nameof(CreatedAt), IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table(Name = "site_message_replies")]
[Index("idx_site_reply_message_created", $"{nameof(MessageId)}, {nameof(CreatedAt)}", false)]
public sealed class SiteMessageReply
{
    [Column(Name = nameof(Id), IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column(Name = nameof(MessageId), IsNullable = false)]
    public long MessageId { get; set; }

    [Column(Name = nameof(UserId), IsNullable = false)]
    public long UserId { get; set; }

    [Column(Name = nameof(Content), StringLength = 1000, IsNullable = false)]
    public string Content { get; set; } = string.Empty;

    [Column(Name = nameof(CreatedAt), IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
