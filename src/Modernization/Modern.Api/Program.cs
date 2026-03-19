using SeguroAuto.Data;
using SeguroAuto.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSeguroAutoData(builder.Configuration);
builder.Services.AddControllers();

var app = builder.Build();

await app.Services.SeedDatabaseAsync();

app.UseRouting();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
