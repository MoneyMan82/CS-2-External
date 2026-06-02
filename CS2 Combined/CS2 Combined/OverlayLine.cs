using System.Numerics;

namespace External_Aimbot
{
    public readonly struct OverlayLine
    {
        public OverlayLine(Vector2 screenPos, bool visible)
        {
            ScreenPos = screenPos;
            Visible = visible;
        }

        public Vector2 ScreenPos { get; }
        public bool Visible { get; }
    }
}
