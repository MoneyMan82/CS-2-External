using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace External_Aimbot
{
    public class Entity
    {
        public IntPtr pawnAddress { get; set; }
        public IntPtr controllerAddress { get; set; }
        public Vector3 origin { get; set; }
        public Vector3 view { get; set; }
        public int health { get; set; }
        public int team { get; set; }
        public bool isVisible { get; set; }
        public uint lifeState { get; set; }
        public float distance { get; set; } //from localplayer

        internal Vector3 GetAimPosition(GameMemory mem) =>
            AimTarget.GetHeadPosition(mem, pawnAddress, origin, view);

        public Vector3 GetAimPosition() =>
            view.Z is > 10f and < 90f ? origin + view : origin + new Vector3(0f, 0f, 64f);

        internal Vector3 GetChestPosition(GameMemory mem) =>
            AimTarget.GetChestPosition(mem, pawnAddress, origin);

        public Vector3 GetChestPosition() => origin + new Vector3(0f, 0f, 36f);
    }
}
