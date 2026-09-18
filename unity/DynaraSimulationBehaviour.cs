using System;
using DynaraSim.Core;
using UnityEngine;

public sealed class DynaraSimulationBehaviour : MonoBehaviour
{
    [Header("Simulation")]
    [SerializeField] private int seed = 1337;
    [SerializeField] private bool seedDemoWorld = true;
    [SerializeField] private bool autoTick = true;
    [SerializeField] private float secondsPerDirectorTick = 5f;
    [SerializeField] private float simulatedMinutesPerTick = 1f;

    [Header("Debug")]
    [SerializeField] private bool logDirectorCycles = true;

    private SimulationKernel _kernel;
    private float _timer;

    public SimulationKernel Kernel { get { return _kernel; } }
    public WorldState World { get { return _kernel == null ? null : _kernel.World; } }
    public DynaraDirector Dynara { get { return _kernel == null ? null : _kernel.Dynara; } }

    public event Action<DirectorCycleResult> DirectorCycleCompleted;

    private void Awake()
    {
        _kernel = new SimulationKernel(seed);
        if (seedDemoWorld) _kernel.SeedDemoWorld();
        Debug.Log("Dynara initialized. Purpose: " + _kernel.Dynara.Profile.Purpose);
    }

    private void Update()
    {
        if (!autoTick || _kernel == null) return;
        _timer += Time.deltaTime;
        if (_timer < secondsPerDirectorTick) return;
        _timer = 0f;
        Dispatch(_kernel.Tick(simulatedMinutesPerTick));
    }

    public string AddPlayer(string displayName)
    {
        if (_kernel == null) return null;

        string location = _kernel.World.FindEntity("lobby") != null ? "lobby" : null;
        EntityState player = _kernel.AddPlayer(
            string.IsNullOrWhiteSpace(displayName) ? "Jugador" : displayName,
            location);

        if (logDirectorCycles)
            Debug.Log("Dynara player registered: " + player.Id + " / " + player.Name);

        return player.Id;
    }

    public void SendPlayerText(string playerId, string text)
    {
        if (_kernel == null || string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(text)) return;
        Dispatch(_kernel.ProcessPlayerInput(playerId, text));
    }

    public void PerceiveWorldEvent(string description)
    {
        PerceiveWorldEvent(description, null, 0.6f, 0f);
    }

    public void PerceiveWorldEvent(string description, string locationId, float importance, float threat)
    {
        if (_kernel == null || string.IsNullOrWhiteSpace(description)) return;
        _kernel.EmitExternalEvent(description, locationId, importance, threat, "unity");
        Dispatch(_kernel.RunDirectorCycle());
    }

    public void TickNow(float simulatedMinutes)
    {
        if (_kernel == null) return;
        Dispatch(_kernel.Tick(Mathf.Max(0f, simulatedMinutes)));
    }

    private void Dispatch(DirectorCycleResult cycle)
    {
        if (cycle == null) return;

        if (logDirectorCycles && cycle.Plan != null)
        {
            string goal = cycle.Plan.Goal == null ? "none" : cycle.Plan.Goal.Id + " / " + cycle.Plan.Goal.Label;
            Debug.Log("DYNARA goal: " + goal);

            for (int i = 0; i < cycle.Plan.ReasoningTrace.Count; i++)
                Debug.Log("  trace: " + cycle.Plan.ReasoningTrace[i]);

            for (int i = 0; i < cycle.Results.Count; i++)
            {
                ActionResult result = cycle.Results[i];
                Debug.Log("  action: " + result.Code + " success=" + result.Success + " · " + result.Message);
            }
        }

        Action<DirectorCycleResult> handler = DirectorCycleCompleted;
        if (handler != null) handler(cycle);
    }
}
