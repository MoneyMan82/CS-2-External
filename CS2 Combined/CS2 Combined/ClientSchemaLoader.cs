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
                SetField(classes, "C_CSWeaponBase", "m_iRecoilIndex", v => Offsets.m_iRecoilIndex = v);
                SetField(classes, "C_CSWeaponBase", "m_flRecoilIndex", v => Offsets.m_flRecoilIndex = v);
                SetField(classes, "C_CSWeaponBase", "m_fAccuracyPenalty", v => Offsets.m_fAccuracyPenalty = v);
                SetField(classes, "C_CSWeaponBase", "m_bBurstMode", v => Offsets.m_bBurstMode = v);
                SetField(classes, "EntitySpottedState_t", "m_bSpotted", v => Offsets.m_bSpotted = v);
                SetField(classes, "EntitySpottedState_t", "m_bSpottedByMask", v => Offsets.m_bSpottedByMask = v);
                SetField(classes, "C_BaseEntity", "m_pGameSceneNode", v => Offsets.m_pGameSceneNode = v);
                SetField(classes, "CSkeletonInstance", "m_modelState", v => Offsets.m_modelState = v);
                SetField(classes, "CBasePlayerController", "m_iszPlayerName", v => Offsets.m_iszPlayerName = v);
                SetField(classes, "C_CSPlayerPawn", "m_ArmorValue", v => Offsets.m_ArmorValue = v);
                SetField(classes, "C_BaseEntity", "m_fFlags", v => Offsets.m_fFlags = v);

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
