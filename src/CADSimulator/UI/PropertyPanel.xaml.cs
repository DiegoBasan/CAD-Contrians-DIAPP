using System.Windows;
using System.Windows.Controls;

namespace CADSimulator.UI
{
    public partial class PropertyPanel : UserControl
    {
        public PropertyPanel()
        {
            InitializeComponent();
        }

        private void AddConstraint_Click(object sender, RoutedEventArgs e)
        {
            // TODO: build a Constraint from the selected components + ConstraintTypeCombo
            // and hand it to ConstraintSolver.
        }
    }
}
