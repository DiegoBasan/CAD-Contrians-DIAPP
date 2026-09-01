using System.Collections.Generic;
using System.Linq;
using CADSimulator.Models;
using CADSimulator.Utils;

namespace CADSimulator.Core
{
    /// <summary>Evaluates a Sequence at a given time by interpolating between its keyframes.</summary>
    public static class KinematicSimulator
    {
        public static Dictionary<string, Pose> EvaluateAtTime(Sequence sequence, double timeMs)
        {
            var result = new Dictionary<string, Pose>();
            if (sequence.Frames.Count == 0)
            {
                return result;
            }

            var ordered = sequence.Frames.OrderBy(f => f.Time).ToList();

            var before = ordered.LastOrDefault(f => f.Time <= timeMs) ?? ordered.First();
            var after = ordered.FirstOrDefault(f => f.Time >= timeMs) ?? ordered.Last();

            var span = after.Time - before.Time;
            var t = span <= 0 ? 0 : MathHelper.Clamp01((timeMs - before.Time) / span);

            var componentIds = before.Poses.Keys.Union(after.Poses.Keys);
            foreach (var id in componentIds)
            {
                var hasBefore = before.Poses.TryGetValue(id, out var poseBefore);
                var hasAfter = after.Poses.TryGetValue(id, out var poseAfter);

                if (hasBefore && hasAfter)
                {
                    result[id] = MathHelper.Lerp(poseBefore!, poseAfter!, t);
                }
                else
                {
                    result[id] = hasBefore ? poseBefore! : poseAfter!;
                }
            }

            return result;
        }
    }
}
