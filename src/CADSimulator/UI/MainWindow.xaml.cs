using System;
using System.IO;
using System.Windows;
using CADSimulator.Core;
using CADSimulator.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace CADSimulator.UI
{
    public partial class MainWindow : Window
    {
        private Assembly? _currentAssembly;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await WebView.EnsureCoreWebView2Async();

            var wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "appassets", wwwrootPath, CoreWebView2HostResourceAccessKind.Allow);

            WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            WebView.CoreWebView2.Navigate("https://appassets/index.html");
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.WebMessageAsJson;

            WebMessage? message;
            try
            {
                message = JsonConvert.DeserializeObject<WebMessage>(json);
            }
            catch (JsonException)
            {
                return;
            }

            switch (message?.Type)
            {
                case "importStep":
                    ImportStep();
                    break;
                case "saveProject":
                    SaveProject(json);
                    break;
            }
        }

        private void ImportStep()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "STEP files (*.step;*.stp)|*.step;*.stp|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _currentAssembly = AssemblyLoader.LoadFromStep(dialog.FileName);
                var dto = AssemblySceneExport.ToDto(_currentAssembly);
                PostToWeb(new { type = "assemblyLoaded", assembly = dto });
            }
            catch (Exception ex)
            {
                PostToWeb(new { type = "error", message = $"Could not read '{dialog.FileName}': {ex.Message}" });
            }
        }

        private void SaveProject(string webMessageAsJson)
        {
            try
            {
                var projectsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
                Directory.CreateDirectory(projectsDir);

                var path = Path.Combine(projectsDir, $"project-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.WriteAllText(path, webMessageAsJson);

                PostToWeb(new { type = "saved", path });
            }
            catch (Exception ex)
            {
                PostToWeb(new { type = "error", message = $"Could not save project: {ex.Message}" });
            }
        }

        private void PostToWeb(object payload)
        {
            WebView.CoreWebView2.PostWebMessageAsJson(JsonConvert.SerializeObject(payload));
        }

        private class WebMessage
        {
            public string Type { get; set; } = string.Empty;
        }
    }
}
