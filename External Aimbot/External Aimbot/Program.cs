using External_Aimbot;
using System.Numerics;
using System.Runtime.InteropServices;

try
{
    string offsetsPath = Path.Combine(AppContext.BaseDirectory, "offsets.json");

    using GameMemory mem = new GameMemory();

    bool offsetsLoaded = OffsetsLoader.TryLoadFromFile(offsetsPath);
    if (offsetsLoaded)
        OffsetPresets.ApplyLoadedOffsets();

    string schemaPath = Path.Combine(AppContext.BaseDirectory, "client_dll.json");
    ClientSchemaLoader.TryLoad(schemaPath);

    if (!OffsetPresets.TryApplyWorkingPreset(mem, out _))
    {
        if (await OffsetsUpdater.TryUpdateValidatedAsync(offsetsPath, mem))
        {
            OffsetPresets.ApplyLoadedOffsets();
            OffsetPresets.TryApplyWorkingPreset(mem, out _);
        }
    }

    Renderer renderer = new Renderer();
    renderer.Start().Wait();

    Entity localPlayer = new Entity();

    const int HOTKEY_MOUSE4 = 0x05;
    const int HOTKEY_MOUSE5 = 0x06;

    while (true)
    {
        IntPtr entitySystem = mem.ReadPtr(mem.Client, Offsets.dwGameEntitySystem);

        localPlayer.pawnAddress = LocalPlayer.ResolvePawn(mem, entitySystem, out LocalPlayerDebug localDebug);

        if (localPlayer.pawnAddress == IntPtr.Zero)
        {
            renderer.SetOverlayLines([]);
            Thread.Sleep(250);
            continue;
        }

        MapCollision.Update(mem);

        localPlayer.team = mem.ReadByte(localPlayer.pawnAddress + Offsets.m_iTeamNum);
        localPlayer.origin = mem.ReadVec(localPlayer.pawnAddress, Offsets.m_vOldOrigin);
        localPlayer.view = mem.ReadVec(localPlayer.pawnAddress, Offsets.m_vecViewOffset);

        Vector3 viewAnglesVec = mem.ReadVec(mem.Client, Offsets.dwViewAngles);
        Vector2 viewAngles = new(viewAnglesVec.Y, viewAnglesVec.X);
        Vector3 playerView = Vector3.Add(localPlayer.origin, localPlayer.view);

        int localPlayerIndex = localDebug.ControllerIndex;

        List<Entity> entities = EntityScanner.Scan(
            mem,
            localPlayer,
            renderer.gameMode,
            renderer.aimOnTeam
        );

        foreach (Entity entity in entities)
        {
            entity.isVisible = !renderer.visibilityCheck ||
                Visibility.CanSeeTarget(
                    mem,
                    entity.pawnAddress,
                    localPlayerIndex,
                    playerView,
                    entity.GetAimPosition(),
                    entity.GetChestPosition(),
                    viewAngles,
                    renderer.fov,
                    renderer.mapRaytracing);
        }

        float[] viewMatrix = ViewMatrix.Read(mem);
        var overlayLines = new List<OverlayLine>(entities.Count);
        Vector2 screenSize = renderer.GetScreenSize();

        foreach (Entity entity in entities)
        {
            if (ViewMatrix.WorldToScreen(entity.GetAimPosition(), viewMatrix, screenSize.X, screenSize.Y, out Vector2 screenPos))
                overlayLines.Add(new OverlayLine(screenPos, entity.isVisible));
        }

        renderer.SetOverlayLines(overlayLines);

        WeaponContext weapon = WeaponReader.Read(mem, localPlayer.pawnAddress, entitySystem);
        Vector3 aimPunch = RecoilControl.GetAimPunch(mem, localPlayer.pawnAddress);

        Vector2 predictionPoint = Vector2.Zero;
        renderer.HasPredictionPoint = renderer.recoilPredictor &&
            RecoilPredictor.TryGetLandingScreenPoint(
                viewAngles,
                aimPunch,
                weapon,
                playerView,
                viewMatrix,
                screenSize.X,
                screenSize.Y,
                out predictionPoint);
        renderer.PredictionPoint = predictionPoint;

        bool aimKeyHeld =
            GetAsyncKeyState(HOTKEY_MOUSE4) < 0 ||
            GetAsyncKeyState(HOTKEY_MOUSE5) < 0;

        if (aimKeyHeld && renderer.aimbot && entities.Count > 0)
        {
            Vector2 currentAngles = viewAngles;

            if (weapon.ShotsFired <= 1)
                RecoilControl.Reset();

            Entity? bestTarget = null;
            float bestFov = float.MaxValue;

            foreach (Entity entity in entities)
            {
                if (renderer.visibilityCheck && !entity.isVisible)
                    continue;

                Vector3 entityView = entity.GetAimPosition();
                Vector2 targetAngles = Calculate.CalculateAngles(playerView, entityView);
                float fovDistance = Calculate.GetFovDistance(currentAngles, targetAngles);

                if (fovDistance <= renderer.fov && fovDistance < bestFov)
                {
                    bestFov = fovDistance;
                    bestTarget = entity;
                }
            }

            if (bestTarget != null)
            {
                Vector3 entityView = bestTarget.GetAimPosition();
                Vector2 targetAngles = Calculate.NormalizeAngles(
                    Calculate.CalculateAngles(playerView, entityView)
                );

                Vector2 smoothedAngles = Calculate.NormalizeAngles(
                    Calculate.SmoothAngles(currentAngles, targetAngles, renderer.smoothness)
                );

                if (renderer.recoilPredictor && weapon.IsValid && RecoilControl.ShouldCompensate(mem, localPlayer.pawnAddress))
                {
                    Vector3 punch = RecoilControl.GetAimPunch(mem, localPlayer.pawnAddress);
                    smoothedAngles = Calculate.NormalizeAngles(
                        RecoilPredictor.CompensateForHit(smoothedAngles, punch, weapon, renderer.recoilStrength)
                    );
                }
                else if (renderer.recoilControl && RecoilControl.ShouldCompensate(mem, localPlayer.pawnAddress))
                {
                    Vector3 punch = RecoilControl.GetAimPunch(mem, localPlayer.pawnAddress);
                    smoothedAngles = Calculate.NormalizeAngles(
                        RecoilControl.ApplyDeltaCompensation(smoothedAngles, punch, renderer.recoilStrength)
                    );
                }

                Vector3 newAnglesVec3 = new Vector3(smoothedAngles.Y, smoothedAngles.X, 0.0f);
                mem.WriteVec(mem.Client, Offsets.dwViewAngles, newAnglesVec3);
            }
        }

        Thread.Sleep(5);
    }
}
catch (Exception ex)
{
    MessageBox(IntPtr.Zero, ex.Message, "External Aimbot", 0);
}

[DllImport("user32.dll")]
static extern short GetAsyncKeyState(int vKey);

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
