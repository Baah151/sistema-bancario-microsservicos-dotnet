using Microsoft.EntityFrameworkCore;
using MSNotificacoes.Data;
using MSNotificacoes.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MS-Notificacoes", Version = "v1", Description = "Microsserviço de Notificações Bancárias" });
});

builder.Services.AddDbContext<NotificacoesDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=notificacoes.db"));

builder.Services.AddScoped<NotificacaoService>();

builder.Services.AddHttpClient("MSContas", client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:MSContas"] ?? "http://localhost:5001"));

builder.WebHost.UseUrls("http://localhost:5003");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificacoesDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MS-Notificacoes v1"));

app.MapControllers();

Console.WriteLine("✅ MS-NOTIFICACOES rodando em http://localhost:5003");
Console.WriteLine("   Banco de dados: notificacoes.db (exclusivo deste serviço)");
Console.WriteLine("   Modo: simulação de e-mail (logs no console)");
Console.WriteLine("   Swagger: http://localhost:5003/swagger");

app.Run();
