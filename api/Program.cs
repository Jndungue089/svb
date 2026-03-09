using BeneditaApi.Data;
using BeneditaApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Banco de dados (SQLite) ────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=benedita.db"));

// ── Serviços de negócio ────────────────────────────────────────
builder.Services.AddScoped<VoteService>();

// ── Serviço serial (singleton — mantém a porta aberta) ─────────
builder.Services.AddSingleton<SerialHostedService>();
builder.Services.AddHostedService(p => p.GetRequiredService<SerialHostedService>());

// ── Web API ───────────────────────────────────────────────────
builder.Services.AddControllers();
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
