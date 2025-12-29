using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSeguroAutoData(builder.Configuration);

var app = builder.Build();

// Ordem correta dos middlewares:
// 1. Swagger (se desenvolvimento)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 2. UseRouting - necessário para o roteamento funcionar corretamente
app.UseRouting();

// 3. MapControllers - mapeia os controllers após o routing
app.MapControllers();

await app.Services.SeedDatabaseAsync();

app.Run();

