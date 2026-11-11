// M0 connection probe (docs/MULTIPLAYER.md): Device ID login, a lobby joined by a short code,
// and a P2P ping between two machines. Prints round-trip times and whether the link is direct or relayed.
//
//   EosProbe host [--relay allow|force]          create a room, print its code, echo pings
//   EosProbe join <CODE> [--relay allow|force]   join that room and measure pings to the host
//
// Credentials come from eos.local.json (next to the exe, or secrets/eos.local.json up the tree).
using System.Diagnostics;
using System.Text.Json;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.P2P;
using Epic.OnlineServices.Platform;

internal static class Program
{
    private const string LobbyPrefix = "petcoop-probe-";
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int PingCount = 40;
    private const byte PingTag = (byte)'P', PongTag = (byte)'Q', ByeTag = (byte)'B';

    private static PlatformInterface platform;
    private static ProductUserId localUser;
    private static readonly SocketId Socket = new() { SocketName = "petping" };

    private static int Main(string[] args)
    {
        if (args.Length == 0 || (args[0] != "host" && args[0] != "join") || (args[0] == "join" && args.Length < 2))
        {
            Console.WriteLine("Usage: EosProbe host [--relay allow|force]\n       EosProbe join <CODE> [--relay allow|force]");
            return 2;
        }
        var relay = RelayControl.AllowRelays;
        int relayArg = Array.IndexOf(args, "--relay");
        if (relayArg >= 0 && relayArg + 1 < args.Length && args[relayArg + 1] == "force")
            relay = RelayControl.ForceRelays;

        try
        {
            if (!Start(relay)) return 1;
            return args[0] == "host" ? Host() : Join(args[1].Trim().ToUpperInvariant());
        }
        finally
        {
            platform?.Release();
            PlatformInterface.Shutdown();
        }
    }

    private static bool Start(RelayControl relay)
    {
        var config = LoadConfig();
        if (config == null) return false;

        var init = new InitializeOptions { ProductName = "DesktopPetEosProbe", ProductVersion = "0.1" };
        var result = PlatformInterface.Initialize(ref init);
        if (result != Result.Success && result != Result.AlreadyConfigured)
        {
            Console.WriteLine($"Initialize failed: {result}");
            return false;
        }
        LoggingInterface.SetLogLevel(LogCategory.AllCategories, LogLevel.Warning);
        LoggingInterface.SetCallback((ref LogMessage m) =>
        {
            string text = m.Message;
            // Normal on every run after the first: the device already has its Device ID.
            if (text != null && text.Contains("DeviceId access credentials already exist")) return;
            Console.WriteLine($"  [eos {m.Level}] {m.Category}: {text}");
        });

        var options = new Options
        {
            ProductId = config.productId,
            SandboxId = config.sandboxId,
            DeploymentId = config.deploymentId,
            ClientCredentials = new ClientCredentials { ClientId = config.clientId, ClientSecret = config.clientSecret },
            Flags = PlatformFlags.DisableOverlay | PlatformFlags.DisableSocialOverlay,
            IsServer = false,
        };
        platform = PlatformInterface.Create(ref options);
        if (platform == null)
        {
            Console.WriteLine("Platform create failed: check the ids in eos.local.json.");
            return false;
        }

        var relayOptions = new SetRelayControlOptions { RelayControl = relay };
        Console.WriteLine($"Relay mode: {relay} ({platform.GetP2PInterface().SetRelayControl(ref relayOptions)})");

        if (!Login()) return false;
        QueryNat();
        return true;
    }

    private static bool Login()
    {
        var connect = platform.GetConnectInterface();
        Result? created = null;
        var createOptions = new CreateDeviceIdOptions { DeviceModel = "PC Windows" };
        connect.CreateDeviceId(ref createOptions, null, (ref CreateDeviceIdCallbackInfo info) => created = info.ResultCode);
        if (!Pump(() => created.HasValue, 15)) { Console.WriteLine("CreateDeviceId timed out."); return false; }
        if (created != Result.Success && created != Result.DuplicateNotAllowed)
        {
            Console.WriteLine($"CreateDeviceId failed: {created}");
            return false;
        }

        Result? login = null;
        ContinuanceToken continuance = null;
        var loginOptions = new LoginOptions
        {
            Credentials = new Credentials { Type = ExternalCredentialType.DeviceidAccessToken, Token = null },
            UserLoginInfo = new UserLoginInfo { DisplayName = "Probe" },
        };
        connect.Login(ref loginOptions, null, (ref LoginCallbackInfo info) =>
        {
            login = info.ResultCode;
            localUser = info.LocalUserId;
            continuance = info.ContinuanceToken;
        });
        if (!Pump(() => login.HasValue, 15)) { Console.WriteLine("Login timed out."); return false; }

        if (login == Result.InvalidUser && continuance != null)
        {
            Result? createdUser = null;
            var createUser = new CreateUserOptions { ContinuanceToken = continuance };
            connect.CreateUser(ref createUser, null, (ref CreateUserCallbackInfo info) =>
            {
                createdUser = info.ResultCode;
                localUser = info.LocalUserId;
            });
            if (!Pump(() => createdUser.HasValue, 15)) { Console.WriteLine("CreateUser timed out."); return false; }
            login = createdUser;
        }
        if (login != Result.Success || localUser == null)
        {
            Console.WriteLine($"Login failed: {login}");
            return false;
        }
        Console.WriteLine($"Logged in (Device ID), user {Short(localUser)}");
        return true;
    }

    private static void QueryNat()
    {
        bool done = false;
        var options = new QueryNATTypeOptions();
        platform.GetP2PInterface().QueryNATType(ref options, null, (ref OnQueryNATTypeCompleteInfo info) =>
        {
            Console.WriteLine($"NAT type: {info.NATType} ({info.ResultCode})");
            done = true;
        });
        Pump(() => done, 10);
    }

    private static int Host()
    {
        var lobby = platform.GetLobbyInterface();
        string code = null, lobbyId = null;
        for (int attempt = 0; attempt < 3 && lobbyId == null; attempt++)
        {
            code = NewCode();
            Result? created = null;
            var options = new CreateLobbyOptions
            {
                LocalUserId = localUser,
                MaxLobbyMembers = 2,
                PermissionLevel = LobbyPermissionLevel.Publicadvertised,
                PresenceEnabled = false,
                AllowInvites = false,
                BucketId = "petcoop:probe",
                DisableHostMigration = true,
                EnableJoinById = true,
                LobbyId = LobbyPrefix + code,
            };
            lobby.CreateLobby(ref options, null, (ref CreateLobbyCallbackInfo info) => created = info.ResultCode);
            if (!Pump(() => created.HasValue, 15)) { Console.WriteLine("CreateLobby timed out."); return 1; }
            if (created == Result.Success) lobbyId = LobbyPrefix + code;
            else if (created != Result.LobbyLobbyAlreadyExists) { Console.WriteLine($"CreateLobby failed: {created}"); return 1; }
        }
        if (lobbyId == null) { Console.WriteLine("Could not find a free room code."); return 1; }

        Console.WriteLine();
        Console.WriteLine($"  ROOM CODE: {code}");
        Console.WriteLine($"  On the other machine: EosProbe join {code}");
        Console.WriteLine("  Waiting... (Ctrl+C to stop)");
        Console.WriteLine();

        var memberOptions = new AddNotifyLobbyMemberStatusReceivedOptions();
        lobby.AddNotifyLobbyMemberStatusReceived(ref memberOptions, null, (ref LobbyMemberStatusReceivedCallbackInfo info) =>
            Console.WriteLine($"Member {Short(info.TargetUserId)}: {info.CurrentStatus}"));

        var p2p = platform.GetP2PInterface();
        var requestOptions = new AddNotifyPeerConnectionRequestOptions { LocalUserId = localUser, SocketId = Socket };
        p2p.AddNotifyPeerConnectionRequest(ref requestOptions, null, (ref OnIncomingConnectionRequestInfo info) =>
        {
            var accept = new AcceptConnectionOptions { LocalUserId = localUser, RemoteUserId = info.RemoteUserId, SocketId = Socket };
            Console.WriteLine($"Connection request from {Short(info.RemoteUserId)}: accept {p2p.AcceptConnection(ref accept)}");
        });
        WatchEstablished(p2p);

        bool stop = false, bye = false;
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop = true; };
        int echoed = 0;
        var buffer = new byte[1170];
        while (!stop && !bye)
        {
            platform.Tick();
            while (TryReceive(p2p, buffer, out var peer, out int length))
            {
                if (length > 0 && buffer[0] == PingTag)
                {
                    buffer[0] = PongTag;
                    Send(p2p, peer, new ArraySegment<byte>(buffer, 0, length));
                    echoed++;
                }
                else if (length > 0 && buffer[0] == ByeTag)
                {
                    bye = true;
                }
            }
            Thread.Sleep(5);
        }
        Console.WriteLine($"Echoed {echoed} pings. Closing room.");
        Leave(lobbyId);
        return 0;
    }

    private static int Join(string code)
    {
        var lobby = platform.GetLobbyInterface();
        string lobbyId = LobbyPrefix + code;
        Result? joined = null;
        var options = new JoinLobbyByIdOptions { LobbyId = lobbyId, LocalUserId = localUser, PresenceEnabled = false };
        var joinClock = Stopwatch.StartNew();
        lobby.JoinLobbyById(ref options, null, (ref JoinLobbyByIdCallbackInfo info) => joined = info.ResultCode);
        if (!Pump(() => joined.HasValue, 20)) { Console.WriteLine("JoinLobbyById timed out."); return 1; }
        if (joined != Result.Success) { Console.WriteLine($"Join failed: {joined} (wrong code, or the room is closed/full)"); return 1; }
        Console.WriteLine($"Joined room {code} in {joinClock.ElapsedMilliseconds} ms");

        var copy = new CopyLobbyDetailsHandleOptions { LobbyId = lobbyId, LocalUserId = localUser };
        if (lobby.CopyLobbyDetailsHandle(ref copy, out var details) != Result.Success || details == null)
        {
            Console.WriteLine("Could not read room details.");
            Leave(lobbyId);
            return 1;
        }
        var ownerOptions = new LobbyDetailsGetLobbyOwnerOptions();
        var host = details.GetLobbyOwner(ref ownerOptions);
        details.Release();
        Console.WriteLine($"Host is {Short(host)}");

        var p2p = platform.GetP2PInterface();
        WatchEstablished(p2p);

        var rtts = new List<double>();
        var sent = new Dictionary<int, long>();
        var buffer = new byte[1170];
        var packet = new byte[13];
        int seq = 0, warmup = 0, measured = 0;
        var clock = Stopwatch.StartNew();
        long nextSend = 0, deadline = Stopwatch.Frequency * 60, connectedAt = long.MaxValue;
        // Pings sent while the connection opens wait in the SDK queue (delayed delivery), so their
        // round trip includes the connect time. Only pings sent after "connected" are measured.
        while (rtts.Count < PingCount && clock.ElapsedTicks < deadline)
        {
            platform.Tick();
            if (connected && connectedAt == long.MaxValue)
            {
                connectedAt = clock.ElapsedTicks;
                Console.WriteLine($"Connected after {connectedAt * 1000 / Stopwatch.Frequency} ms; measuring {PingCount} pings...");
            }
            if (clock.ElapsedTicks >= nextSend)
            {
                packet[0] = PingTag;
                BitConverter.TryWriteBytes(packet.AsSpan(1, 4), seq);
                BitConverter.TryWriteBytes(packet.AsSpan(5, 8), clock.ElapsedTicks);
                sent[seq++] = clock.ElapsedTicks;
                if (clock.ElapsedTicks >= connectedAt) measured++;
                Send(p2p, host, packet);
                nextSend = clock.ElapsedTicks + Stopwatch.Frequency / 5;
            }
            while (TryReceive(p2p, buffer, out _, out int length))
            {
                if (length == 13 && buffer[0] == PongTag)
                {
                    long then = BitConverter.ToInt64(buffer, 5);
                    if (then >= connectedAt) rtts.Add((clock.ElapsedTicks - then) * 1000.0 / Stopwatch.Frequency);
                    else warmup++;
                }
            }
            Thread.Sleep(2);
        }

        packet[0] = ByeTag;
        Send(p2p, host, new ArraySegment<byte>(packet, 0, 1), PacketReliability.ReliableOrdered);
        Pump(() => false, 1);
        Leave(lobbyId);

        Console.WriteLine();
        if (rtts.Count == 0)
        {
            Console.WriteLine($"RESULT: no answer from host ({seq} pings sent). P2P did not connect.");
            return 1;
        }
        rtts.Sort();
        Console.WriteLine($"RESULT: {rtts.Count} answers / {measured} pings sent after connecting ({warmup} warm-up pings ignored)");
        Console.WriteLine($"  round trip ms  min {rtts[0]:0.0}  median {rtts[rtts.Count / 2]:0.0}  p90 {rtts[(int)(rtts.Count * 0.9)]:0.0}  max {rtts[^1]:0.0}");
        return 0;
    }

    private static bool connected;

    private static void WatchEstablished(P2PInterface p2p)
    {
        var options = new AddNotifyPeerConnectionEstablishedOptions { LocalUserId = localUser, SocketId = Socket };
        p2p.AddNotifyPeerConnectionEstablished(ref options, null, (ref OnPeerConnectionEstablishedInfo info) =>
        {
            connected = true;
            Console.WriteLine($"P2P connected to {Short(info.RemoteUserId)}: {info.NetworkType} ({info.ConnectionType})");
        });
    }

    private static void Send(P2PInterface p2p, ProductUserId to, ArraySegment<byte> data,
        PacketReliability reliability = PacketReliability.UnreliableUnordered)
    {
        var options = new SendPacketOptions
        {
            LocalUserId = localUser,
            RemoteUserId = to,
            SocketId = Socket,
            Channel = 0,
            Data = data,
            AllowDelayedDelivery = true,
            Reliability = reliability,
        };
        var result = p2p.SendPacket(ref options);
        if (result != Result.Success) Console.WriteLine($"  send: {result}");
    }

    private static bool TryReceive(P2PInterface p2p, byte[] buffer, out ProductUserId peer, out int length)
    {
        peer = null;
        length = 0;
        var options = new ReceivePacketOptions { LocalUserId = localUser, MaxDataSizeBytes = (uint)buffer.Length };
        var socket = new SocketId();
        var result = p2p.ReceivePacket(ref options, ref peer, ref socket, out _, new ArraySegment<byte>(buffer), out uint written);
        if (result != Result.Success) return false;
        length = (int)written;
        return true;
    }

    private static void Leave(string lobbyId)
    {
        bool done = false;
        var options = new LeaveLobbyOptions { LocalUserId = localUser, LobbyId = lobbyId };
        platform.GetLobbyInterface().LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo _) => done = true);
        Pump(() => done, 5);
    }

    private static bool Pump(Func<bool> done, double seconds)
    {
        var clock = Stopwatch.StartNew();
        while (!done())
        {
            if (clock.Elapsed.TotalSeconds > seconds) return false;
            platform.Tick();
            Thread.Sleep(10);
        }
        return true;
    }

    private static string NewCode()
    {
        Span<char> chars = stackalloc char[6];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)];
        return new string(chars);
    }

    private static string Short(ProductUserId id)
    {
        string text = id?.ToString() ?? "?";
        return text.Length > 8 ? text[..8] + "…" : text;
    }

    private sealed class EosConfig
    {
        public string productId { get; set; }
        public string sandboxId { get; set; }
        public string deploymentId { get; set; }
        public string clientId { get; set; }
        public string clientSecret { get; set; }
    }

    private static EosConfig LoadConfig()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "eos.local.json");
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); !File.Exists(path) && dir != null; dir = dir.Parent)
            path = Path.Combine(dir.FullName, "secrets", "eos.local.json");
        if (!File.Exists(path))
        {
            Console.WriteLine("eos.local.json not found (put it next to EosProbe.exe).");
            return null;
        }
        var config = JsonSerializer.Deserialize<EosConfig>(File.ReadAllText(path));
        if (string.IsNullOrWhiteSpace(config?.clientId) || string.IsNullOrWhiteSpace(config.clientSecret))
        {
            Console.WriteLine($"clientId/clientSecret empty in {path}");
            return null;
        }
        return config;
    }
}
