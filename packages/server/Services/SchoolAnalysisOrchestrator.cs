using SchoolRecommender.Api.Models;

namespace SchoolRecommender.Api.Services;

public sealed class SchoolAnalysisOrchestrator(
    SchoolAnalysisAgent analysisAgent,
    ILogger<SchoolAnalysisOrchestrator> logger)
{
    public async Task<IReadOnlyDictionary<string, SchoolAnalysis>> AnalyzeAsync(IReadOnlyList<School> schools, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, SchoolAnalysis>(StringComparer.OrdinalIgnoreCase);
        var throttler = new SemaphoreSlim(3);

        var tasks = schools.Select(async school =>
        {
            await throttler.WaitAsync(cancellationToken);
            try
            {
                var analysis = await analysisAgent.AnalyzeAsync(school, cancellationToken);

                lock (result)
                {
                    result[school.Id] = analysis;
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "学校分析链路失败: {SchoolName}", school.Name);
                lock (result)
                {
                    result[school.Id] = new SchoolAnalysis(
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
                        []);
                }
            }
            finally
            {
                throttler.Release();
            }
        });

        await Task.WhenAll(tasks);
        return result;
    }
}