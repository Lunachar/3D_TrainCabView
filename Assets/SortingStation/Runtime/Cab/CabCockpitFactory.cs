using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Builds the realistic driver's cab for the immersive 3D mode from procedural meshes and the
    /// CC0 PBR textures. Units are metres; the origin is the driver's eye, +Z looks through the
    /// windscreen and +Y is up.
    ///
    /// Object names follow what <see cref="CabInterior3DRenderer"/> animates: control anchors,
    /// "GaugeNeedle", "WiperArm3D_Left/Right", "CeilingCabinLamp3D", "SpeedScreen3D",
    /// "MessageScreen3D", "VigilanceButton3D", "KeychainCharm3D" and the toggle indicators
    /// ("Headlights3D", "CabinLight3D", ...). Every moving part sits under a mount whose local +Z
    /// points into its panel, so the renderer's press animation pushes it inwards.
    /// </summary>
    public static class CabCockpitFactory
    {
        public const string RootName = "CabCockpit3D";
        public const int ControlLayer = 30;
        // The whole cab sits a little ahead of and below the eye, so the desk is seen from a
        // comfortable seated distance instead of right under the driver's nose.
        public static readonly Vector3 SeatOffset = new Vector3(0f, -0.05f, 0.22f);

        // Cab envelope (metres from the driver's eye).
        private const float FloorY = -1.22f;
        private const float CeilingY = 0.66f;
        private const float HalfWidth = 1.42f;
        private const float RearZ = -1.1f;

        // Windscreen: the sill sits below the view line and the header just above it.
        private const float GlassHalfWidth = 1.02f;
        private const float SillY = -0.36f;
        private const float SillZ = 1.22f;
        private const float HeaderY = 0.40f;
        private const float HeaderZ = 1.04f;

        // Driver's desk: a gently rising surface from the front edge to the instrument hood.
        private const float DeskFrontY = -0.34f;
        private const float DeskFrontZ = 0.56f;
        private const float DeskBackY = -0.27f;
        private const float DeskBackZ = 0.96f;
        private const float DeskHalfWidth = 0.98f;
        private const float DeskTopThickness = 0.024f;
        private static readonly Quaternion DeskTilt = Quaternion.Euler(-10f, 0f, 0f);
        // Mount orientation for parts set into the desk: local +Z points down into the surface.
        private static readonly Quaternion IntoDesk = DeskTilt * Quaternion.Euler(90f, 0f, 0f);

        private struct Palette
        {
            public Material Paint;
            public Material Console;
            public Material DeskTop;
            public Material Steel;
            public Material DarkMetal;
            public Material Rubber;
            public Material Seat;
            public Material Leather;
            public Material Glass;
            public Material Black;
            public Material Dial;
            public Material Red;
            public Material Yellow;
            public Material Label;
        }

        public static GameObject Create(Transform parent)
        {
            GameObject root = new GameObject(RootName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = SeatOffset;
            Palette p = CreatePalette();

            BuildShell(root.transform, p);
            BuildWindscreen(root.transform, p);
            BuildDesk(root.transform, p);
            BuildInstrumentHood(root.transform, p);
            BuildControls(root.transform, p);
            BuildWipers(root.transform, p);
            BuildCeiling(root.transform, p);
            BuildKeychain(root.transform, p);
            BuildSeat(root.transform, p);
            SetLayerRecursively(root, ControlLayer);
            return root;
        }

        private static Palette CreatePalette()
        {
            return new Palette
            {
                Paint = CabPbrMaterials.Get("Plastic010", new Color(0.70f, 0.71f, 0.68f), 0.5f),
                Console = CabPbrMaterials.Get("Plastic010", new Color(0.42f, 0.45f, 0.48f), 0.45f),
                DeskTop = CabPbrMaterials.Get("Rubber004", new Color(0.62f, 0.64f, 0.66f), 0.5f),
                Steel = CabPbrMaterials.Get("Metal032", Color.white, 0.9f),
                DarkMetal = CabPbrMaterials.Get("Metal046A", new Color(0.55f, 0.57f, 0.60f), 0.8f),
                Rubber = CabPbrMaterials.Get("Rubber004", new Color(0.55f, 0.55f, 0.55f), 0.6f),
                Seat = CabPbrMaterials.Get("Fabric030", new Color(0.36f, 0.45f, 0.66f), 0.5f),
                Leather = CabPbrMaterials.Get("Leather026", new Color(0.55f, 0.55f, 0.58f), 0.8f),
                Glass = CabPbrMaterials.Glass("CabWindscreen", new Color(0.78f, 0.86f, 0.90f, 0.07f)),
                Black = CabPbrMaterials.Plain("CabBlackBezel", new Color(0.035f, 0.038f, 0.042f), 0.45f),
                Dial = CabPbrMaterials.Plain("CabGaugeDial", new Color(0.92f, 0.92f, 0.88f), 0.3f),
                Red = CabPbrMaterials.Plain("CabSafetyRed", new Color(0.78f, 0.06f, 0.04f), 0.55f),
                Yellow = CabPbrMaterials.Plain("CabSafetyYellow", new Color(0.95f, 0.72f, 0.08f), 0.5f),
                Label = CabPbrMaterials.Plain("CabLabelPlate", new Color(0.12f, 0.13f, 0.14f), 0.3f)
            };
        }

        private static void BuildShell(Transform root, Palette p)
        {
            Transform shell = Group(root, "CabShell");
            CabMeshBuilder floor = new CabMeshBuilder(0.8f);
            floor.AddBox(new Vector3(0f, FloorY - 0.03f, 0.1f), new Vector3(HalfWidth * 2f, 0.06f, 2.7f), Quaternion.identity);
            MeshPart(shell, "Floor", floor, p.Rubber);

            CabMeshBuilder walls = new CabMeshBuilder(1.2f);
            // Side walls with a window opening between the pillar and the door post.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * HalfWidth;
                walls.AddBox(new Vector3(x, (FloorY + -0.30f) * 0.5f, 0.1f), new Vector3(0.06f, -0.30f - FloorY, 2.7f), Quaternion.identity);
                walls.AddBox(new Vector3(x, (0.42f + CeilingY) * 0.5f, 0.1f), new Vector3(0.06f, CeilingY - 0.42f, 2.7f), Quaternion.identity);
                walls.AddBox(new Vector3(x, 0.06f, -0.72f), new Vector3(0.06f, 0.72f, 0.76f), Quaternion.identity);
                walls.AddBox(new Vector3(x, 0.06f, 1.34f), new Vector3(0.06f, 0.72f, 0.22f), Quaternion.identity);
            }
            walls.AddBox(new Vector3(0f, (FloorY + CeilingY) * 0.5f, RearZ), new Vector3(HalfWidth * 2f, CeilingY - FloorY, 0.06f), Quaternion.identity);
            MeshPart(shell, "Walls", walls, p.Paint);

            CabMeshBuilder sideGlass = new CabMeshBuilder();
            for (int side = -1; side <= 1; side += 2)
                sideGlass.AddBox(new Vector3(side * (HalfWidth + 0.01f), 0.06f, 0.43f), new Vector3(0.01f, 0.72f, 1.54f), Quaternion.identity);
            MeshPart(shell, "SideWindows", sideGlass, p.Glass, castShadows: false);

            // Front wall under the windscreen, behind the desk.
            CabMeshBuilder front = new CabMeshBuilder(1f);
            front.AddBox(new Vector3(0f, (FloorY + SillY) * 0.5f, SillZ + 0.12f), new Vector3(HalfWidth * 2f, SillY - FloorY, 0.08f), Quaternion.identity);
            MeshPart(shell, "FrontWall", front, p.Paint);
        }

        private static void BuildWindscreen(Transform root, Palette p)
        {
            Transform frame = Group(root, "CabShellAndWindow_Editable");
            Vector3 sillLeft = new Vector3(-GlassHalfWidth, SillY, SillZ);
            Vector3 sillRight = new Vector3(GlassHalfWidth, SillY, SillZ);
            Vector3 headLeft = new Vector3(-GlassHalfWidth, HeaderY, HeaderZ);
            Vector3 headRight = new Vector3(GlassHalfWidth, HeaderY, HeaderZ);

            CabMeshBuilder glass = new CabMeshBuilder();
            glass.AddQuad(sillLeft, sillRight, headRight, headLeft, Vector3.back);
            MeshPart(frame, "Windscreen", glass, p.Glass, castShadows: false);
            BuildWindscreenWeather(frame, sillLeft, sillRight, headRight, headLeft);

            CabMeshBuilder surround = new CabMeshBuilder(0.8f);
            Vector3 glassUp = (headLeft - sillLeft).normalized;
            Quaternion glassTilt = Quaternion.LookRotation(Vector3.Cross(Vector3.right, glassUp), glassUp);
            // Pillars follow the raked glass edges; the header and sill close the frame.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 bottom = new Vector3(side * (GlassHalfWidth + 0.09f), SillY, SillZ);
                Vector3 top = new Vector3(side * (GlassHalfWidth + 0.09f), HeaderY, HeaderZ);
                surround.AddChamferBox((bottom + top) * 0.5f, new Vector3(0.18f, Vector3.Distance(bottom, top) + 0.12f, 0.10f), 0.025f, glassTilt);
                // Wide corner post that joins the pillar to the side wall.
                surround.AddChamferBox(new Vector3(side * (HalfWidth - 0.2f), 0.08f, 1.22f), new Vector3(0.42f, 0.86f, 0.12f), 0.03f,
                    Quaternion.Euler(0f, side * 38f, 0f));
            }
            surround.AddChamferBox(new Vector3(0f, HeaderY + 0.09f, HeaderZ - 0.02f), new Vector3(GlassHalfWidth * 2f + 0.36f, 0.2f, 0.14f), 0.03f, glassTilt);
            surround.AddChamferBox(new Vector3(0f, SillY - 0.035f, SillZ - 0.02f), new Vector3(GlassHalfWidth * 2f + 0.36f, 0.07f, 0.16f), 0.02f, Quaternion.identity);
            MeshPart(frame, "WindscreenFrame", surround, p.Paint);

            // Sun visor folded against the header.
            CabMeshBuilder visor = new CabMeshBuilder(0.5f);
            visor.AddChamferBox(new Vector3(-0.48f, HeaderY + 0.02f, HeaderZ - 0.10f), new Vector3(0.62f, 0.14f, 0.02f), 0.008f,
                Quaternion.Euler(-62f, 0f, 0f));
            MeshPart(frame, "SunVisor", visor, p.Console);
        }

        private static void BuildDesk(Transform root, Palette p)
        {
            Transform desk = Group(root, "DriverDesk");
            // Side profile (y, z): desk surface, instrument hood base, front wall and knee recess.
            Vector2[] profile =
            {
                new Vector2(DeskFrontY, DeskFrontZ),
                new Vector2(DeskBackY, DeskBackZ),
                new Vector2(SillY - 0.05f, SillZ),
                new Vector2(FloorY, SillZ),
                new Vector2(FloorY, 0.95f),
                new Vector2(-0.72f, 0.95f),
                new Vector2(-0.72f, 0.62f),
                new Vector2(DeskFrontY - 0.07f, 0.52f)
            };
            CabMeshBuilder body = new CabMeshBuilder(0.7f);
            body.AddExtrudedProfile(profile, -DeskHalfWidth, DeskHalfWidth, Vector3.zero);
            MeshPart(desk, "DeskBody", body, p.Console);

            // Rubber-edged desk top slightly proud of the body.
            Vector3 deskCenter = DeskPlanePoint(0f, 0.5f) + DeskTilt * Vector3.up * (DeskTopThickness * 0.5f);
            CabMeshBuilder top = new CabMeshBuilder(0.6f);
            top.AddChamferBox(deskCenter, new Vector3(DeskHalfWidth * 2f - 0.04f, DeskTopThickness, DeskDepth() + 0.02f), 0.01f, DeskTilt);
            MeshPart(desk, "DeskTop", top, p.DeskTop);

            CabMeshBuilder edge = new CabMeshBuilder(0.4f);
            edge.AddCylinder(new Vector3(0f, DeskFrontY - 0.012f, DeskFrontZ - 0.01f), 0.028f, DeskHalfWidth * 2f, 16,
                Quaternion.Euler(0f, 0f, 90f));
            MeshPart(desk, "DeskArmRest", edge, p.Leather);

            // Foot rest in the knee recess.
            CabMeshBuilder footrest = new CabMeshBuilder(0.4f);
            footrest.AddChamferBox(new Vector3(0f, FloorY + 0.14f, 0.82f), new Vector3(0.9f, 0.04f, 0.3f), 0.01f, Quaternion.Euler(-22f, 0f, 0f));
            MeshPart(desk, "CabFootwellAndPedals_Editable", footrest, p.DarkMetal);
        }

        private static void BuildInstrumentHood(Transform root, Palette p)
        {
            Transform hood = Group(root, "InstrumentHood");
            // Binnacle rising from the back of the desk; its face leans back toward the driver.
            // Raised so the screens and dials sit just under the windscreen, above the desk
            // controls, where the driver actually looks.
            Quaternion faceTilt = Quaternion.Euler(-24f, 0f, 0f);
            Vector3 hoodCenter = new Vector3(0f, -0.21f, 1.04f);
            // The hood body sits lower than the instruments so the view over it is wider; the
            // screens and dials keep their place and stand up from it in their own bezels.
            Vector3 bodyCenter = hoodCenter + new Vector3(0f, -0.1f, 0.02f);
            CabMeshBuilder body = new CabMeshBuilder(0.6f);
            body.AddChamferBox(bodyCenter, new Vector3(1.66f, 0.25f, 0.2f), 0.03f, faceTilt);
            body.AddChamferBox(bodyCenter + new Vector3(0f, 0.13f, 0.05f), new Vector3(1.7f, 0.025f, 0.24f), 0.01f, Quaternion.Euler(-6f, 0f, 0f));
            MeshPart(hood, "HoodBody", body, p.Console);

            Vector3 faceNormal = faceTilt * Vector3.back;
            Vector3 facePoint = hoodCenter + faceNormal * 0.101f;
            Quaternion screenRotation = Quaternion.LookRotation(-faceNormal, faceTilt * Vector3.up);
            Vector3 upper = faceTilt * Vector3.up * 0.05f;
            // Two wide screens carry speed, traction, brake and journey details; the brake
            // percentage replaced the old centre dial.
            CreateScreen(hood, "SpeedScreen3D", facePoint + upper + new Vector3(-0.245f, 0f, 0f), screenRotation, new Vector2(0.44f, 0.15f), p, "000 km/h");
            CreateScreen(hood, "MessageScreen3D", facePoint + upper + new Vector3(0.245f, 0f, 0f), screenRotation, new Vector2(0.44f, 0.15f), p, "СИСТЕМА ГОТОВА");

            // Speed, traction and brake-pressure gauges at the outer ends of the hood.
            CreateGauge(hood, facePoint + upper + new Vector3(-0.72f, 0f, 0f), screenRotation, p, "СКОРОСТЬ");
            CreateGauge(hood, facePoint + upper + new Vector3(0.72f, 0f, 0f), screenRotation, p, "ТЯГА");
        }

        private static void BuildControls(Transform root, Palette p)
        {
            Transform controls = Group(root, "CabControls");
            // Row of illuminated rocker switches along the front of the desk.
            (CabControlAction action, string indicator, string caption, Color lamp)[] toggles =
            {
                (CabControlAction.Headlights, "Headlights3D", "ФАРЫ", new Color(0.30f, 0.62f, 1f)),
                (CabControlAction.CabinLight, "CabinLight3D", "СВЕТ", new Color(1f, 0.55f, 0.15f)),
                (CabControlAction.Wipers, "Wipers3D", "ДВОРН.", new Color(0.35f, 0.85f, 1f)),
                (CabControlAction.WindowHeater, "WindowHeater3D", "ОБОГРЕВ", new Color(1f, 0.48f, 0.12f)),
                (CabControlAction.Doors, "Doors3D", "ДВЕРИ", new Color(0.30f, 1f, 0.45f)),
                (CabControlAction.Radio, "Radio3D", "РАДИО", new Color(0.20f, 0.95f, 0.55f))
            };
            for (int i = 0; i < toggles.Length; i++)
            {
                float x = -0.425f + i * 0.17f;
                CreateRocker(controls, toggles[i].action, toggles[i].indicator, toggles[i].caption, x, 0.2f, p);
            }

            CreateLever(controls, CabControlAction.Brake, "BrakeLever3D", -0.56f, 0.62f, p, p.Red, "ТОРМОЗ");
            CreateLever(controls, CabControlAction.Throttle, "ThrottleLever3D", 0.56f, 0.62f, p, p.Black, "ТЯГА");

            CreatePushButton(controls, CabControlAction.Horn, "HornButton3D", -0.34f, 0.72f, 0.042f, p.Yellow, p, "ГУДОК");
            CreatePushButton(controls, CabControlAction.Bell, "BellButton3D", 0.34f, 0.72f, 0.042f, p.Steel, p, "ЗВОНОК");
            CreateVigilanceButton(controls, 0f, 0.76f, p);
            CreateRadioHandset(controls, p);
        }

        /// <summary>A layer just outside the glass that shows rain drops and snow (see CabWindscreenWeather).</summary>
        private static void BuildWindscreenWeather(Transform frame, Vector3 sillLeft, Vector3 sillRight, Vector3 headRight, Vector3 headLeft)
        {
            Vector3 up = headLeft - sillLeft;
            float height = up.magnitude;
            Vector3 outward = Vector3.Cross(sillRight - sillLeft, up).normalized;
            if (outward.z < 0f) outward = -outward;
            Vector3 lift = outward * 0.004f;
            Mesh mesh = new Mesh { name = "WindscreenWeather" };
            mesh.vertices = new[] { sillLeft + lift, sillRight + lift, headRight + lift, headLeft + lift };
            // UVs in metres on the glass: x across (matching the cab), y up the glass from the sill.
            mesh.uv = new[] { new Vector2(sillLeft.x, 0f), new Vector2(sillRight.x, 0f), new Vector2(headRight.x, height), new Vector2(headLeft.x, height) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GameObject layer = new GameObject("WindscreenWeather", typeof(MeshFilter), typeof(MeshRenderer));
            layer.transform.SetParent(frame, false);
            layer.GetComponent<MeshFilter>().sharedMesh = mesh;
            Material material = new Material(CabShaders.Windscreen) { name = "WindscreenWeather" };
            float pivotV = 0.03f / Mathf.Max(0.1f, up.normalized.y);
            material.SetVector("_PivotLeft", new Vector4(-0.46f, pivotV, 0f, 0f));
            material.SetVector("_PivotRight", new Vector4(0.46f, pivotV, 0f, 0f));
            MeshRenderer renderer = layer.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            layer.AddComponent<CabWindscreenWeather>().Configure(material);
        }

        private static void BuildWipers(Transform root, Palette p)
        {
            Transform wipers = Group(root, "WipersOnGlass_Editable");
            Vector3 glassUp = (new Vector3(0f, HeaderY, HeaderZ) - new Vector3(0f, SillY, SillZ)).normalized;
            Quaternion onGlass = Quaternion.LookRotation(Vector3.Cross(Vector3.right, glassUp), glassUp);
            for (int side = -1; side <= 1; side += 2)
            {
                string name = side < 0 ? "WiperArm3D_Left" : "WiperArm3D_Right";
                Transform pivot = new GameObject(name).transform;
                pivot.SetParent(wipers, false);
                pivot.localPosition = new Vector3(side * 0.46f, SillY + 0.03f, SillZ + 0.035f);
                // Parked arms lean toward the centre; the renderer sweeps them around local Z.
                pivot.localRotation = onGlass * Quaternion.Euler(0f, 0f, side * 86f);
                // Long heavy-duty wipers: a sprung arm and a 60 cm rubber blade on a frame.
                CabMeshBuilder arm = new CabMeshBuilder(0.3f);
                arm.AddChamferBox(new Vector3(0f, 0.3f, 0.016f), new Vector3(0.022f, 0.6f, 0.014f), 0.004f, Quaternion.identity);
                arm.AddChamferBox(new Vector3(0f, 0.38f, 0.03f), new Vector3(0.03f, 0.62f, 0.012f), 0.004f, Quaternion.identity);
                arm.AddChamferBox(new Vector3(0f, 0.38f, 0.008f), new Vector3(0.012f, 0.6f, 0.012f), 0.002f, Quaternion.identity);
                arm.AddCylinder(Vector3.zero, 0.032f, 0.045f, 14, Quaternion.Euler(90f, 0f, 0f));
                MeshPart(pivot, "WiperBlade", arm, p.Black, castShadows: false);
                CabMeshBuilder spring = new CabMeshBuilder(0.3f);
                spring.AddChamferBox(new Vector3(0f, 0.09f, 0.03f), new Vector3(0.018f, 0.16f, 0.018f), 0.004f, Quaternion.identity);
                spring.AddCylinder(new Vector3(0f, 0f, 0.02f), 0.018f, 0.03f, 10, Quaternion.Euler(90f, 0f, 0f));
                MeshPart(pivot, "WiperSpring", spring, p.Steel, castShadows: false);
            }
        }

        private static void BuildCeiling(Transform root, Palette p)
        {
            Transform ceiling = Group(root, "CabCeilingLighting_Editable");
            CabMeshBuilder panel = new CabMeshBuilder(1f);
            panel.AddBox(new Vector3(0f, CeilingY + 0.02f, 0.1f), new Vector3(HalfWidth * 2f, 0.04f, 2.7f), Quaternion.identity);
            MeshPart(ceiling, "CeilingPanel", panel, p.Paint);
            for (int i = 0; i < 2; i++)
            {
                CabMeshBuilder lamp = new CabMeshBuilder(0.3f);
                lamp.AddChamferBox(Vector3.zero, new Vector3(0.42f, 0.03f, 0.16f), 0.01f, Quaternion.identity);
                GameObject lampObject = MeshPart(ceiling, "CeilingCabinLamp3D_" + i, lamp,
                    CabPbrMaterials.Emissive("CeilingLamp", new Color(0.95f, 0.93f, 0.86f), Color.black), castShadows: false);
                lampObject.transform.localPosition = new Vector3(i == 0 ? -0.5f : 0.5f, CeilingY - 0.005f, 0.05f);
            }
        }

        private static void BuildKeychain(Transform root, Palette p)
        {
            // Hangs from the windscreen header; the renderer swings it around local Z.
            Transform charm = new GameObject("KeychainCharm3D").transform;
            charm.SetParent(root, false);
            charm.localPosition = new Vector3(0.62f, HeaderY - 0.02f, HeaderZ - 0.12f);
            charm.localScale = Vector3.one * 0.7f;
            CabMeshBuilder cord = new CabMeshBuilder(0.2f);
            cord.AddCylinder(new Vector3(0f, -0.06f, 0f), 0.002f, 0.12f, 6, Quaternion.identity);
            MeshPart(charm, "KeychainCord", cord, p.Black, castShadows: false);
            CabMeshBuilder body = new CabMeshBuilder(0.1f);
            body.AddChamferBox(new Vector3(0f, -0.145f, 0f), new Vector3(0.075f, 0.036f, 0.028f), 0.006f, Quaternion.identity);
            body.AddChamferBox(new Vector3(-0.018f, -0.118f, 0f), new Vector3(0.03f, 0.022f, 0.026f), 0.004f, Quaternion.identity);
            MeshPart(charm, "KeychainLocomotive", body, p.Red, castShadows: false);
            CabMeshBuilder wheels = new CabMeshBuilder(0.1f);
            for (int i = 0; i < 3; i++)
                wheels.AddCylinder(new Vector3(-0.024f + i * 0.024f, -0.166f, 0f), 0.009f, 0.032f, 10, Quaternion.Euler(90f, 0f, 0f));
            MeshPart(charm, "KeychainWheels", wheels, p.Black, castShadows: false);
        }

        private static void BuildSeat(Transform root, Palette p)
        {
            Transform seat = Group(root, "DriverSeat3D");
            CabMeshBuilder cushion = new CabMeshBuilder(0.5f);
            cushion.AddChamferBox(new Vector3(0f, -0.72f, -0.08f), new Vector3(0.56f, 0.12f, 0.52f), 0.04f, Quaternion.identity);
            MeshPart(seat, "SeatCushion", cushion, p.Seat);
            CabMeshBuilder armrests = new CabMeshBuilder(0.4f);
            for (int side = -1; side <= 1; side += 2)
                armrests.AddChamferBox(new Vector3(side * 0.34f, -0.50f, -0.02f), new Vector3(0.08f, 0.06f, 0.42f), 0.02f, Quaternion.identity);
            MeshPart(seat, "SeatArmrests", armrests, p.Leather);
        }

        private static void CreateRocker(Transform parent, CabControlAction action, string indicatorName, string caption,
            float x, float depth01, Palette p)
        {
            Vector3 surface = DeskPoint(x, depth01);
            Transform anchorObject = new GameObject(action + "Switch3D").transform;
            anchorObject.SetParent(parent, false);
            anchorObject.localPosition = surface;
            anchorObject.localRotation = IntoDesk;

            CabMeshBuilder housing = new CabMeshBuilder(0.2f);
            housing.AddChamferBox(new Vector3(0f, 0f, 0.004f), new Vector3(0.118f, 0.078f, 0.02f), 0.006f, Quaternion.identity);
            MeshPart(anchorObject, "SwitchHousing", housing, p.Black);

            Transform rocker = new GameObject("Rocker").transform;
            rocker.SetParent(anchorObject, false);
            rocker.localPosition = new Vector3(0f, 0f, -0.01f);
            CabMeshBuilder cap = new CabMeshBuilder(0.2f);
            cap.AddChamferBox(new Vector3(0f, 0f, -0.012f), new Vector3(0.09f, 0.056f, 0.026f), 0.008f, Quaternion.identity);
            MeshPart(rocker, "RockerCap", cap, p.DarkMetal);

            // Indicator lamp just behind the switch, facing the driver.
            CabMeshBuilder lamp = new CabMeshBuilder(0.1f);
            lamp.AddCylinder(Vector3.zero, 0.013f, 0.012f, 16, Quaternion.Euler(90f, 0f, 0f));
            GameObject lampObject = MeshPart(anchorObject, indicatorName, lamp,
                CabPbrMaterials.Emissive(indicatorName + "Lamp", new Color(0.14f, 0.16f, 0.18f), Color.black), castShadows: false);
            lampObject.transform.localPosition = new Vector3(0f, 0.062f, -0.008f);

            CreateCaption(anchorObject, caption, new Vector3(0f, -0.058f, -0.006f), 0.0022f);
            CreateIcon(rocker, action.ToString(), new Vector3(0f, 0f, -0.0262f), 0.05f);

            BoxCollider hit = anchorObject.gameObject.AddComponent<BoxCollider>();
            // Kept low over the desk so it never shadows the round buttons behind it.
            hit.center = new Vector3(0f, 0.004f, -0.014f);
            hit.size = new Vector3(0.13f, 0.13f, 0.045f);
            anchorObject.gameObject.AddComponent<Cab3DControlAnchor>().Configure(action, rocker);
        }

        private static void CreateLever(Transform parent, CabControlAction action, string name, float x, float depth01,
            Palette p, Material knob, string caption)
        {
            Vector3 surface = DeskPoint(x, depth01);
            Transform anchorObject = new GameObject(name).transform;
            anchorObject.SetParent(parent, false);
            anchorObject.localPosition = surface;
            anchorObject.localRotation = DeskTilt;

            CabMeshBuilder quadrant = new CabMeshBuilder(0.2f);
            quadrant.AddChamferBox(new Vector3(0f, 0.018f, 0f), new Vector3(0.14f, 0.036f, 0.3f), 0.012f, Quaternion.identity);
            MeshPart(anchorObject, "LeverQuadrant", quadrant, p.DarkMetal);
            CabMeshBuilder slot = new CabMeshBuilder(0.2f);
            slot.AddBox(new Vector3(0f, 0.037f, 0f), new Vector3(0.026f, 0.004f, 0.26f), Quaternion.identity);
            MeshPart(anchorObject, "LeverSlot", slot, p.Black, castShadows: false);

            // The renderer rotates this pivot around local X (forward/back through the slot).
            Transform pivot = new GameObject("LeverPivot").transform;
            pivot.SetParent(anchorObject, false);
            pivot.localPosition = new Vector3(0f, 0.03f, 0f);
            CabMeshBuilder shaft = new CabMeshBuilder(0.2f);
            shaft.AddCylinder(new Vector3(0f, 0.07f, 0f), 0.011f, 0.14f, 12, Quaternion.identity);
            MeshPart(pivot, "LeverShaft", shaft, p.Steel);
            CabMeshBuilder handle = new CabMeshBuilder(0.2f);
            handle.AddChamferBox(new Vector3(0f, 0.16f, 0f), new Vector3(0.13f, 0.05f, 0.06f), 0.02f, Quaternion.identity);
            MeshPart(pivot, "LeverHandle", handle, knob);

            CreateCaption(anchorObject, caption, new Vector3(0f, 0.04f, -0.17f), 0.0024f, Quaternion.Euler(90f, 0f, 0f));

            BoxCollider hit = anchorObject.gameObject.AddComponent<BoxCollider>();
            // Covers the handle and upper shaft: the part a child grabs, clear of the switch row.
            hit.center = new Vector3(0f, 0.135f, 0f);
            hit.size = new Vector3(0.16f, 0.14f, 0.2f);
            anchorObject.gameObject.AddComponent<Cab3DControlAnchor>().Configure(action, pivot);
        }

        private static void CreatePushButton(Transform parent, CabControlAction action, string name, float x, float depth01,
            float radius, Material cap, Palette p, string caption)
        {
            Transform anchorObject = new GameObject(name).transform;
            anchorObject.SetParent(parent, false);
            anchorObject.localPosition = DeskPoint(x, depth01);
            anchorObject.localRotation = IntoDesk;

            CabMeshBuilder collar = new CabMeshBuilder(0.2f);
            collar.AddCylinder(new Vector3(0f, 0f, 0.002f), radius + 0.014f, 0.014f, 24, Quaternion.Euler(90f, 0f, 0f));
            MeshPart(anchorObject, "ButtonCollar", collar, p.Steel);

            Transform plunger = new GameObject("ButtonCap").transform;
            plunger.SetParent(anchorObject, false);
            CabMeshBuilder capMesh = new CabMeshBuilder(0.2f);
            capMesh.AddCylinder(new Vector3(0f, 0f, -0.018f), radius, 0.03f, 24, Quaternion.Euler(90f, 0f, 0f));
            MeshPart(plunger, "Cap", capMesh, cap);
            CreateIcon(plunger, action.ToString(), new Vector3(0f, 0f, -0.0335f), radius * 1.25f);

            CreateCaption(anchorObject, caption, new Vector3(0f, -(radius + 0.03f), -0.006f), 0.0022f);

            BoxCollider hit = anchorObject.gameObject.AddComponent<BoxCollider>();
            hit.center = new Vector3(0f, 0f, -0.015f);
            hit.size = new Vector3(0.15f, 0.15f, 0.05f);
            anchorObject.gameObject.AddComponent<Cab3DControlAnchor>().Configure(action, plunger);
        }

        private static void CreateVigilanceButton(Transform parent, float x, float depth01, Palette p)
        {
            // The renderer moves this transform itself (it flashes and rises when a check is due),
            // so the anchor, the moving cap and the name are the same object.
            Transform mount = new GameObject("VigilanceMount").transform;
            mount.SetParent(parent, false);
            mount.localPosition = DeskPoint(x, depth01);
            mount.localRotation = IntoDesk;
            CabMeshBuilder collar = new CabMeshBuilder(0.2f);
            collar.AddCylinder(new Vector3(0f, 0f, 0.002f), 0.07f, 0.016f, 28, Quaternion.Euler(90f, 0f, 0f));
            MeshPart(mount, "VigilanceCollar", collar, p.Yellow);

            CabMeshBuilder cap = new CabMeshBuilder(0.2f);
            cap.AddCylinder(new Vector3(0f, 0f, -0.02f), 0.055f, 0.03f, 28, Quaternion.Euler(90f, 0f, 0f));
            GameObject button = MeshPart(mount, "VigilanceButton3D", cap,
                CabPbrMaterials.Emissive("VigilanceButton", new Color(0.72f, 0.05f, 0.03f), new Color(0.16f, 0.006f, 0.004f)));
            BoxCollider hit = button.AddComponent<BoxCollider>();
            hit.center = new Vector3(0f, 0f, -0.02f);
            hit.size = new Vector3(0.17f, 0.17f, 0.08f);
            button.AddComponent<Cab3DControlAnchor>().ConfigureDispatcherAcknowledgement(button.transform);
            CreateCaption(mount, "РБ", new Vector3(0f, -0.09f, -0.006f), 0.0026f);
        }

        private static void CreateRadioHandset(Transform parent, Palette p)
        {
            // Dispatcher radio: a handset hanging on the right corner post, turned toward the
            // driver, as in real cabs where the desk is full of controls.
            Vector3 position = new Vector3(0.78f, 0.2f, 1.0f);
            Vector3 fromEye = position + SeatOffset;
            Transform anchorObject = new GameObject("DispatcherRadioHandset3D").transform;
            anchorObject.SetParent(parent, false);
            anchorObject.localPosition = position;
            anchorObject.localRotation = Quaternion.Euler(0f, Mathf.Atan2(fromEye.x, fromEye.z) * Mathf.Rad2Deg, 0f);
            CabMeshBuilder cradle = new CabMeshBuilder(0.2f);
            cradle.AddChamferBox(new Vector3(0f, 0f, 0.02f), new Vector3(0.1f, 0.24f, 0.04f), 0.012f, Quaternion.identity);
            MeshPart(anchorObject, "RadioCradle", cradle, p.DarkMetal);
            Transform handset = new GameObject("Handset").transform;
            handset.SetParent(anchorObject, false);
            CabMeshBuilder receiver = new CabMeshBuilder(0.2f);
            receiver.AddChamferBox(new Vector3(0f, 0f, -0.012f), new Vector3(0.06f, 0.21f, 0.035f), 0.014f, Quaternion.identity);
            receiver.AddChamferBox(new Vector3(0f, 0.085f, -0.03f), new Vector3(0.07f, 0.05f, 0.03f), 0.012f, Quaternion.identity);
            receiver.AddChamferBox(new Vector3(0f, -0.085f, -0.03f), new Vector3(0.07f, 0.05f, 0.03f), 0.012f, Quaternion.identity);
            MeshPart(handset, "Receiver", receiver, p.Black);
            CreateCaption(anchorObject, "СВЯЗЬ", new Vector3(0f, -0.145f, -0.01f), 0.0022f);

            BoxCollider hit = anchorObject.gameObject.AddComponent<BoxCollider>();
            hit.center = new Vector3(0f, 0f, -0.02f);
            hit.size = new Vector3(0.14f, 0.26f, 0.05f);
            anchorObject.gameObject.AddComponent<Cab3DControlAnchor>().Configure(CabControlAction.DispatcherRadio, handset);
        }

        private static void CreateScreen(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 size,
            Palette p, string initialText)
        {
            CabMeshBuilder bezel = new CabMeshBuilder(0.2f);
            bezel.AddChamferBox(Vector3.zero, new Vector3(size.x + 0.03f, size.y + 0.03f, 0.016f), 0.005f, Quaternion.identity);
            // A shallow case behind the screen, since the screens now stand above the hood body.
            bezel.AddChamferBox(new Vector3(0f, -0.01f, 0.045f), new Vector3(size.x + 0.01f, size.y + 0.01f, 0.075f), 0.008f, Quaternion.identity);
            GameObject bezelObject = MeshPart(parent, name + "_Bezel", bezel, p.Black);
            bezelObject.transform.localPosition = position;
            bezelObject.transform.localRotation = rotation;

            CabMeshBuilder glass = new CabMeshBuilder(0.2f);
            glass.AddQuad(new Vector3(-size.x * 0.5f, -size.y * 0.5f, 0f), new Vector3(size.x * 0.5f, -size.y * 0.5f, 0f),
                new Vector3(size.x * 0.5f, size.y * 0.5f, 0f), new Vector3(-size.x * 0.5f, size.y * 0.5f, 0f), Vector3.back);
            GameObject screen = MeshPart(parent, name, glass,
                CabPbrMaterials.Emissive(name + "Glass", new Color(0.008f, 0.075f, 0.025f), new Color(0.02f, 0.2f, 0.06f)), castShadows: false);
            screen.transform.localPosition = position + rotation * new Vector3(0f, 0f, -0.0085f);
            screen.transform.localRotation = rotation;

            GameObject label = new GameObject(name + "_Readout", typeof(TextMesh));
            label.transform.SetParent(screen.transform, false);
            // Text hangs from the top edge: the lower part of the screen can sit behind the round
            // desk buttons when seen from the driver's seat.
            label.transform.localPosition = new Vector3(0f, size.y * 0.5f - 0.008f, -0.002f);
            TextMesh text = label.GetComponent<TextMesh>();
            text.text = initialText;
            text.anchor = TextAnchor.UpperCenter;
            text.alignment = TextAlignment.Center;
            // A large font size keeps the glyphs sharp; rich text sizes the lines.
            text.fontSize = 96;
            text.characterSize = 0.0024f;
            text.richText = true;
            text.lineSpacing = 0.95f;
            text.color = new Color(0.36f, 1f, 0.52f);
        }

        private static void CreateGauge(Transform parent, Vector3 position, Quaternion rotation, Palette p, string caption)
        {
            Transform gauge = new GameObject("Gauge_" + caption).transform;
            gauge.SetParent(parent, false);
            gauge.localPosition = position;
            gauge.localRotation = rotation;
            CabMeshBuilder bezel = new CabMeshBuilder(0.2f);
            bezel.AddCylinder(new Vector3(0f, 0f, 0.002f), 0.076f, 0.018f, 32, Quaternion.Euler(90f, 0f, 0f));
            MeshPart(gauge, "GaugeBezel", bezel, p.Steel);
            CabMeshBuilder dial = new CabMeshBuilder(0.2f);
            dial.AddDisc(new Vector3(0f, 0f, -0.0075f), 0.066f, 32, Vector3.back);
            MeshPart(gauge, "GaugeDial", dial, p.Dial, castShadows: false);

            // The renderer turns the needle around its local Z, which faces the driver here.
            Transform needle = new GameObject("GaugeNeedle").transform;
            needle.SetParent(gauge, false);
            needle.localPosition = new Vector3(0f, 0f, -0.009f);
            CabMeshBuilder needleMesh = new CabMeshBuilder(0.1f);
            needleMesh.AddBox(new Vector3(0f, 0.026f, 0f), new Vector3(0.005f, 0.056f, 0.0015f), Quaternion.identity);
            needleMesh.AddCylinder(Vector3.zero, 0.006f, 0.003f, 12, Quaternion.Euler(90f, 0f, 0f));
            MeshPart(needle, "Needle", needleMesh, p.Red, castShadows: false);
            CreateCaption(gauge, caption, new Vector3(0f, -0.036f, -0.009f), 0.002f, Quaternion.identity, new Color(0.1f, 0.1f, 0.1f));
        }

        private static readonly System.Collections.Generic.Dictionary<string, Material> IconMaterials =
            new System.Collections.Generic.Dictionary<string, Material>();

        /// <summary>
        /// White pictogram (Resources/Pbr/Icon_&lt;action&gt;) on the face of a control, facing out of
        /// its panel (local -Z), so each switch is recognisable without reading its caption.
        /// </summary>
        private static void CreateIcon(Transform parent, string iconName, Vector3 localPosition, float size)
        {
            if (!IconMaterials.TryGetValue(iconName, out Material material) || material == null)
            {
                Texture2D texture = Resources.Load<Texture2D>("Pbr/Icon_" + iconName);
                if (texture == null) return;
                material = new Material(CabShaders.UnlitTransparent) { name = "Icon_" + iconName, mainTexture = texture };
                IconMaterials[iconName] = material;
            }
            float h = size * 0.5f;
            Mesh mesh = new Mesh { name = "Icon_" + iconName };
            mesh.vertices = new[] { new Vector3(-h, -h, 0f), new Vector3(h, -h, 0f), new Vector3(h, h, 0f), new Vector3(-h, h, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            GameObject icon = new GameObject("Icon", typeof(MeshFilter), typeof(MeshRenderer));
            icon.transform.SetParent(parent, false);
            icon.transform.localPosition = localPosition;
            icon.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = icon.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void CreateCaption(Transform parent, string caption, Vector3 localPosition, float characterSize)
        {
            CreateCaption(parent, caption, localPosition, characterSize, Quaternion.identity, new Color(0.92f, 0.94f, 0.9f));
        }

        private static void CreateCaption(Transform parent, string caption, Vector3 localPosition, float characterSize,
            Quaternion localRotation)
        {
            CreateCaption(parent, caption, localPosition, characterSize, localRotation, new Color(0.92f, 0.94f, 0.9f));
        }

        private static void CreateCaption(Transform parent, string caption, Vector3 localPosition, float characterSize,
            Quaternion localRotation, Color color)
        {
            GameObject label = new GameObject("Caption", typeof(TextMesh));
            label.transform.SetParent(parent, false);
            label.transform.localPosition = localPosition;
            label.transform.localRotation = localRotation;
            TextMesh text = label.GetComponent<TextMesh>();
            text.text = caption;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = color;
        }

        private static float DeskDepth() => Vector2.Distance(new Vector2(DeskFrontY, DeskFrontZ), new Vector2(DeskBackY, DeskBackZ));

        /// <summary>Point on the desk surface: x across, depth01 from the front edge (0) to the hood (1).</summary>
        private static Vector3 DeskPoint(float x, float depth01)
        {
            return DeskPlanePoint(x, depth01) + DeskTilt * Vector3.up * DeskTopThickness;
        }

        /// <summary>Point on the desk body below the rubber top (the top is <see cref="DeskTopThickness"/> thick).</summary>
        private static Vector3 DeskPlanePoint(float x, float depth01)
        {
            return new Vector3(x, Mathf.Lerp(DeskFrontY, DeskBackY, depth01), Mathf.Lerp(DeskFrontZ, DeskBackZ, depth01));
        }

        private static Transform Group(Transform parent, string name)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static GameObject MeshPart(Transform parent, string name, CabMeshBuilder builder, Material material, bool castShadows = true)
        {
            GameObject part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = builder.Build(name);
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return part;
        }

        private static void SetLayerRecursively(GameObject value, int layer)
        {
            value.layer = layer;
            foreach (Transform child in value.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
