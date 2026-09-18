# Integración con Unity

`DynaraSimulationBehaviour` es el puente mínimo entre Unity y `DynaraSim.Core`.

La regla de arquitectura es deliberada:

```text
Unity / GameObjects / NavMesh / Physics
              |
              v
       WorldEvent / ActionResult
              |
              v
        DynaraSim.Core
              |
              v
          ActionIntent
              |
              v
        Unity executors
```

No conviertas a `DynaraDirector` en un `MonoBehaviour`. El director debe poder ejecutarse en tests, servidor o consola sin Unity.

## Primer montaje

1. Incluye `src/NpcInt.Core` y `src/DynaraSim.Core` en tu solución/assembly.
2. Añade `unity/DynaraSimulationBehaviour.cs` a Unity.
3. Coloca el componente en un GameObject persistente, por ejemplo `DynaraRuntime`.
4. Registra al jugador con `AddPlayer(...)`.
5. Convierte sucesos reales del juego en `PerceiveWorldEvent(...)`.
6. Más adelante conecta cada `ActionIntent` a ejecutores concretos de NavMesh, animación, spawn, escenas y físicas.

## Importante

La versión actual ejecuta varias acciones directamente contra el `WorldState` simbólico para probar el ciclo. Para el juego final, las acciones físicas deberán quedar pendientes hasta que Unity confirme el resultado:

```text
Decision
  -> ActionIntent
  -> Unity executor
  -> ActionResult(success/failure)
  -> WorldState + memoria
```

Eso evita que "decidir comer", "decidir abrir" o "decidir teletransportar" altere el estado antes de que el motor confirme que la acción ocurrió.
