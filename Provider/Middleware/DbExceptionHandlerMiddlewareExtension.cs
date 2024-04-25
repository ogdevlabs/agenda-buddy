namespace Provider.Middleware;

public static class DbExceptionHandlerMiddlewareExtension
{
    public static IApplicationBuilder UseDbExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<DbExceptionHandlerMiddleware>();
    }
}