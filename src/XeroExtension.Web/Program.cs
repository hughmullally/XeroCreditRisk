using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Xero.NetStandard.OAuth2.Client;
using Xero.NetStandard.OAuth2.Config;
using XeroExtension.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Xero OAuth 2.0 ──────────────────────────────────────────────────────────
var xeroConfig = builder.Configuration
    .GetSection("Xero")
    .Get<XeroConfiguration>()
    ?? throw new InvalidOperationException("Xero configuration section is missing.");

builder.Services.AddSingleton(xeroConfig);
builder.Services.AddHttpClient<IXeroClient, XeroClient>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();
builder.Services.AddScoped<IXeroService, XeroService>();
builder.Services.AddScoped<ICreditRiskService, CreditRiskService>();

// ── ASP.NET Core ─────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;

    context.Response.ContentType = "application/json";
    context.Response.StatusCode = error is NotConnectedException
        ? StatusCodes.Status401Unauthorized
        : StatusCodes.Status500InternalServerError;

    var message = error is NotConnectedException
        ? error.Message
        : "An unexpected error occurred.";

    await context.Response.WriteAsJsonAsync(new { error = message });
}));

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
