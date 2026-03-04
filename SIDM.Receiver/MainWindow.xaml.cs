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
        // Drag helpers for bubble to allow moving above screen edges
        private bool _isDraggingBubble = false;
        private System.Windows.Point _dragStartScreenPoint;
        private double _dragStartLeft;
        private double _dragStartTop;
        // Pointer offset (screen coordinates) to avoid leading the bubble during drag
        private double _pointerOffsetX;
        private double _pointerOffsetY;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT {
            public int X;
            public int Y;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        // Obtiene la posición del cursor en unidades WPF (DIPs), corrigiendo DPI
        private Point GetCursorPositionInDips()
        {
            if (GetCursorPos(out POINT p))
            {
                var screenPoint = new Point(p.X, p.Y);
                var source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    var transform = source.CompositionTarget.TransformFromDevice;
                    return transform.Transform(screenPoint);
                }
                return screenPoint;
            }
            // Fallback: use WPF mouse position relative to window and convert to screen
            var rel = Mouse.GetPosition(this);
            var screen = this.PointToScreen(rel);
            return screen;
        }
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
                .WithUrl("http://192.168.137.22:5271/sidmHub")
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string, string>("ReceiveAlert", (message, level) => {
                Dispatcher.Invoke(() => procesarNuevoMensaje(message ?? "", level ?? "Informativo"));
            });

            StartConnection();

            // Ubicación inicial de la burbuja (ajustada para no salirse de la pantalla)
            _ultimoLeftBurbuja = SystemParameters.WorkArea.Width - 160;
            _ultimoTopBurbuja = SystemParameters.WorkArea.Height - 160;
            // Ubicación inicial de la burbuja sin límite (se mantiene comportamiento original)
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

            // Asegurar que la ventana completa no se salga del área de trabajo
            double newLeft = this.Left;
            double newTop = this.Top;
            var clipped = ClampToWorkAreaAndDetect(ref newLeft, ref newTop, this.Width, this.Height);
            this.Left = newLeft;
            this.Top = newTop;

            MainBorder.Background = Brushes.White;
            MainBorder.BorderThickness = new Thickness(1.5);
            this.Topmost = true;
            this.Activate();
        }

        // Asegura que una posición (left, top) con un tamaño dado quede dentro del área
        // de trabajo del sistema para que la burbuja/window no sobresalga de la pantalla.
        // Además detecta y devuelve qué bordes habrían quedado fuera antes de ajustar.
        private (bool left, bool top, bool right, bool bottom) ClampToWorkAreaAndDetect(ref double left, ref double top, double width, double height)
        {
            var wa = SystemParameters.WorkArea;
            bool clippedLeft = left < wa.Left;
            bool clippedTop = top < wa.Top;
            bool clippedRight = left + width > wa.Right;
            bool clippedBottom = top + height > wa.Bottom;

            // Ajuste para mantener dentro
            if (clippedLeft) left = wa.Left;
            if (clippedTop) top = wa.Top;
            if (clippedRight) left = wa.Right - width;
            if (clippedBottom) top = wa.Bottom - height;

            // Log de los bordes recortados para diagnóstico
            if (clippedLeft || clippedTop || clippedRight || clippedBottom)
            {
                string edges = string.Empty;
                if (clippedLeft) edges += "Left ";
                if (clippedTop) edges += "Top ";
                if (clippedRight) edges += "Right ";
                if (clippedBottom) edges += "Bottom ";
                Console.WriteLine($"[Clamp] Se recortaron los bordes: {edges.Trim()}");
            }

            return (clippedLeft, clippedTop, clippedRight, clippedBottom);
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
            // Aplicar la posición guardada tal como está (sin limitar) para la burbuja
            this.Left = _ultimoLeftBurbuja; this.Top = _ultimoTopBurbuja;
            // Opcional: manejar visualmente si fue recortada alguna arista (logging ya realizado)

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
                // Inicio de arrastre manual para permitir mover la burbuja incluso más arriba del área de trabajo
                _isDraggingBubble = true;
                // Obtener posición del cursor en DIPs (corrige DPI) y calcular offset respecto a la esquina superior izquierda de la ventana
                var cursorDip = GetCursorPositionInDips();
                _pointerOffsetX = cursorDip.X - this.Left;
                _pointerOffsetY = cursorDip.Y - this.Top;
                // Capturar el ratón y suscribirse a eventos de movimiento/soltar
                GridBurbuja.CaptureMouse();
                this.MouseMove += Window_MouseMove_ForBubble;
                this.PreviewMouseLeftButtonUp += Window_MouseLeftButtonUp_ForBubble;
                // Guardar posición inicial
                _ultimoLeftBurbuja = this.Left;
                _ultimoTopBurbuja = this.Top;
            }
        }

        private void Window_MouseMove_ForBubble(object? sender, MouseEventArgs e)
        {
            if (!_isDraggingBubble) return;
            // Obtener posición actual del cursor en DIPs y mover la ventana de modo que el cursor mantenga el mismo offset
            var cur = GetCursorPositionInDips();
            this.Left = cur.X - _pointerOffsetX;
            this.Top = cur.Y - _pointerOffsetY;

            // Actualizar posición guardada de la burbuja
            _ultimoLeftBurbuja = this.Left;
            _ultimoTopBurbuja = this.Top;
        }

        private void Window_MouseLeftButtonUp_ForBubble(object? sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingBubble) return;
            _isDraggingBubble = false;
            try { GridBurbuja.ReleaseMouseCapture(); } catch { }
            // Quitar listeners
            this.MouseMove -= Window_MouseMove_ForBubble;
            this.PreviewMouseLeftButtonUp -= Window_MouseLeftButtonUp_ForBubble;
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