using System.Collections.Generic;

namespace CADSimulator.Models
{
    public abstract class ControlItem
    {
        public string Name { get; set; } = string.Empty;
    }

    public class ControlButton : ControlItem
    {
        public string Sequence { get; set; } = string.Empty;
    }

    public class ControlSlider : ControlItem
    {
        public string Joint { get; set; } = string.Empty;
        public double Min { get; set; }
        public double Max { get; set; }
    }

    public class ControlToggle : ControlItem
    {
        public string Event { get; set; } = string.Empty;
    }

    public class ControlStatusLight : ControlItem
    {
        public string Joint { get; set; } = string.Empty;
        public Vector3d Value { get; set; }
    }

    public class ControlPanelDefinition
    {
        public List<ControlItem> Items { get; set; } = new List<ControlItem>();
    }
}
