using System.Collections.ObjectModel;
using System.Windows;

namespace AudioMixer.Desktop;

public partial class MainWindow : Window
{
    public ObservableCollection<ChannelPlaceholder> Channels { get; } = new()
    {
        new("GAME"),
        new("CHAT"),
        new("MEDIA"),
        new("AUX"),
        new("MICROPHONE")
    };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public sealed record ChannelPlaceholder(string Name);
}
