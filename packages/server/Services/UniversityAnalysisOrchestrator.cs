using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Options;

namespace SchoolRecommender.Api.Services;

/// <summary>
/// 大学报考分析编排器。
/// 并行分析多所大学，以流式方式逐条 yield 结果。
/// </summary>
public sealed class UniversityAnalysisOrchestrator(
    UniversityAnalysisAgent analysisAgent,
    SchoolServiceOptions options,
    ILogger<UniversityAnalysisOrchestrator> logger)
{
    private int MaxConcurrency => options.AnalysisMaxConcurrency;

    /// <summary>
    /// 并行分析多所大学，以流式方式逐条 yield 结果（先分析完的先返回）。
    /// </summary>
    public async IAsyncEnumerable<(string UniversityName, UniversityAnalysis Analysis)> AnalyzeStreamAsync(
        IReadOnlyList<string> universityNames,
        int studentScore,
        IReadOnlyList<string>? preferredMajors,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<(string UniversityName, UniversityAnalysis Analysis)>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        var concurrency = Math.Min(MaxConcurrency, Math.Max(universityNames.Count, 1));
        var throttler = new SemaphoreSlim(concurrency);
        logger.LogInformation("开始并行分析 {Count} 所大学，最大并发 {Concurrency}", universityNames.Count, concurrency);

        var producer = Task.Run(async () =>
        {
            try
            {
                var tasks = universityNames.Select(async name =>
                {
                    await throttler.WaitAsync(cancellationToken);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        UniversityAnalysis analysis;
                        try
                        {
                            logger.LogInformation("[UniAnalyze:Start] {University}", name);
                            analysis = await analysisAgent.AnalyzeUniversityAsync(name, studentScore, preferredMajors, cancellationToken);
                            logger.LogInformation("[UniAnalyze:Done] {University} 耗时 {Elapsed}ms", name, sw.ElapsedMilliseconds);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            logger.LogWarning(exception, "大学分析链路失败: {University}", name);
                            analysis = CreateFallback(name);
                        }

                        await channel.Writer.WriteAsync((name, analysis), cancellationToken);
                    }
                    finally
                    {
                        throttler.Release();
                    }
                });

                await Task.WhenAll(tasks);
                channel.Writer.TryComplete();
            }
            catch (Exception exception)
            {
                channel.Writer.TryComplete(exception);
            }
        }, cancellationToken);

        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }

        await producer;
    }

    private static UniversityAnalysis CreateFallback(string name)
    {
        return new UniversityAnalysis(
            new UniversityInfo(name, null, null, null, null, null),
            Array.Empty<UniversityAdmissionYear>(),
            Array.Empty<MajorAnalysis>(),
            OverallScore: 50,
            AdmissionMatchScore: 50,
            Summary: $"暂无法获取 {name} 的详细数据。");
    }
}
