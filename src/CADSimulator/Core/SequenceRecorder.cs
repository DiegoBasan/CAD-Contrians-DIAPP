using System.Collections.Generic;
using CADSimulator.Models;

namespace CADSimulator.Core
{
    /// <summary>Captures component poses over time into a Sequence while the user manually poses an assembly.</summary>
    public class SequenceRecorder
    {
        public Sequence Sequence { get; } = new Sequence();

        public bool IsRecording { get; private set; }

        public void Start(string sequenceName)
        {
            Sequence.Name = sequenceName;
            Sequence.Frames.Clear();
            Sequence.Events.Clear();
            IsRecording = true;
        }

        public void CaptureFrame(double timeMs, Dictionary<string, Pose> poses)
        {
            if (!IsRecording)
            {
                return;
            }

            Sequence.Frames.Add(new Frame
            {
                Time = timeMs,
                Poses = new Dictionary<string, Pose>(poses)
            });
        }

        public void CaptureEvent(double timeMs, string action, object? value)
        {
            if (!IsRecording)
            {
                return;
            }

            Sequence.Events.Add(new SequenceEvent
            {
                Time = timeMs,
                Action = action,
                Value = value
            });
        }

        public void Stop()
        {
            IsRecording = false;
        }
    }
}
