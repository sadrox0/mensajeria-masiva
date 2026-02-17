using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.AspNetCore.SignalR.Client;

namespace SIDM.Receiver
{
    public class MensajeItem
    {
        public string Hora { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public string PrioridadTexto { get; set; } = string.Empty;
        public Brush ColorPrioridad { get; set; } = Brushes.Gray;
    }

    public partial class MainWindow : Window
    {
        private HubConnection _connection;
        private bool _esModoBurbuja = false;
        private double _ultimoLeftBurbuja = -1;
        private double _ultimoTopBurbuja = -1;
        public ObservableCollection<MensajeItem> HistorialMensajes { get; set; } = new ObservableCollection<MensajeItem>();

        public MainWindow()
        {
            InitializeComponent();
            lstHistorial.ItemsSource = HistorialMensajes;

            _connection = new HubConnectionBuilder()
                .WithUrl("http://10.1.0.145:5271/sidmHub")
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string, string>("ReceiveAlert", (message, level) => {
                Dispatcher.Invoke(() => {
                    procesarNuevoMensaje(message ?? "", level ?? "Informativo");
                });
            });

            StartConnection();
            _ultimoLeftBurbuja = SystemParameters.WorkArea.Width - 140;
            _ultimoTopBurbuja = SystemParameters.WorkArea.Height - 140;
        }

        private void procesarNuevoMensaje(string message, string level)
        {
            Brush colorPrioridad = (SolidColorBrush?)new BrushConverter().ConvertFrom("#004581") ?? Brushes.Blue;
            if (level.Contains("Emergencia")) colorPrioridad = Brushes.DarkRed;
            else if (level.Contains("Urgente")) colorPrioridad = Brushes.DarkOrange;

            HistorialMensajes.Insert(0, new MensajeItem
            {
                Hora = DateTime.Now.ToString("HH:mm"),
                Texto = message,
                PrioridadTexto = level.ToUpper(),
                ColorPrioridad = colorPrioridad
            });

            if (HistorialMensajes.Count > 20) HistorialMensajes.RemoveAt(20);

            txtMensaje.Text = message;
            txtPrioridad.Text = level.ToUpper();
            AplicarColoresSegunNivel(level);

            if (level.Contains("Emergencia") || level.Contains("Urgente"))
                MostrarVentanaCompleta();
            else if (_esModoBurbuja)
                BadgeAlerta.Visibility = Visibility.Visible;
            else
                MostrarVentanaCompleta();
        }

        private async void StartConnection()
        {
            try { await _connection.StartAsync(); } catch { }
        }

        private void MostrarVentanaCompleta()
        {
            if (_esModoBurbuja) { _ultimoLeftBurbuja = this.Left; _ultimoTopBurbuja = this.Top; }
            _esModoBurbuja = false;
            GridAlerta.Visibility = Visibility.Visible;
            GridBurbuja.Visibility = Visibility.Collapsed;
            BadgeAlerta.Visibility = Visibility.Collapsed;

            this.Width = 600; this.Height = 600;
            this.Left = (SystemParameters.WorkArea.Width - 600) / 2;
            this.Top = (SystemParameters.WorkArea.Height - 600) / 2;

            MainBorder.Background = Brushes.White;
            MainBorder.BorderThickness = new Thickness(1.5);
            this.Topmost = true;
            this.Activate();
        }

        private void ActivarModoBurbuja()
        {
            _esModoBurbuja = true;
            GridAlerta.Visibility = Visibility.Collapsed;
            GridBurbuja.Visibility = Visibility.Visible;
            this.Width = 120; this.Height = 120;
            this.Left = _ultimoLeftBurbuja; this.Top = _ultimoTopBurbuja;
            MainBorder.Background = Brushes.Transparent;
            MainBorder.BorderThickness = new Thickness(0);
        }

        private void AplicarColoresSegunNivel(string nivel)
        {
            if (nivel.Contains("Emergencia")) { HeaderBar.Background = Brushes.DarkRed; txtPrioridad.Foreground = Brushes.DarkRed; }
            else if (nivel.Contains("Urgente")) { HeaderBar.Background = Brushes.DarkOrange; txtPrioridad.Foreground = Brushes.DarkOrange; }
            else
            {
                var azul = (SolidColorBrush?)new BrushConverter().ConvertFrom("#004581") ?? Brushes.Blue;
                HeaderBar.Background = azul; txtPrioridad.Foreground = azul;
            }
        }

        private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) this.DragMove(); }
        private void BtnMinimizar_Click(object sender, RoutedEventArgs e) => ActivarModoBurbuja();
        private async void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (_connection.State == HubConnectionState.Connected) await _connection.InvokeAsync("Ack");
            ActivarModoBurbuja();
        }
        private void GridBurbuja_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) MostrarVentanaCompleta();
            else if (e.LeftButton == MouseButtonState.Pressed) { this.DragMove(); _ultimoLeftBurbuja = this.Left; _ultimoTopBurbuja = this.Top; }
        }
    }
}