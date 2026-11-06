using Microsoft.Extensions.FileProviders;

namespace ArtAround.Api.Infrastructure;

public static class SiteExtensions
{
    /// <summary>
    /// Serves everything from one origin: uploads, the static marketplace and the navigator's
    /// build. A missing folder is skipped: during development the navigator runs on Vite.
    /// </summary>
    public static void MapSite(this WebApplication app)
    {
        var root = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, app.Configuration["SiteRoot"]!));

        app.ServeFolder(Path.Combine(root, "uploads"), "/uploads");

        app.MapGet("/marketplace", () => Results.Redirect("/marketplace/pages/homepage.html")).AllowAnonymous();
        app.ServeFolder(Path.Combine(root, "frontend-marketplace"), "/marketplace");

        var navigator = Path.Combine(root, "frontend-navigator", "dist");
        if (app.ServeFolder(navigator, ""))
        {
            // The navigator's routes are deep links, so any other page path gets its index.html.
            // Not the other prefixes: an unknown API route or missing upload must stay a 404, not become a web page.
            app.MapFallbackToFile("{*path:regex(^(?!api/|hubs/|uploads/|marketplace/).*$)}", "index.html", new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(navigator),
            }).AllowAnonymous();
        }
    }

    private static bool ServeFolder(this WebApplication app, string folder, string requestPath)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        var files = new PhysicalFileProvider(folder);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files, RequestPath = requestPath });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files, RequestPath = requestPath });
        return true;
    }
}
