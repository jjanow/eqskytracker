using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace EqSkyTracker.Gui;

public partial class ErrorDialog : Window
{
    public ErrorDialog()
    {
        InitializeComponent();
    }

    public ErrorDialog(string title, string message) : this()
    {
        Title = title;
        this.FindControl<TextBlock>("MessageText")!.Text = message;
        this.FindControl<Button>("OkButton")!.Click += OnOkClick;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
