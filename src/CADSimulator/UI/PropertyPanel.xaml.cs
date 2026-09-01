using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CADSimulator.Models;

namespace CADSimulator.UI
{
    public partial class PropertyPanel : UserControl
    {
        private Assembly? _assembly;
        private Component? _selected;
        private Component? _pendingA;
        private Component? _pendingB;

        /// <summary>Raised after a pose edit is applied, so the host can refresh the viewport.</summary>
        public event Action? PoseChanged;

        /// <summary>Raised after a new constraint is added to the current assembly.</summary>
        public event Action<Constraint>? ConstraintAdded;

        public PropertyPanel()
        {
            InitializeComponent();
            ConstraintTypeCombo.ItemsSource = Enum.GetValues(typeof(ConstraintType));
            ConstraintTypeCombo.SelectedIndex = 0;
        }

        /// <summary>Called by the host window whenever the tree selection changes.</summary>
        public void SetSelection(Assembly? assembly, Component? component)
        {
            _assembly = assembly;
            _selected = component;

            SelectedNameText.Text = component?.Name ?? "(none selected)";

            PositionXBox.Text = FormatOrEmpty(component?.Pose.Position.X);
            PositionYBox.Text = FormatOrEmpty(component?.Pose.Position.Y);
            PositionZBox.Text = FormatOrEmpty(component?.Pose.Position.Z);
            RotationXBox.Text = FormatOrEmpty(component?.Pose.Rotation.X);
            RotationYBox.Text = FormatOrEmpty(component?.Pose.Rotation.Y);
            RotationZBox.Text = FormatOrEmpty(component?.Pose.Rotation.Z);
        }

        private void ApplyPose_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                return;
            }

            _selected.Pose = new Pose
            {
                Position = new Vector3d(
                    ParseOr(PositionXBox.Text, _selected.Pose.Position.X),
                    ParseOr(PositionYBox.Text, _selected.Pose.Position.Y),
                    ParseOr(PositionZBox.Text, _selected.Pose.Position.Z)),
                Rotation = new Vector3d(
                    ParseOr(RotationXBox.Text, _selected.Pose.Rotation.X),
                    ParseOr(RotationYBox.Text, _selected.Pose.Rotation.Y),
                    ParseOr(RotationZBox.Text, _selected.Pose.Rotation.Z))
            };

            PoseChanged?.Invoke();
        }

        private void UseAsA_Click(object sender, RoutedEventArgs e)
        {
            _pendingA = _selected;
            ComponentALabel.Text = _selected?.Name ?? "(none)";
        }

        private void UseAsB_Click(object sender, RoutedEventArgs e)
        {
            _pendingB = _selected;
            ComponentBLabel.Text = _selected?.Name ?? "(none)";
        }

        private void AddConstraint_Click(object sender, RoutedEventArgs e)
        {
            if (_assembly == null || _pendingA == null || _pendingB == null || ConstraintTypeCombo.SelectedItem == null)
            {
                MessageBox.Show(Window.GetWindow(this), "Select components for both A and B first (via the assembly tree).",
                    "Add Constraint", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var constraint = new Constraint
            {
                Type = (ConstraintType)ConstraintTypeCombo.SelectedItem,
                ComponentAId = _pendingA.Id,
                ComponentBId = _pendingB.Id
            };

            _assembly.Constraints.Add(constraint);
            ConstraintList.Items.Add($"{constraint.Type}: {_pendingA.Name} <-> {_pendingB.Name}");
            ConstraintAdded?.Invoke(constraint);
        }

        private static string FormatOrEmpty(double? value) =>
            value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;

        private static double ParseOr(string text, double fallback) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }
}
