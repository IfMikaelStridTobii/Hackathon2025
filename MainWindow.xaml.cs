using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace sample_wpf;

public partial class MainWindow : Window
{
    private readonly OpenAIChatService? _chatService;
    private CancellationTokenSource? _inFlightRequest;

    public ObservableCollection<string> Todos { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        try
        {
            _chatService = new OpenAIChatService();
        }
        catch (Exception ex)
        {
            SendButton.IsEnabled = false;
            StatusTextBlock.Text = ex.Message;
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (_chatService is null)
        {
            StatusTextBlock.Text = "Configure OPENAI_API_KEY and restart the app.";
            return;
        }

        var prompt = PromptTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            StatusTextBlock.Text = "Please enter a prompt first.";
            return;
        }

        try
        {
            _inFlightRequest?.Cancel();
            _inFlightRequest = new CancellationTokenSource();

            SendButton.IsEnabled = false;
            StatusTextBlock.Text = "Contacting OpenAI...";
            ResponseTextBox.Clear();

            var response = await _chatService.GetChatCompletionAsync(prompt, _inFlightRequest.Token);
            ResponseTextBox.Text = response;
            StatusTextBlock.Text = "Response received.";
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Request canceled.";
        }
        catch (Exception ex)
        {
            ResponseTextBox.Text = ex.Message;
            StatusTextBlock.Text = "Failed to fetch response.";
        }
        finally
        {
            SendButton.IsEnabled = true;
            _inFlightRequest?.Dispose();
            _inFlightRequest = null;
        }
    }

    private void AddTodo_Click(object sender, RoutedEventArgs e)
    {
        var text = TodoInputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Todos.Add(text);
        TodoInputTextBox.Clear();
        TodoInputTextBox.Focus();
    }

    private void RemoveTodo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (button.DataContext is string todo && Todos.Contains(todo))
        {
            Todos.Remove(todo);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _inFlightRequest?.Cancel();
        _inFlightRequest?.Dispose();
        _chatService?.Dispose();
        base.OnClosed(e);
    }
}
