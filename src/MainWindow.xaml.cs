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

    private void NewFile_Click(object sender, RoutedEventArgs e)
    {
        AddTab("Untitled.cs", "// New file content here...");
        OutputLog.AppendText("Created a new file.\n");
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "C# Files (*.cs)|*.cs|All files (*.*)|*.*";
        if (openFileDialog.ShowDialog() == true)
        {
            string content = File.ReadAllText(openFileDialog.FileName);
            AddTab(openFileDialog.FileName, content);
        }
    }
    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog(); // .NET 10 (WinAppSDK) 等で利用可能
        if (dialog.ShowDialog() == true)
        {
            string folderPath = dialog.FolderName;
            LoadFolder(folderPath);
        }
    }

    private void LoadFolder(string path)
    {
        FileExplorer.Items.Clear();
        var rootItem = CreateTreeItem(path);
        FileExplorer.Items.Add(rootItem);
    }

    // Save File (Overwrite)
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (EditorTabs.SelectedItem is TabItem currentTab)
        {
            string? filePath = currentTab.ToolTip as string;
            if (string.IsNullOrEmpty(filePath) || filePath == "Untitled.cs")
            {
                SaveAsButton_Click(sender, e);
            }
            else
            {
                File.WriteAllText(filePath, CurrentEditor?.Text);
                OutputLog.AppendText($"Saved: {filePath}\n");
            }
        }
    }

    // Save As
    private void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        if (EditorTabs.SelectedItem is TabItem currentTab)
        {
            var sfd = new SaveFileDialog { Filter = "C# Files (*.cs)|*.cs|All files (*.*)|*.*" };
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, CurrentEditor?.Text);
                currentTab.ToolTip = sfd.FileName;
                OutputLog.AppendText($"Saved As: {sfd.FileName}\n");
            }
        }
    }

    // Retrieve folders and files hierarchically
    private TreeViewItem CreateTreeItem(string path)
    {
        var item = new TreeViewItem
        {
            Header = System.IO.Path.GetFileName(path),
            Tag = path // Keep the full path
        };

        try
        {
            // Add directory
            foreach (var dir in Directory.GetDirectories(path))
            {
                item.Items.Add(CreateTreeItem(dir));
            }
            // Add files
            foreach (var file in Directory.GetFiles(path))
            {
                var fileItem = new TreeViewItem
                {
                    Header = System.IO.Path.GetFileName(file),
                    Tag = file
                };
                item.Items.Add(fileItem);
            }
        }
        catch { /* Error handling for access denied and similar issues */ }

        return item;
    }
    private void FileExplorer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem selectedItem)
        {
            string ?fullPath = selectedItem.Tag.ToString();

            // If the file exists and is not a directory
            if (File.Exists(fullPath))
            {
                // If a tab is already open, select it; otherwise, create a new one.
                var existingTab = EditorTabs.Items.Cast<TabItem>()
                    .FirstOrDefault(t => t.ToolTip?.ToString() == fullPath);

                if (existingTab != null)
                {
                    EditorTabs.SelectedItem = existingTab;
                }
                else
                {
                    string content = File.ReadAllText(fullPath);
                    AddTab(fullPath, content);
                }
            }
        }
    }
    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        OutputLog.Clear();
        OutputLog.AppendText("--- Compile ---\n");

        try
        {
            // 1. Get the code from the editor
            if (CurrentEditor == null) return;
            string codeToCompile = CurrentEditor.Text;

            // 2. Syntax Parsing (Building the Code Tree Structure)
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(codeToCompile);

            // 3. Compilation Settings (Load Standard Libraries)
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

            // 4. Output to memory
            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    // If an error occurs, it will be displayed in the console.
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
                    OutputLog.AppendText("Compilation successful! Running...\n\n");

                    // 5. Execution (Load the assembly into memory and call Main)
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());
                    var entryPoint = assembly.EntryPoint;

                    // Capture standard output and redirect it to the IDE console (simplified version)
                    var sw = new StringWriter();
                    Console.SetOut(sw);

                    var parameters = entryPoint?.GetParameters().Length > 0 ? new object[] { Array.Empty<string>() } : null;
                    entryPoint?.Invoke(null, parameters);

                    OutputLog.AppendText(sw.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            OutputLog.AppendText($"Error: {ex.Message}\n");
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
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#"),

            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212)),
            LineNumbersForeground = Brushes.DimGray
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

    // Toggle Sidebar (Tree) Display
    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        // SideBarColumn is the zero-indexed element in Grid.ColumnDefinitions
        SideBarColumn.Width = (SideBarColumn.Width.Value > 0) 
        ? new GridLength(0) 
        : new GridLength(200);
    }

    // Switching console displays
    private void ToggleConsole_Click(object sender, RoutedEventArgs e)
    {
        ConsoleRow.Height = (ConsoleRow.Height.Value > 0) 
        ? new GridLength(0) 
        : new GridLength(150);
    }

    // App closed
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}