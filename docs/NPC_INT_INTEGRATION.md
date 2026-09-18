# Relación con npc-int / NIA

Este repositorio no pretende reemplazar `npc-int`.

- **npc-int**: motor cognitivo de un agente individual.
- **Dynara-sim**: simulación compartida + director global + privilegios administrativos.

La copia actual de `src/NpcInt.Core` es un snapshot de arranque para evitar bloquear la primera vertical. No debería convertirse en una bifurcación permanente.

Objetivo de dependencia:

```text
             NpcInt.Core
              /      \
             /        \
          NIA       Dynara-sim
                     |
              +------+------+
              |             |
           Dynara          NPCs
```

Cuando `NpcInt.Core` tenga una API suficientemente estable, la migración recomendada es publicarlo como paquete UPM/NuGet o incluirlo mediante una dependencia versionada. Dynara debe depender del motor cognitivo; el motor cognitivo no debe depender de Dynara.
