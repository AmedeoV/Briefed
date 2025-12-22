using Hangfire.Dashboard;

namespace Briefed.Web;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        
        // Allow access only to authenticated users in the Admin role
        return httpContext.User.Identity?.IsAuthenticated == true 
            && httpContext.User.IsInRole("Admin");
    }
}
