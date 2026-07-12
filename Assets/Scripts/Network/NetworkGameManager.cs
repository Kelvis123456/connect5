using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Server-authoritative game manager for online mode.
// Lives in GameScene as a NetworkObject; spawned automatically by NGO.
public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    // 0 = Player1 (host), 1 = Player2 (client)
    private NetworkVariable<int> _currentPlayerIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Authoritative board on the server only.
    private Board _board;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        Instance = this;
        _board   = new Board();

        NetworkBridge.IsOnlineActive = true;
        NetworkBridge.IsHostPlayer   = IsHost;

        _currentPlayerIndex.OnValueChanged += HandleTurnChanged;

        // Delay one frame: OnNetworkSpawn fires during sceneLoaded (before Start()),
        // so a synchronous raise would be missed by GameUI which subscribes in Start().
        StartCoroutine(RaiseInitialTurnNextFrame());
    }

    private IEnumerator RaiseInitialTurnNextFrame()
    {
        yield return null;
        NetworkBridge.RaiseTurnChanged(CellState.Player1);
    }

    public override void OnNetworkDespawn()
    {
        _currentPlayerIndex.OnValueChanged -= HandleTurnChanged;
        if (Instance == this) Instance = null;
        NetworkBridge.Reset();
    }

    private void HandleTurnChanged(int _, int curr)
        => NetworkBridge.RaiseTurnChanged(curr == 0 ? CellState.Player1 : CellState.Player2);

    // ── Public API ───────────────────────────────────────────────────────────

    // Called by GameUI when the local player clicks a column.
    public void RequestMove(int col)
    {
        PlacePieceServerRpc(col);
    }

    // ── Server-side logic ────────────────────────────────────────────────────

    [ServerRpc(RequireOwnership = false)]
    private void PlacePieceServerRpc(int col, ServerRpcParams rpcParams = default)
    {
        // Validate it is the sender's turn.
        // Host (ServerClientId == 0) is always Player1; the remote client is Player2.
        bool senderIsHost  = rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId;
        bool isPlayer1Turn = _currentPlayerIndex.Value == 0;
        if (isPlayer1Turn != senderIsHost) return;

        if (_board.IsColumnFull(col)) return;

        var player = isPlayer1Turn ? CellState.Player1 : CellState.Player2;
        if (!_board.PlacePiece(col, player, out int row)) return;

        // Notify all clients of the placed piece.
        PiecePlacedClientRpc(col, row, (int)player);

        if (_board.CheckWin(col, row, player, out var winCells))
        {
            GameOverClientRpc((int)player, SerializeCells(winCells));
            return;
        }

        if (_board.IsFull())
        {
            GameOverClientRpc(0, new int[0]); // 0 = draw sentinel
            return;
        }

        _currentPlayerIndex.Value = 1 - _currentPlayerIndex.Value;
    }

    // ── ClientRpcs ───────────────────────────────────────────────────────────

    [ClientRpc]
    private void PiecePlacedClientRpc(int col, int row, int playerIndex)
        => NetworkBridge.RaisePiecePlaced(col, row, (CellState)playerIndex);

    [ClientRpc]
    private void GameOverClientRpc(int winnerIndex, int[] winCellsFlat)
    {
        if (winnerIndex == 0 && winCellsFlat.Length == 0)
        {
            NetworkBridge.RaiseGameDraw();
        }
        else
        {
            var cells = DeserializeCells(winCellsFlat);
            NetworkBridge.RaiseGameWon((CellState)winnerIndex, cells);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int[] SerializeCells(List<Vector2Int> cells)
    {
        var arr = new int[cells.Count * 2];
        for (int i = 0; i < cells.Count; i++)
        {
            arr[i * 2]     = cells[i].x;
            arr[i * 2 + 1] = cells[i].y;
        }
        return arr;
    }

    private static List<Vector2Int> DeserializeCells(int[] flat)
    {
        var list = new List<Vector2Int>(flat.Length / 2);
        for (int i = 0; i < flat.Length; i += 2)
            list.Add(new Vector2Int(flat[i], flat[i + 1]));
        return list;
    }
}
