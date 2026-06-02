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
        public static int m_aimPunchCache = 0x88;
        public static int m_predictableBaseAngle = 0x50;
        public static int m_iShotsFired = 0x1C64;
        public static int m_entitySpottedState = 0x1C38;
        public static int m_bSpotted = 0x8;
        public static int m_bSpottedByMask = 0xC;

        public static int m_pWeaponServices = 0x11E0;
        public static int m_hActiveWeapon = 0x60;
        public static int m_AttributeManager = 0x1180;
        public static int m_Item = 0x50;
        public static int m_iItemDefinitionIndex = 0x1BA;
        public static int m_iRecoilIndex = 0x17DC;
        public static int m_fAccuracyPenalty = 0x17D0;
    }
}
