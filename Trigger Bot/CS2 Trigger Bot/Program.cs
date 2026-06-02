using Swed64;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;

const uint MouseEventLeftDown = 0x0002;
const uint MouseEventLeftUp = 0x0004;

GameOffsets offsets;
try
{
    offsets = await GameOffsets.FetchAsync();
    Console.WriteLine("Loaded offsets from cs2-dumper (GitHub).");
}
catch (Exception ex)
{
    Console.WriteLine($"Could not fetch offsets online ({ex.Message}). Using embedded fallback.");
    offsets = GameOffsets.Fallback;
}

offsets.Print();

Console.WriteLine("Waiting for CS2...");
while (Process.GetProcessesByName("cs2").Length == 0)
    Thread.Sleep(500);

Swed swed = new Swed("cs2");

IntPtr client = swed.GetModuleBase("client.dll");
if (client == IntPtr.Zero)
{
    Console.WriteLine("Could not find client.dll. Join a match or wait for the game to finish loading.");
    return;
}

Console.WriteLine("Trigger bot running. Hold mouse 4 or 5.");
Console.WriteLine("Press F7 to toggle Deathmatch mode (needed for DM — everyone shares a team).");

bool deathmatchMode = false;

while (true)
{
    if ((GetAsyncKeyState(0x76) & 1) != 0)
        deathmatchMode = !deathmatchMode;

    Console.Clear();
    Console.WriteLine($"Mode: {(deathmatchMode ? "Deathmatch (shoot any player)" : "Casual/Comp (enemies only)")}");

    IntPtr localPlayerPawn = swed.ReadPointer(client, offsets.DwLocalPlayerPawn);
    if (localPlayerPawn == IntPtr.Zero)
    {
        Console.WriteLine("Not in game yet (no local pawn).");
        Thread.Sleep(100);
        continue;
    }

    int entityId = swed.ReadInt(localPlayerPawn, offsets.M_iIDEntIndex);
    Console.WriteLine($"Crosshair entity ID: {entityId}");

    bool hotkeyHeld = IsHotkeyHeld();
    Console.WriteLine($"Hotkey (mouse 4/5): {(hotkeyHeld ? "HELD" : "not held")}");

    if (entityId <= 0)
    {
        Console.WriteLine("Not aiming at a valid target.");
        Thread.Sleep(1);
        continue;
    }

    IntPtr gameEntitySystem = swed.ReadPointer(client, offsets.DwEntityList);
    IntPtr entity = ResolveEntity(swed, gameEntitySystem, entityId);
    if (entity == IntPtr.Zero)
    {
        Console.WriteLine("Could not resolve crosshair entity (tried strides 0x70 and 0x78).");
        Thread.Sleep(1);
        continue;
    }

    IntPtr targetPawn = ResolvePawn(swed, gameEntitySystem, entity, offsets.M_hPlayerPawn);
    int entityTeam = swed.ReadInt(targetPawn, offsets.M_iTeamNum);
    int localTeam = swed.ReadInt(localPlayerPawn, offsets.M_iTeamNum);
    int entityHealth = swed.ReadInt(targetPawn, offsets.M_iHealth);
    if (entityHealth <= 0 && targetPawn != entity)
        entityHealth = swed.ReadInt(entity, offsets.M_iPawnHealth);

    Console.WriteLine($"Entity team: {entityTeam}, your team: {localTeam}, HP: {entityHealth}");

    if (!hotkeyHeld)
    {
        Thread.Sleep(1);
        continue;
    }

    if (targetPawn == localPlayerPawn)
    {
        Console.WriteLine("Aiming at yourself — not shooting.");
        Thread.Sleep(1);
        continue;
    }

    bool shouldShoot = deathmatchMode
        ? entityId > 0
        : entityTeam != localTeam && entityTeam != 0 && localTeam != 0;

    if (!shouldShoot)
    {
        Console.WriteLine(deathmatchMode
            ? "Invalid target."
            : "Same team or invalid team — not shooting. (Press F7 for Deathmatch mode)");
        Thread.Sleep(1);
        continue;
    }

    mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
    Thread.Sleep(10);
    mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
    Console.WriteLine("SHOT");

    Thread.Sleep(50);
}

static IntPtr ResolveEntity(Swed swed, IntPtr gameEntitySystem, int index)
{
    if (gameEntitySystem == IntPtr.Zero)
        return IntPtr.Zero;

    foreach (int stride in new[] { GameOffsets.EntityStrideNew, GameOffsets.EntityStrideOld })
    {
        foreach (int idx in new[] { index & 0x7FFF, index })
        {
            IntPtr entry = swed.ReadLong(gameEntitySystem, 0x8 * (idx >> 9) + 0x10);
            if (!IsValidPointer(entry))
                continue;

            IntPtr entity = swed.ReadLong(entry, stride * (idx & 0x1FF));
            if (IsValidPointer(entity))
                return entity;
        }
    }

    return IntPtr.Zero;
}

static IntPtr ResolvePawn(Swed swed, IntPtr gameEntitySystem, IntPtr entity, int m_hPlayerPawn)
{
    int pawnHandle = swed.ReadInt(entity, m_hPlayerPawn);
    if (pawnHandle <= 0)
        return entity;

    IntPtr pawn = ResolveEntity(swed, gameEntitySystem, pawnHandle);
    return pawn != IntPtr.Zero ? pawn : entity;
}

static bool IsValidPointer(IntPtr ptr) =>
    ptr != IntPtr.Zero && ptr > (IntPtr)0x10000;

static bool IsHotkeyHeld() =>
    (GetAsyncKeyState(0x05) & 0x8000) != 0 || (GetAsyncKeyState(0x06) & 0x8000) != 0;

[DllImport("user32.dll")]
static extern short GetAsyncKeyState(int vKey);

[DllImport("user32.dll")]
static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

sealed class GameOffsets
{
    const string OffsetsUrl = "https://raw.githubusercontent.com/a2x/cs2-dumper/main/output/offsets.json";
    const string ClientDllUrl = "https://raw.githubusercontent.com/a2x/cs2-dumper/main/output/client_dll.json";

    public const int EntityStrideNew = 0x70;
    public const int EntityStrideOld = 0x78;

    public int DwLocalPlayerPawn { get; init; }
    public int DwEntityList { get; init; }
    public int M_iIDEntIndex { get; init; }
    public int M_iTeamNum { get; init; }
    public int M_iHealth { get; init; }
    public int M_hPlayerPawn { get; init; }
    public int M_iPawnHealth { get; init; }

    public static GameOffsets Fallback { get; } = new()
    {
        DwLocalPlayerPawn = 0x233F698,
        DwEntityList = 0x24E5590,
        M_iIDEntIndex = 0x33FC,
        M_iTeamNum = 0x3EB,
        M_iHealth = 0x34C,
        M_hPlayerPawn = 0x90C,
        M_iPawnHealth = 0x918,
    };

    public static async Task<GameOffsets> FetchAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        var offsetsTask = http.GetStringAsync(OffsetsUrl);
        var clientTask = http.GetStringAsync(ClientDllUrl);
        await Task.WhenAll(offsetsTask, clientTask);

        using var offsetsJson = JsonDocument.Parse(offsetsTask.Result);
        using var clientJson = JsonDocument.Parse(clientTask.Result);

        var client = offsetsJson.RootElement.GetProperty("client.dll");
        var classes = clientJson.RootElement.GetProperty("client.dll").GetProperty("classes");

        return new GameOffsets
        {
            DwLocalPlayerPawn = client.GetProperty("dwLocalPlayerPawn").GetInt32(),
            DwEntityList = client.GetProperty("dwEntityList").GetInt32(),
            M_iIDEntIndex = Field(classes, "C_CSPlayerPawn", "m_iIDEntIndex"),
            M_iTeamNum = Field(classes, "C_BaseEntity", "m_iTeamNum"),
            M_iHealth = Field(classes, "C_BaseEntity", "m_iHealth"),
            M_hPlayerPawn = Field(classes, "CCSPlayerController", "m_hPlayerPawn"),
            M_iPawnHealth = Field(classes, "CCSPlayerController", "m_iPawnHealth"),
        };
    }

    static int Field(JsonElement classes, string className, string fieldName) =>
        classes.GetProperty(className).GetProperty("fields").GetProperty(fieldName).GetInt32();

    public void Print()
    {
        Console.WriteLine($"  dwLocalPlayerPawn = 0x{DwLocalPlayerPawn:X}");
        Console.WriteLine($"  dwEntityList      = 0x{DwEntityList:X}");
        Console.WriteLine($"  m_iIDEntIndex     = 0x{M_iIDEntIndex:X}");
        Console.WriteLine($"  m_iTeamNum        = 0x{M_iTeamNum:X}");
        Console.WriteLine($"  m_iHealth         = 0x{M_iHealth:X}");
        Console.WriteLine($"  entity stride     = 0x{EntityStrideNew:X} (+ 0x{EntityStrideOld:X} fallback)");
    }
}
