# Connect5

A Connect-Four-style board game, except the board is 10×10 and you need five in a row instead of four. Built in Unity 6. You can play it against a friend on the same machine, against a local AI, or against someone else online.

## Why it exists

I wanted a small, complete multiplayer project I could finish end to end — rules, AI, and real networking — without the scope exploding. Connect-5 on a bigger grid was simple enough to implement correctly in a weekend but still gave me a reason to build a server-authoritative netcode layer instead of a toy one.

## Modes

- **Local** — two players, same screen, click a column to drop a piece.
- **Vs AI** — three difficulties (`AIPlayer.cs`). Easy just picks a random open column. Medium checks for an immediate win, then an immediate block, then falls back to random. Hard does the same win/block check first, then scores every candidate move by sliding a 5-cell window across every row, column and diagonal and weighting how many of your pieces (or the opponent's) are already in each window — it's not a real minimax, just a one-ply heuristic, but it's enough to make Hard actually hard to beat.
- **Online** — real multiplayer over the internet using Unity Netcode for GameObjects, with Unity Relay handling the connection so neither player has to open a port or run a dedicated server.

## How the online part actually works

`NetworkGameManager` is server-authoritative: the real board only exists on the host, and a move is just a request (`PlacePieceServerRpc`) that the server validates (is it actually your turn, is the column full) before it applies anything and broadcasts the result back to both clients. The host is always Player 1 (`ServerClientId == 0`), the joining client is always Player 2. I did it this way so a modified or lagging client can't just tell the game it won — the server is the only thing that decides that.

One real bug I hit while building this: `OnNetworkSpawn` fires before `GameUI.Start()` runs, so if the server raised the "it's Player 1's turn" event immediately on spawn, the UI hadn't subscribed yet and silently missed it — the board would just sit there with no visual indication of whose turn it was. Fixed by delaying that first broadcast by one frame with a coroutine (`RaiseInitialTurnNextFrame`).

## Project structure

```
Assets/Scripts/
├─ Core/       — board state (10×10 grid, win detection), game settings, shared enums
├─ Gameplay/   — local/AI game manager, AI opponent logic
├─ Network/    — NetworkGameManager (server-authoritative), RelayManager (room create/join), NetworkBridge (decouples netcode from UI)
└─ UI/         — column input, in-game board UI, lobby, victory screen
```

`NetworkBridge` exists purely so the UI layer never has to know whether it's talking to the local `GameManager` or the networked `NetworkGameManager` — both raise the same events through it.

## Running it

Open the project in Unity 6000.4.10f1+ via Unity Hub, open `LobbyScene`, press Play. Local and AI modes work immediately. Online mode needs a Unity Gaming Services project linked (Project Settings → Services) since it uses Relay + Authentication.

## Stack

- Unity 6000.4.10f1 (Unity 6)
- Unity Netcode for GameObjects + Unity Relay
- C#

Build artifacts and Unity's generated caches (`Library/`, `Temp/`, `Builds/`) aren't tracked — build locally via Unity's Build Settings.

## Status

The three modes all work end to end (local, AI at all three difficulties, online host/join). What's still rough: there's no reconnect handling if a client drops mid-match, and the lobby is functional but not pretty — it does the job of getting two players into a game, nothing more.
