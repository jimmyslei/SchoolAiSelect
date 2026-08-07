namespace SchoolRecommender.Api.Endpoints;

internal static class EndpointRouteBuilderExtensions
{
    public static WebApplication MapApplicationEndpoints(this WebApplication app)
    {
        app.MapSystemEndpoints();
        app.MapAuthEndpoints();
        app.MapMessageEndpoints();
        app.MapLocationEndpoints();
        app.MapSchoolEndpoints();
        app.MapUniversityEndpoints();
        app.MapHighSchoolEndpoints();
        app.MapHistoryEndpoints();

        return app;
    }
}
