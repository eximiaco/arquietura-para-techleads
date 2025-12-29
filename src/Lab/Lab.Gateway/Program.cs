var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Ordem correta dos middlewares:
// 1. UseRouting - necessário para o roteamento funcionar corretamente
app.UseRouting();

// 2. Feature flag middleware (modifica o path antes do reverse proxy)
app.Use(async (context, next) =>
{
    var useModern = Environment.GetEnvironmentVariable("USE_MODERN_API")?.ToLower() == "true";
    
    if (useModern && context.Request.Path.StartsWithSegments("/api"))
    {
        // Roteia para Modern API
        context.Request.Path = context.Request.Path;
    }
    else if (!useModern && context.Request.Path.StartsWithSegments("/api"))
    {
        // Roteia para Legacy (converte REST para SOAP)
        context.Request.Path = "/legacy" + context.Request.Path;
    }
    
    await next();
});

// 3. YARP Reverse Proxy - roteia requisições para os serviços backend
app.MapReverseProxy();

app.Run();

