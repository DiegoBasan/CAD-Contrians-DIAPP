using System.Windows;
using System.Windows.Controls;
using CADSimulator.Models;

namespace CADSimulator.UI
{
    public partial class ControlPad : UserControl
    {
        public ControlPad()
        {
            InitializeComponent();
        }

        public void Render(ControlPanelDefinition definition)
        {
            ControlsHost.Children.Clear();

            foreach (var item in definition.Items)
            {
                FrameworkElement? element = item switch
                {
                    ControlButton button => new Button { Content = button.Name, Tag = button.Sequence },
                    ControlSlider slider => new Slider { Minimum = slider.Min, Maximum = slider.Max, Tag = slider.Joint },
                    ControlToggle toggle => new CheckBox { Content = toggle.Name, Tag = toggle.Event },
                    ControlStatusLight light => new TextBlock { Text = light.Name, Tag = light.Joint },
                    _ => null
                };

                if (element == null)
                {
                    continue;
                }

                element.Margin = new Thickness(4);
                ControlsHost.Children.Add(element);
            }
        }
    }
}
