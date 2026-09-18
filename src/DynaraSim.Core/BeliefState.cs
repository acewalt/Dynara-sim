using System;
using System.Collections.Generic;
using System.Linq;

namespace DynaraSim.Core
{
    public sealed class BeliefRecord
    {
        public string Key;
        public string Value;
        public string Source;
        public float Confidence;
        public double LearnedAtMinutes;
        public bool IsHypothesis;
        public readonly List<string> Evidence = new List<string>();
    }

    public sealed class BeliefState
    {
        private readonly Dictionary<string, BeliefRecord> _beliefs =
            new Dictionary<string, BeliefRecord>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<BeliefRecord> Items
        {
            get { return _beliefs.Values.OrderByDescending(x => x.Confidence); }
        }

        public void Upsert(string key, string value, string source, float confidence, double timeMinutes, bool hypothesis = false)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            BeliefRecord item;
            if (!_beliefs.TryGetValue(key, out item))
            {
                item = new BeliefRecord { Key = key };
                _beliefs[key] = item;
            }

            item.Value = value ?? string.Empty;
            item.Source = source ?? "unknown";
            item.Confidence = Clamp01(confidence);
            item.LearnedAtMinutes = timeMinutes;
            item.IsHypothesis = hypothesis;
        }

        public BeliefRecord Get(string key)
        {
            BeliefRecord item;
            return !string.IsNullOrWhiteSpace(key) && _beliefs.TryGetValue(key, out item) ? item : null;
        }

        public void ObserveWorld(WorldState world)
        {
            if (world == null) return;
            foreach (EntityState entity in world.Entities.Values)
            {
                Upsert("entity:" + entity.Id + ":location", entity.LocationId ?? string.Empty, "WorldState", 1f, world.TimeMinutes);
                Upsert("entity:" + entity.Id + ":active", entity.Active ? "true" : "false", "WorldState", 1f, world.TimeMinutes);
            }
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
