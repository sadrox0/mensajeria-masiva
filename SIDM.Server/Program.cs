using SIDM.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar Kestrel para que escuche en la red
builder.WebHost.ConfigureKestrel(options =>
{
    // Escucha en el puerto 5271 en todas las IPs de la computadora
    options.ListenAnyIP(5271);
});

builder.Services.AddSignalR();
builder.Services.AddCors();

var app = builder.Build();

// 2. Importante: AllowCredentials() es necesario para SignalR si usas CORS específico
// pero con AllowAnyOrigin NO puedes usar AllowCredentials. 
// Para desarrollo esta línea está bien:
app.UseCors(policy => policy
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowAnyOrigin());

app.MapHub<AlertHub>("/sidmHub");

app.Run();