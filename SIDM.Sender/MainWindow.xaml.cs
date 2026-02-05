
using System;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Tasks;

namespace SIDM.Sender;

public partial class MainWindow : Window
{
    private HubConnection _connection;

    public MainWindow()
    {
        InitializeComponent();

        // Conexión a la IP de tu servidor
        _connection = new HubConnectionBuilder()
            .WithUrl("http://10.1.0.145:5271/sidmHub")
            .WithAutomaticReconnect()
            .Build();

        IniciarConexion();
    }

    private async void IniciarConexion()
    {
        try { await _connection.StartAsync(); }
        catch { /* Reintento automático activo */ }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        // Validación de seguridad
        if (string.IsNullOrWhiteSpace(txtInput.Text))
        {
            MessageBox.Show("Operación cancelada: El mensaje no puede estar vacío.", "Validación de Seguridad", MessageBoxButton.OK, MessageBoxImage.Stop);
            return;
        }

        if (_connection.State != HubConnectionState.Connected)
        {
            MessageBox.Show("Error de enlace: No hay conexión con la base del 911.", "Fallo de Red");
            return;
        }

        string mensaje = txtInput.Text;
        string nivel = (cbNivel.SelectedItem as System.Windows.Controls.ComboBoxItem).Content.ToString();

        try
        {
            await _connection.InvokeAsync("SendAlert", mensaje, nivel);
            txtInput.Clear();
            MessageBox.Show("Difusión enviada con éxito a todas las unidades de patrullaje.", "Confirmación de Mando");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error crítico en el protocolo de envío: {ex.Message}");
        }
    }
}