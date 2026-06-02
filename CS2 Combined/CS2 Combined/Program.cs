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

    string buttonsPath = Path.Combine(AppContext.BaseDirectory, "buttons.json");
    ButtonsLoader.TryLoadFromFile(buttonsPath);

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

    long lastTriggerShotTicks = 0;

    while (true)
    {
        IntPtr entitySystem = mem.ReadPtr(mem.Client, Offsets.dwGameEntitySystem);

        localPlayer.pawnAddress = LocalPlayer.ResolvePawn(mem, entitySystem, out LocalPlayerDebug localDebug);

        if (localPlayer.pawnAddress == IntPtr.Zero)
        {
            renderer.SetOverlayLines([]);
            renderer.SetEspPlayers([]);
            TriggerBot.Process(
                mem,
                entitySystem,
                IntPtr.Zero,
                renderer.triggerBot,
                renderer.triggerHotkey,
                renderer.triggerGameMode,
                renderer.triggerClickDelayMs,
                renderer.triggerCooldownMs,
                ref lastTriggerShotTicks,
                out TriggerBotDebug idleDebug);
            renderer.SetTriggerDebug(idleDebug);
            Bhop.Process(
                mem,
                IntPtr.Zero,
                renderer.bhopEnabled,
                renderer.bhopHoldSpace,
                renderer.bhopSubtick,
                out BhopDebug idleBhopDebug);
            renderer.SetBhopDebug(idleBhopDebug);
            AntiFlash.Process(
                mem,
                IntPtr.Zero,
                renderer.antiFlashEnabled,
                out AntiFlashDebug idleAntiFlashDebug);
            renderer.SetAntiFlashDebug(idleAntiFlashDebug);
            renderer.SetMiscDebug(default);
            renderer.SetRadarBlips([]);
            renderer.SetSkinChangerDebug(default);
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

        if (renderer.espEnabled)
        {
            List<Entity> espEntities = EntityScanner.Scan(
                mem,
                localPlayer,
                renderer.espGameMode,
                renderer.espShowTeam);

            foreach (Entity entity in espEntities)
            {
                entity.isVisible = Visibility.CanSeeTarget(
                    mem,
                    entity.pawnAddress,
                    localPlayerIndex,
                    playerView,
                    entity.GetAimPosition(),
                    entity.GetChestPosition(),
                    viewAngles,
                    renderer.mapRaytracing);
            }

            var espPlayers = new List<EspPlayerData>(espEntities.Count);
            foreach (Entity entity in espEntities)
            {
                EspPlayerData data = EspBuilder.Build(mem, entitySystem, entity, viewMatrix, screenSize.X, screenSize.Y);
                if (data.Valid)
                    espPlayers.Add(data);
            }

            renderer.SetEspPlayers(espPlayers);
        }
        else
        {
            renderer.SetEspPlayers([]);
        }

        WeaponContext weapon = WeaponReader.Read(mem, localPlayer.pawnAddress, entitySystem);
        renderer.CurrentWeapon = weapon;
        Vector3 aimPunch = RecoilControl.GetAimPunch(mem, localPlayer.pawnAddress);

        Vector2 predictionPoint = Vector2.Zero;
        renderer.HasPredictionPoint = renderer.recoilPredictor &&
            weapon.IsAttacking &&
            weapon.ShotsFired > 0 &&
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

        bool aimKeyHeld = HotkeyInput.IsHeld(renderer.aimbotHotkey);
        bool wantsAim = aimKeyHeld && renderer.aimbot;
        bool wantsRecoil = (renderer.recoilControl || renderer.recoilPredictor) && weapon.IsAttacking;

        RecoilControl.TrackWeapon(weapon);

        if ((wantsAim || wantsRecoil) && localPlayer.pawnAddress != IntPtr.Zero)
        {
            Vector2 currentAngles = viewAngles;

            if (!weapon.IsAttacking || weapon.ShotsFired <= 0)
                RecoilControl.Reset();

            Vector2 finalAngles = currentAngles;
            bool shouldWrite = false;

            if (wantsAim && entities.Count > 0)
            {
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

                    finalAngles = Calculate.NormalizeAngles(
                        Calculate.SmoothAngles(currentAngles, targetAngles, renderer.smoothness)
                    );
                    shouldWrite = true;
                }
            }

            if (wantsRecoil && RecoilControl.ShouldCompensate(mem, localPlayer.pawnAddress, weapon))
            {
                Vector3 punch = RecoilControl.GetAimPunch(mem, localPlayer.pawnAddress);
                finalAngles = Calculate.NormalizeAngles(
                    RecoilControl.Apply(
                        finalAngles,
                        punch,
                        weapon,
                        renderer.recoilPredictor,
                        renderer.recoilControl,
                        renderer.recoilStrength)
                );
                shouldWrite = true;
            }

            if (shouldWrite)
            {
                Vector3 newAnglesVec3 = new Vector3(finalAngles.Y, finalAngles.X, 0.0f);
                mem.WriteVec(mem.Client, Offsets.dwViewAngles, newAnglesVec3);
            }
        }

        TriggerBot.Process(
            mem,
            entitySystem,
            localPlayer.pawnAddress,
            renderer.triggerBot,
            renderer.triggerHotkey,
            renderer.triggerGameMode,
            renderer.triggerClickDelayMs,
            renderer.triggerCooldownMs,
            ref lastTriggerShotTicks,
            out TriggerBotDebug triggerDebug);
        renderer.SetTriggerDebug(triggerDebug);

        Bhop.Process(
            mem,
            localPlayer.pawnAddress,
            renderer.bhopEnabled,
            renderer.bhopHoldSpace,
            renderer.bhopSubtick,
            out BhopDebug bhopDebug);
        if (renderer.bhopEnabled && renderer.bhopSubtick)
        {
            Bhop.Process(
                mem,
                localPlayer.pawnAddress,
                renderer.bhopEnabled,
                renderer.bhopHoldSpace,
                renderer.bhopSubtick,
                out bhopDebug);
        }
        renderer.SetBhopDebug(bhopDebug);

        AntiFlash.Process(
            mem,
            localPlayer.pawnAddress,
            renderer.antiFlashEnabled,
            out AntiFlashDebug antiFlashDebug);
        renderer.SetAntiFlashDebug(antiFlashDebug);

        IntPtr localController = mem.ReadPtr(mem.Client, Offsets.dwLocalPlayerController);
        MiscFeatures.Process(
            mem,
            entitySystem,
            localPlayer.pawnAddress,
            localController,
            localPlayer,
            renderer.miscRadarReveal,
            renderer.miscFovChanger,
            renderer.miscFovValue,
            renderer.miscBombTimer,
            renderer.miscSpectatorList,
            out MiscDebug miscDebug);
        renderer.SetMiscDebug(miscDebug);

        SkinChanger.Process(
            mem,
            localPlayer.pawnAddress,
            entitySystem,
            renderer.skinChangerEnabled,
            renderer.GetSkinConfigs(),
            out SkinChangerDebug skinDebug);
        renderer.SetSkinChangerDebug(skinDebug);

        if (renderer.miscOverlayRadar)
        {
            List<Entity> radarEntities = EntityScanner.Scan(
                mem,
                localPlayer,
                AimbotGameMode.Casual,
                renderer.miscRadarShowTeam);

            foreach (Entity entity in radarEntities)
            {
                entity.isVisible = Visibility.CanSeeTarget(
                    mem,
                    entity.pawnAddress,
                    localPlayerIndex,
                    playerView,
                    entity.GetAimPosition(),
                    entity.GetChestPosition(),
                    viewAngles,
                    renderer.mapRaytracing);
            }

            renderer.SetRadarBlips(
                RadarOverlay.BuildBlips(
                    localPlayer,
                    radarEntities,
                    viewAnglesVec.Y,
                    renderer.miscRadarRange));
        }
        else
        {
            renderer.SetRadarBlips([]);
        }

        Thread.Sleep(renderer.bhopEnabled && renderer.bhopSubtick ? 2 : 5);
    }
}
catch (Exception ex)
{
    MessageBox(IntPtr.Zero, ex.Message, "CS2 Combined", 0);
}

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
