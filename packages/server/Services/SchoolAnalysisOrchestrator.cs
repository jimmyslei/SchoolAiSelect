using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Options;

namespace SchoolRecommender.Api.Services;

public sealed class SchoolAnalysisOrchestrator(
    SchoolAnalysisAgent analysisAgent,
    SchoolServiceOptions options,
    ILogger<SchoolAnalysisOrchestrator> logger)
{
    private int MaxConcurrency => options.AnalysisMaxConcurrency;

    /// <summary>
    /// 并行分析多所学校，以流式方式逐条 yield 结果（先分析完的学校先返回）。
    /// 并发度由 <see cref="SchoolServiceOptions.AnalysisMaxConcurrency"/> 控制。
    /// </summary>
    public async IAsyncEnumerable<(string SchoolId, SchoolAnalysis Analysis)> AnalyzeStreamAsync(
        IReadOnlyList<School> schools,
        int? studentScore,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<(string SchoolId, SchoolAnalysis Analysis)>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        var concurrency = Math.Min(MaxConcurrency, Math.Max(schools.Count, 1));
        var throttler = new SemaphoreSlim(concurrency);
        logger.LogInformation("开始并行分析 {Count} 所学校，最大并发 {Concurrency}", schools.Count, concurrency);

        var producer = Task.Run(async () =>
        {
            try
            {
                var tasks = schools.Select(async school =>
                {
                    await throttler.WaitAsync(cancellationToken);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        SchoolAnalysis analysis;
                        try
                        {
                            logger.LogInformation("[Analyze:Start] {SchoolName}", school.Name);
                            analysis = await analysisAgent.AnalyzeAsync(school, studentScore, cancellationToken);
                            logger.LogInformation("[Analyze:Done] {SchoolName} 耗时 {Elapsed}ms", school.Name, sw.ElapsedMilliseconds);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            logger.LogWarning(exception, "学校分析链路失败: {SchoolName}", school.Name);
                            analysis = CreateFallback();
                        }

                        await channel.Writer.WriteAsync((school.Id, analysis), cancellationToken);
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

    private static SchoolAnalysis CreateFallback() => new(
        50,
        DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
        50,
        "暂无足够公开数据，建议结合学校开放日与家长群进一步核实。",
        50,
        50,
        "暂无详细师资公开数据。",
        0,
        [],
        50,
        "暂无充分的学生活动公开数据。",
        [],
        AdmissionCutoffScores: null);
}