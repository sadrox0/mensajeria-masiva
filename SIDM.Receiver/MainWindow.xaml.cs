using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Media;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace SIDM.Receiver
{
    // Estructura para el historial de mensajes
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

        // Colores institucionales definidos para el proyecto SIDM
        private readonly Brush _colorPrincipal = (SolidColorBrush)new BrushConverter().ConvertFrom("#9F2241")!; // Guinda
        private readonly Brush _colorInformativoTexto = (SolidColorBrush)new BrushConverter().ConvertFrom("#229F80")!; // Verde para letras
        private readonly Brush _colorEmergencia = (SolidColorBrush)new BrushConverter().ConvertFrom("#8F0800")!; // Guinda oscuro

        public MainWindow()
        {
            InitializeComponent();
            lstHistorial.ItemsSource = HistorialMensajes;

            // Configuración de SignalR para recibir alertas en tiempo real
            _connection = new HubConnectionBuilder()
                .WithUrl("http://10.1.1.98:5271/sidmHub")
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string, string>("ReceiveAlert", (message, level) => {
                Dispatcher.Invoke(() => procesarNuevoMensaje(message ?? "", level ?? "Informativo"));
            });

            StartConnection();

            // Ubicación inicial de la burbuja
            _ultimoLeftBurbuja = SystemParameters.WorkArea.Width - 160;
            _ultimoTopBurbuja = SystemParameters.WorkArea.Height - 160;
        }

        private void procesarNuevoMensaje(string message, string level)
        {
            // Aplicación de lógica de colores por prioridad
            Brush colorPrioridad = level.Contains("Informativo") ? _colorInformativoTexto : _colorPrincipal;

            if (level.Contains("Emergencia"))
            {
                colorPrioridad = _colorEmergencia;
                // MEJORA: Sonido Beep para emergencias de 2 segundos (2000ms)
                Task.Run(() => Console.Beep(800, 2000));
                MostrarVentanaCompleta();
            }
            else if (level.Contains("Informativo"))
            {
                ReproducirNotificacionSintetica();
                if (_esModoBurbuja)
                {
                    BadgeAlerta.Visibility = Visibility.Visible;
                    MostrarPopupMessenger(message);
                }
                else
                {
                    MostrarVentanaCompleta();
                }
            }
            else
            {
                MostrarVentanaCompleta();
            }

            // Inserción en historial respetando el formato visual de la terminal
            HistorialMensajes.Insert(0, new MensajeItem
            {
                Hora = DateTime.Now.ToString("dd/MMM/yyyy HH:mm"),
                Texto = message,
                PrioridadTexto = level.ToUpper(),
                ColorPrioridad = colorPrioridad
            });

            if (HistorialMensajes.Count > 20) HistorialMensajes.RemoveAt(20);

            // Actualización de UI y scroll automático para uniformidad
            txtMensaje.Text = message;
            txtPrioridad.Text = level.ToUpper();
            txtPrioridad.Foreground = colorPrioridad;

            if (lstHistorial.Items.Count > 0)
            {
                lstHistorial.ScrollIntoView(lstHistorial.Items[0]);
            }
        }

        private async void MostrarPopupMessenger(string texto)
        {
            txtPopUp.Text = texto;
            GridPopUpMessenger.Visibility = Visibility.Visible;
            await Task.Delay(5000);
            GridPopUpMessenger.Visibility = Visibility.Collapsed;
        }

        // --- MÉTODOS DE VENTANA (Mantenimiento de posición) ---
        private void MostrarVentanaCompleta()
        {
            double cLeft = this.Left;
            double cTop = this.Top;

            _esModoBurbuja = false;

            // Limpiar iconos de notificación al abrir para confirmar lectura
            BadgeAlerta.Visibility = Visibility.Collapsed;
            GridPopUpMessenger.Visibility = Visibility.Collapsed;

            GridAlerta.Visibility = Visibility.Visible;
            GridBurbuja.Visibility = Visibility.Collapsed;

            // Tamaño solicitado para el SIDM
            this.Width = 500;
            this.Height = 400;

            // Apertura in situ centrada sobre la burbuja
            this.Left = cLeft - 150;
            this.Top = cTop - 75;

            MainBorder.Background = Brushes.White;
            MainBorder.BorderThickness = new Thickness(1.5);
            this.Topmost = true;
            this.Activate();
        }

        private void ActivarModoBurbuja()
        {
            _esModoBurbuja = true;

            // Limpieza de estados de notificación al volver a modo burbuja
            BadgeAlerta.Visibility = Visibility.Collapsed;
            GridPopUpMessenger.Visibility = Visibility.Collapsed;

            GridAlerta.Visibility = Visibility.Collapsed;
            GridBurbuja.Visibility = Visibility.Visible;

            this.Width = 200; this.Height = 250;
            this.Left = _ultimoLeftBurbuja; this.Top = _ultimoTopBurbuja;

            MainBorder.Background = Brushes.Transparent;
            MainBorder.BorderThickness = new Thickness(0);
        }

        private async void StartConnection() { try { await _connection.StartAsync(); } catch { } }

        private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnMinimizar_Click(object sender, RoutedEventArgs e) => ActivarModoBurbuja();
        private void BtnOk_Click(object sender, RoutedEventArgs e) => ActivarModoBurbuja();

        private void GridBurbuja_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Doble clic para expandir en la misma posición
            if (e.ClickCount == 2)
            {
                MostrarVentanaCompleta();
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
                _ultimoLeftBurbuja = this.Left;
                _ultimoTopBurbuja = this.Top;
            }
        }

        private void ReproducirNotificacionSintetica()
        {
            try
            {
                using MemoryStream ms = GenerateNotificationSound();
                SoundPlayer player = new SoundPlayer(ms);
                player.Load(); player.Play();
            }
            catch { }
        }

        private MemoryStream GenerateNotificationSound()
        {
            int sampleRate = 44100; double duration = 0.4; int samples = (int)(sampleRate * duration);
            MemoryStream ms = new MemoryStream(); BinaryWriter bw = new BinaryWriter(ms);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + samples * 2);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE")); bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(sampleRate); bw.Write(sampleRate * 2);
            bw.Write((short)2); bw.Write((short)16); bw.Write(System.Text.Encoding.ASCII.GetBytes("data")); bw.Write(samples * 2);
            for (int n = 0; n < samples; n++)
            {
                double t = (double)n / sampleRate; double freq = t < duration / 2 ? 880 : 1320;
                short sample = (short)(0.4 * short.MaxValue * Math.Exp(-8 * t) * Math.Sin(2 * Math.PI * freq * t));
                bw.Write(sample);
            }
            ms.Position = 0; return ms;
        }
    }
}