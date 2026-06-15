using Microsoft.EntityFrameworkCore;
using MSContas.Data;
using MSContas.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MS-Contas", Version = "v1", Description = "Microsserviço de Gestão de Contas Bancárias" });
});

builder.Services.AddDbContext<ContasDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=contas.db"));

builder.Services.AddScoped<ContaService>();

builder.WebHost.UseUrls("http://localhost:5001");

var app = builder.Build();

// Aplicar migrations automaticamente
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContasDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MS-Contas v1"));

app.MapControllers();

Console.WriteLine("✅ MS-CONTAS rodando em http://localhost:5001");
Console.WriteLine("   Banco de dados: contas.db (exclusivo deste serviço)");
Console.WriteLine("   Swagger: http://localhost:5001/swagger");

app.Run();
