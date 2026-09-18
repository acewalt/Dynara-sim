using System;

namespace DynaraSim.Core
{
    public sealed class Percept
    {
        public string ObserverId;
        public long SourceEventSequence;
        public string Type;
        public string Description;
        public string SubjectId;
        public string LocationId;
        public float Confidence;
        public float Salience;
        public float Threat;
    }

    public sealed class PerceptionSystem
    {
        public Percept Perceive(WorldEvent worldEvent, AgentProfile observer, WorldState world)
        {
            if (worldEvent == null || observer == null || world == null) return null;

            if (observer.Can(AgentCapability.ObserveGlobalWorld))
                return Build(worldEvent, observer.Id, 1f, Math.Max(0.35f, worldEvent.Importance));

            EntityState entity = world.FindEntity(observer.Id);
            if (entity == null || !entity.Active) return null;

            bool directlyInvolved =
                string.Equals(worldEvent.SourceId, observer.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(worldEvent.TargetId, observer.Id, StringComparison.OrdinalIgnoreCase);

            bool sameLocation =
                !string.IsNullOrWhiteSpace(entity.LocationId) &&
                string.Equals(entity.LocationId, worldEvent.LocationId, StringComparison.OrdinalIgnoreCase);

            if (!directlyInvolved && !sameLocation) return null;

            float confidence = directlyInvolved ? 0.98f : 0.82f;
            return Build(worldEvent, observer.Id, confidence, Math.Max(0.25f, worldEvent.Importance));
        }

        private static Percept Build(WorldEvent e, string observerId, float confidence, float salience)
        {
            return new Percept
            {
                ObserverId = observerId,
                SourceEventSequence = e.Sequence,
                Type = e.Type,
                Description = e.Description,
                SubjectId = !string.IsNullOrWhiteSpace(e.TargetId) ? e.TargetId : e.SourceId,
                LocationId = e.LocationId,
                Confidence = Clamp01(confidence),
                Salience = Clamp01(salience),
                Threat = Clamp01(e.Threat)
            };
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
