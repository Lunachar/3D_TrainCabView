using UnityEngine;

namespace SortingStation
{
    /// <summary>Rare happenings at stations: festivals, police, markets, weddings, fireworks and more.</summary>
    public sealed partial class WorldChunkBuilder
    {
        private float eventCentre;

        /// <summary>Position beside the station: along = metres from the platform centre, lateral from the track.</summary>
        private Vector3 S(float along, float lateral, float y) => P(eventCentre + along, lateral, y);
        private Quaternion SR(float along, float yaw = 0f) => R(eventCentre + along) * Quaternion.Euler(0f, yaw, 0f);
        private float Back => PlatformEdge - PlatformWidth - 1f;

        private void BuildStationEvent(float centre)
        {
            if (plan.Event == StationEvent.None) return;
            eventCentre = centre;
            switch (plan.Event)
            {
                case StationEvent.Festival: Festival(); break;
                case StationEvent.Police: Police(); break;
                case StationEvent.Market: Market(); break;
                case StationEvent.Wedding: Wedding(); break;
                case StationEvent.TrackWorks: TrackWorks(); break;
                case StationEvent.BrassBand: BrassBand(); break;
                case StationEvent.FireDrill: FireDrill(); break;
                case StationEvent.Circus: Circus(); break;
                case StationEvent.Hikers: Hikers(); break;
                case StationEvent.Welcome: Welcome(); break;
                case StationEvent.FilmCrew: FilmCrew(); break;
                case StationEvent.Concert: Concert(); break;
                case StationEvent.Ambulance: Ambulance(); break;
                case StationEvent.Fireworks: Fireworks(); break;
            }
        }

        private Transform Group(string name)
        {
            Transform group = new GameObject("Event_" + name).transform;
            group.SetParent(chunk.Root.transform, false);
            return group;
        }

        /// <summary>A few people standing around a spot, turned toward a point of interest.</summary>
        private void Crowd(Transform parent, float along, float lateral, float y, float spread, int count, Vector3 lookAt, Color[] palette = null, float scale = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 spot = S(along + Range(-spread, spread), lateral + Range(-spread * 0.5f, spread * 0.5f), y);
                Color coat = palette != null ? palette[i % palette.Length] : Color.HSVToRGB((float)random.NextDouble(), Range(0.3f, 0.7f), Range(0.25f, 0.7f));
                GameObject person = CabWorld3DPrototypeFactory.CreatePassenger(parent, "EventPerson_" + i, spot, coat, scale < 1f || i % 7 == 6, false, false, out _, out _, out _);
                Vector3 toward = lookAt - spot;
                toward.y = 0f;
                if (toward.sqrMagnitude > 0.01f) person.transform.localRotation = Quaternion.LookRotation(toward, Vector3.up) * Quaternion.Euler(0f, Range(-25f, 25f), 0f);
                if (scale != 1f) person.transform.localScale *= scale;
                TrackMaterials(person);
            }
        }

        private GameObject Vehicle(VehicleType type, int paint, float along, float lateral, float yaw)
        {
            GameObject car = VehicleFactory.Create(chunk.Root.transform, type, paint);
            car.transform.localPosition = S(along, lateral, Ground(eventCentre + along, lateral));
            car.transform.localRotation = SR(along, yaw);
            return car;
        }

        /// <summary>Flashing light bar on a vehicle's roof.</summary>
        private void LightBar(GameObject vehicle, Color a, Color b, float roof)
        {
            Blinker blinker = vehicle.AddComponent<Blinker>();
            blinker.Configure(WorldMaterials.Glow("Flash" + ColorUtility.ToHtmlStringRGB(a), a, 3f), WorldMaterials.Glow("Flash" + ColorUtility.ToHtmlStringRGB(b), b, 3f),
                WorldMaterials.Plain("FlashOff", new Color(0.15f, 0.15f, 0.18f), 0.8f), 0.45f);
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                RemoveCollider(lamp);
                lamp.transform.SetParent(vehicle.transform, false);
                lamp.transform.localPosition = new Vector3(s * 0.35f, roof + 0.08f, 0f);
                lamp.transform.localScale = new Vector3(0.6f, 0.14f, 0.28f);
                blinker.Add(lamp.GetComponent<Renderer>(), s < 0);
            }
            Light flash = CreateLight("EventFlash", vehicle.transform.localPosition + Vector3.up * (roof + 0.5f), LightType.Point, a, 2f, 12f);
            chunk.Lamps.Add(flash);
        }

        private void Banner(string text, float along, float lateral, float y, Color board, Color letters)
        {
            Quaternion facing = SR(along, -90f);
            float width = text.Length * 0.95f + 1.2f;
            meshes.For(WorldMaterials.Plain("Banner" + ColorUtility.ToHtmlStringRGB(board), board, 0.3f), 1f).AddBox(S(along, lateral, y), new Vector3(width, 1.2f, 0.06f), facing);
            AddText(text, S(along, lateral, y) + facing * Vector3.back * 0.05f, facing, letters, 0.7f);
        }

        private void Balloons(float along, float lateral, float y, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 p = S(along + Range(-1.2f, 1.2f), lateral + Range(-0.6f, 0.6f), y + Range(1.2f, 2.6f));
                meshes.For(WorldMaterials.Neon(random.Next(0, WorldMaterials.NeonColours.Length)), 1f).AddCylinder(p, 0.24f, 0.45f, 8, Quaternion.identity);
                AddBeam(meshes.For(WorldMaterials.Wire, 1f), p, S(along, lateral, y + 0.9f), 0.01f);
            }
        }

        // ---- The events -------------------------------------------------------------------------

        private void Festival()
        {
            Transform group = Group("Festival");
            // Garlands of little lamps strung along the platform.
            for (float a = -24f; a < 24f; a += 0.8f)
            {
                float sag = Mathf.Sin(Mathf.Repeat(a + 24f, 8f) / 8f * Mathf.PI) * 0.6f;
                meshes.For(WorldMaterials.Neon(Mathf.Abs(Mathf.RoundToInt(a * 1.25f)) % WorldMaterials.NeonColours.Length), 1f)
                    .AddBox(S(a, PlatformEdge - 2.6f, PlatformHeight + 3.6f - sag), new Vector3(0.12f, 0.16f, 0.12f), SR(a));
            }
            Banner("ПРАЗДНИК", -4f, Back + 0.4f, PlatformHeight + 2.6f, new Color(0.8f, 0.15f, 0.2f), new Color(1f, 0.92f, 0.4f));
            Balloons(-12f, PlatformEdge - 3f, PlatformHeight, 6);
            Balloons(10f, PlatformEdge - 3f, PlatformHeight, 6);
            Crowd(group, 0f, PlatformEdge - 2.8f, PlatformHeight, 12f, 14, S(0f, 0f, 0f));
        }

        private void Police()
        {
            Transform group = Group("Police");
            GameObject car = Vehicle(VehicleType.Sedan, 10, 6f, Back - 6f, 90f);
            LightBar(car, new Color(0.1f, 0.3f, 1f), new Color(1f, 0.1f, 0.1f), VehicleFactory.Size(VehicleType.Sedan).y);
            Color[] uniform = { new Color(0.10f, 0.14f, 0.25f) };
            Crowd(group, 4f, PlatformEdge - 2.2f, PlatformHeight, 3f, 3, S(4f, 0f, 0f), uniform);
            Crowd(group, 12f, PlatformEdge - 3f, PlatformHeight, 4f, 5, S(4f, PlatformEdge - 2.2f, 0f));
        }

        private void Market()
        {
            Transform group = Group("Market");
            float lateral = Back - 8f;
            for (int i = 0; i < 6; i++)
            {
                float along = -22f + i * 8f;
                float g = Ground(eventCentre + along, lateral);
                Color awning = WorldMaterials.NeonColours[i % WorldMaterials.NeonColours.Length] * 0.8f;
                meshes.For(WorldMaterials.Planks, 1f, true).AddBox(S(along, lateral, g + 0.45f), new Vector3(1.6f, 0.9f, 3.4f), SR(along));
                meshes.For(WorldMaterials.Plain("Awning" + i, awning, 0.2f), 1f, true).AddBox(S(along, lateral, g + 2.3f), new Vector3(2.4f, 0.08f, 3.8f), SR(along) * Quaternion.Euler(0f, 0f, 10f));
                foreach (float corner in new[] { -1.6f, 1.6f })
                    meshes.For(WorldMaterials.Steel, 1f).AddBox(S(along + corner, lateral + 1f, g + 1.1f), new Vector3(0.06f, 2.2f, 0.06f), SR(along));
                // Produce on the counter.
                for (int k = 0; k < 6; k++)
                    meshes.For(WorldMaterials.Plain("Produce" + (k % 4), k % 4 == 0 ? new Color(0.85f, 0.2f, 0.1f) : k % 4 == 1 ? new Color(0.95f, 0.75f, 0.1f)
                        : k % 4 == 2 ? new Color(0.3f, 0.6f, 0.15f) : new Color(0.6f, 0.35f, 0.15f), 0.4f), 1f)
                        .AddBox(S(along + Range(-1.3f, 1.3f), lateral + Range(-0.5f, 0.5f), g + 0.97f), Vector3.one * 0.22f, SR(along));
                Block(eventCentre + along - 2.5f, eventCentre + along + 2.5f, lateral - 2f, lateral + 2f);
            }
            Crowd(group, 0f, lateral + 2.6f, Ground(eventCentre, lateral), 22f, 16, S(0f, lateral, 0f));
        }

        private void Wedding()
        {
            Transform group = Group("Wedding");
            Crowd(group, 2f, PlatformEdge - 2.5f, PlatformHeight, 0.6f, 1, S(2f, 0f, 0f), new[] { new Color(0.97f, 0.97f, 0.95f) });
            Crowd(group, 2.9f, PlatformEdge - 2.5f, PlatformHeight, 0.3f, 1, S(2f, 0f, 0f), new[] { new Color(0.05f, 0.05f, 0.06f) });
            Crowd(group, 2f, PlatformEdge - 3.6f, PlatformHeight, 6f, 10, S(2f, PlatformEdge - 2.5f, 0f));
            Balloons(-2f, PlatformEdge - 3.5f, PlatformHeight, 8);
            GameObject car = Vehicle(VehicleType.Sedan, 1, -8f, Back - 6f, 90f);
            // Rings and ribbons on the wedding car.
            meshes.For(WorldMaterials.Plain("WeddingRibbon", new Color(0.95f, 0.8f, 0.85f), 0.4f), 1f)
                .AddBox(car.transform.localPosition + Vector3.up * 1.5f, new Vector3(0.1f, 0.1f, 4.2f), car.transform.localRotation);
        }

        private void TrackWorks()
        {
            Transform group = Group("TrackWorks");
            float lateral = PlacementMask.CorridorRight + 2.5f;
            Color[] vests = { new Color(1f, 0.45f, 0.05f) };
            Crowd(group, 30f, lateral, Ground(eventCentre + 30f, lateral), 5f, 5, S(30f, WorldChunkBuilder.SecondTrackOffset, 0f), vests);
            for (int i = 0; i < 8; i++)
            {
                float along = 20f + i * 2.5f;
                meshes.For(WorldMaterials.Plain("Cone", new Color(1f, 0.4f, 0.05f), 0.4f), 1f).AddCone(S(along, lateral - 1.4f, Ground(eventCentre + along, lateral) + 0.02f),
                    S(along, lateral - 1.4f, Ground(eventCentre + along, lateral) + 0.75f), 0.18f, 8);
            }
            GameObject crane = Vehicle(VehicleType.Truck, 7, 40f, lateral + 5f, 0f);
            crane.name = "TrackWorksTruck";
            Material yellow = WorldMaterials.Plain("CraneYellow", new Color(0.95f, 0.72f, 0.05f), 0.5f);
            meshes.For(yellow, 1f, true).AddBox(S(40f, lateral + 5f, Ground(eventCentre + 40f, lateral + 5f) + 5.5f), new Vector3(0.4f, 0.4f, 8f), SR(40f) * Quaternion.Euler(-35f, 0f, 0f));
            chunk.Lamps.Add(CreateLight("WorksLamp", S(30f, lateral, 4f), LightType.Point, new Color(1f, 0.85f, 0.6f), 2.5f, 16f));
        }

        private void BrassBand()
        {
            Transform group = Group("BrassBand");
            Material gold = WorldMaterials.Plain("Brass", new Color(0.85f, 0.65f, 0.2f), 0.85f, 1f);
            Color[] uniforms = { new Color(0.55f, 0.1f, 0.1f) };
            for (int i = 0; i < 7; i++)
            {
                float a = (i - 3f) * 0.35f;
                Vector3 spot = S(-6f + Mathf.Sin(a) * 4f, PlatformEdge - 3.2f + Mathf.Cos(a) * 1.2f, PlatformHeight);
                GameObject musician = CabWorld3DPrototypeFactory.CreatePassenger(group, "Musician_" + i, spot, uniforms[0], false, false, false, out _, out _, out _);
                musician.transform.localRotation = SR(-6f, 90f);
                meshes.For(gold, 1f).AddCylinder(spot + Vector3.up * 1.1f + SR(-6f, 90f) * Vector3.forward * 0.35f, i % 3 == 0 ? 0.28f : 0.1f, 0.6f, 10,
                    SR(-6f, 90f) * Quaternion.Euler(70f, 0f, 0f));
                TrackMaterials(musician);
            }
            Crowd(group, -6f, PlatformEdge - 1.4f, PlatformHeight, 8f, 8, S(-6f, PlatformEdge - 3.2f, 0f));
        }

        private void FireDrill()
        {
            Transform group = Group("FireDrill");
            GameObject engine = Vehicle(VehicleType.Truck, 3, 4f, Back - 7f, 90f);
            LightBar(engine, new Color(0.1f, 0.3f, 1f), new Color(0.1f, 0.3f, 1f), VehicleFactory.Size(VehicleType.Truck).y);
            Color[] suits = { new Color(0.2f, 0.2f, 0.18f), new Color(0.85f, 0.7f, 0.1f) };
            Crowd(group, 0f, Back - 3f, Ground(eventCentre, Back - 3f), 5f, 6, S(-10f, Back - 10f, 0f), suits);
            // A hose laid out across the square.
            AddBeam(meshes.For(WorldMaterials.Plain("Hose", new Color(0.8f, 0.15f, 0.1f), 0.4f), 1f),
                S(4f, Back - 7f, Ground(eventCentre, Back - 7f) + 0.1f), S(-12f, Back - 12f, Ground(eventCentre - 12f, Back - 12f) + 0.1f), 0.08f);
        }

        private void Circus()
        {
            float lateral = Back - 24f;
            float g = Ground(eventCentre, lateral);
            Material red = WorldMaterials.Plain("CircusRed", new Color(0.85f, 0.1f, 0.1f), 0.3f);
            Material white = WorldMaterials.Plain("CircusWhite", new Color(0.95f, 0.93f, 0.88f), 0.3f);
            // A striped big top: alternating wall panels and a cone roof, with a flag on the pole.
            const int panels = 16;
            for (int i = 0; i < panels; i++)
            {
                float a0 = i / (float)panels * Mathf.PI * 2f, a1 = (i + 1) / (float)panels * Mathf.PI * 2f;
                Vector3 c = S(0f, lateral, g);
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 11f, p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 11f;
                Vector3 outward = ((p0 + p1) * 0.5f - c).normalized;
                meshes.For(i % 2 == 0 ? red : white, 1f, true).AddQuad(p0, p1, p1 + Vector3.up * 4f, p0 + Vector3.up * 4f, outward);
                meshes.For(i % 2 == 0 ? white : red, 1f, true).AddTriangle(p0 + Vector3.up * 4f, p1 + Vector3.up * 4f, c + Vector3.up * 11f,
                    (outward + Vector3.up).normalized);
            }
            meshes.For(WorldMaterials.Neon(2), 1f).AddBox(S(0f, lateral, g + 12.5f), new Vector3(0.1f, 3f, 0.1f), SR(0f));
            Block(eventCentre - 13f, eventCentre + 13f, lateral - 13f, lateral + 13f);
            // A carousel turning beside it.
            GameObject carousel = new GameObject("Carousel");
            carousel.transform.SetParent(chunk.Root.transform, false);
            carousel.transform.localPosition = S(18f, lateral + 4f, g);
            WorldMesh ride = new WorldMesh(1f);
            ride.AddCylinder(Vector3.zero, 3.5f, 0.3f, 16, Quaternion.identity);
            ride.AddCylinder(Vector3.up * 3.2f, 3.8f, 0.4f, 16, Quaternion.identity);
            ride.AddCylinder(Vector3.zero, 0.25f, 3.3f, 8, Quaternion.identity);
            for (int h = 0; h < 6; h++)
            {
                float a = h / 6f * Mathf.PI * 2f;
                ride.AddBox(new Vector3(Mathf.Cos(a) * 2.6f, 1.2f, Mathf.Sin(a) * 2.6f), new Vector3(0.4f, 0.7f, 1.2f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
                ride.AddBox(new Vector3(Mathf.Cos(a) * 2.6f, 2.2f, Mathf.Sin(a) * 2.6f), new Vector3(0.05f, 2.1f, 0.05f), Quaternion.identity);
            }
            AttachMesh(carousel.transform, "CarouselBody", ride, WorldMaterials.Neon(0));
            carousel.AddComponent<Spinner>().Configure(Vector3.up, 25f);
            Crowd(Group("Circus"), 8f, lateral + 12f, g, 10f, 10, S(0f, lateral, g));
            chunk.Lamps.Add(CreateLight("CircusLight", S(0f, lateral + 12f, g + 4f), LightType.Point, new Color(1f, 0.6f, 0.3f), 2.5f, 20f));
        }

        private void Hikers()
        {
            Transform group = Group("Hikers");
            Color[] jackets = { new Color(0.1f, 0.5f, 0.3f), new Color(0.9f, 0.4f, 0.1f), new Color(0.2f, 0.3f, 0.7f), new Color(0.7f, 0.1f, 0.2f) };
            Crowd(group, 14f, PlatformEdge - 2.4f, PlatformHeight, 3f, 7, S(0f, 0f, 0f), jackets);
            // Big rucksacks piled on the platform and a tent pitched beyond it.
            for (int i = 0; i < 5; i++)
                meshes.For(WorldMaterials.Plain("Rucksack" + (i % 3), jackets[i % jackets.Length] * 0.8f, 0.3f), 1f)
                    .AddBox(S(10f + i * 0.7f, PlatformEdge - 3.8f, PlatformHeight + 0.45f), new Vector3(0.45f, 0.9f, 0.35f), SR(10f));
            float lateral = Back - 9f, g = Ground(eventCentre + 20f, lateral);
            Material tent = WorldMaterials.Plain("Tent", new Color(0.95f, 0.55f, 0.1f), 0.2f);
            Vector3 ridgeA = S(18f, lateral, g + 1.4f), ridgeB = S(21f, lateral, g + 1.4f);
            meshes.For(tent, 1f, true).AddQuad(S(18f, lateral - 1.2f, g), S(21f, lateral - 1.2f, g), ridgeB, ridgeA, SR(18f) * new Vector3(-1f, 1f, 0f).normalized);
            meshes.For(tent, 1f, true).AddQuad(S(18f, lateral + 1.2f, g), S(21f, lateral + 1.2f, g), ridgeB, ridgeA, SR(18f) * new Vector3(1f, 1f, 0f).normalized);
        }

        private void Welcome()
        {
            Transform group = Group("Welcome");
            Banner("ДОБРО ПОЖАЛОВАТЬ!", 0f, Back + 0.4f, PlatformHeight + 2.8f, new Color(0.1f, 0.35f, 0.7f), Color.white);
            Crowd(group, 0f, PlatformEdge - 2.6f, PlatformHeight, 10f, 16, S(0f, 0f, 0f));
            // Little flags on sticks among the crowd.
            for (int i = 0; i < 10; i++)
            {
                Vector3 p = S(Range(-9f, 9f), PlatformEdge - Range(1.8f, 3.4f), PlatformHeight + 2.1f);
                meshes.For(WorldMaterials.Neon(i % 3), 1f).AddBox(p, new Vector3(0.02f, 0.3f, 0.45f), SR(0f));
            }
        }

        private void FilmCrew()
        {
            Transform group = Group("FilmCrew");
            float along = 8f, lateral = PlatformEdge - 3f;
            meshes.For(WorldMaterials.Plain("Camera", new Color(0.08f, 0.08f, 0.09f), 0.5f, 0.4f), 1f)
                .AddBox(S(along, lateral, PlatformHeight + 1.5f), new Vector3(0.35f, 0.35f, 0.7f), SR(along, 60f));
            AddBeam(meshes.For(WorldMaterials.Steel, 1f), S(along, lateral, PlatformHeight + 1.3f), S(along - 0.4f, lateral - 0.3f, PlatformHeight), 0.03f);
            AddBeam(meshes.For(WorldMaterials.Steel, 1f), S(along, lateral, PlatformHeight + 1.3f), S(along + 0.4f, lateral - 0.3f, PlatformHeight), 0.03f);
            AddBeam(meshes.For(WorldMaterials.Steel, 1f), S(along, lateral, PlatformHeight + 1.3f), S(along, lateral + 0.4f, PlatformHeight), 0.03f);
            // Two big soft lights on stands aimed at the actors.
            foreach (float a in new[] { 3f, 13f })
            {
                meshes.For(WorldMaterials.Steel, 1f).AddBox(S(a, lateral - 1f, PlatformHeight + 1.2f), new Vector3(0.05f, 2.4f, 0.05f), SR(a));
                meshes.For(WorldMaterials.Glow("SoftBox", new Color(1f, 0.97f, 0.9f), 1.2f), 1f).AddBox(S(a, lateral - 1f, PlatformHeight + 2.5f), new Vector3(1.1f, 0.8f, 0.1f), SR(a, 40f));
                chunk.Lamps.Add(CreateLight("FilmLight", S(a, lateral - 0.5f, PlatformHeight + 2.4f), LightType.Point, new Color(1f, 0.96f, 0.9f), 3f, 10f));
            }
            Crowd(group, 8f, PlatformEdge - 1.6f, PlatformHeight, 2f, 2, S(8f, lateral, 0f));
            Crowd(group, 10f, lateral - 1.2f, PlatformHeight, 4f, 5, S(6f, PlatformEdge - 1.4f, 0f));
        }

        private void Concert()
        {
            float lateral = Back - 12f, g = Ground(eventCentre, lateral);
            meshes.For(WorldMaterials.DarkConcrete, 1f, true).AddBox(S(0f, lateral, g + 0.6f), new Vector3(8f, 1.2f, 14f), SR(0f));
            WorldMesh truss = meshes.For(WorldMaterials.Steel, 1f, true);
            foreach (float a in new[] { -6.8f, 6.8f })
                truss.AddBox(S(a, lateral, g + 3.7f), new Vector3(0.3f, 5f, 0.3f), SR(a));
            truss.AddBox(S(0f, lateral, g + 6.2f), new Vector3(0.3f, 0.3f, 14f), SR(0f));
            for (float a = -6f; a <= 6f; a += 1f)
                meshes.For(WorldMaterials.Neon(Mathf.Abs(Mathf.RoundToInt(a)) % 6), 1f).AddBox(S(a, lateral + 0.3f, g + 6f), new Vector3(0.25f, 0.25f, 0.25f), SR(a));
            foreach (float a in new[] { -5.5f, 5.5f })
                meshes.For(WorldMaterials.Plain("Speaker", new Color(0.05f, 0.05f, 0.05f), 0.3f), 1f).AddBox(S(a, lateral + 2.5f, g + 2.4f), new Vector3(1f, 2.4f, 1f), SR(a));
            Block(eventCentre - 9f, eventCentre + 9f, lateral - 5f, lateral + 5f);
            Transform group = Group("Concert");
            Crowd(group, 0f, lateral + 1f, g + 1.2f, 3f, 3, S(0f, lateral + 12f, 0f));
            Crowd(group, 0f, lateral + 9f, g, 9f, 20, S(0f, lateral, 0f));
            chunk.Lamps.Add(CreateLight("StageLight", S(0f, lateral + 2f, g + 5f), LightType.Point, new Color(0.8f, 0.3f, 1f), 3f, 18f));
        }

        private void Ambulance()
        {
            Transform group = Group("Ambulance");
            GameObject van = Vehicle(VehicleType.Van, 10, -6f, Back - 6f, 90f);
            LightBar(van, new Color(0.1f, 0.3f, 1f), new Color(0.1f, 0.3f, 1f), VehicleFactory.Size(VehicleType.Van).y);
            meshes.For(WorldMaterials.Plain("AmbulanceStripe", new Color(0.85f, 0.1f, 0.1f), 0.4f), 1f)
                .AddBox(van.transform.localPosition + Vector3.up * 1.1f, new Vector3(2.05f, 0.25f, 5.1f), van.transform.localRotation);
            Color[] medics = { new Color(0.92f, 0.92f, 0.95f), new Color(0.2f, 0.45f, 0.7f) };
            Crowd(group, -3f, PlatformEdge - 2.6f, PlatformHeight, 1.5f, 2, S(-3f, PlatformEdge - 3.5f, 0f), medics);
            Crowd(group, -3f, PlatformEdge - 3.2f, PlatformHeight, 4f, 5, S(-3f, PlatformEdge - 2.6f, 0f));
        }

        private void Fireworks()
        {
            GameObject show = new GameObject("Fireworks");
            show.transform.SetParent(chunk.Root.transform, false);
            show.transform.localPosition = S(0f, Back - 30f, 0f);
            show.AddComponent<FireworkShow>();
            show.SetActive(false);
            chunk.NightOnly.Add(show);
            Crowd(Group("FireworksCrowd"), 0f, PlatformEdge - 2.8f, PlatformHeight, 12f, 14, S(0f, Back - 30f, 20f));
        }
    }
}
