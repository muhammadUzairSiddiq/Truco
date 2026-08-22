# TrucoCard

Unity 1v1 Truco card game project.

## Confirm this is the correct folder

If you can open this file in Unity / Explorer, Cursor is editing the right project:

```
c:\Users\User\Desktop\D drive Data\UNITY PROJECTS\Truco\TrucoCard
```

## Project info

| Item | Value |
|------|--------|
| Engine | Unity |
| Mode focus | 1v1 (not tournament / admin / premium) |
| Multiplayer | Photon |
| Backend API | Hostinger (`https://srv983121.hstgr.cloud/api`) |

## Key scripts (1v1 rules / scoring)

- `Assets/Scripts/GameManager.cs` — hand scoring, MAZO, Flor, Envido, match end
- `Assets/Scripts/UIMANAGER.cs` — challenge buttons / UI state
- `Assets/Scripts/TurnManager.cs` — turns / parda lead
- `Assets/Scripts/TrucoRulePoints.cs` — point tables (Truco / Envido / MAZO)
- `Assets/Scripts/OneVsOne/` — lobby, rooms, refunds, match lifecycle
- `Assets/__TrucoCard/Code/Core/TrucoCardAPI/ApiController.cs` — HTTP API

## Created by Cursor

This README was added so you can verify the agent is writing into **TrucoCard**, not a different Unity project.
