using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using CADSimulator.Core;
using CADSimulator.Models;
using Microsoft.Win32;

namespace CADSimulator.UI
{
    public partial class MainWindow : Window
    {
        private Assembly? _currentAssembly;
        private Component? _selectedComponent;

        private double _cameraDistance = 1000;
        private double _cameraYaw = 45;
        private double _cameraPitch = 30;
        private Point3D _cameraTarget = new Point3D(0, 0, 0);
        private Point? _lastMousePosition;
        private bool _isOrbiting;
        private bool _isPanning;

        public MainWindow()
        {
            InitializeComponent();
            PropertyPanelControl.PoseChanged += RefreshViewport;
            UpdateCamera();
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
                RefreshViewport();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not read '{dialog.FileName}':\n{ex.Message}", "Import STEP",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshViewport()
        {
            if (_currentAssembly == null)
            {
                SceneRoot.Content = null;
                return;
            }

            var sceneGroup = AssemblyViewportBuilder.Build(_currentAssembly);
            SceneRoot.Content = sceneGroup;
            FitCameraToBounds(sceneGroup.Bounds);
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

        private void AssemblyTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _selectedComponent = (e.NewValue as TreeViewItem)?.Tag as Component;
            PropertyPanelControl.SetSelection(_currentAssembly, _selectedComponent);
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            // TODO: serialize the current assembly + sequences to the Projects/ folder.
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void FitCameraToBounds(Rect3D bounds)
        {
            if (bounds.IsEmpty)
            {
                return;
            }

            _cameraTarget = new Point3D(
                bounds.X + (bounds.SizeX / 2),
                bounds.Y + (bounds.SizeY / 2),
                bounds.Z + (bounds.SizeZ / 2));

            var diagonal = Math.Sqrt((bounds.SizeX * bounds.SizeX) + (bounds.SizeY * bounds.SizeY) + (bounds.SizeZ * bounds.SizeZ));
            _cameraDistance = diagonal > 0 ? diagonal * 1.5 : 1000;

            UpdateCamera();
        }

        private void UpdateCamera()
        {
            var yawRad = _cameraYaw * Math.PI / 180.0;
            var pitchRad = _cameraPitch * Math.PI / 180.0;

            var offset = new Vector3D(
                _cameraDistance * Math.Cos(pitchRad) * Math.Cos(yawRad),
                _cameraDistance * Math.Cos(pitchRad) * Math.Sin(yawRad),
                _cameraDistance * Math.Sin(pitchRad));

            var position = _cameraTarget + offset;
            Camera.Position = position;
            Camera.LookDirection = _cameraTarget - position;
            Camera.UpDirection = new Vector3D(0, 0, 1);
            Camera.NearPlaneDistance = Math.Max(_cameraDistance / 1000.0, 0.001);
            Camera.FarPlaneDistance = _cameraDistance * 100;
        }

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePosition = e.GetPosition(Viewport);
            _isOrbiting = e.ChangedButton == MouseButton.Left;
            _isPanning = e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle;
            Viewport.CaptureMouse();
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isOrbiting = false;
            _isPanning = false;
            Viewport.ReleaseMouseCapture();
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (_lastMousePosition == null || (!_isOrbiting && !_isPanning))
            {
                return;
            }

            var current = e.GetPosition(Viewport);
            var delta = current - _lastMousePosition.Value;
            _lastMousePosition = current;

            if (_isOrbiting)
            {
                _cameraYaw -= delta.X * 0.3;
                _cameraPitch = Clamp(_cameraPitch + (delta.Y * 0.3), -89, 89);
                UpdateCamera();
            }
            else if (_isPanning)
            {
                var right = Vector3D.CrossProduct(Camera.LookDirection, Camera.UpDirection);
                right.Normalize();
                var up = Vector3D.CrossProduct(right, Camera.LookDirection);
                up.Normalize();

                var panScale = _cameraDistance * 0.001;
                _cameraTarget -= right * (delta.X * panScale);
                _cameraTarget += up * (delta.Y * panScale);
                UpdateCamera();
            }
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var factor = e.Delta > 0 ? 0.9 : 1.1;
            _cameraDistance = Math.Max(_cameraDistance * factor, 0.01);
            UpdateCamera();
        }

        private static double Clamp(double value, double min, double max) => value < min ? min : (value > max ? max : value);
    }
}
