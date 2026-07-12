# Connect5

A Connect-Four-style board game with a five-in-a-row win condition, built in Unity. Supports a local AI opponent and real online multiplayer.

## Features

- **Classic drop-a-piece gameplay** — click a column to drop your piece, win detection tracks the connected cells for the win highlight (`Board`, `GameManager`)
- **AI opponent** for local single-player matches (`AIPlayer`)
- **Online multiplayer** using Unity Netcode for GameObjects + Unity Relay for peer connection without port forwarding (`NetworkGameManager`, `NetworkBridge`, `RelayManager`)
- **Lobby flow** for hosting/joining online matches (`LobbyUI`)
- Win/draw detection and victory screen (`VictoryUI`)

## Stack

- Unity 6000.4.10f1 (Unity 6)
- Unity Netcode for GameObjects + Unity Relay (online multiplayer)
- C#

## Project structure

```
Assets/Scripts/
├─ Core/       — board state, game settings, shared enums
├─ Gameplay/   — game manager, AI opponent
├─ Network/    — netcode bridge, network game manager, relay manager
└─ UI/         — column input, in-game UI, lobby UI, victory UI
```

## Running it

Open the project in Unity 6000.4.10f1+ via Unity Hub, open `LobbyScene`, and press Play. Online mode requires a Unity Gaming Services project linked for Relay.

Build artifacts and Unity's generated caches (`Library/`, `Temp/`, `Builds/`) are not tracked — build the project locally via Unity's Build Settings.
