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
using Microsoft.Win32;
using ICSharpCode.AvalonEdit;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace bismuthIDE.src;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddTab("Untitled.cs", "// Write your code here...");
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        // 1. File Selection Dialog Settings
        OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "C# Files (*.cs)|*.cs|All files (*.*)|*.*";

        // 2. Display a dialog and only proceed if the user clicks "Open"
        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                string content = File.ReadAllText(openFileDialog.FileName);
                AddTab(openFileDialog.FileName, content);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open the file: {ex.Message}");
            }
        }
    }
    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Click \"OpenFolder\"button!");
    }
    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        OutputLog.Clear();
        OutputLog.AppendText("--- コンパイル開始 ---\n");

        try
        {
            // 1. エディタからコードを取得
            if (CurrentEditor == null) return;
            string codeToCompile = CurrentEditor.Text;

            // 2. 構文解析（コードの木構造を作る）
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(codeToCompile);

            // 3. コンパイル設定（標準的なライブラリを読み込む）
            string assemblyName = System.IO.Path.GetRandomFileName();
            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(System.IO.Path.PathSeparator);
            var references = trustedAssemblies
                .Select(path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToList();

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: [syntaxTree],
                references: references,
                options: new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            // 4. メモリ上に出力
            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    // エラーがあればコンソールに表示
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                    {
                        OutputLog.AppendText($"{diagnostic.Id}: {diagnostic.GetMessage()}\n");
                    }
                }
                else
                {
                    OutputLog.AppendText("コンパイル成功！実行中...\n\n");

                    // 5. 実行（メモリ上のアセンブリをロードしてMainを呼ぶ）
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());
                    var entryPoint = assembly.EntryPoint;

                    // 標準出力をキャプチャしてIDEのコンソールに向ける（簡易版）
                    var sw = new StringWriter();
                    Console.SetOut(sw);

                    var parameters = entryPoint.GetParameters().Length > 0 ? new object[] { Array.Empty<string>() } : null;
                    entryPoint.Invoke(null, parameters);

                    OutputLog.AppendText(sw.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            OutputLog.AppendText($"エラー: {ex.Message}\n");
        }
    }
    /// <summary>
    /// A property for easily obtaining the currently active editor
    /// </summary>
    private TextEditor ?CurrentEditor => (EditorTabs.SelectedItem as TabItem)?.Content as TextEditor;

    private void AddTab(string fileName, string content)
    {
        // 1. Create a new editor
        var newEditor = new TextEditor
        {
            Text = content,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14,
            ShowLineNumbers = true,
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#")
        };

        // 2. Construct the tab header (appearance)
    var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
    var headerText = new TextBlock
    {
        Text = System.IO.Path.GetFileName(fileName),
        VerticalAlignment = VerticalAlignment.Center
    };

    // Close button (x)
    var closeButton = new Button
    {
        Content = "×",
        Margin = new Thickness(5, 0, 0, 0),
        Padding = new Thickness(2),
        FontSize = 10,
        Width = 18,
        Height = 18,
        Background = Brushes.Transparent,
        BorderBrush = Brushes.Transparent,
        VerticalAlignment = VerticalAlignment.Center
    };

    // 3. Handling when the close button is pressed
    var newTab = new TabItem();
    closeButton.Click += (s, e) => {
        EditorTabs.Items.Remove(newTab);
        e.Handled = true; // Prevent the tab selection event from propagating further
    };

    headerStack.Children.Add(headerText);
    headerStack.Children.Add(closeButton);

    // 4. Tab Item Settings
    newTab.Header = headerStack;
    newTab.Content = newEditor;
    newTab.ToolTip = fileName;

    // 5. Add and Select
    EditorTabs.Items.Add(newTab);
    EditorTabs.SelectedItem = newTab;
    }
}