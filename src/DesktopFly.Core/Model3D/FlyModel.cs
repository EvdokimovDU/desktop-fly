using System.Numerics;
using DesktopFly.Core.Behavior;

namespace DesktopFly.Core.Model3D;

public class FlyModel
{
    public const float FlyScale = 1.15f;

    public SceneNode Root { get; }
    public Leg[] Legs { get; }
    public SceneNode FoldedWings { get; }
    public SceneNode BlurWingL { get; }
    public SceneNode BlurWingR { get; }
    public SceneNode Abdomen { get; }

    public FlyModel(SceneNode root, Leg[] legs, SceneNode foldedWings, SceneNode blurWingL, SceneNode blurWingR, SceneNode abdomen)
    {
        Root = root;
        Legs = legs;
        FoldedWings = foldedWings;
        BlurWingL = blurWingL;
        BlurWingR = blurWingR;
        Abdomen = abdomen;
    }

    private static Material Mat(Vector4 color, float specular = 0.25f, float shininess = 0.25f)
    {
        return new Material
        {
            Diffuse = color,
            Specular = new Vector4(specular, specular, specular, 1f),
            Shininess = shininess
        };
    }

    private static Leg BuildLeg(Vector3 attach, float baseYaw, float swingSign, float phase,
                                bool isFront, float femur, float tibia, float tarsus)
    {
        var legColor = new Vector4(0.33f, 0.24f, 0.14f, 1f);
        var root = new SceneNode { Position = attach };

        var femurGeo = FlyGeometry.CreateCapsule(0.48f, femur);
        var femurNode = new SceneNode
        {
            Geometry = femurGeo,
            Material = Mat(legColor),
            EulerAngles = new Vector3(0, 0, -MathF.PI / 2),
            Position = new Vector3(femur / 2, 0, 0)
        };
        root.AddChildNode(femurNode);

        var knee = new SceneNode
        {
            Position = new Vector3(femur, 0, 0),
            EulerAngles = new Vector3(0, 0.75f, -0.30f * swingSign)
        };
        root.AddChildNode(knee);

        var tibiaGeo = FlyGeometry.CreateCapsule(0.38f, tibia);
        var tibiaNode = new SceneNode
        {
            Geometry = tibiaGeo,
            Material = Mat(legColor),
            EulerAngles = new Vector3(0, 0, -MathF.PI / 2),
            Position = new Vector3(tibia / 2, 0, 0)
        };
        knee.AddChildNode(tibiaNode);

        var ankle = new SceneNode
        {
            Position = new Vector3(tibia, 0, 0),
            EulerAngles = new Vector3(0, 0.35f, -0.15f * swingSign)
        };
        knee.AddChildNode(ankle);

        var tarsusColor = new Vector4(legColor.X * 0.75f, legColor.Y * 0.75f, legColor.Z * 0.75f, 1f);
        var tarsusGeo = FlyGeometry.CreateCapsule(0.24f, tarsus);
        var tarsusNode = new SceneNode
        {
            Geometry = tarsusGeo,
            Material = Mat(tarsusColor),
            EulerAngles = new Vector3(0, 0, -MathF.PI / 2),
            Position = new Vector3(tarsus / 2, 0, 0)
        };
        ankle.AddChildNode(tarsusNode);

        var leg = new Leg(root, baseYaw, swingSign, phase, isFront);
        leg.Apply();
        return leg;
    }

    public static FlyModel Create()
    {
        var root = new SceneNode
        {
            Scale = new Vector3(FlyScale, FlyScale, FlyScale)
        };

        var bodyBrown = new Vector4(0.50f, 0.38f, 0.22f, 1f);

        // Thorax
        var thoraxGeo = FlyGeometry.CreateSphere(4.6f);
        var thorax = new SceneNode
        {
            Geometry = thoraxGeo,
            Material = Mat(bodyBrown, specular: 0.35f, shininess: 0.4f),
            Position = new Vector3(0, 2.5f, 6.2f),
            Scale = new Vector3(0.95f, 1.15f, 0.85f)
        };
        root.AddChildNode(thorax);

        // Abdomen
        var (texBytes, texW, texH) = AbdomenTexture.Generate();
        var abdMat = new Material
        {
            Diffuse = Vector4.One,
            Specular = new Vector4(0.3f, 0.3f, 0.3f, 1f),
            Shininess = 0.35f,
            TextureRgba = texBytes,
            TextureWidth = texW,
            TextureHeight = texH
        };
        var abdGeo = FlyGeometry.CreateSphere(5.0f);
        var abdomen = new SceneNode
        {
            Geometry = abdGeo,
            Material = abdMat,
            Position = new Vector3(0, -6.5f, 5.6f),
            Scale = new Vector3(0.9f, 1.5f, 0.75f)
        };
        root.AddChildNode(abdomen);

        // Head
        var headBrown = new Vector4(bodyBrown.X * 0.85f + 0.15f, bodyBrown.Y * 0.85f + 0.15f, bodyBrown.Z * 0.85f + 0.15f, 1f);
        var headGeo = FlyGeometry.CreateSphere(3.0f);
        var head = new SceneNode
        {
            Geometry = headGeo,
            Material = Mat(headBrown),
            Position = new Vector3(0, 9.0f, 6.0f),
            Scale = new Vector3(1.0f, 0.85f, 0.9f)
        };
        root.AddChildNode(head);

        // Eyes
        var eyeGeo = FlyGeometry.CreateSphere(2.0f);
        var eyeMat = Mat(new Vector4(0.62f, 0.10f, 0.07f, 1f), specular: 0.9f, shininess: 0.9f);
        foreach (float side in new[] { -1f, 1f })
        {
            var eye = new SceneNode
            {
                Geometry = eyeGeo,
                Material = eyeMat,
                Position = new Vector3(side * 2.1f, 9.7f, 6.4f),
                Scale = new Vector3(0.8f, 1.0f, 1.15f)
            };
            root.AddChildNode(eye);
        }

        // Antennae
        var antGeo = FlyGeometry.CreateCapsule(0.16f, 2.2f);
        var antMat = Mat(new Vector4(0.3f, 0.22f, 0.13f, 1f));
        foreach (float side in new[] { -1f, 1f })
        {
            var ant = new SceneNode
            {
                Geometry = antGeo,
                Material = antMat,
                Position = new Vector3(side * 0.9f, 11.6f, 6.3f),
                EulerAngles = new Vector3(-1.15f, 0, side * 0.35f)
            };
            root.AddChildNode(ant);
        }

        // Proboscis
        var probGeo = FlyGeometry.CreateCone(0.6f, 0.22f, 2.4f);
        var probMat = Mat(new Vector4(0.35f, 0.26f, 0.16f, 1f));
        var prob = new SceneNode
        {
            Geometry = probGeo,
            Material = probMat,
            Position = new Vector3(0, 10.4f, 4.6f),
            EulerAngles = new Vector3(-0.5f, 0, 0)
        };
        root.AddChildNode(prob);

        // 6 Legs
        var legs = new List<Leg>();
        const float z = 4.5f;
        var specs = new (float Side, Vector3 Attach, float YawOff, float Phase, bool IsFront, float Femur, float Tibia, float Tarsus)[]
        {
            ( 1f, new Vector3( 3.1f,  5.3f, z),  0.95f, 0.0f, true,  4.2f,  4.8f, 3.2f),
            (-1f, new Vector3(-3.1f,  5.3f, z),  0.95f, 0.5f, true,  4.2f,  4.8f, 3.2f),
            ( 1f, new Vector3( 3.7f,  2.0f, z), -0.10f, 0.5f, false, 4.8f,  5.6f, 3.8f),
            (-1f, new Vector3(-3.7f,  2.0f, z), -0.10f, 0.0f, false, 4.8f,  5.6f, 3.8f),
            ( 1f, new Vector3( 3.3f, -1.2f, z), -0.95f, 0.0f, false, 5.8f,  7.0f, 4.6f),
            (-1f, new Vector3(-3.3f, -1.2f, z), -0.95f, 0.5f, false, 5.8f,  7.0f, 4.6f)
        };

        foreach (var (side, attach, yawOff, phase, isFront, f, t, ta) in specs)
        {
            float baseYaw = side > 0 ? yawOff : (MathF.PI - yawOff);
            var leg = BuildLeg(attach, baseYaw, side, phase, isFront, f, t, ta);
            root.AddChildNode(leg.Root);
            legs.Add(leg);
        }

        // Folded Wings
        var foldedWings = new SceneNode();
        var wingGeo = FlyGeometry.CreateWingShape();
        var wingMat = new Material
        {
            Diffuse = new Vector4(0.92f, 0.92f, 0.92f, 0.28f),
            Specular = new Vector4(0.9f, 0.9f, 0.9f, 1f),
            Shininess = 0.9f,
            IsDoubleSided = true
        };

        foreach (float side in new[] { -1f, 1f })
        {
            var wing = new SceneNode
            {
                Geometry = wingGeo,
                Material = wingMat,
                Position = new Vector3(side * 1.6f, 0.5f, side > 0 ? 7.7f : 7.55f),
                EulerAngles = new Vector3(0, 0, side * 0.13f)
            };
            foldedWings.AddChildNode(wing);
        }
        root.AddChildNode(foldedWings);

        // Motion-blur wing discs for flight
        SceneNode CreateBlurWing(float side)
        {
            var g = FlyGeometry.CreateSphere(1.0f);
            var m = new Material
            {
                Diffuse = new Vector4(0.85f, 0.85f, 0.85f, 0.30f),
                IsDoubleSided = true,
                BlendMode = BlendMode.Alpha
            };
            return new SceneNode
            {
                Geometry = g,
                Material = m,
                Position = new Vector3(side * 6.0f, 1.5f, 8.2f),
                Scale = new Vector3(5.5f, 2.4f, 0.3f),
                EulerAngles = new Vector3(0, 0, side * -0.45f),
                IsHidden = true
            };
        }

        var bl = CreateBlurWing(-1f);
        var br = CreateBlurWing(1f);
        root.AddChildNode(bl);
        root.AddChildNode(br);

        return new FlyModel(root, legs.ToArray(), foldedWings, bl, br, abdomen);
    }
}
