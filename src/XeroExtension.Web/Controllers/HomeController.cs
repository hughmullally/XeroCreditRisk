using Markdig;
using Microsoft.AspNetCore.Mvc;

namespace XeroExtension.Web.Controllers;

/// <summary>Landing page linking to the Dashboard, Dev/Seed tool, and the project docs.</summary>
[Route("")]
public class HomeController : ControllerBase
{
    private const string DefaultTenantId = "e5be2af3-6963-44b0-9f7e-855921499c72";

    private static readonly MarkdownPipeline MarkdownPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private const string SharedStyle = """
        body { font-family: -apple-system, "Segoe UI", sans-serif; margin: 2rem; background: #f7f7f8; color: #222; }
        h1 { font-size: 1.4rem; }
        a { color: #13b5ea; text-decoration: none; }
        a:hover { text-decoration: underline; }
        .muted { color: #999; }
        """;

    [HttpGet]
    public ContentResult Index()
    {
        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8" />
              <title>Xero Credit Risk Dashboard</title>
              <style>
                {{SharedStyle}}
                .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; margin-top: 1.5rem; }
                .card { background: white; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); padding: 1.2rem 1.4rem; text-decoration: none; color: #222; display: block; }
                .card:hover { box-shadow: 0 2px 8px rgba(0,0,0,0.15); }
                .card h2 { margin: 0 0 0.3rem; font-size: 1.05rem; color: #13b5ea; }
                .card p { margin: 0; font-size: 0.85rem; color: #666; }
                .connect-btn { display: inline-block; margin-top: 1rem; background: #13b5ea; color: white; font-weight: 600; padding: 0.6rem 1.2rem; border-radius: 6px; text-decoration: none; }
                .connect-btn:hover { background: #0f9bc9; text-decoration: none; }
              </style>
            </head>
            <body>
              <h1>Xero Credit Risk Dashboard</h1>
              <p class="muted">Internal tools for HughsCreditApp</p>
              <a class="connect-btn" href="/auth/xero/connect">🔌 Connect to Xero</a>
              <div class="cards">
                <a class="card" href="/dashboard?tenantId={{DefaultTenantId}}">
                  <h2>📊 Dashboard</h2>
                  <p>Live credit risk table with scores, warnings, and recommended limits.</p>
                </a>
                <a class="card" href="/dev?tenantId={{DefaultTenantId}}">
                  <h2>🌱 Seed / Dev Tool</h2>
                  <p>Populate demo invoices and company numbers against test counterparties.</p>
                </a>
                <a class="card" href="/overview">
                  <h2>📘 Overview</h2>
                  <p>What this tool does and how to read the dashboard.</p>
                </a>
                <a class="card" href="/architecture">
                  <h2>🏗 Architecture</h2>
                  <p>How the system is built and how the pieces fit together.</p>
                </a>
                <a class="card" href="/demo-script">
                  <h2>🎬 Demo Script</h2>
                  <p>A walkthrough script for presenting the dashboard.</p>
                </a>
              </div>
            </body>
            </html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }

    [HttpGet("overview")]
    public ContentResult Overview() => DocPage("Overview", "OVERVIEW.md");

    [HttpGet("architecture")]
    public ContentResult Architecture() => DocPage("Architecture", "ARCHITECTURE.md");

    [HttpGet("demo-script")]
    public ContentResult DemoScript() => DocPage("Demo Script", "DEMO_SCRIPT.md");

    private static ContentResult DocPage(string title, string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Docs", fileName);
        var markdown = System.IO.File.Exists(path)
            ? System.IO.File.ReadAllText(path)
            : $"# {title}\n\n_{fileName} not found._";
        var body = Markdown.ToHtml(markdown, MarkdownPipeline);

        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8" />
              <title>{{title}} — Xero Credit Risk Dashboard</title>
              <style>
                {{SharedStyle}}
                .doc { background: white; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); padding: 1.5rem 2rem; max-width: 860px; }
                .doc h1 { font-size: 1.6rem; margin-top: 0; }
                .doc h2 { font-size: 1.2rem; border-bottom: 1px solid #eee; padding-bottom: 0.3rem; margin-top: 1.8rem; }
                .doc table { border-collapse: collapse; width: 100%; margin: 0.8rem 0; }
                .doc th, .doc td { text-align: left; padding: 0.5rem 0.8rem; border-bottom: 1px solid #eee; font-size: 0.9rem; }
                .doc th { background: #fafafa; }
                .doc code { background: #f0f0f0; padding: 0.1rem 0.35rem; border-radius: 3px; font-size: 0.85em; }
                .doc pre { background: #272822; color: #f8f8f2; padding: 1rem; border-radius: 6px; overflow-x: auto; }
                .doc pre code { background: none; padding: 0; color: inherit; }
              </style>
            </head>
            <body>
              <p><a href="/">← Back</a></p>
              <div class="doc">
                {{body}}
              </div>
            </body>
            </html>
            """;

        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8" };
    }
}
