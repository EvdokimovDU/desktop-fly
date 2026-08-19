using System.Numerics;
using DesktopFly.Core.Model3D;

namespace DesktopFly.Core.Behavior;

public class Leg
{
    public SceneNode Root { get; }
    public float BaseYaw { get; }
    public float SwingSign { get; }
    public float Phase { get; }
    public bool IsFront { get; }
    public float Angle { get; set; } = 0f;
    public float Lift { get; set; } = 0f;

    public Leg(SceneNode root, float baseYaw, float swingSign, float phase, bool isFront)
    {
        Root = root;
        BaseYaw = baseYaw;
        SwingSign = swingSign;
        Phase = phase;
        IsFront = isFront;
    }

    public void Apply()
    {
        Root.EulerAngles = new Vector3(0f, -Lift, BaseYaw + SwingSign * Angle);
    }
}
