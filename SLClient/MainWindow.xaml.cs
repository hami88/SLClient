using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SLClient
{
    public partial class MainWindow : Window
    {
        private TelnetClient? _telnet;
        private bool _awaitingResponse = false;
        private Map.Map? _mapWindow;

        public MainWindow()
        {
            InitializeComponent();

            // Shortcut STRG+M registrieren
            var toggleMapCommand = new RoutedCommand();
            toggleMapCommand.InputGestures.Add(new KeyGesture(Key.M, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(toggleMapCommand, ToggleMap));

            InputBox?.Focus();
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ConnectDialog
            {
                Owner = this,
                Host = HostBox?.Text ?? string.Empty,
                Port = int.TryParse(PortBox?.Text, out var p) ? p : 4711
            };

            if (dlg.ShowDialog() == true)
            {
                if (HostBox != null)
                    HostBox.Text = dlg.Host;
                if (PortBox != null)
                    PortBox.Text = dlg.Port.ToString();

                if (!int.TryParse(PortBox?.Text, out int port))
                {
                    OutputBox?.AppendText("Ungültiger Port.\n");
                    return;
                }

                await ConnectToServer(dlg.Host, port);
            }
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            if (_telnet?.IsConnected == true)
            {
                _telnet.Disconnect();
                _telnet = null;
                OutputBox?.AppendText("Verbindung wurde getrennt.\n");
            }
            else
            {
                OutputBox?.AppendText("Keine aktive Verbindung.\n");
            }
        }

        private async Task ReceiveLoop()
        {
            while (_telnet?.IsConnected == true)
            {
                string? line = await _telnet.ReceiveAsync();
                if (!string.IsNullOrEmpty(line))
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (line.StartsWith(">"))
                            line = line.Substring(1).TrimStart();

                        if (_awaitingResponse)
                        {
                            var text = OutputBox?.Text ?? string.Empty;
                            if (!text.EndsWith("\n\n") && !text.EndsWith("\r\n\r\n"))
                                OutputBox?.AppendText("\n");

                            _awaitingResponse = false;
                        }

                        OutputBox?.AppendText(line + "\n");
                        OutputBox?.ScrollToEnd();
                    });
                }
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (InputBox == null)
                return;

            await HandleInput(InputBox.Text);

            // Text nicht löschen, sondern markieren und Fokus setzen
            InputBox.SelectAll();
            InputBox.Focus();
        }

        private async Task HandleInput(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            string cmd = command.Trim().ToLower();

            string normalizedCmd = NormalizeDirection(cmd);

            if (_mapWindow is not null && _mapWindow.IsVisible && ValidDirections.Contains(normalizedCmd))
            {
                _mapWindow.Move(normalizedCmd);
            }

            if (_telnet?.IsConnected == true)
            {
                await _telnet.SendAsync(command);
                _awaitingResponse = true;
            }
            else
            {
                OutputBox?.AppendText("Nicht verbunden.\n");
            }
        }

        private async Task ConnectToServer(string host, int port)
        {
            OutputBox?.AppendText($"Verbinde zu {host}:{port}...\n");
            await ConnectToTelnet(host, port);
        }

        private async Task ConnectToTelnet(string host, int port)
        {
            _telnet = new TelnetClient();
            bool connected = await _telnet.ConnectAsync(host, port);

            if (connected)
            {
                OutputBox?.AppendText($"Verbunden mit {host}:{port}\n\n");
                _ = ReceiveLoop();

                // Fokus auf InputBox setzen, sobald verbunden
                Dispatcher.Invoke(() =>
                {
                    InputBox?.Focus();
                });
            }
            else
            {
                OutputBox?.AppendText("Verbindung fehlgeschlagen.\n");
                _telnet = null;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Möchten Sie das Programm wirklich beenden?", "Beenden",
                                         MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Net deppad sei....", "Über", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OutputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Optional: AutoScroll oder ähnliches
        }

        private async void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (InputBox == null)
                    return;

                await HandleInput(InputBox.Text);

                // Text nicht löschen, sondern markieren und Fokus setzen
                InputBox.SelectAll();
                InputBox.Focus();

                e.Handled = true;
            }
        }

        #region Map
        private string NormalizeDirection(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return input.ToLower()
                        .Replace("ä", "ae")
                        .Replace("ö", "oe")
                        .Replace("ü", "ue")
                        .Replace("ß", "ss");
        }

        private static readonly HashSet<string> ValidDirections = new(StringComparer.OrdinalIgnoreCase)
        {
            // 2D Richtungen
            "north", "n", "norden",
            "south", "s", "sued", "sueden", "süden",
            "west", "w", "westen",
            "east", "e", "o", "osten",
            "northwest", "nw", "nordwest", "nordwesten",
            "northeast", "ne", "no", "nordost", "nordosten",
            "southwest", "sw", "südwest", "südwesten",
            "southeast", "se", "so", "südost", "südosten",

            // Nur Z-Richtung (oben/unten)
            "oben", "ob", "up",
            "unten", "u", "down",

            // 3D-Richtungen: Nordost
            "nordostoben", "northeastup", "noob", "neu",
            "nordostunten", "northeastdown", "nou", "ned",

            // Nordwest
            "nordwestoben", "northwestup", "nwup", "nwob",
            "nordwestunten", "northwestdown", "nwdown",

            // Südost
            "südostoben", "southeastup", "seup", "soob",
            "südostunten", "southeastdown", "sedown", "sou",

            // Südwest
            "südwestoben", "southwestup", "swup", "swob",
            "südwestunten", "southwestdown", "swdown", "swu"
        };

        private void MapOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_mapWindow == null || !_mapWindow.IsVisible)
                OpenMapWindow();
            else
                _mapWindow.Activate();
        }

        private void ToggleMap(object sender, ExecutedRoutedEventArgs e)
        {
            ToggleMapWindow();
        }

        private void ToggleMapWindow()
        {
            if (_mapWindow == null || !_mapWindow.IsVisible)
                OpenMapWindow();
            else
                CloseMapWindow();
        }

        private void OpenMapWindow()
        {
            _mapWindow = new Map.Map
            {
                Owner = this
            };
            _mapWindow.Show();

            // Fokus auf MainWindow setzen, damit Fokus nicht auf Map geht
            this.Activate();
            this.Focus();
        }

        private void CloseMapWindow()
        {
            if (_mapWindow is not null)
            {
                _mapWindow.Close();
                _mapWindow = null;
            }
        }
        #endregion
    }
}
