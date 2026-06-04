using System.Numerics;

namespace External_Aimbot
{
    public readonly struct NoRecoilDebug
    {
        public bool Shooting { get; init; }
        public Vector3 PunchBefore { get; init; }
        public string Status { get; init; }
    }

    internal static class NoRecoil
    {
        private static readonly Vector3 Zero = Vector3.Zero;

        public static void Process(
            GameMemory mem,
            IntPtr pawn,
            bool enabled,
            WeaponContext weapon,
            Vector2 viewAngles,
            out NoRecoilDebug debug)
        {
            if (!enabled)
            {
                debug = new NoRecoilDebug { Status = "Disabled" };
                return;
            }

            if (pawn == IntPtr.Zero)
            {
                debug = new NoRecoilDebug { Status = "Not in game" };
                return;
            }

            if (!IsShooting(mem, pawn, weapon))
            {
                debug = new NoRecoilDebug { Status = "Idle" };
                return;
            }

            Vector3 punchBefore = RecoilControl.GetAimPunch(mem, pawn);
            ZeroPunch(mem, pawn);

            Vector3 punchAfterZero = RecoilControl.GetAimPunch(mem, pawn);
            if (punchAfterZero != Vector3.Zero)
            {
                Vector2 compensated = RecoilControl.ApplyInstantCompensation(viewAngles, punchAfterZero, 1f);
                mem.WriteVec(mem.Client, Offsets.dwViewAngles, new Vector3(compensated.Y, compensated.X, 0f));
            }

            debug = new NoRecoilDebug
            {
                Shooting = true,
                PunchBefore = punchBefore,
                Status = punchBefore == Vector3.Zero ? "Active" : "Compensating",
            };
        }

        private static bool IsShooting(GameMemory mem, IntPtr pawn, WeaponContext weapon)
        {
            if (!weapon.IsAttacking)
                return false;

            return mem.ReadInt(pawn, Offsets.m_iShotsFired) >= 1;
        }

        private static void ZeroPunch(GameMemory mem, IntPtr pawn)
        {
            IntPtr aimPunchServices = mem.ReadPtr(pawn, Offsets.m_pAimPunchServices);
            if (aimPunchServices != IntPtr.Zero)
            {
                mem.WriteVec(aimPunchServices, Offsets.m_predictableBaseAngle, Zero);
                mem.WriteVec(aimPunchServices, Offsets.m_unpredictableBaseAngle, Zero);
            }

            IntPtr cameraServices = mem.ReadPtr(pawn, Offsets.m_pCameraServices);
            if (cameraServices != IntPtr.Zero)
                mem.WriteVec(cameraServices, Offsets.m_vecCsViewPunchAngle, Zero);
        }
    }
}
