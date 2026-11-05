using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace SchoolPlatform.Host.Common;

/// <summary>
/// Serves the compiled Angular application fast: content-hashed files (main-HASH.js, chunk-HASH.js, styles-HASH.css) are
/// immutable for a year and, when the build left a precompressed copy (.br/.gz, see scripts/Copy-Web.mjs), that copy is
/// sent without compressing on each request. Everything else in the app folder revalidates (no-cache), so a new
/// deployment is picked up at once.
/// </summary>
public static partial class SpaAssets
{
    public const string Immutable = "public, max-age=31536000, immutable";

    [GeneratedRegex(@"^[A-Za-z0-9_]+-[A-Za-z0-9_-]{8,}\.(js|css|woff2?|png|jpe?g|svg|webp)$")]
    private static partial Regex HashedName();

    public static bool IsHashed(string fileName) => HashedName().IsMatch(fileName);

    public static void UseSpaAssets(this WebApplication app, string directory)
    {
        var files = new PhysicalFileProvider(directory);
        var types = new FileExtensionContentTypeProvider();
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value;
            var get = HttpMethods.IsGet(context.Request.Method);
            if ((get || HttpMethods.IsHead(context.Request.Method)) && path is { Length: > 1 } && path.LastIndexOf('/') == 0 && IsHashed(path[1..]))
            {
                var accept = context.Request.Headers.AcceptEncoding.ToString();
                foreach (var (token, suffix) in Encodings)
                {
                    if (!Accepts(accept, token)) continue;
                    var file = files.GetFileInfo(path + suffix);
                    if (!file.Exists || file.IsDirectory) continue;
                    context.Response.ContentType = types.TryGetContentType(path, out var type) ? type : "application/octet-stream";
                    context.Response.Headers.ContentEncoding = token;
                    context.Response.Headers.Vary = "Accept-Encoding";
                    context.Response.Headers.CacheControl = Immutable;
                    context.Response.ContentLength = file.Length;
                    if (get) await context.Response.SendFileAsync(file, context.RequestAborted);
                    return;
                }
            }
            await next();
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            OnPrepareResponse = x => x.Context.Response.Headers.CacheControl = IsHashed(x.File.Name) ? Immutable : "no-cache"
        });
    }

    private static readonly (string Token, string Suffix)[] Encodings = [("br", ".br"), ("gzip", ".gz")];

    private static bool Accepts(string header, string token)
    {
        foreach (var part in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split(';', StringSplitOptions.TrimEntries);
            if (!pieces[0].Equals(token, StringComparison.OrdinalIgnoreCase)) continue;
            return !pieces.Skip(1).Any(x => x.Replace(" ", "") is "q=0" or "q=0.0" or "q=0.00" or "q=0.000");
        }
        return false;
    }
}
