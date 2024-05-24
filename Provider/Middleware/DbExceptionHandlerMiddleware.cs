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
        catch (DbUpdateException)
        {
            // ToDo:
            // Log the exception or handle it gracefully
            context.Response.StatusCode = 409;
            var errorResponse = new
            {
                status = "Error",
                code = 409,
                message = "Duplicate record found",
                details = new
                {
                    value = "[FirstName, LastName, Email] might be a duplicate. Please verify and retry"
                }
            };
            await context.Response.WriteAsJsonAsync(errorResponse);
        }
    }
}

