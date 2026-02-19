using System.Text;
using LBElectronica.Server.Data;
using LBElectronica.Server.Endpoints;
using LBElectronica.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=lb_electronica.db"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<DbInitializer>();
builder.Services.AddScoped<CodeService>();
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<ReceiptService>();
builder.Services.AddScoped<DateRangeService>();
builder.Services.AddScoped<SqlMigrationService>();
builder.Services.AddScoped<ExchangeRateService>();

var key = builder.Configuration["Jwt:Key"] ?? "dev-super-secret-key-change-me";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "lb-electronica";
var audience = builder.Configuration["Jwt:Audience"] ?? "lb-electronica-clients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("lb_auth", out var token))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("lan", p => p
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("lan");
app.UseAuthentication();
app.UseAuthorization();

if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapGet("/api/health", () => Results.Ok(new { ok = true, at = DateTime.UtcNow }));
app.MapAuth();
app.MapUsers();
app.MapProducts();
app.MapStock();
app.MapSales();
app.MapCash();
app.MapReports();
app.MapSystem();
app.MapConfig();

app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsJsonAsync(new { message = "Ruta API no encontrada" });
        return;
    }

    var indexPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
    if (File.Exists(indexPath))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(indexPath);
        return;
    }

    context.Response.StatusCode = 404;
    await context.Response.WriteAsJsonAsync(new { message = "No encontrado" });
});

using (var scope = app.Services.CreateScope())
{
    var migrations = scope.ServiceProvider.GetRequiredService<SqlMigrationService>();
    await migrations.RunAsync();

    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync();
}

app.Run();
