using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Collections;

/// <summary>
/// Manages online rooms: Relay for transport, NGO for game networking.
/// Profile (name) is exchanged via CustomMessagingManager right after NGO connects.
/// </summary>
public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    public string JoinCode      { get; private set; } = "";
    public bool   IsInitialized { get; private set; } = false;
    public bool   IsHost        { get; private set; } = false;

    // Host:   fired after StartHost, arg = relay join code
    public event Action<string>  OnRoomCreated;
    // Client: fired right after StartClient (before NGO handshake completes)
    public event Action          OnJoiningRoom;
    // Both:   fired when the opponent's profile arrives — arg = opponent name
    public event Action<string>  OnOpponentJoined;
    // Both:   error string
    public event Action<string>  OnError;

    private const string MSG_PROFILE = "rm_profile";

    // ── Singleton ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy() => UnsubscribeNGO();

    // ── Init ──────────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;
        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            IsInitialized = true;
        }
        catch (Exception e) { OnError?.Invoke($"Error de servicios: {e.Message}"); }
    }

    // ── Create room (host) ─────────────────────────────────────────────────────

    public async Task CreateRoom(string myName)
    {
        await InitializeAsync();
        if (!IsInitialized) return;
        try
        {
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(1);
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            var data = new RelayServerData(alloc, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(data);

            GameSettings.Mode             = GameMode.Online;
            GameSettings.LaunchedFromMenu = true;
            GameSettings.Player1Name      = string.IsNullOrWhiteSpace(myName) ? "Jugador 1" : myName;
            GameSettings.RoomCode         = JoinCode;

            IsHost = true;
            SubscribeNGO();
            NetworkManager.Singleton.StartHost();
            NetworkManager.Singleton.CustomMessagingManager
                .RegisterNamedMessageHandler(MSG_PROFILE, OnReceiveProfile);

            OnRoomCreated?.Invoke(JoinCode);
        }
        catch (Exception e) { OnError?.Invoke($"Error al crear sala: {e.Message}"); }
    }

    // ── Join room (client) ────────────────────────────────────────────────────

    public async Task JoinRoom(string code, string myName)
    {
        await InitializeAsync();
        if (!IsInitialized) return;
        try
        {
            JoinAllocation alloc = await RelayService.Instance
                .JoinAllocationAsync(code.Trim().ToUpper());

            var data = new RelayServerData(alloc, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(data);

            GameSettings.Mode             = GameMode.Online;
            GameSettings.LaunchedFromMenu = true;
            GameSettings.Player2Name      = string.IsNullOrWhiteSpace(myName) ? "Jugador 2" : myName;

            IsHost = false;
            SubscribeNGO();
            NetworkManager.Singleton.StartClient();
            NetworkManager.Singleton.CustomMessagingManager
                .RegisterNamedMessageHandler(MSG_PROFILE, OnReceiveProfile);

            OnJoiningRoom?.Invoke();
        }
        catch (Exception e) { OnError?.Invoke($"Error al unirse: {e.Message}"); }
    }

    // ── Start game (host only) ─────────────────────────────────────────────────

    public void StartGame()
    {
        if (!NetworkManager.Singleton.IsHost) return;
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }

    // ── Disconnect ────────────────────────────────────────────────────────────

    public void Disconnect()
    {
        UnsubscribeNGO();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        JoinCode                      = "";
        IsHost                        = false;
        GameSettings.RoomCode         = "";
        GameSettings.LaunchedFromMenu = false;
        NetworkBridge.Reset();
    }

    // ── NGO callbacks ─────────────────────────────────────────────────────────

    private void SubscribeNGO()
    {
        var nm = NetworkManager.Singleton;
        nm.OnClientConnectedCallback  += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void UnsubscribeNGO()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback  -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm.IsHost)
        {
            // A remote client joined — send them our (P1) profile
            if (clientId != nm.LocalClientId)
                SendProfile(clientId, GameSettings.Player1Name);
        }
        else
        {
            // We (client) just got a connection — send our (P2) profile to host
            if (clientId == nm.LocalClientId)
                SendProfile(NetworkManager.ServerClientId, GameSettings.Player2Name);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Optionally surface a "disconnected" event for the UI
    }

    // ── Profile messaging ──────────────────────────────────────────────────────

    private void SendProfile(ulong target, string name)
    {
        using var writer = new FastBufferWriter(128, Allocator.Temp);
        FixedString64Bytes fix = name ?? "";
        writer.WriteValueSafe(fix);
        NetworkManager.Singleton.CustomMessagingManager
            .SendNamedMessage(MSG_PROFILE, target, writer);
    }

    private void OnReceiveProfile(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out FixedString64Bytes fix);
        string name = fix.ToString();

        if (NetworkManager.Singleton.IsHost)
            GameSettings.Player2Name = name;   // received P2 name
        else
            GameSettings.Player1Name = name;   // received P1 name

        OnOpponentJoined?.Invoke(name);
    }
}
