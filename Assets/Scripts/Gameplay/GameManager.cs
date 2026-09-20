using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ── Events ──────────────────────────────────────────────────────────────
    public event Action<int, int, CellState>              OnPiecePlaced;  // col, row, player
    public event Action<CellState>                         OnTurnChanged;
    public event Action<CellState, List<Vector2Int>>       OnGameWon;     // winner, winCells
    public event Action                                    OnGameDraw;

    // ── State ────────────────────────────────────────────────────────────────
    private Board      _board;
    private CellState  _currentPlayer;
    private GamePhase  _phase;
    private bool       _aiThinking;

    public CellState  CurrentPlayer => _currentPlayer;
    public GamePhase  Phase         => _phase;
    public Board      Board         => _board;

    // ────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        Instance = this;

        if (!GameSettings.LaunchedFromMenu)
        {
            SceneManager.LoadScene("LobbyScene");
            return;
        }

        // Online mode is fully handled by NetworkGameManager.
        if (GameSettings.Mode == GameMode.Online)
        {
            gameObject.SetActive(false);
            return;
        }

        StartNewGame();
    }

    // ── Public API ──────────────────────────────────────────────────────────

    public bool TryPlacePiece(int col)
    {
        if (_phase != GamePhase.Playing)   return false;
        if (_board.IsColumnFull(col))      return false;
        if (_aiThinking)                   return false;
        if (GameSettings.Mode == GameMode.AI && _currentPlayer != CellState.Player1) return false;

        ExecuteMove(col);
        return true;
    }

    // ── Internal ────────────────────────────────────────────────────────────

    private void StartNewGame()
    {
        _board         = new Board();
        _currentPlayer = CellState.Player1;
        _phase         = GamePhase.Playing;
        _aiThinking    = false;

        OnTurnChanged?.Invoke(_currentPlayer);
    }

    private void ExecuteMove(int col)
    {
        if (!_board.PlacePiece(col, _currentPlayer, out int row)) return;

        OnPiecePlaced?.Invoke(col, row, _currentPlayer);

        if (_board.CheckWin(col, row, _currentPlayer, out var winCells))
        {
            _phase = _currentPlayer == CellState.Player1 ? GamePhase.Player1Won : GamePhase.Player2Won;
            OnGameWon?.Invoke(_currentPlayer, winCells);
            return;
        }

        if (_board.IsFull())
        {
            _phase = GamePhase.Draw;
            OnGameDraw?.Invoke();
            return;
        }

        SwitchTurn();
    }

    private void SwitchTurn()
    {
        _currentPlayer = _currentPlayer == CellState.Player1 ? CellState.Player2 : CellState.Player1;
        OnTurnChanged?.Invoke(_currentPlayer);

        if (GameSettings.Mode == GameMode.AI && _currentPlayer == CellState.Player2)
        {
            _aiThinking = true;
            StartCoroutine(RunAITurn());
        }
    }

    private IEnumerator RunAITurn()
    {
        yield return new WaitForSeconds(0.55f);

        int col = AIPlayer.GetMove(_board, CellState.Player2, GameSettings.Difficulty);
        _aiThinking = false;

        if (col >= 0) ExecuteMove(col);
    }
}
