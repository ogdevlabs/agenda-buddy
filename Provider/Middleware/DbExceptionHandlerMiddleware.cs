using Microsoft.EntityFrameworkCore;

namespace Provider.Middleware;

public class DbExceptionHandlerMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DbUpdateException ex)
        {
            // Log the exception or handle it gracefully
            // For demonstration purposes, let's just return a generic error response
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Database operation failed. Please try again later.");
        }
    }
}

