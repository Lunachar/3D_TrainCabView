using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// First, hand-editable slice of the 3D cab. It intentionally does not replace the trusted
    /// 2D accessibility surface yet: every prop is named and mapped to the same action.
    /// </summary>
    public static class CabInterior3DPrototypeFactory
    {
        public static GameObject Create(Transform parent = null)
        {
            GameObject root = new GameObject("CabInterior3D_Editable");
            root.transform.SetParent(parent, false);

            Material trim = Material("CabTrim", new Color(0.075f, 0.085f, 0.09f), 0.66f, 0.31f);
            Texture2D trimTexture = Resources.Load<Texture2D>("Cab3D/CabMetalTrim_v1");
            if (trimTexture != null)
            {
                trim.mainTexture = trimTexture;
                // Standard shader multiplies albedo texture by material color.  Keeping the old
                // near-black tint here made the photoreal trim almost invisible in the preview.
                trim.color = Color.white;
                // Keep one calm material pass per movable panel; repeating the generated trim
                // would make its subtle horizontal seams look like an unintended stripe pattern.
                trim.mainTextureScale = Vector2.one;
            }
            Material black = Material("CabRubber", new Color(0.015f, 0.018f, 0.02f), 0.15f, 0.16f);
            Material steel = Material("CabSteel", new Color(0.22f, 0.25f, 0.27f), 0.88f, 0.48f);
            Material screen = Material("CabScreenGreen", new Color(0.02f, 0.17f, 0.05f), 0.15f, 0.30f);
            Material red = Material("CabSafetyRed", new Color(0.63f, 0.035f, 0.025f), 0.35f, 0.44f);
            Material blue = Material("CabIndicatorBlue", new Color(0.04f, 0.34f, 0.82f), 0.55f, 0.42f);
            Material amber = Material("CabBacklitAmber", new Color(0.84f, 0.36f, 0.045f), 0.25f, 0.34f);
            Material glass = Material("CabInstrumentGlass", new Color(0.025f, 0.09f, 0.11f), 0.72f, 0.82f);

            Transform shellGroup = Group(root.transform, "CabShellAndWindow_Editable");
            Transform screenGroup = Group(root.transform, "ScreensAndGauges_Editable");
            Transform leverGroup = Group(root.transform, "PhysicalLevers_Editable");
            Transform buttonGroup = Group(root.transform, "ButtonsAndSwitches_Editable");
            Transform wiperGroup = Group(root.transform, "WipersOnGlass_Editable");
            Transform seatingGroup = Group(root.transform, "CabSeatingAndFittings_Editable");
            Transform lightingGroup = Group(root.transform, "CabCeilingLighting_Editable");
            Transform footwellGroup = Group(root.transform, "CabFootwellAndPedals_Editable");
            CreateCabShell(shellGroup, trim, black, steel);
            CreateCabSeatingAndFittings(seatingGroup, trim, black, steel);
            CreateCabCeilingLighting(lightingGroup, trim, steel);
            CreateCabFootwellAndPedals(footwellGroup, trim, black, steel);
            CreateCabKeychain(shellGroup, steel, black, amber);
            CreatePhysicalWipers(wiperGroup, steel, black);

            GameObject console = Primitive(root.transform, PrimitiveType.Cube, "PhotorealConsolePanel", new Vector3(0f, -0.25f, 1.1f),
                new Vector3(18f, 7.2f, 0.72f), trim);
            Texture2D panelTexture = Resources.Load<Texture2D>("Cab3D/CabConsolePanel_v2");
            if (panelTexture != null)
            {
                Material panelMaterial = new Material(trim) { name = "CabConsolePhotorealTexture", mainTexture = panelTexture };
                panelMaterial.mainTextureScale = Vector2.one;
                console.GetComponent<Renderer>().sharedMaterial = panelMaterial;
            }

            // A shallow layered dashboard keeps the photo panel while giving the controls a real
            // depth hierarchy: lower shelf, recessed screens and a small instrument brow.
            Primitive(root.transform, PrimitiveType.Cube, "DashboardLowerShelf", new Vector3(0f, -3.18f, 0.96f),
                new Vector3(18.6f, 0.46f, 1.28f), black);
            Primitive(root.transform, PrimitiveType.Cube, "DashboardInstrumentBrow", new Vector3(0f, 2.88f, 0.79f),
                new Vector3(17.6f, 0.36f, 1.04f), black);
            Primitive(root.transform, PrimitiveType.Cube, "DashboardLeftCheek", new Vector3(-8.05f, -0.10f, 0.76f),
                new Vector3(1.35f, 5.5f, 0.86f), black);
            Primitive(root.transform, PrimitiveType.Cube, "DashboardRightCheek", new Vector3(8.05f, -0.10f, 0.76f),
                new Vector3(1.35f, 5.5f, 0.86f), black);

            CreateScreen(screenGroup, "SpeedScreen3D", new Vector3(2.15f, 1.20f, 0.68f), new Vector3(3.6f, 1.85f, 0.08f), screen);
            CreateScreen(screenGroup, "MessageScreen3D", new Vector3(-2.25f, 1.20f, 0.68f), new Vector3(3.2f, 1.65f, 0.08f), screen);
            CreateGaugeCluster(screenGroup, "LeftGaugeCluster", new Vector3(-6.65f, 0.85f, 0.70f), steel, glass, amber);
            CreateGaugeCluster(screenGroup, "RightGaugeCluster", new Vector3(6.65f, 0.85f, 0.70f), steel, glass, amber);

            CreateLever(leverGroup, "ThrottleLever3D", new Vector3(-5.35f, -1.75f, 0.58f), CabControlAction.Throttle, steel, black);
            CreateLever(leverGroup, "BrakeLever3D", new Vector3(5.35f, -1.75f, 0.58f), CabControlAction.Brake, steel, black);
            CreateRoundControl(buttonGroup, "Horn3D", new Vector3(-0.95f, -2.10f, 0.62f), CabControlAction.Horn, blue, 0.44f);
            CreateRoundControl(buttonGroup, "Bell3D", new Vector3(-2.20f, -2.10f, 0.62f), CabControlAction.Bell, steel, 0.34f);
            CreateRoundControl(buttonGroup, "Radio3D", new Vector3(0.28f, -2.10f, 0.62f), CabControlAction.Radio, blue, 0.36f);
            CreateRoundControl(buttonGroup, "CabinLight3D", new Vector3(1.48f, -2.10f, 0.62f), CabControlAction.CabinLight, steel, 0.34f);
            CreateRoundControl(buttonGroup, "Wipers3D", new Vector3(2.65f, -2.10f, 0.62f), CabControlAction.Wipers, steel, 0.34f);
            CreateRoundControl(buttonGroup, "Headlights3D", new Vector3(3.82f, -2.10f, 0.62f), CabControlAction.Headlights, blue, 0.34f);
            CreateRoundControl(buttonGroup, "Doors3D", new Vector3(4.85f, -2.10f, 0.62f), CabControlAction.Doors, steel, 0.34f);
            CreateRoundControl(buttonGroup, "WindowHeater3D", new Vector3(0.10f, -3.00f, 0.62f), CabControlAction.WindowHeater, steel, 0.32f);
            CreateToggleSwitch(buttonGroup, "DoorToggle3D", new Vector3(5.92f, -2.10f, 0.62f), CabControlAction.Doors, steel, blue);
            CreateToggleSwitch(buttonGroup, "WiperToggle3D", new Vector3(2.65f, -2.78f, 0.62f), CabControlAction.Wipers, steel, amber);
            CreateBacklitLabel(buttonGroup, "HornPlate", new Vector3(-0.95f, -2.72f, 0.59f), "ГУДОК", steel);
            CreateBacklitLabel(buttonGroup, "BellPlate", new Vector3(-2.20f, -2.72f, 0.59f), "ЗВОНОК", steel);
            CreateBacklitLabel(buttonGroup, "RadioPlate", new Vector3(0.28f, -2.72f, 0.59f), "РАДИО", steel);
            CreateBacklitLabel(buttonGroup, "CabinLightPlate", new Vector3(1.48f, -2.72f, 0.59f), "СВЕТ", steel);
            CreateBacklitLabel(buttonGroup, "WipersPlate", new Vector3(2.65f, -1.58f, 0.59f), "ДВОРНИКИ", steel);
            CreateBacklitLabel(buttonGroup, "HeadlightsPlate", new Vector3(3.82f, -2.72f, 0.59f), "ФАРЫ", steel);
            CreateBacklitLabel(buttonGroup, "DoorsPlate", new Vector3(4.85f, -2.72f, 0.59f), "ДВЕРИ", steel);

            GameObject vigilanceRing = Primitive(buttonGroup, PrimitiveType.Cylinder, "VigilanceButton3D_Ring", new Vector3(6.95f, -2.25f, 0.70f),
                new Vector3(0.84f, 0.13f, 0.84f), steel);
            vigilanceRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject vigilance = Primitive(buttonGroup, PrimitiveType.Cylinder, "VigilanceButton3D", new Vector3(6.95f, -2.25f, 0.58f),
                new Vector3(0.66f, 0.18f, 0.66f), red);
            vigilance.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            vigilance.AddComponent<Cab3DControlAnchor>().ConfigureDispatcherAcknowledgement(vigilance.transform);

            return root;
        }

        private static Transform Group(Transform parent, string name)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void CreateCabShell(Transform root, Material trim, Material black, Material steel)
        {
            // This shallow enclosure is intentionally made from named, independently movable parts.
            // It makes the prototype read as a cabin in the Scene view now, and can later replace
            // the photographic frame in the ride without changing the control mappings.
            GameObject shell = new GameObject("CabShell_Editable");
            shell.transform.SetParent(root, false);
            Primitive(shell.transform, PrimitiveType.Cube, "WindshieldTopFrame", new Vector3(0f, 5.15f, 1.85f),
                new Vector3(21.6f, 0.52f, 0.86f), black);
            GameObject leftPillar = Primitive(shell.transform, PrimitiveType.Cube, "WindshieldLeftPillar", new Vector3(-9.72f, 0.86f, 1.74f),
                new Vector3(0.72f, 8.25f, 0.92f), trim);
            leftPillar.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
            GameObject rightPillar = Primitive(shell.transform, PrimitiveType.Cube, "WindshieldRightPillar", new Vector3(9.72f, 0.86f, 1.74f),
                new Vector3(0.72f, 8.25f, 0.92f), trim);
            rightPillar.transform.localRotation = Quaternion.Euler(0f, 0f, 10f);
            Primitive(shell.transform, PrimitiveType.Cube, "WindshieldLowerSill", new Vector3(0f, -3.55f, 1.55f),
                new Vector3(20.4f, 0.56f, 1.12f), black);

            GameObject leftWing = Primitive(shell.transform, PrimitiveType.Cube, "LeftDashboardWing", new Vector3(-9.45f, -1.05f, 1.03f),
                new Vector3(2.55f, 4.72f, 1.14f), trim);
            leftWing.transform.localRotation = Quaternion.Euler(0f, -12f, 0f);
            GameObject rightWing = Primitive(shell.transform, PrimitiveType.Cube, "RightDashboardWing", new Vector3(9.45f, -1.05f, 1.03f),
                new Vector3(2.55f, 4.72f, 1.14f), trim);
            rightWing.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);

            // Shallow bezels and mounting bolts add physical scale under the soft cabin light.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 3; row++)
                {
                    GameObject bolt = Primitive(shell.transform, PrimitiveType.Sphere, "CabPanelBolt", new Vector3(side * 8.9f, 3.9f - row * 3.15f, 1.34f),
                        Vector3.one * 0.14f, steel);
                    bolt.transform.localScale = new Vector3(0.16f, 0.16f, 0.07f);
                }
            }
            Primitive(shell.transform, PrimitiveType.Cube, "CenterWindscreenDivider", new Vector3(0f, 3.42f, 1.70f),
                new Vector3(0.23f, 3.28f, 0.24f), black);
        }

        private static void CreateCabSeatingAndFittings(Transform root, Material trim, Material black, Material steel)
        {
            Material fabric = Material("CabSeatBlueFabric", new Color(0.022f, 0.10f, 0.24f), 0.04f, 0.46f);
            Texture2D upholstery = Resources.Load<Texture2D>("Cab3D/CabSeatUpholstery_v2");
            if (upholstery != null)
            {
                fabric.mainTexture = upholstery;
                fabric.mainTextureScale = new Vector2(2.2f, 2.2f);
            }
            CreateSeat(root, "DriverSeat3D", -1f, fabric, black, steel);
            CreateSeat(root, "CompanionSeat3D", 1f, fabric, black, steel);

            // The two shallow visors and the gooseneck microphone make the silhouette read as a
            // cab even before the photographic side trim has been fully retired.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject visor = Primitive(root, PrimitiveType.Cube, "SunVisor3D", new Vector3(side * 4.65f, 4.35f, 1.44f),
                    new Vector3(5.25f, 0.72f, 0.10f), black);
                visor.transform.localRotation = Quaternion.Euler(7f, 0f, side * 1.5f);
                Primitive(visor.transform, PrimitiveType.Cylinder, "VisorHinge", new Vector3(0f, 0.42f, 0.04f),
                    new Vector3(0.09f, 2.2f, 0.09f), steel).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            GameObject microphone = new GameObject("CabMicrophone3D");
            microphone.transform.SetParent(root, false);
            microphone.transform.localPosition = new Vector3(0.62f, 1.35f, 1.46f);
            microphone.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            Primitive(microphone.transform, PrimitiveType.Cylinder, "MicrophoneGooseneck", new Vector3(0f, 0.72f, 0f),
                new Vector3(0.055f, 0.78f, 0.055f), black);
            Primitive(microphone.transform, PrimitiveType.Sphere, "MicrophoneHead", new Vector3(-0.13f, 1.45f, -0.02f),
                new Vector3(0.30f, 0.20f, 0.22f), steel);
            Primitive(microphone.transform, PrimitiveType.Cylinder, "MicrophoneBase", new Vector3(0f, 0.06f, 0f),
                new Vector3(0.22f, 0.07f, 0.22f), trim).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void CreateCabCeilingLighting(Transform root, Material trim, Material steel)
        {
            // Named fixtures are deliberately separate scene objects: their placement can be
            // matched to a future scanned cab model and their emission follows the cab-light
            // control in CabInterior3DRenderer.
            Material diffuser = Material("CabCeilingDiffuser", new Color(1f, 0.48f, 0.15f), 0.02f, 0.55f);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject housing = Primitive(root, PrimitiveType.Cube, "CeilingLampHousing3D", new Vector3(side * 4.35f, 4.72f, 1.23f),
                    new Vector3(3.15f, 0.40f, 0.22f), trim);
                Primitive(housing.transform, PrimitiveType.Cube, "CeilingLampBezel3D", new Vector3(0f, -0.05f, -0.13f),
                    new Vector3(2.76f, 0.19f, 0.055f), steel);
                Primitive(housing.transform, PrimitiveType.Cube, "CeilingCabinLamp3D", new Vector3(0f, -0.05f, -0.17f),
                    new Vector3(2.45f, 0.10f, 0.04f), diffuser);
            }
        }

        private static void CreateCabFootwellAndPedals(Transform root, Material trim, Material black, Material steel)
        {
            // The floor and pedals give the cab a believable lower depth plane. They are kept as
            // independent editable objects rather than being fused into the console texture.
            Primitive(root, PrimitiveType.Cube, "CabFloorPlate3D", new Vector3(0f, -5.28f, 1.08f),
                new Vector3(19.1f, 1.05f, 2.85f), trim);
            Primitive(root, PrimitiveType.Cube, "CabFootwellRecess3D", new Vector3(0f, -4.86f, 0.25f),
                new Vector3(5.35f, 0.58f, 1.12f), black);

            CreatePedal(root, "DeadmanPedal3D", new Vector3(-1.22f, -4.63f, -0.13f), steel, black);
            CreatePedal(root, "SanderPedal3D", new Vector3(1.22f, -4.63f, -0.13f), steel, black);
            Primitive(root, PrimitiveType.Cylinder, "FootwellDrainCover3D", new Vector3(0f, -4.83f, -0.25f),
                new Vector3(0.30f, 0.06f, 0.30f), steel).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void CreatePedal(Transform root, string name, Vector3 position, Material steel, Material rubber)
        {
            GameObject pedal = Primitive(root, PrimitiveType.Cube, name, position, new Vector3(0.88f, 0.16f, 0.65f), steel);
            pedal.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            Primitive(pedal.transform, PrimitiveType.Cube, "PedalRubberPad", new Vector3(0f, 0.10f, -0.03f),
                new Vector3(0.67f, 0.045f, 0.42f), rubber);
            Primitive(pedal.transform, PrimitiveType.Cylinder, "PedalHinge", new Vector3(0f, -0.10f, 0.27f),
                new Vector3(0.10f, 0.52f, 0.10f), steel).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private static void CreateSeat(Transform root, string name, float side, Material fabric, Material black, Material steel)
        {
            GameObject seat = new GameObject(name);
            seat.transform.SetParent(root, false);
            seat.transform.localPosition = new Vector3(side * 9.15f, -4.18f, 0.70f);
            seat.transform.localRotation = Quaternion.Euler(0f, side * -7f, 0f);
            Primitive(seat.transform, PrimitiveType.Cube, "SeatCushion", new Vector3(0f, 0.34f, 0f), new Vector3(2.65f, 0.58f, 2.35f), fabric);
            Primitive(seat.transform, PrimitiveType.Cube, "SeatBack", new Vector3(0f, 2.05f, 0.78f), new Vector3(2.65f, 3.15f, 0.52f), fabric);
            Primitive(seat.transform, PrimitiveType.Cube, "SeatHeadrest", new Vector3(0f, 4.02f, 0.72f), new Vector3(1.48f, 0.94f, 0.52f), fabric);
            Primitive(seat.transform, PrimitiveType.Cube, "SeatArmrest", new Vector3(-side * 1.52f, 1.02f, 0.08f), new Vector3(0.24f, 0.20f, 1.46f), black);
            Primitive(seat.transform, PrimitiveType.Cylinder, "SeatPedestal", new Vector3(0f, -0.48f, 0f), new Vector3(0.34f, 0.58f, 0.34f), steel);
            Primitive(seat.transform, PrimitiveType.Cube, "SeatRail", new Vector3(0f, -0.92f, 0f), new Vector3(2.34f, 0.13f, 1.48f), black);
        }

        private static void CreatePhysicalWipers(Transform root, Material metal, Material rubber)
        {
            // These sit just inside the glass plane. The existing full-screen rain and clearing
            // effect stays in UI; the mesh arms make the motion read as part of the 3D cab.
            CreateWiper(root, "WiperArm3D_Left", new Vector3(-3.95f, 2.72f, 1.58f), -28f, metal, rubber);
            CreateWiper(root, "WiperArm3D_Right", new Vector3(3.95f, 2.72f, 1.58f), 28f, metal, rubber);
        }

        private static void CreateCabKeychain(Transform root, Material metal, Material rubber, Material accent)
        {
            // The pivot sits at the attachment point. Its rotation is driven by the same damped
            // pendulum as the accessible touch target, so it feels like one real object.
            GameObject charm = new GameObject("KeychainCharm3D");
            charm.transform.SetParent(root, false);
            charm.transform.localPosition = new Vector3(0.15f, 3.45f, 1.52f);
            Primitive(charm.transform, PrimitiveType.Cylinder, "KeychainRing", new Vector3(0f, -0.06f, 0f), new Vector3(0.16f, 0.035f, 0.16f), metal)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Primitive(charm.transform, PrimitiveType.Cube, "KeychainCord3D", new Vector3(0f, -0.64f, 0f), new Vector3(0.045f, 1.18f, 0.045f), rubber);
            GameObject locomotive = new GameObject("KeychainLocomotive3D");
            locomotive.transform.SetParent(charm.transform, false);
            locomotive.transform.localPosition = new Vector3(0f, -1.40f, -0.02f);
            Primitive(locomotive.transform, PrimitiveType.Cube, "CharmCab", new Vector3(0f, 0.18f, 0.12f), new Vector3(0.46f, 0.42f, 0.44f), metal);
            Primitive(locomotive.transform, PrimitiveType.Cylinder, "CharmBoiler", new Vector3(0f, 0.16f, -0.26f), new Vector3(0.22f, 0.40f, 0.22f), accent)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Primitive(locomotive.transform, PrimitiveType.Cylinder, "CharmSmokestack", new Vector3(0f, 0.55f, -0.27f), new Vector3(0.08f, 0.17f, 0.08f), rubber);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject wheel = Primitive(locomotive.transform, PrimitiveType.Cylinder, "CharmWheel", new Vector3(side * 0.27f, -0.10f, 0.02f),
                    new Vector3(0.14f, 0.05f, 0.14f), rubber);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        private static void CreateWiper(Transform parent, string name, Vector3 position, float angle, Material metal, Material rubber)
        {
            GameObject armRoot = new GameObject(name);
            armRoot.transform.SetParent(parent, false);
            armRoot.transform.localPosition = position;
            armRoot.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            Primitive(armRoot.transform, PrimitiveType.Cylinder, "WiperPivot", Vector3.zero, new Vector3(0.22f, 0.10f, 0.22f), metal)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Primitive(armRoot.transform, PrimitiveType.Cube, "WiperMetalArm", new Vector3(0f, 1.28f, 0f), new Vector3(0.12f, 2.56f, 0.10f), metal);
            GameObject blade = Primitive(armRoot.transform, PrimitiveType.Cube, "WiperBlade", new Vector3(0f, 2.53f, 0.03f), new Vector3(0.17f, 1.76f, 0.12f), rubber);
            blade.transform.localRotation = Quaternion.Euler(0f, 0f, 86f);
        }

        private static void CreateScreen(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject frame = Primitive(parent, PrimitiveType.Cube, name + "_Bezel", position + new Vector3(0f, 0f, 0.025f),
                new Vector3(scale.x + 0.34f, scale.y + 0.34f, scale.z + 0.10f), Material("ScreenBezel", new Color(0.018f, 0.023f, 0.026f), 0.56f, 0.34f));
            GameObject screen = Primitive(parent, PrimitiveType.Cube, name, position, scale, material);
            GameObject label = new GameObject(name + "_Readout", typeof(TextMesh));
            label.transform.SetParent(screen.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.52f);
            // TextMesh is already front-facing for the cab camera. Rotating it 180° mirrors
            // Cyrillic and turns the speed display into a giant reflected inscription.
            label.transform.localRotation = Quaternion.identity;
            TextMesh text = label.GetComponent<TextMesh>();
            text.text = name.StartsWith("Speed") ? "000 km/h" : "READY";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 32;
            text.characterSize = 0.035f;
            text.color = new Color(0.27f, 1f, 0.44f);
        }

        private static void CreateGaugeCluster(Transform parent, string name, Vector3 position, Material metal, Material glass, Material accent)
        {
            GameObject cluster = new GameObject(name);
            cluster.transform.SetParent(parent, false);
            cluster.transform.localPosition = position;
            for (int i = 0; i < 3; i++)
            {
                float x = -0.68f + i * 0.68f;
                Primitive(cluster.transform, PrimitiveType.Cylinder, "GaugeBezel", new Vector3(x, 0f, 0f), new Vector3(0.43f, 0.08f, 0.43f), metal)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Primitive(cluster.transform, PrimitiveType.Cylinder, "GaugeGlass", new Vector3(x, 0f, -0.06f), new Vector3(0.32f, 0.025f, 0.32f), glass)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                GameObject needle = Primitive(cluster.transform, PrimitiveType.Cube, "GaugeNeedle", new Vector3(x, 0.02f, -0.095f), new Vector3(0.025f, 0.22f, 0.025f), accent);
                needle.transform.localRotation = Quaternion.Euler(0f, 0f, -35f + i * 24f);
            }
        }

        private static void CreateLever(Transform parent, string name, Vector3 position, CabControlAction action, Material steel, Material black)
        {
            GameObject basePlate = Primitive(parent, PrimitiveType.Cube, name + "_Base", position, new Vector3(2.0f, 0.22f, 1.5f), black);
            GameObject lever = new GameObject(name);
            lever.transform.SetParent(basePlate.transform, false);
            lever.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            Primitive(lever.transform, PrimitiveType.Cube, "LeverArm", new Vector3(0f, 0.44f, 0f), new Vector3(0.22f, 1.0f, 0.25f), steel);
            Primitive(lever.transform, PrimitiveType.Cylinder, "LeverHandle", new Vector3(0f, 1.0f, 0f), new Vector3(0.33f, 0.54f, 0.33f), black)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // A recessed slot and detent marks make the two key levers readable as real railway
            // controls from a cab-distance view, rather than loose blocks on a flat panel.
            Primitive(basePlate.transform, PrimitiveType.Cube, "LeverTravelSlot", new Vector3(0f, 0.13f, -0.34f), new Vector3(0.48f, 1.06f, 0.06f), black);
            for (int i = 0; i < 7; i++)
                Primitive(basePlate.transform, PrimitiveType.Cube, "LeverDetent", new Vector3(0.37f, -0.28f + i * 0.14f, -0.37f),
                    new Vector3(0.13f, 0.025f, 0.05f), steel);
            lever.AddComponent<Cab3DControlAnchor>().Configure(action, lever.transform);
        }

        private static void CreateBacklitLabel(Transform parent, string name, Vector3 position, string caption, Material frameMaterial)
        {
            GameObject plate = Primitive(parent, PrimitiveType.Cube, name, position, new Vector3(0.95f, 0.28f, 0.075f), frameMaterial);
            GameObject inset = Primitive(plate.transform, PrimitiveType.Cube, "DarkInset", new Vector3(0f, 0f, -0.052f),
                new Vector3(0.82f, 0.16f, 0.018f), Material("LabelInset", new Color(0.008f, 0.015f, 0.017f), 0.1f, 0.23f));
            GameObject textObject = new GameObject("Caption", typeof(TextMesh));
            textObject.transform.SetParent(inset.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, -0.035f);
            textObject.transform.localRotation = Quaternion.identity;
            TextMesh text = textObject.GetComponent<TextMesh>();
            text.text = caption;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 32;
            text.characterSize = 0.045f;
            text.color = new Color(0.52f, 0.75f, 0.82f);
        }

        private static void CreateRoundControl(Transform parent, string name, Vector3 position, CabControlAction action, Material material, float size)
        {
            GameObject button = Primitive(parent, PrimitiveType.Cylinder, name, position, new Vector3(size, 0.14f, size), material);
            button.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            button.AddComponent<Cab3DControlAnchor>().Configure(action, button.transform);
        }

        private static void CreateToggleSwitch(Transform parent, string name, Vector3 position, CabControlAction action, Material body, Material cap)
        {
            GameObject toggle = Primitive(parent, PrimitiveType.Cube, name + "_Housing", position, new Vector3(0.52f, 0.68f, 0.20f), body);
            GameObject bat = Primitive(toggle.transform, PrimitiveType.Cube, name, new Vector3(0f, 0.24f, -0.16f), new Vector3(0.13f, 0.44f, 0.11f), cap);
            bat.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            toggle.AddComponent<Cab3DControlAnchor>().Configure(action, bat.transform);
        }

        private static GameObject Primitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static Material Material(string name, Color color, float metallic, float smoothness)
        {
            return CabShaders.CreateLit(name, color, smoothness, metallic);
        }
    }
}
