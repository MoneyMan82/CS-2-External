using System.Text.Json;

namespace External_Aimbot
{
    internal static class ClientSchemaLoader
    {
        public static bool TryLoad(string path)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);

                if (!doc.RootElement.TryGetProperty("client.dll", out JsonElement client))
                    return false;

                if (!client.TryGetProperty("classes", out JsonElement classes))
                    return false;

                SetField(classes, "C_BasePlayerPawn", "m_pWeaponServices", v => Offsets.m_pWeaponServices = v);
                SetField(classes, "C_CSPlayerPawn", "m_iIDEntIndex", v => Offsets.m_iIDEntIndex = v);
                SetField(classes, "C_CSPlayerPawn", "m_iShotsFired", v => Offsets.m_iShotsFired = v);
                SetField(classes, "C_CSPlayerPawn", "m_pAimPunchServices", v => Offsets.m_pAimPunchServices = v);
                SetField(classes, "CCSPlayer_AimPunchServices", "m_predictableBaseAngle", v => Offsets.m_predictableBaseAngle = v);
                SetField(classes, "CCSPlayer_AimPunchServices", "m_unpredictableBaseAngle", v => Offsets.m_unpredictableBaseAngle = v);
                SetField(classes, "C_BasePlayerPawn", "m_pCameraServices", v => Offsets.m_pCameraServices = v);
                SetField(classes, "CPlayer_CameraServices", "m_vecCsViewPunchAngle", v => Offsets.m_vecCsViewPunchAngle = v);
                SetField(classes, "C_CSPlayerPawn", "m_entitySpottedState", v => Offsets.m_entitySpottedState = v);
                SetField(classes, "C_CSPlayerPawn", "m_angEyeAngles", v => Offsets.m_angEyeAngles = v);
                SetField(classes, "C_CSPlayerPawnBase", "m_vecViewOffset", v => Offsets.m_vecViewOffset = v);
                SetField(classes, "C_BaseEntity", "m_iHealth", v => Offsets.m_iHealth = v);
                SetField(classes, "C_BaseEntity", "m_iTeamNum", v => Offsets.m_iTeamNum = v);
                SetField(classes, "C_BaseEntity", "m_lifeState", v => Offsets.m_lifeState = v);
                SetField(classes, "C_BasePlayerPawn", "m_vOldOrigin", v => Offsets.m_vOldOrigin = v);
                SetField(classes, "CBasePlayerController", "m_hPawn", v => Offsets.m_hPawn = v);
                SetField(classes, "CCSPlayerController", "m_hPlayerPawn", v => Offsets.m_hPlayerPawn = v);
                SetField(classes, "CCSPlayerController", "m_iPawnHealth", v => Offsets.m_iPawnHealth = v);
                SetField(classes, "CBasePlayerController", "m_bIsLocalPlayerController", v => Offsets.m_bIsLocalPlayerController = v);
                SetField(classes, "CCSPlayerController", "m_iPendingTeamNum", v => Offsets.m_iPendingTeamNum = v);
                SetField(classes, "CPlayer_WeaponServices", "m_hActiveWeapon", v => Offsets.m_hActiveWeapon = v);
                SetField(classes, "C_EconEntity", "m_AttributeManager", v => Offsets.m_AttributeManager = v);
                SetField(classes, "C_AttributeContainer", "m_Item", v => Offsets.m_Item = v);
                SetField(classes, "C_EconItemView", "m_iItemDefinitionIndex", v => Offsets.m_iItemDefinitionIndex = v);
                SetField(classes, "C_EconItemView", "m_iItemIDHigh", v => Offsets.m_iItemIDHigh = v);
                SetField(classes, "C_EconItemView", "m_iItemIDLow", v => Offsets.m_iItemIDLow = v);
                SetField(classes, "C_EconItemView", "m_bInitialized", v => Offsets.m_bInitialized = v);
                SetField(classes, "C_EconItemView", "m_bDisallowSOC", v => Offsets.m_bDisallowSOC = v);
                SetField(classes, "CPlayer_WeaponServices", "m_hMyWeapons", v => Offsets.m_hMyWeapons = v);
                SetField(classes, "C_EconEntity", "m_nFallbackPaintKit", v => Offsets.m_nFallbackPaintKit = v);
                SetField(classes, "C_EconEntity", "m_nFallbackSeed", v => Offsets.m_nFallbackSeed = v);
                SetField(classes, "C_EconEntity", "m_flFallbackWear", v => Offsets.m_flFallbackWear = v);
                SetField(classes, "C_EconEntity", "m_nFallbackStatTrak", v => Offsets.m_nFallbackStatTrak = v);
                SetField(classes, "C_BaseEntity", "m_hOwnerEntity", v => Offsets.m_hOwnerEntity = v);
                SetField(classes, "C_CSPlayerPawn", "m_hHudModelArms", v => Offsets.m_hHudModelArms = v);
                SetField(classes, "CGameSceneNode", "m_pChild", v => Offsets.m_pChild = v);
                SetField(classes, "CGameSceneNode", "m_pNextSibling", v => Offsets.m_pNextSibling = v);
                SetField(classes, "CGameSceneNode", "m_pOwner", v => Offsets.m_pOwner = v);
                SetField(classes, "CModelState", "m_MeshGroupMask", v => Offsets.m_MeshGroupMask = v);
                SetField(classes, "C_EconItemView", "m_AttributeList", v => Offsets.m_AttributeList = v);
                SetField(classes, "CAttributeList", "m_Attributes", v => Offsets.m_Attributes = v);
                SetField(classes, "C_CSPlayerPawn", "m_nCustomEconReloadEventId", v => Offsets.m_nCustomEconReloadEventId = v);
                SetField(classes, "C_CSWeaponBase", "m_iRecoilIndex", v => Offsets.m_iRecoilIndex = v);
                SetField(classes, "C_CSWeaponBase", "m_flRecoilIndex", v => Offsets.m_flRecoilIndex = v);
                SetField(classes, "C_CSWeaponBase", "m_fAccuracyPenalty", v => Offsets.m_fAccuracyPenalty = v);
                SetField(classes, "C_CSWeaponBase", "m_bBurstMode", v => Offsets.m_bBurstMode = v);
                SetField(classes, "C_CSWeaponBase", "m_bInReload", v => Offsets.m_bInReload = v);
                SetField(classes, "C_CSWeaponBase", "m_fLastShotTime", v => Offsets.m_fLastShotTime = v);
                SetField(classes, "C_CSWeaponBase", "m_flNextClientFireBulletTime", v => Offsets.m_flNextClientFireBulletTime = v);
                SetField(classes, "C_CSWeaponBase", "m_flNextClientFireBulletTime_Repredict", v => Offsets.m_flNextClientFireBulletTime_Repredict = v);
                SetField(classes, "C_CSWeaponBase", "m_nPostponeFireReadyTicks", v => Offsets.m_nPostponeFireReadyTicks = v);
                SetField(classes, "C_CSWeaponBase", "m_flPostponeFireReadyFrac", v => Offsets.m_flPostponeFireReadyFrac = v);
                SetField(classes, "C_BasePlayerWeapon", "m_nNextPrimaryAttackTick", v => Offsets.m_nNextPrimaryAttackTick = v);
                SetField(classes, "C_BasePlayerWeapon", "m_flNextPrimaryAttackTickRatio", v => Offsets.m_flNextPrimaryAttackTickRatio = v);
                SetField(classes, "C_CSWeaponBaseGun", "m_iBurstShotsRemaining", v => Offsets.m_iBurstShotsRemaining = v);
                SetField(classes, "C_CSWeaponBaseGun", "m_bNeedsBoltAction", v => Offsets.m_bNeedsBoltAction = v);
                SetField(classes, "EntitySpottedState_t", "m_bSpotted", v => Offsets.m_bSpotted = v);
                SetField(classes, "EntitySpottedState_t", "m_bSpottedByMask", v => Offsets.m_bSpottedByMask = v);
                SetField(classes, "C_BaseEntity", "m_pGameSceneNode", v => Offsets.m_pGameSceneNode = v);
                SetField(classes, "CGameSceneNode", "m_vecAbsOrigin", v => Offsets.m_vecAbsOrigin = v);
                SetField(classes, "CSkeletonInstance", "m_modelState", v => Offsets.m_modelState = v);
                SetField(classes, "CBasePlayerController", "m_iszPlayerName", v => Offsets.m_iszPlayerName = v);
                SetField(classes, "C_CSPlayerPawn", "m_ArmorValue", v => Offsets.m_ArmorValue = v);
                SetField(classes, "C_BaseEntity", "m_fFlags", v => Offsets.m_fFlags = v);
                SetField(classes, "C_CSPlayerPawnBase", "m_flFlashBangTime", v => Offsets.m_flFlashBangTime = v);
                SetField(classes, "C_CSPlayerPawnBase", "m_flFlashMaxAlpha", v => Offsets.m_flFlashMaxAlpha = v);
                SetField(classes, "C_CSPlayerPawnBase", "m_flFlashOverlayAlpha", v => Offsets.m_flFlashOverlayAlpha = v);
                SetField(classes, "C_CSPlayerPawnBase", "m_flFlashDuration", v => Offsets.m_flFlashDuration = v);
                SetField(classes, "C_BasePlayerPawn", "m_pObserverServices", v => Offsets.m_pObserverServices = v);
                SetField(classes, "CPlayer_ObserverServices", "m_iObserverMode", v => Offsets.m_iObserverMode = v);
                SetField(classes, "CPlayer_ObserverServices", "m_hObserverTarget", v => Offsets.m_hObserverTarget = v);
                SetField(classes, "CCSPlayerController", "m_hObserverPawn", v => Offsets.m_hObserverPawn = v);
                SetField(classes, "CCSPlayerController", "m_sSanitizedPlayerName", v => Offsets.m_sSanitizedPlayerName = v);
                SetField(classes, "CCSPlayerBase_CameraServices", "m_iFOV", v => Offsets.m_iFOV = v);
                SetField(classes, "C_PlantedC4", "m_bBombTicking", v => Offsets.m_bBombTicking = v);
                SetField(classes, "C_PlantedC4", "m_flC4Blow", v => Offsets.m_flC4Blow = v);
                SetField(classes, "C_PlantedC4", "m_nBombSite", v => Offsets.m_nBombSite = v);
                SetField(classes, "C_PlantedC4", "m_bBeingDefused", v => Offsets.m_bBeingDefused = v);
                SetField(classes, "C_PlantedC4", "m_flDefuseCountDown", v => Offsets.m_flDefuseCountDown = v);
                SetField(classes, "C_PlantedC4", "m_bHasExploded", v => Offsets.m_bHasExploded = v);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SetField(JsonElement classes, string className, string fieldName, Action<int> setter)
        {
            if (!classes.TryGetProperty(className, out JsonElement classDef))
                return;

            if (!classDef.TryGetProperty("fields", out JsonElement fields))
                return;

            if (fields.TryGetProperty(fieldName, out JsonElement value) && value.TryGetInt32(out int offset))
                setter(offset);
        }
    }
}
