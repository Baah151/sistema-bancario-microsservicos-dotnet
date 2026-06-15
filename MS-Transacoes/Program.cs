using Microsoft.EntityFrameworkCore;
using MSTransacoes.Data;
using MSTransacoes.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MS-Transacoes", Version = "v1", Description = "Microsserviço de Movimentações Financeiras" });
});

builder.Services.AddDbContext<TransacoesDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=transacoes.db"));

var msContasUrl = builder.Configuration["Services:MSContas"] ?? "http://localhost:5001";
var msNotificacoesUrl = builder.Configuration["Services:MSNotificacoes"] ?? "http://localhost:5003";

builder.Services.AddHttpClient("MSContas", c => c.BaseAddress = new Uri(msContasUrl));
builder.Services.AddHttpClient("MSNotificacoes", c => c.BaseAddress = new Uri(msNotificacoesUrl));

builder.Services.AddScoped<TransacaoService>();

builder.WebHost.UseUrls("http://localhost:5002");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TransacoesDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MS-Transacoes v1"));

app.MapControllers();

Console.WriteLine("✅ MS-TRANSACOES rodando em http://localhost:5002");
Console.WriteLine("   Banco de dados: transacoes.db (exclusivo deste serviço)");
Console.WriteLine($"   Conectado a: MS-CONTAS ({msContasUrl})");
Console.WriteLine($"   Conectado a: MS-NOTIFICACOES ({msNotificacoesUrl})");
Console.WriteLine("   Swagger: http://localhost:5002/swagger");

app.Run();
