using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSeguroAutoData(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Não usar HTTPS redirection em desenvolvimento local com Aspire (usa HTTP)
// app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

await app.Services.SeedDatabaseAsync();

app.Run();

