using System;
using System.Globalization;
using System.Xml.Linq;
using CADSimulator.Models;

namespace CADSimulator.Core
{
    /// <summary>
    /// Parses a project's &lt;ControlPanel&gt; XML (buttons, sliders, toggles, status lights)
    /// into a ControlPanelDefinition that the UI can render dynamically.
    /// </summary>
    public static class ControlPanelLoader
    {
        public static ControlPanelDefinition Load(string filePath)
        {
            var doc = XDocument.Load(filePath);
            var root = doc.Root ?? throw new InvalidOperationException("Empty control panel definition.");

            var definition = new ControlPanelDefinition();

            foreach (var element in root.Elements())
            {
                ControlItem? item = element.Name.LocalName switch
                {
                    "Button" => new ControlButton
                    {
                        Name = Attr(element, "Name"),
                        Sequence = Attr(element, "Sequence")
                    },
                    "Slider" => new ControlSlider
                    {
                        Name = Attr(element, "Name"),
                        Joint = Attr(element, "Joint"),
                        Min = ParseDouble(Attr(element, "Min")),
                        Max = ParseDouble(Attr(element, "Max"))
                    },
                    "Toggle" => new ControlToggle
                    {
                        Name = Attr(element, "Name"),
                        Event = Attr(element, "Event")
                    },
                    "StatusLight" => new ControlStatusLight
                    {
                        Name = Attr(element, "Name"),
                        Joint = Attr(element, "Joint")
                    },
                    _ => null
                };

                if (item != null)
                {
                    definition.Items.Add(item);
                }
            }

            return definition;
        }

        private static string Attr(XElement element, string name) =>
            element.Attribute(name)?.Value ?? string.Empty;

        private static double ParseDouble(string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }
}
