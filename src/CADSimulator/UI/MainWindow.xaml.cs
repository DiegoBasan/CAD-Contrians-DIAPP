using System;
using System.Windows;
using System.Windows.Controls;
using CADSimulator.Core;
using CADSimulator.Models;
using Microsoft.Win32;

namespace CADSimulator.UI
{
    public partial class MainWindow : Window
    {
        private Assembly? _currentAssembly;

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

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _currentAssembly = AssemblyLoader.LoadFromStep(dialog.FileName);
                PopulateAssemblyTree(_currentAssembly);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not read '{dialog.FileName}':\n{ex.Message}", "Import STEP",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // TODO: render the loaded geometry in Viewport (needs BREP face tessellation).
        }

        private void PopulateAssemblyTree(Assembly assembly)
        {
            AssemblyTree.Items.Clear();
            foreach (var component in assembly.Components)
            {
                AssemblyTree.Items.Add(BuildTreeItem(component));
            }
        }

        private static TreeViewItem BuildTreeItem(Component component)
        {
            var item = new TreeViewItem
            {
                Header = $"{component.Name} ({component.Faces.Count} faces)",
                Tag = component,
                IsExpanded = true
            };

            foreach (var child in component.Children)
            {
                item.Items.Add(BuildTreeItem(child));
            }

            return item;
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
