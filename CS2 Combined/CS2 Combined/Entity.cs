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

        public Vector3 GetAimPosition()
        {
            if (view.Z is > 10f and < 90f)
                return Vector3.Add(origin, view);

            return origin + new Vector3(0f, 0f, 64f);
        }

        public Vector3 GetChestPosition() => origin + new Vector3(0f, 0f, 36f);
    }
}
