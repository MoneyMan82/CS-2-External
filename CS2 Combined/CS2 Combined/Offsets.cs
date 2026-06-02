namespace External_Aimbot
{
    public static class Offsets
    {
        public static int dwViewAngles = 0x23558B8;
        public static int dwViewMatrix = 0x2346570;
        public static int dwCSGOInput = 0x23557D0;
        public static int dwLocalPlayerPawn = 0x2340698;
        public static int dwLocalPlayerController = 0x231F700;
        public static int dwEntityList = 0x24E6590;
        public static int dwGameEntitySystem = 0x24E6590;
        public static int dwGlobalVars = 0x20606D0;
        public static int dwGameEntitySystem_highestEntityIndex = 0x2090;
        public static int dwJump = 0x2062E80;
        public static int dwAttack = 0x2064A90;

        public static int dwNetworkGameClient = 0x90A1A0;
        public static int dwNetworkGameClient_signOnState = 0x230;
        public static int dwBuildNumber = 0x60CC74;

        public static int m_hPlayerPawn = 0x90C;
        public static int m_hPawn = 0x6BC;
        public static int m_bIsLocalPlayerController = 0x788;
        public static int m_bPawnIsAlive = 0x914;
        public static int m_iPawnHealth = 0x918;
        public static int m_iPendingTeamNum = 0x840;
        public static int m_iHealth = 0x34C;
        public static int m_vOldOrigin = 0x1390;
        public static int m_iTeamNum = 0x3EB;
        public static int m_vecViewOffset = 0xE70;
        public static int m_lifeState = 0x354;
        public static int m_angEyeAngles = 0x3320;

        public static int m_pAimPunchServices = 0x1490;
        public static int m_predictableBaseAngle = 0x50;
        public static int m_unpredictableBaseAngle = 0xA4;
        public static int m_pCameraServices = 0x1218;
        public static int m_vecCsViewPunchAngle = 0x48;
        public static int m_iShotsFired = 0x1C64;
        public static int m_entitySpottedState = 0x1C38;
        public static int m_bSpotted = 0x8;
        public static int m_bSpottedByMask = 0xC;

        public static int m_pWeaponServices = 0x11E0;
        public static int m_hActiveWeapon = 0x60;
        public static int m_AttributeManager = 0x1180;
        public static int m_Item = 0x50;
        public static int m_iItemDefinitionIndex = 0x1BA;
        public static int m_iItemIDHigh = 0x1D0;
        public static int m_iItemIDLow = 0x1D4;
        public static int m_hMyWeapons = 0x48;

        public static int m_nFallbackPaintKit = 0x1658;
        public static int m_nFallbackSeed = 0x165C;
        public static int m_flFallbackWear = 0x1660;
        public static int m_nFallbackStatTrak = 0x1664;
        public static int m_iRecoilIndex = 0x17DC;
        public static int m_flRecoilIndex = 0x17E0;
        public static int m_fAccuracyPenalty = 0x17D0;
        public static int m_bBurstMode = 0x17E4;
        public static int m_bInReload = 0x17F4;
        public static int m_fLastShotTime = 0x1900;
        public static int m_flNextClientFireBulletTime = 0x1908;
        public static int m_flNextClientFireBulletTime_Repredict = 0x190C;
        public static int m_nPostponeFireReadyTicks = 0x17EC;
        public static int m_flPostponeFireReadyFrac = 0x17F0;
        public static int m_nNextPrimaryAttackTick = 0x16C8;
        public static int m_flNextPrimaryAttackTickRatio = 0x16CC;
        public static int m_iBurstShotsRemaining = 0x1CB4;
        public static int m_bNeedsBoltAction = 0x1CCD;
        public static int m_iIDEntIndex = 0x33FC;
        public static int m_pGameSceneNode = 0x330;
        public static int m_modelState = 0x150;
        public static int m_boneArray = 0x80;
        public static int m_boneArrayLegacy = 0x210;
        public static int m_iszPlayerName = 0x6F4;
        public static int m_ArmorValue = 0x1C7C;
        public static int m_fFlags = 0x3F8;

        public static int m_flFlashBangTime = 0x13EC;
        public static int m_flFlashMaxAlpha = 0x13FC;
        public static int m_flFlashOverlayAlpha = 0x13F4;
        public static int m_flFlashDuration = 0x1400;

        public static int m_pObserverServices = 0x11F8;
        public static int m_iObserverMode = 0x48;
        public static int m_hObserverTarget = 0x4C;
        public static int m_hObserverPawn = 0x910;
        public static int m_sSanitizedPlayerName = 0x860;
        public static int m_iFOV = 0x290;

        public static int m_bBombTicking = 0x1160;
        public static int m_flC4Blow = 0x1190;
        public static int m_nBombSite = 0x1164;
        public static int m_bBeingDefused = 0x119C;
        public static int m_flDefuseCountDown = 0x11B0;
        public static int m_bHasExploded = 0x1195;
    }
}
