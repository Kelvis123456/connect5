using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class LobbyUI : MonoBehaviour
{
    private UIDocument    _doc;
    private VisualElement _root;

    // ── Panels ────────────────────────────────────────────────────────────────
    private VisualElement _panelMain, _panelAI, _panelOnline, _panelCreate, _panelJoin;

    // ── Mode cards ────────────────────────────────────────────────────────────
    private VisualElement _cardLocal, _cardAI, _cardOnline;

    // ── AI ────────────────────────────────────────────────────────────────────
    private Button _btnEasy, _btnMedium, _btnHard, _btnStartAI;
    private Button[] _diffBtns;
    private AIDifficulty _difficulty = AIDifficulty.Medium;

    // ── Online: profile step ──────────────────────────────────────────────────
    private TextField     _playerNameInput;
    private Button        _btnCreateRoom, _btnShowJoin;

    // ── Online: waiting room ──────────────────────────────────────────────────
    private Label         _roomTitle;
    private VisualElement _codeRow;
    private Label         _joinCodeText;
    private Button        _btnCopyCode;
    private VisualElement _p1RoomAvatar, _p2RoomAvatar;
    private Label         _p1RoomInitial, _p2RoomInitial;
    private Label         _p1RoomName, _p2RoomName;

    private Label         _waitingText;
    private Button        _btnStartOnline;

    // ── Online: join code step ────────────────────────────────────────────────
    private Button    _btnJoin, _btnBack;
    private TextField _codeInput;
    private Label     _joinStatus, _errorLabel;

    // ── Mini-board demo ────────────────────────────────────────────────────────

    const int DemoCols = 7, DemoRows = 7;

    static readonly int[][] DemoGames =
    {
        new[] { 1,6, 2,6, 3,6, 4,6, 5 },
        new[] { 0,3, 1,3, 2,3, 4,3, 5,3 },
        new[] { 0,1, 1,2, 6,2, 2,3, 6,3, 6,3, 3,4, 5,4, 5,4, 6,4, 4 },
        new[] { 6,5, 5,4, 0,4, 4,3, 1,3, 1,3, 3,2, 0,2, 0,2, 1,2, 2 },
    };

    private VisualElement[] _miniCells  = new VisualElement[DemoCols * DemoRows];
    private int[]           _demoBoard  = new int[DemoCols * DemoRows];
    private Coroutine       _demoCoroutine;

    static int DemoIdx(int col, int row) => row * DemoCols + col;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake() => _doc = GetComponent<UIDocument>();

    private void OnEnable()
    {
        GameSettings.LaunchedFromMenu = false;
        _root = _doc.rootVisualElement;

        QueryAll();
        BuildMiniBoard();
        BuildPiecesPreview();
        BindAll();

        SubscribeRelay();
        SetDifficulty(AIDifficulty.Medium);
        ShowPanel("main");

        _demoCoroutine = StartCoroutine(DemoGameLoop());
    }

    private void OnDisable()
    {
        if (_demoCoroutine != null) { StopCoroutine(_demoCoroutine); _demoCoroutine = null; }
        UnsubscribeRelay();
    }

    // ── Query ─────────────────────────────────────────────────────────────────

    private void QueryAll()
    {
        _panelMain   = _root.Q("panel-main");
        _panelAI     = _root.Q("panel-ai");
        _panelOnline = _root.Q("panel-online");
        _panelCreate = _root.Q("panel-create");
        _panelJoin   = _root.Q("panel-join");

        _cardLocal  = _root.Q("btn-local");
        _cardAI     = _root.Q("btn-ai");
        _cardOnline = _root.Q("btn-online");

        _btnEasy    = _root.Q<Button>("btn-easy");
        _btnMedium  = _root.Q<Button>("btn-medium");
        _btnHard    = _root.Q<Button>("btn-hard");
        _btnStartAI = _root.Q<Button>("btn-start-ai");
        _diffBtns   = new[] { _btnEasy, _btnMedium, _btnHard };

        // Online: profile step
        _playerNameInput = _root.Q<TextField>("player-name-input");
        _btnCreateRoom   = _root.Q<Button>("btn-create-room");
        _btnShowJoin     = _root.Q<Button>("btn-show-join");

        // Online: waiting room
        _roomTitle      = _root.Q<Label>("room-title");
        _codeRow        = _root.Q("code-row");
        _joinCodeText   = _root.Q<Label>("join-code-text");
        _btnCopyCode    = _root.Q<Button>("btn-copy-code");
        _p1RoomAvatar   = _root.Q("p1-room-avatar");
        _p1RoomInitial  = _root.Q<Label>("p1-room-initial");
        _p1RoomName     = _root.Q<Label>("p1-room-name");
        _p2RoomAvatar   = _root.Q("p2-room-avatar");
        _p2RoomInitial  = _root.Q<Label>("p2-room-initial");
        _p2RoomName     = _root.Q<Label>("p2-room-name");
        _waitingText    = _root.Q<Label>("waiting-text");
        _btnStartOnline = _root.Q<Button>("btn-start-online");

        // Online: join code step
        _codeInput  = _root.Q<TextField>("code-input");
        _btnJoin    = _root.Q<Button>("btn-join");
        _joinStatus = _root.Q<Label>("join-status");
        _btnBack    = _root.Q<Button>("btn-back");
        _errorLabel = _root.Q<Label>("error-text");
    }

    // ── Bind ──────────────────────────────────────────────────────────────────

    private void BindAll()
    {
        _cardLocal?.RegisterCallback<ClickEvent>(_ => StartLocal());
        _cardAI?.RegisterCallback<ClickEvent>(_ => ShowPanel("ai"));
        _cardOnline?.RegisterCallback<ClickEvent>(_ => ShowPanel("online"));

        _btnEasy.clicked   += () => SetDifficulty(AIDifficulty.Easy);
        _btnMedium.clicked += () => SetDifficulty(AIDifficulty.Medium);
        _btnHard.clicked   += () => SetDifficulty(AIDifficulty.Hard);
        _btnStartAI.clicked += StartAI;

        _btnCreateRoom.clicked += OnCreateRoomClicked;
        _btnShowJoin.clicked   += OnShowJoinClicked;
        _btnJoin.clicked       += OnJoinRoomClicked;
        _btnBack.clicked       += OnBackClicked;

        _btnCopyCode?.RegisterCallback<ClickEvent>(_ => CopyCodeToClipboard());
        _btnStartOnline?.RegisterCallback<ClickEvent>(_ => OnStartOnlineClicked());
    }

    // ── Relay subscriptions ───────────────────────────────────────────────────

    private void SubscribeRelay()
    {
        if (RelayManager.Instance == null) return;
        RelayManager.Instance.OnRoomCreated    += HandleRoomCreated;
        RelayManager.Instance.OnJoiningRoom    += HandleJoiningRoom;
        RelayManager.Instance.OnOpponentJoined += HandleOpponentJoined;
        RelayManager.Instance.OnError          += HandleRelayError;
    }

    private void UnsubscribeRelay()
    {
        if (RelayManager.Instance == null) return;
        RelayManager.Instance.OnRoomCreated    -= HandleRoomCreated;
        RelayManager.Instance.OnJoiningRoom    -= HandleJoiningRoom;
        RelayManager.Instance.OnOpponentJoined -= HandleOpponentJoined;
        RelayManager.Instance.OnError          -= HandleRelayError;
    }

    // ── Mode logic ────────────────────────────────────────────────────────────

    private void StartLocal()
    {
        GameSettings.Mode             = GameMode.Local;
        GameSettings.LaunchedFromMenu = true;
        SceneManager.LoadScene("GameScene");
    }

    private void StartAI()
    {
        GameSettings.Mode             = GameMode.AI;
        GameSettings.Difficulty       = _difficulty;
        GameSettings.LaunchedFromMenu = true;
        SceneManager.LoadScene("GameScene");
    }

    private void SetDifficulty(AIDifficulty d)
    {
        _difficulty = d;
        var diffs = new[] { AIDifficulty.Easy, AIDifficulty.Medium, AIDifficulty.Hard };
        for (int i = 0; i < _diffBtns.Length; i++)
        {
            if (diffs[i] == d) _diffBtns[i].AddToClassList("diff-selected");
            else               _diffBtns[i].RemoveFromClassList("diff-selected");
        }
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private void OnBackClicked()
    {
        // From code-entry step go back to profile, not all the way to main
        bool inJoin = _panelJoin != null && !_panelJoin.ClassListContains("hidden");
        ShowPanel(inJoin ? "online" : "main");
    }

    // ── Online: profile step → create / join ─────────────────────────────────

    private async void OnCreateRoomClicked()
    {
        string name = (_playerNameInput?.value ?? "").Trim();
        HideError();
        ShowWaitingRoom(isHost: true, localName: name, code: "...");
        if (_waitingText != null) _waitingText.text = "Creando sala...";

        if (RelayManager.Instance != null)
            await RelayManager.Instance.CreateRoom(name);
        else
            HandleRelayError("RelayManager no encontrado.");
    }

    private void OnShowJoinClicked()
    {
        HideError();
        ShowPanel("join");
        if (_joinStatus != null) _joinStatus.text = "";
    }

    private async void OnJoinRoomClicked()
    {
        string code = (_codeInput?.value ?? "").Trim();
        string name = (_playerNameInput?.value ?? "").Trim();

        if (code.Length < 5)
        {
            if (_joinStatus != null) _joinStatus.text = "Código inválido (mín. 5 caracteres).";
            return;
        }
        if (_joinStatus != null) _joinStatus.text = "Conectando...";

        if (RelayManager.Instance != null)
            await RelayManager.Instance.JoinRoom(code, name);
        else
            HandleRelayError("RelayManager no encontrado.");
    }

    private void OnStartOnlineClicked()
    {
        RelayManager.Instance?.StartGame();
    }

    // ── Relay event handlers ──────────────────────────────────────────────────

    // Host: room created — show code
    private void HandleRoomCreated(string code)
    {
        string name = GameSettings.Player1Name;
        ShowWaitingRoom(isHost: true, localName: name, code: code);
        if (_waitingText != null) _waitingText.text = "Comparte el código con tu rival.";
    }

    // Client: StartClient called — show waiting room with own card
    private void HandleJoiningRoom()
    {
        string name = GameSettings.Player2Name;
        ShowWaitingRoom(isHost: false, localName: name, code: null);
        if (_waitingText != null) _waitingText.text = "Conectando...";
    }

    // Both: opponent's profile received — fill in the second card
    private void HandleOpponentJoined(string opponentName)
    {
        bool weAreHost = RelayManager.Instance != null && RelayManager.Instance.IsHost;

        if (weAreHost)
        {
            // P2 (the client) just connected
            SetCard(p1: false, name: opponentName, isEmpty: false);
            if (_roomTitle    != null) _roomTitle.text    = "¡Rival encontrado!";
            if (_waitingText  != null) _waitingText.text  = "";
            SetVisible(_btnStartOnline, true);
        }
        else
        {
            // P1 (the host) profile received by client
            SetCard(p1: true, name: opponentName, isEmpty: false);
            if (_roomTitle   != null) _roomTitle.text   = "¡Conectado!";
            if (_waitingText != null) _waitingText.text = "Esperando que el anfitrión inicie la partida...";
        }
    }

    // ── Waiting room builder ──────────────────────────────────────────────────

    private void ShowWaitingRoom(bool isHost, string localName, string code)
    {
        ShowPanel("create");

        // Code row: visible only for host
        SetVisible(_codeRow, isHost);
        if (isHost && code != null && _joinCodeText != null)
            _joinCodeText.text = code;

        if (isHost)
        {
            // Local player is P1 (host)
            SetCard(p1: true,  name: localName, isEmpty: false);
            SetCard(p1: false, name: "Esperando...", isEmpty: true);
            SetVisible(_btnStartOnline, false);
        }
        else
        {
            // Local player is P2 (client)
            SetCard(p1: true,  name: "...", isEmpty: true);
            SetCard(p1: false, name: localName, isEmpty: false);
            SetVisible(_btnStartOnline, false);
        }
    }

    // Update a player card's avatar and name
    private void SetCard(bool p1, string name, bool isEmpty)
    {
        var avatar  = p1 ? _p1RoomAvatar  : _p2RoomAvatar;
        var initial = p1 ? _p1RoomInitial : _p2RoomInitial;
        var nameL   = p1 ? _p1RoomName    : _p2RoomName;

        if (avatar != null)
        {
            if (isEmpty) avatar.AddToClassList("room-card-avatar--empty");
            else         avatar.RemoveFromClassList("room-card-avatar--empty");
        }

        if (initial != null)
        {
            bool dim = isEmpty;
            if (dim) initial.AddToClassList("avatar-initial--dim");
            else     initial.RemoveFromClassList("avatar-initial--dim");
            initial.text = (!string.IsNullOrEmpty(name) && !isEmpty)
                ? name[0].ToString().ToUpper() : "?";
        }

        if (nameL != null)
        {
            if (isEmpty) nameL.AddToClassList("room-card-name--dim");
            else         nameL.RemoveFromClassList("room-card-name--dim");
            nameL.text = name ?? "";
        }
    }

    // ── Panel navigation ──────────────────────────────────────────────────────

    private void ShowPanel(string which)
    {
        SetVisible(_panelMain,   which == "main");
        SetVisible(_panelAI,     which == "ai");
        SetVisible(_panelOnline, which == "online");
        SetVisible(_panelCreate, which == "create");
        SetVisible(_panelJoin,   which == "join");
        SetVisible(_btnBack,     which != "main" && which != "create");
        HideError();
    }

    // ── Copy code ─────────────────────────────────────────────────────────────

    private void CopyCodeToClipboard()
    {
        string code = RelayManager.Instance?.JoinCode ?? "";
        if (string.IsNullOrEmpty(code)) return;
        GUIUtility.systemCopyBuffer = code;
        StartCoroutine(FlashCopyButton());
    }

    private IEnumerator FlashCopyButton()
    {
        if (_btnCopyCode == null) yield break;
        _btnCopyCode.text = "¡Copiado!";
        yield return new WaitForSeconds(1.5f);
        _btnCopyCode.text = "Copiar";
    }

    // ── Error ─────────────────────────────────────────────────────────────────

    private void HandleRelayError(string msg)
    {
        if (_errorLabel != null) _errorLabel.text = msg;
        SetVisible(_errorLabel, true);
        // Return to profile panel so user can retry
        ShowPanel("online");
    }

    private void HideError()
    {
        if (_errorLabel != null) _errorLabel.text = "";
        SetVisible(_errorLabel, false);
    }

    // ── Mini-board construction ───────────────────────────────────────────────

    private void BuildMiniBoard()
    {
        var board = _root.Q("mini-board");
        if (board == null) return;
        board.Clear();

        for (int i = 0; i < DemoCols * DemoRows; i++)
        {
            var cell = new VisualElement();
            cell.AddToClassList("mini-cell");
            cell.pickingMode = PickingMode.Ignore;
            _miniCells[i] = cell;
            board.Add(cell);
        }
    }

    // ── Demo animation loop ───────────────────────────────────────────────────

    private IEnumerator DemoGameLoop()
    {
        yield return new WaitForSeconds(0.2f);
        int gameIndex = 0;
        while (true)
        {
            yield return StartCoroutine(PlayDemoGame(DemoGames[gameIndex % DemoGames.Length]));
            gameIndex++;
            yield return new WaitForSeconds(2.2f);
            yield return StartCoroutine(FadeDemoBoard());
            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator PlayDemoGame(int[] moves)
    {
        Array.Clear(_demoBoard, 0, _demoBoard.Length);
        ResetAllMiniCells();

        for (int m = 0; m < moves.Length; m++)
        {
            int col    = moves[m];
            int player = (m % 2 == 0) ? 1 : 2;
            int row    = GetDemoDropRow(col);
            if (row < 0) continue;

            yield return StartCoroutine(AnimateDrop(col, row, player));

            _demoBoard[DemoIdx(col, row)] = player;
            SetMiniCell(_miniCells[DemoIdx(col, row)], player);

            yield return new WaitForSeconds(m < 3 ? 0.65f : 0.42f);

            List<int> winLine = CheckDemoWin(col, row, player);
            if (winLine != null)
            {
                yield return StartCoroutine(FlashWin(winLine, player));
                yield break;
            }
        }
    }

    private IEnumerator AnimateDrop(int col, int targetRow, int player)
    {
        for (int r = 0; r < targetRow; r++)
        {
            if (_demoBoard[DemoIdx(col, r)] != 0) break;
            var cell = _miniCells[DemoIdx(col, r)];
            SetMiniCell(cell, player);
            yield return new WaitForSeconds(0.06f);
            SetMiniCell(cell, _demoBoard[DemoIdx(col, r)]);
        }
    }

    private IEnumerator FlashWin(List<int> winLine, int player)
    {
        for (int flash = 0; flash < 3; flash++)
        {
            foreach (int wi in winLine) SetMiniCell(_miniCells[wi], 3);
            yield return new WaitForSeconds(0.28f);
            foreach (int wi in winLine) SetMiniCell(_miniCells[wi], player);
            yield return new WaitForSeconds(0.18f);
        }
        foreach (int wi in winLine) SetMiniCell(_miniCells[wi], 3);
    }

    private IEnumerator FadeDemoBoard()
    {
        for (int row = DemoRows - 1; row >= 0; row--)
        {
            for (int col = 0; col < DemoCols; col++)
            {
                int idx = DemoIdx(col, row);
                if (_demoBoard[idx] != 0 || _miniCells[idx].ClassListContains("mini-win"))
                    SetMiniCell(_miniCells[idx], 0);
            }
            yield return new WaitForSeconds(0.05f);
        }
        Array.Clear(_demoBoard, 0, _demoBoard.Length);
    }

    private void ResetAllMiniCells()
    {
        Array.Clear(_demoBoard, 0, _demoBoard.Length);
        for (int i = 0; i < _miniCells.Length; i++) SetMiniCell(_miniCells[i], 0);
    }

    // ── Demo helpers ──────────────────────────────────────────────────────────

    private void SetMiniCell(VisualElement cell, int state)
    {
        if (cell == null) return;
        cell.RemoveFromClassList("mini-p1");
        cell.RemoveFromClassList("mini-p2");
        cell.RemoveFromClassList("mini-win");
        switch (state)
        {
            case 1: cell.AddToClassList("mini-p1");  break;
            case 2: cell.AddToClassList("mini-p2");  break;
            case 3: cell.AddToClassList("mini-win"); break;
        }
    }

    private int GetDemoDropRow(int col)
    {
        for (int row = DemoRows - 1; row >= 0; row--)
            if (_demoBoard[DemoIdx(col, row)] == 0) return row;
        return -1;
    }

    private List<int> CheckDemoWin(int col, int row, int player)
    {
        int[] dc = { 1, 0,  1,  1 };
        int[] dr = { 0, 1,  1, -1 };

        for (int d = 0; d < 4; d++)
        {
            var line = new List<int> { DemoIdx(col, row) };
            for (int s = 1; s <= 4; s++)
            {
                int c = col + dc[d] * s, r = row + dr[d] * s;
                if (c < 0 || c >= DemoCols || r < 0 || r >= DemoRows) break;
                if (_demoBoard[DemoIdx(c, r)] != player) break;
                line.Add(DemoIdx(c, r));
            }
            for (int s = 1; s <= 4; s++)
            {
                int c = col - dc[d] * s, r = row - dr[d] * s;
                if (c < 0 || c >= DemoCols || r < 0 || r >= DemoRows) break;
                if (_demoBoard[DemoIdx(c, r)] != player) break;
                line.Add(DemoIdx(c, r));
            }
            if (line.Count >= 5) return line;
        }
        return null;
    }

    // ── Gradient dots preview ─────────────────────────────────────────────────

    private void BuildPiecesPreview()
    {
        var preview = _root.Q("pieces-preview");
        if (preview == null) return;
        preview.Clear();

        Color p1 = new Color(0.937f, 0.267f, 0.267f);
        Color p2 = new Color(1.000f, 0.831f, 0.000f);
        for (int i = 0; i < 5; i++)
        {
            var dot = new VisualElement();
            dot.AddToClassList("preview-dot");
            dot.style.backgroundColor = Color.Lerp(p1, p2, i / 4f);
            preview.Add(dot);
        }
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static void SetVisible(VisualElement el, bool visible)
    {
        if (el == null) return;
        if (visible) el.RemoveFromClassList("hidden");
        else         el.AddToClassList("hidden");
    }
}
