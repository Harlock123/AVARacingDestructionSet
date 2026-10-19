using Avalonia.Controls;
using AVARacingDestructionSet.Views;

namespace AVARacingDestructionSet;

public partial class MainWindow : Window
{
    public MainWindow() : this([]) { }

    public MainWindow(string[] args)
    {
        InitializeComponent();
        Content = new GameCanvas(silent: args.Contains("--no-audio"));
    }
}
