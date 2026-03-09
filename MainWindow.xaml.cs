using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit.Highlighting;

namespace bismuthIDE;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Click \"OpenFile\"button!");
    }
    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Click \"OpenFolder\"button!");
    }
    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
    // Output logs along with the current time
    string timestamp = DateTime.Now.ToString("HH:mm:ss");
    OutputLog.AppendText($"[{timestamp}] Execution has begun...\n");

    // Retrieve the editor's contents and output them to the console (for testing)
    string code = CodeEditor.Text;
    OutputLog.AppendText($"Number of characters in the entered code: {code.Length}\n");

    // Automatically scroll to the bottom
    OutputLog.ScrollToEnd();
    }
}