using System.Collections.Generic;

namespace CADSimulator.Utils.Step
{
    public enum StepValueKind
    {
        Reference,
        Number,
        Text,
        Enumeration,
        Omitted,
        Inherited,
        List
    }

    /// <summary>One parsed value from a STEP (ISO 10303-21) entity parameter list.</summary>
    public class StepValue
    {
        public StepValueKind Kind { get; set; }
        public int Reference { get; set; }
        public double Number { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<StepValue> Items { get; set; } = new List<StepValue>();

        /// <summary>Set only when this value is an inline typed constructor, otherwise null.</summary>
        public string? Keyword { get; set; }

        public static StepValue MakeReference(int id) => new StepValue { Kind = StepValueKind.Reference, Reference = id };
        public static StepValue MakeNumber(double value) => new StepValue { Kind = StepValueKind.Number, Number = value };
        public static StepValue MakeText(string value) => new StepValue { Kind = StepValueKind.Text, Text = value };
        public static StepValue MakeEnumeration(string value) => new StepValue { Kind = StepValueKind.Enumeration, Text = value };
        public static StepValue MakeOmitted() => new StepValue { Kind = StepValueKind.Omitted };
        public static StepValue MakeInherited() => new StepValue { Kind = StepValueKind.Inherited };

        public static StepValue MakeList(List<StepValue> items, string? keyword = null) =>
            new StepValue { Kind = StepValueKind.List, Items = items, Keyword = keyword };
    }

    public static class StepValueExtensions
    {
        public static int AsReference(this StepValue value) => value.Kind == StepValueKind.Reference ? value.Reference : 0;
        public static string AsText(this StepValue value) => value.Kind == StepValueKind.Text ? value.Text : string.Empty;
        public static double AsNumber(this StepValue value) => value.Kind == StepValueKind.Number ? value.Number : 0;
        public static List<StepValue> AsList(this StepValue value) => value.Kind == StepValueKind.List ? value.Items : new List<StepValue>();
    }
}
