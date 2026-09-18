using System;
using System.Collections.Generic;

namespace DynaraSim.Core
{
    public sealed class PlayerModel
    {
        public string PlayerId;
        public float Engagement = 0.55f;
        public float Frustration = 0.15f;
        public float Confusion = 0.20f;
        public float Curiosity = 0.50f;
        public float Stress = 0.10f;
        public float Confidence = 0.35f;
        public int InteractionCount;
        public readonly List<string> RecentInputs = new List<string>();
        public readonly HashSet<string> Interests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void ObserveInput(string text)
        {
            string value = (text ?? string.Empty).Trim();
            if (value.Length == 0) return;

            InteractionCount++;
            RecentInputs.Add(value);
            if (RecentInputs.Count > 20) RecentInputs.RemoveAt(0);

            string n = Fold(value);
            bool question = value.IndexOf('?') >= 0 || value.IndexOf('¿') >= 0;
            bool bored = ContainsAny(n, "aburrido", "aburrida", "aburre", "aburrimiento");
            bool frustrated = ContainsAny(n, "no puedo", "imposible", "molesto", "molesta", "frustrado", "frustrada");
            bool curious = question || ContainsAny(n, "quiero saber", "que pasa", "que hay", "explorar", "investigar");

            if (bored)
            {
                Engagement = Clamp01(Engagement - 0.18f);
                Frustration = Clamp01(Frustration + 0.08f);
            }
            else Engagement = Clamp01(Engagement + 0.025f);

            if (frustrated) Frustration = Clamp01(Frustration + 0.18f);
            else Frustration = Clamp01(Frustration - 0.015f);

            if (question) Confusion = Clamp01(Confusion + 0.04f);
            else Confusion = Clamp01(Confusion - 0.01f);

            if (curious) Curiosity = Clamp01(Curiosity + 0.12f);

            if (ContainsAny(n, "miedo", "peligro", "demasiado", "para", "basta"))
                Stress = Clamp01(Stress + 0.12f);
            else Stress = Clamp01(Stress - 0.01f);

            Confidence = Clamp01(0.25f + Math.Min(InteractionCount, 20) * 0.03f);
            CaptureInterest(n);
        }

        public void ApplyAdventureFeedback(float engagementDelta, float frustrationDelta, float stressDelta)
        {
            Engagement = Clamp01(Engagement + engagementDelta);
            Frustration = Clamp01(Frustration + frustrationDelta);
            Stress = Clamp01(Stress + stressDelta);
        }

        private void CaptureInterest(string normalized)
        {
            string[] candidates = { "misterio", "terror", "aventura", "fantasia", "ciencia", "espacio", "combate", "puzzle", "acertijo", "explorar" };
            for (int i = 0; i < candidates.Length; i++)
                if (normalized.Contains(candidates[i])) Interests.Add(candidates[i]);
        }

        private static bool ContainsAny(string source, params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
                if (source.Contains(values[i])) return true;
            return false;
        }

        private static string Fold(string value)
        {
            return (value ?? string.Empty).ToLowerInvariant()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ü", "u");
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
