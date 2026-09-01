using System.Windows;
using CADSimulator.Core;
using Microsoft.Win32;

namespace CADSimulator.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ImportStep_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "STEP files (*.step;*.stp)|*.step;*.stp|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var assembly = AssemblyLoader.LoadFromStep(dialog.FileName);
                // TODO: populate AssemblyTree and Viewport from the loaded assembly.
            }
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            // TODO: serialize the current assembly + sequences to the Projects/ folder.
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
