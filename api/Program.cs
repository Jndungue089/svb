using BeneditaApi.Data;
using BeneditaApi.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ── Banco de dados (MySQL via Pomelo) ──────────────────────
var connStr = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Defina ConnectionStrings:Default no appsettings.json (MySQL).");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(
        connStr,
        ServerVersion.AutoDetect(connStr),
        mySql => mySql.EnableRetryOnFailure(3)
    ));

// ── Serviços de negócio ────────────────────────────────────────
builder.Services.AddScoped<VoteService>();

// ── Serviço serial (singleton — mantém a porta aberta) ─────────
builder.Services.AddSingleton<SerialHostedService>();
builder.Services.AddHostedService(p => p.GetRequiredService<SerialHostedService>());

// ── Web API ───────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        // Evita exceções de serialização com ciclos de navegação do EF Core.
        opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Benedita API", Version = "v1" });
});

var app = builder.Build();

// ── Cria o banco se não existir ───────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ── Pipeline HTTP ─────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
