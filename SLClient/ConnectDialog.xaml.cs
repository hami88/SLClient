using System.Windows;

namespace SLClient;

public partial class ConnectDialog : Window
{
    public string Host
    {
        get => HostBox.Text;
        set => HostBox.Text = value;
    }

    public int Port
    {
        get => int.TryParse(PortBox.Text, out int port) ? port : 0;
        set => PortBox.Text = value.ToString();
    }

    public ConnectDialog()
    {
        InitializeComponent();
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            MessageBox.Show("Bitte einen Host eingeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Port <= 0)
        {
            MessageBox.Show("Bitte eine gültige Portnummer eingeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
