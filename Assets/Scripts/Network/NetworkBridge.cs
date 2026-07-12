using System;
using System.Collections.Generic;
using UnityEngine;

// Static decoupler so core/UI scripts never reference NGO types directly.
// NetworkGameManager writes to this bridge; UI reads from it.
public static class NetworkBridge
{
    public static bool IsHostPlayer  { get; set; } = false;
    public static bool IsOnlineActive { get; set; } = false;

    public static event Action<int, int, CellState>         OnPiecePlaced;
    public static event Action<CellState>                    OnTurnChanged;
    public static event Action<CellState, List<Vector2Int>>  OnGameWon;
    public static event Action                               OnGameDraw;

    public static void RaisePiecePlaced(int col, int row, CellState player)
        => OnPiecePlaced?.Invoke(col, row, player);

    public static void RaiseTurnChanged(CellState player)
        => OnTurnChanged?.Invoke(player);

    public static void RaiseGameWon(CellState winner, List<Vector2Int> winCells)
        => OnGameWon?.Invoke(winner, winCells);

    public static void RaiseGameDraw()
        => OnGameDraw?.Invoke();

    // Call on disconnect / scene unload to prevent stale subscribers.
    public static void Reset()
    {
        IsHostPlayer  = false;
        IsOnlineActive = false;
        OnPiecePlaced  = null;
        OnTurnChanged  = null;
        OnGameWon      = null;
        OnGameDraw     = null;
    }
}
