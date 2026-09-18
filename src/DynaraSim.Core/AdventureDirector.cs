using System;
using System.Collections.Generic;
using System.Linq;

namespace DynaraSim.Core
{
    public sealed class NpcBlueprint
    {
        public string Name;
        public string Role;
        public string Purpose;
        public readonly List<string> Traits = new List<string>();
        public readonly List<string> Capabilities = new List<string>();
    }

    public sealed class AdventureSpec
    {
        public string Id;
        public string Theme;
        public int Seed;
        public readonly List<string> Participants = new List<string>();
        public readonly List<string> Objectives = new List<string>();
        public readonly List<string> Beats = new List<string>();
        public readonly List<string> Rules = new List<string>();
        public readonly List<NpcBlueprint> Npcs = new List<NpcBlueprint>();
        public float Difficulty;
        public float TargetIntensity;
        public float EstimatedMinutes;
    }

    public sealed class AdventureDirector
    {
        private int _sequence;

        public AdventureSpec Generate(string theme, IEnumerable<string> participants, int seed, float targetIntensity = 0.55f)
        {
            string normalizedTheme = string.IsNullOrWhiteSpace(theme) ? "misterio" : theme.Trim();
            var rng = new Random(seed);
            var spec = new AdventureSpec
            {
                Id = "adv_" + (++_sequence).ToString("0000"),
                Theme = normalizedTheme,
                Seed = seed,
                Difficulty = Clamp01(0.35f + targetIntensity * 0.45f),
                TargetIntensity = Clamp01(targetIntensity),
                EstimatedMinutes = 8f + rng.Next(0, 8)
            };

            if (participants != null) spec.Participants.AddRange(participants.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());

            spec.Objectives.Add("descubrir qué está alterando el escenario");
            spec.Objectives.Add("resolver el conflicto principal sin romper las reglas del mundo");

            spec.Beats.Add("entrada: presentar una anomalía clara");
            spec.Beats.Add("exploración: ofrecer dos rutas con información distinta");
            spec.Beats.Add("complicación: introducir una consecuencia de las decisiones del grupo");
            spec.Beats.Add("decisión: forzar una elección con coste visible");
            spec.Beats.Add("resolución: cerrar el conflicto y registrar consecuencias");

            spec.Rules.Add("toda amenaza debe tener al menos una salida legible");
            spec.Rules.Add("los NPC no conocen información que no hayan percibido o recibido");
            spec.Rules.Add("el director puede adaptar dificultad, pero no reescribir una acción ya resuelta");

            var guide = new NpcBlueprint
            {
                Name = Pick(rng, "Mira", "Kiro", "Vexa", "Orin"),
                Role = "guía ambiguo",
                Purpose = "orientar al grupo sin resolver el problema por ellos"
            };
            guide.Traits.Add("curioso");
            guide.Traits.Add("evasivo");
            guide.Capabilities.Add("speak");
            guide.Capabilities.Add("move");
            spec.Npcs.Add(guide);

            return spec;
        }

        private static string Pick(Random rng, params string[] values)
        {
            return values[rng.Next(values.Length)];
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
