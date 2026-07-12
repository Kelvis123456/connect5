using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class GameUI : MonoBehaviour
{
    // Cell size must match Game.uss (.cell width/height = 68px, border-radius = 34px)
    const float CellPx   = 68f;
    const float CellHalf = 34f;

    private static readonly Color P1 = new Color(0.937f, 0.267f, 0.267f);
    private static readonly Color P2 = new Color(1.000f, 0.831f, 0.000f);

    // ── UI refs ────────────────────────────────────────────────────────────────
    private UIDocument    _doc;
    private VisualElement _root;

    private VisualElement[] _cells = new VisualElement[100];
    private VisualElement   _boardGrid, _colOverlay, _pieceOverlay;

    private Label         _turnText, _p1Name, _p2Name, _p1Score, _p2Score;
    private VisualElement _victoryOverlay;
    private Label         _victTitle, _victSubtitle;
    private Button        _btnPlayAgain, _btnLobby;

    // ── State ──────────────────────────────────────────────────────────────────
    private Board     _displayBoard;
    private CellState _localTurn;
    private int       _ghostIdx = -1;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake() => _doc = GetComponent<UIDocument>();

    // Use Start() so UIDocument.OnEnable() has already loaded the UXML
    // and all Awake() calls (including GameManager.Awake → StartNewGame) are done.
    private void Start()
    {
        // Editor self-heal: if the scene was saved without UXML/PanelSettings assigned
        // (SerializedObject bug in Setup), load the assets directly.
#if UNITY_EDITOR
        if (_doc.panelSettings == null)
            _doc.panelSettings = UnityEditor.AssetDatabase
                .LoadAssetAtPath<PanelSettings>("Assets/UI/Connect5PanelSettings.asset");
        if (_doc.visualTreeAsset == null)
            _doc.visualTreeAsset = UnityEditor.AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Game.uxml");
#endif
        _root = _doc.rootVisualElement;

        BuildBoard();          // creates 100 cell elements inside board-grid
        BuildColumnOverlay();  // creates 10 col-btn strips + sets pieceOverlay to Ignore
        QueryHUD();
        SetupVictory();
        SetNames();
        RefreshScores();

        if (GameSettings.Mode == GameMode.Online)
        {
            NetworkBridge.OnPiecePlaced += OnPiecePlaced;
            NetworkBridge.OnTurnChanged += OnTurnChanged;
            NetworkBridge.OnGameWon     += OnGameWon;
            NetworkBridge.OnGameDraw    += OnGameDraw;
            SetColsEnabled(false);
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPiecePlaced += OnPiecePlaced;
            GameManager.Instance.OnTurnChanged += OnTurnChanged;
            GameManager.Instance.OnGameWon     += OnGameWon;
            GameManager.Instance.OnGameDraw    += OnGameDraw;

            // GameManager.Awake already fired OnTurnChanged — sync the initial state now.
            if (GameManager.Instance.Phase == GamePhase.Playing)
                OnTurnChanged(GameManager.Instance.CurrentPlayer);
        }
    }

    private void OnDestroy()
    {
        NetworkBridge.OnPiecePlaced -= OnPiecePlaced;
        NetworkBridge.OnTurnChanged -= OnTurnChanged;
        NetworkBridge.OnGameWon     -= OnGameWon;
        NetworkBridge.OnGameDraw    -= OnGameDraw;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPiecePlaced -= OnPiecePlaced;
            GameManager.Instance.OnTurnChanged -= OnTurnChanged;
            GameManager.Instance.OnGameWon     -= OnGameWon;
            GameManager.Instance.OnGameDraw    -= OnGameDraw;
        }
    }

    // ── HUD ────────────────────────────────────────────────────────────────────

    private void QueryHUD()
    {
        _turnText = _root.Q<Label>("turn-text");
        _p1Name   = _root.Q<Label>("p1-name");
        _p2Name   = _root.Q<Label>("p2-name");
        _p1Score  = _root.Q<Label>("p1-score");
        _p2Score  = _root.Q<Label>("p2-score");
    }

    // ── Board ──────────────────────────────────────────────────────────────────

    private void BuildBoard()
    {
        _displayBoard = new Board();
        _boardGrid    = _root.Q("board-grid");
        _pieceOverlay = _root.Q("piece-overlay");

        // piece-overlay must never intercept pointer events — column-overlay is directly below
        if (_pieceOverlay != null)
            _pieceOverlay.pickingMode = PickingMode.Ignore;

        if (_boardGrid == null)
        {
            Debug.LogError("[GameUI] board-grid not found in UXML. Check UIDocument has Game.uxml assigned.");
            return;
        }
        _boardGrid.Clear();

        for (int i = 0; i < 100; i++)
        {
            var cell = new VisualElement();
            cell.AddToClassList("cell");
            cell.pickingMode = PickingMode.Ignore; // visual only, column-overlay handles input
            _cells[i] = cell;
            _boardGrid.Add(cell);
        }
    }

    private void BuildColumnOverlay()
    {
        _colOverlay = _root.Q("column-overlay");
        if (_colOverlay == null) return;
        _colOverlay.Clear();

        for (int c = 0; c < 10; c++)
        {
            int col = c;
            var btn = new VisualElement();
            btn.AddToClassList("col-btn");
            btn.RegisterCallback<ClickEvent>(_ => OnColumnClicked(col));
            btn.RegisterCallback<PointerEnterEvent>(_ => OnColumnHover(col));
            btn.RegisterCallback<PointerLeaveEvent>(_ => OnColumnLeave());
            _colOverlay.Add(btn);
        }
    }

    // ── Input ──────────────────────────────────────────────────────────────────

    private void OnColumnClicked(int col)
    {
        if (GameSettings.Mode == GameMode.Online)
            NetworkGameManager.Instance?.RequestMove(col);
        else
            GameManager.Instance?.TryPlacePiece(col);
    }

    private void OnColumnHover(int col)
    {
        ClearGhost();
        if (!IsMyTurn() || _displayBoard == null) return;
        if (_displayBoard.IsColumnFull(col)) return;
        int row = _displayBoard.GetDropRow(col);
        if (row < 0) return;
        int idx = row * Board.Size + col;
        _cells[idx].AddToClassList(_localTurn == CellState.Player1 ? "cell--ghost-p1" : "cell--ghost-p2");
        _ghostIdx = idx;
    }

    private void OnColumnLeave() => ClearGhost();

    // ── Events ─────────────────────────────────────────────────────────────────

    private void OnPiecePlaced(int col, int row, CellState player)
    {
        _displayBoard?.PlacePiece(col, player, out _);
        int idx = row * Board.Size + col;
        ClearGhostAt(idx);
        StartCoroutine(DropAndLand(col, row, idx, player == CellState.Player1 ? P1 : P2));
    }

    private void OnTurnChanged(CellState player)
    {
        _localTurn = player;
        string name = player == CellState.Player1
            ? GameSettings.Player1Name
            : (GameSettings.Mode == GameMode.AI ? "IA" : GameSettings.Player2Name);
        if (_turnText != null) _turnText.text = $"Turno: {name}";
        if (GameSettings.Mode == GameMode.Online) SetColsEnabled(IsMyTurn());
    }

    private void OnGameWon(CellState winner, List<Vector2Int> winCells)
    {
        SetColsEnabled(false);
        foreach (var v in winCells)
        {
            int idx = v.y * Board.Size + v.x;
            if (idx >= 0 && idx < _cells.Length)
            {
                ClearCellClasses(idx);
                _cells[idx].AddToClassList("cell--win");
            }
        }
        if (winner == CellState.Player1) GameSettings.Player1Score++;
        else                             GameSettings.Player2Score++;
        RefreshScores();

        string winnerName = winner == CellState.Player1
            ? GameSettings.Player1Name
            : (GameSettings.Mode == GameMode.AI ? "IA" : GameSettings.Player2Name);
        ShowVictory($"¡{winnerName} ganó!", false);
    }

    private void OnGameDraw()
    {
        SetColsEnabled(false);
        ShowVictory("Nadie ganó esta vez", true);
    }

    // ── Victory ────────────────────────────────────────────────────────────────

    private void SetupVictory()
    {
        _victoryOverlay = _root.Q("victory-overlay");
        _victTitle      = _root.Q<Label>("vict-title");
        _victSubtitle   = _root.Q<Label>("vict-subtitle");
        _btnPlayAgain   = _root.Q<Button>("btn-play-again");
        _btnLobby       = _root.Q<Button>("btn-lobby");

        if (_btnPlayAgain != null)
            _btnPlayAgain.clicked += () =>
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        if (_btnLobby != null)
            _btnLobby.clicked += () =>
            {
                if (GameSettings.Mode == GameMode.Online && RelayManager.Instance != null)
                    RelayManager.Instance.Disconnect();
                GameSettings.LaunchedFromMenu = false;
                GameSettings.ResetScores();
                SceneManager.LoadScene("LobbyScene");
            };
    }

    private void ShowVictory(string subtitle, bool isDraw)
    {
        if (_victTitle    != null) _victTitle.text    = isDraw ? "¡Empate!" : "¡Victoria!";
        if (_victSubtitle != null) _victSubtitle.text = subtitle;
        if (_btnPlayAgain != null)
            _btnPlayAgain.style.display =
                GameSettings.Mode != GameMode.Online ? DisplayStyle.Flex : DisplayStyle.None;
        SetVisible(_victoryOverlay, true);
    }

    // ── Drop animation ─────────────────────────────────────────────────────────

    private IEnumerator DropAndLand(int col, int row, int idx, Color color)
    {
        if (_pieceOverlay == null) { ApplyCellColor(idx, color); yield break; }

        // Wait one frame so UIToolkit has computed layout (worldBound is valid after first render)
        yield return null;

        var ovRect   = _pieceOverlay.worldBound;
        var landRect = _cells[idx].worldBound;
        var topRect  = _cells[col].worldBound; // row-0 cell of same column

        if (ovRect.width <= 0f) { ApplyCellColor(idx, color); yield break; }

        float landX  = landRect.xMin - ovRect.xMin;
        float landY  = landRect.yMin - ovRect.yMin;
        float startY = topRect.yMin  - ovRect.yMin - 90f; // 90px above top of board

        // ── Piece element ──────────────────────────────────────────────────────
        var piece = new VisualElement();
        piece.pickingMode                   = PickingMode.Ignore;
        piece.style.position                = Position.Absolute;
        piece.style.width                   = CellPx;
        piece.style.height                  = CellPx;
        piece.style.borderTopLeftRadius     = CellHalf;
        piece.style.borderTopRightRadius    = CellHalf;
        piece.style.borderBottomLeftRadius  = CellHalf;
        piece.style.borderBottomRightRadius = CellHalf;
        piece.style.backgroundColor         = color;
        piece.style.left                    = landX;
        piece.style.top                     = startY;
        _pieceOverlay.Add(piece);

        // ── Phase 1: free fall (physics: y = y0 + ½g·t²) ─────────────────────
        const float g  = 3800f;
        float dist     = landY - startY;
        float fallTime = Mathf.Sqrt(2f * dist / g);
        float t = 0f;

        while (t < fallTime)
        {
            t += Time.deltaTime;
            piece.style.top = startY + 0.5f * g * Mathf.Min(t, fallTime) * Mathf.Min(t, fallTime);
            yield return null;
        }
        piece.style.top = landY;

        // ── Phase 2: 3 decaying bounces ────────────────────────────────────────
        float[] heights = { dist * 0.18f, dist * 0.07f, dist * 0.025f };
        float[] times   = { 0.22f,        0.14f,        0.09f };
        for (int b = 0; b < heights.Length; b++)
        {
            float e = 0f;
            while (e < times[b])
            {
                e += Time.deltaTime;
                piece.style.top = landY - heights[b] * Mathf.Sin(Mathf.Clamp01(e / times[b]) * Mathf.PI);
                yield return null;
            }
            piece.style.top = landY;
        }

        // ── Settle ─────────────────────────────────────────────────────────────
        ApplyCellColor(idx, color);
        _pieceOverlay.Remove(piece);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void ApplyCellColor(int idx, Color color)
    {
        ClearCellClasses(idx);
        _cells[idx].AddToClassList(color == P1 ? "cell--p1" : "cell--p2");
    }

    private void ClearCellClasses(int idx)
    {
        _cells[idx].RemoveFromClassList("cell--p1");
        _cells[idx].RemoveFromClassList("cell--p2");
        _cells[idx].RemoveFromClassList("cell--ghost-p1");
        _cells[idx].RemoveFromClassList("cell--ghost-p2");
        _cells[idx].RemoveFromClassList("cell--win");
    }

    private void ClearGhost()
    {
        if (_ghostIdx >= 0) { ClearCellClasses(_ghostIdx); _ghostIdx = -1; }
    }

    private void ClearGhostAt(int idx) { if (_ghostIdx == idx) ClearGhost(); }

    private void SetColsEnabled(bool on)
    {
        if (_colOverlay == null) return;
        foreach (var child in _colOverlay.Children()) child.SetEnabled(on);
    }

    private bool IsMyTurn()
    {
        if (GameSettings.Mode != GameMode.Online) return true;
        return (_localTurn == CellState.Player1) == NetworkBridge.IsHostPlayer;
    }

    private void SetNames()
    {
        if (_p1Name != null) _p1Name.text = GameSettings.Player1Name;
        if (_p2Name != null) _p2Name.text = GameSettings.Mode == GameMode.AI ? "IA" : GameSettings.Player2Name;
    }

    private void RefreshScores()
    {
        if (_p1Score != null) _p1Score.text = GameSettings.Player1Score.ToString();
        if (_p2Score != null) _p2Score.text = GameSettings.Player2Score.ToString();
    }

    private static void SetVisible(VisualElement el, bool v)
    {
        if (el == null) return;
        if (v) el.RemoveFromClassList("hidden");
        else   el.AddToClassList("hidden");
    }
}
