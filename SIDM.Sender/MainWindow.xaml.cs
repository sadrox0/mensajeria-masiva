using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Media;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace SIDM.Sender
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
        private bool _historialAbierto = false;
        public ObservableCollection<MensajeItem> HistorialEnviados { get; set; } = new ObservableCollection<MensajeItem>();

        private readonly Brush _colorGuinda = (SolidColorBrush)new BrushConverter().ConvertFrom("#9F2241")!;
        private readonly Brush _colorVerde = (SolidColorBrush)new BrushConverter().ConvertFrom("#229F80")!;
        private readonly Brush _colorEmergencia = (SolidColorBrush)new BrushConverter().ConvertFrom("#8F0800")!;

        public MainWindow()
        {
            InitializeComponent();
            lstHistorial.ItemsSource = HistorialEnviados;

            _connection = new HubConnectionBuilder()
                .WithUrl("http://10.1.1.98:5271/sidmHub")
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string, string>("ReceiveAlert", (message, level) => {
                Dispatcher.Invoke(() => {
                    Brush color = _colorGuinda;
                    if (level.Contains("Informativo")) color = _colorVerde;
                    if (level.Contains("Emergencia")) color = _colorEmergencia;

                    HistorialEnviados.Insert(0, new MensajeItem
                    {
                        Hora = DateTime.Now.ToString("dd/MMM/yyyy HH:mm"),
                        Texto = message,
                        PrioridadTexto = level.ToUpper(),
                        ColorPrioridad = color
                    });

                    if (HistorialEnviados.Count > 20) HistorialEnviados.RemoveAt(20);
                });
            });

            IniciarConexion();
        }

        private async void IniciarConexion()
        {
            try { await _connection.StartAsync(); } catch { }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtInput.Text)) return;
            if (_connection.State != HubConnectionState.Connected) return;

            string nivel = (cbNivel.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Informativo";
            try
            {
                await _connection.InvokeAsync("SendAlert", txtInput.Text, nivel);

                // Fix de sonido: SystemSounds es más confiable
                if (nivel != "Informativo") SystemSounds.Hand.Play();
                else SystemSounds.Asterisk.Play();

                txtInput.Clear();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ToggleHistorial_Click(object sender, RoutedEventArgs e)
        {
            if (!_historialAbierto)
            {
                ColHistorial.Width = new GridLength(480); // Espacio para tarjetas de 440 + scroll
                _historialAbierto = true;
            }
            else
            {
                ColHistorial.Width = new GridLength(0);
                _historialAbierto = false;
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void HeaderBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => this.DragMove();
    }
}