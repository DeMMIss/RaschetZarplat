using System.Windows;

namespace РасчетВыплатЗарплаты;

public partial class ErrorWindow : Window
{
    public string ErrorMessage { get; set; } = "";

    public ErrorWindow(string title, string message)
    {
        InitializeComponent();
        this.Title = title;
        ErrorMessage = message;
        DataContext = this;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ErrorTextBox.SelectAll();
            ErrorTextBox.Copy();
            ErrorTextBox.SelectionLength = 0;
            Clipboard.SetText(ErrorMessage);
            MessageBox.Show("Текст ошибки скопирован в буфер обмена", "Скопировано", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch
        {
            try
            {
                Clipboard.SetText(ErrorMessage);
                MessageBox.Show("Текст ошибки скопирован в буфер обмена", "Скопировано", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
            }
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
