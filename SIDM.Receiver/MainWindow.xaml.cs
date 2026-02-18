using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Media;
using System.IO;
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

            // --- LÓGICA DE SONIDOS ---
            if (level.Contains("Emergencia") || level.Contains("Urgente"))
            {
                colorPrioridad = level.Contains("Emergencia") ? Brushes.DarkRed : Brushes.DarkOrange;
                SystemSounds.Exclamation.Play();
            }
            else
            {
                // REPRODUCIR TU SONIDO SINTÉTICO (Suave y corto)
                ReproducirNotificacionSintetica();
            }

            // Marca de tiempo con mes en letra y año
            string marcaTiempo = DateTime.Now.ToString("dd/MMM/yyyy HH:mm");

            HistorialMensajes.Insert(0, new MensajeItem
            {
                Hora = marcaTiempo,
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

        // ====== TU GENERADOR DE SONIDO INTEGRADO ======
        private void ReproducirNotificacionSintetica()
        {
            try
            {
                using MemoryStream ms = GenerateNotificationSound();
                SoundPlayer player = new SoundPlayer(ms);
                player.Load();
                player.Play(); // Usamos Play para no congelar la interfaz
            }
            catch { /* Silencio si algo falla */ }
        }

        private MemoryStream GenerateNotificationSound()
        {
            int sampleRate = 44100;
            double duration = 0.45;
            int samples = (int)(sampleRate * duration);
            int bytesPerSample = 2;

            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(0);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(sampleRate);
            bw.Write(sampleRate * bytesPerSample);
            bw.Write((short)bytesPerSample);
            bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(samples * bytesPerSample);

            double twoPi = 2 * Math.PI;
            double amp = 0.4 * short.MaxValue;

            for (int n = 0; n < samples; n++)
            {
                double t = (double)n / sampleRate;
                double freq = t < duration / 2 ? 880 : 1320;
                double envelope = Math.Exp(-6 * t);
                double sample = amp * envelope * (Math.Sin(twoPi * freq * t) + 0.2 * Math.Sin(twoPi * freq * 2 * t));
                bw.Write((short)sample);
            }

            long fileSize = ms.Length;
            ms.Seek(4, SeekOrigin.Begin);
            bw.Write((int)(fileSize - 8));
            ms.Seek(40, SeekOrigin.Begin);
            bw.Write(samples * bytesPerSample);
            ms.Position = 0;
            return ms;
        }

        // ... (Resto de métodos de ventana y conexión intactos)
        private async void StartConnection() { try { await _connection.StartAsync(); } catch { } }

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
            this.Topmost = true; this.Activate();
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