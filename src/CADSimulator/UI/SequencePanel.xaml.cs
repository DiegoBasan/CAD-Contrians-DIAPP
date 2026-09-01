using System.Windows;
using System.Windows.Controls;
using CADSimulator.Core;

namespace CADSimulator.UI
{
    public partial class SequencePanel : UserControl
    {
        private readonly SequenceRecorder _recorder = new SequenceRecorder();

        public SequencePanel()
        {
            InitializeComponent();
        }

        private void Record_Click(object sender, RoutedEventArgs e)
        {
            _recorder.Start("New Sequence");
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            // TODO: drive the viewport by evaluating KinematicSimulator.EvaluateAtTime over time.
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _recorder.Stop();
        }
    }
}
