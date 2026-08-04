using Markdig;
using Markdig.Renderers;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace EzNutrition.Client.Services
{
    public static class SafeMarkdown
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();

        public static MarkupString Render(string? markdown)
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            var renderer = new HtmlRenderer(writer)
            {
                LinkRewriter = RewriteLink
            };

            Pipeline.Setup(renderer);
            renderer.Render(Markdown.Parse(markdown ?? string.Empty, Pipeline));
            writer.Flush();
            return new MarkupString(writer.ToString());
        }

        private static string RewriteLink(string url)
        {
            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri) || !uri.IsAbsoluteUri)
            {
                return url;
            }

            return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals("tel", StringComparison.OrdinalIgnoreCase)
                    ? url
                    : "#";
        }
    }
}
