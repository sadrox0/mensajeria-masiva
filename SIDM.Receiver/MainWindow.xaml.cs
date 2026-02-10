using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.AspNetCore.SignalR.Client;

namespace SIDM.Receiver;

public partial class MainWindow : Window
{
    private HubConnection _connection;
    private bool _esModoBurbuja = false;

    // Guardamos la posición de la burbuja
    private double _ultimoLeftBurbuja = -1;
    private double _ultimoTopBurbuja = -1;

    public MainWindow()
    {
        InitializeComponent();

        _connection = new HubConnectionBuilder()
            .WithUrl("http://10.1.1.98:5271/sidmHub")
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string, string>("ReceiveAlert", (message, level) => {
            Dispatcher.Invoke(() => {
                txtMensaje.Text = message;
                txtPrioridad.Text = level.ToUpper();

                if (level == "Alerta de Emergencia" || level == "Urgente")
                {
                    MostrarVentanaCompleta();
                }
                else
                {
                    if (_esModoBurbuja) { BadgeAlerta.Visibility = Visibility.Visible; }
                    else { MostrarVentanaCompleta(); }
                }
                AplicarColoresSegunNivel(level);
            });
        });

        StartConnection();

        // Posición inicial de la burbuja (esquina inferior derecha al arrancar)
        _ultimoLeftBurbuja = SystemParameters.WorkArea.Width - 140;
        _ultimoTopBurbuja = SystemParameters.WorkArea.Height - 140;
    }

    private async void StartConnection()
    {
        try { await _connection.StartAsync(); } catch { }
    }

    private void MostrarVentanaCompleta()
    {
        // Antes de abrir la ventana, si venimos de burbuja, guardamos su posición actual
        if (_esModoBurbuja)
        {
            _ultimoLeftBurbuja = this.Left;
            _ultimoTopBurbuja = this.Top;
        }

        _esModoBurbuja = false;
        GridAlerta.Visibility = Visibility.Visible;
        GridBurbuja.Visibility = Visibility.Collapsed;
        BadgeAlerta.Visibility = Visibility.Collapsed;

        this.Width = 600;
        this.Height = 400;

        // LA VENTANA SIEMPRE AL CENTRO
        this.Left = (SystemParameters.WorkArea.Width - 600) / 2;
        this.Top = (SystemParameters.WorkArea.Height - 400) / 2;

        MainBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#F2F2F2");
        MainBorder.BorderBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#004581");
        MainBorder.BorderThickness = new Thickness(4);
        MainBorder.CornerRadius = new CornerRadius(20);

        this.Topmost = true;
        this.Activate();
    }

    private void ActivarModoBurbuja()
    {
        _esModoBurbuja = true;
        GridAlerta.Visibility = Visibility.Collapsed;
        GridBurbuja.Visibility = Visibility.Visible;

        this.Width = 120;
        this.Height = 120;

        // REGRESAR A DONDE EL USUARIO DEJÓ LA BURBUJA
        this.Left = _ultimoLeftBurbuja;
        this.Top = _ultimoTopBurbuja;

        MainBorder.Background = Brushes.Transparent;
        MainBorder.BorderBrush = Brushes.Transparent;
        MainBorder.BorderThickness = new Thickness(0);

        this.Topmost = true;
    }

    private void AplicarColoresSegunNivel(string nivel)
    {
        var bc = new BrushConverter();
        if (nivel == "Alerta de Emergencia")
        {
            HeaderBar.Background = Brushes.DarkRed;
            MainBorder.BorderBrush = Brushes.DarkRed;
            txtPrioridad.Foreground = Brushes.DarkRed;
        }
        else if (nivel == "Urgente")
        {
            HeaderBar.Background = Brushes.DarkOrange;
            MainBorder.BorderBrush = Brushes.DarkOrange;
            txtPrioridad.Foreground = Brushes.DarkOrange;
        }
        else
        {
            HeaderBar.Background = (SolidColorBrush)bc.ConvertFrom("#004581");
            MainBorder.BorderBrush = (SolidColorBrush)bc.ConvertFrom("#004581");
            txtPrioridad.Foreground = (SolidColorBrush)bc.ConvertFrom("#004581");
        }
    }

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) this.DragMove();
    }

    private void BtnMinimizar_Click(object sender, RoutedEventArgs e) => ActivarModoBurbuja();

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (_connection.State == HubConnectionState.Connected)
        {
            try { await _connection.InvokeAsync("Ack"); } catch { }
        }
        ActivarModoBurbuja();
    }

    private void GridBurbuja_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MostrarVentanaCompleta();
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            this.DragMove();
            // Guardamos la nueva posición después de arrastrar
            _ultimoLeftBurbuja = this.Left;
            _ultimoTopBurbuja = this.Top;
        }
    }
}