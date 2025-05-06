using App.Infrastructure;
using App.Middleware;
using App.Model;
using App.Services.Implementations;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ✅ ใช้ AddStandardResilienceHandler กับ HttpClient
builder.Services.AddHttpClient("RetryClient")
    .AddStandardResilienceHandler(); // ใช้ policy พื้นฐานของ Polly: Retry + Timeout + Circuit Breaker

// Logging
builder.Host.UseSerilog();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Middleware / Filters
builder.Services.AddScoped<LogRequestOnActionFilterAttribute>();
builder.Services.AddScoped<LogResponseOnResultFilterAttribute>();

// AppSettings binding
builder.Services.AddSingleton(resolver =>
{
    var config = resolver.GetRequiredService<IConfiguration>();
    var settings = new AppSettings();
    config.GetSection("AppSettings").Bind(settings);
    return settings;
});
builder.Services.AddServiceCollection(builder.Configuration);

builder.Services.AddScoped<IPaymentService, PaymentService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllersWithViews();
builder.Services.AddSession();
builder.Services.AddDistributedMemoryCache();
var app = builder.Build();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "Handled {RequestPath}";
    options.GetLevel = (httpContext, elapsed, ex) => LogEventLevel.Debug;
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
    };
});
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession(); // ✅ ต้องอยู่ก่อน Authorization
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
