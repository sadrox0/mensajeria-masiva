using Microsoft.AspNetCore.SignalR;

namespace SIDM.Server.Hubs;

public class AlertHub : Hub
{
    // Enviar alerta a todos
    public async Task SendAlert(string message, string level)
    {
        // El servidor retransmite a todos los clientes conectados
        await Clients.All.SendAsync("ReceiveAlert", message, level);
        Console.WriteLine($"[LOG] Alerta Enviada: {level} - {message}");
    }

    // Confirmación de lectura
    public Task Ack()
    {
        Console.WriteLine($"[ACK] Mensaje recibido por un cliente.");
        return Task.CompletedTask;
    }
}