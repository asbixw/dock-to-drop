using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[System.Serializable]
public class WorldRefs
{
    public Transform root;
    // The Amazon gate, yard and dock are built at their authored local
    // coordinates and then shifted to the far end of the deadhead highway.
    public Transform destination;
    public float routeLength;
    public Transform playerSpawn;
    public TruckController truck;
    public Transform truckSeat;
    public Transform truckExit;
    public Transform driverDoor;
    public GateController gate;
    public Transform gateWaypoint;
    public GateTrigger gateTrigger;
    public Transform container;
    public Transform targetTrailer;
    public TrailerRig trailerRig;
    public PhotoTarget sealTarget;
    public List<InspectionPoint> inspections = new List<InspectionPoint>();
    public Light sun;
}

public static class WorldBuilder
{
    static Material lotAsphalt, wornYellow, wornWhite;
    static Material asphalt, roadLine, yardConcrete, lotConcrete, dockConcrete, dirt, siding, warehouseBase, joint, grass, yardAsphalt, wood;
    static Material whitePaint, offWhite, yellowPaint, metal, chrome, dark, glass, orange, orangeGlow, blue, navy, red, green, skin, fabric, concreteDark;
    static Texture2D asphaltD, asphaltR, concD, concR;

    public static WorldRefs Build(GameFlow flow, Transform parent)
    {
        var refs = new WorldRefs();
        var root = new GameObject("World").transform;
        root.SetParent(parent, false);
        refs.root = root;
        LoadMaterials();

        BuildSky();
        BuildLights(refs);
        BuildGround(root);
        BuildClouds(root);
        BuildTruckStop(root);
        BuildRoad(root);
        BuildHorizon(root);
        refs.truck = BuildTruck(flow, root, out refs.truckSeat, out refs.truckExit, out refs.driverDoor);

        // The deadhead is real distance: the yard and gate are built at their
        // authored local coordinates, then placed at the far end of a highway
        // whose length is exactly the assigned 32/60 miles. The odometer
        // integrates metres on this road; nothing scales or compresses it.
        float route = flow.DeadheadAssignedMiles * 1609.344f;
        var hull = refs.truck.GetComponent<BoxCollider>();
        float nose = hull != null ? hull.center.z + hull.size.z * 0.5f : 0f;
        const float gateTriggerZ = 188f, gateTriggerHalf = 5f;
        float contactLocal = gateTriggerZ - gateTriggerHalf - nose;
        float destOffset = route + refs.truck.transform.position.z - contactLocal;
        refs.routeLength = route;

        var dest = new GameObject("Destination").transform;
        dest.SetParent(root, false);
        refs.destination = dest;
        BuildYardGround(dest);
        BuildAmazon(flow, dest, refs);
        BuildGate(flow, dest, refs);
        BuildYardProps(dest);
        dest.position = new Vector3(0f, 0f, destOffset);
        BuildHighway(root, destOffset);
        Debug.Log("D2D ROUTE length=" + route.ToString("F1") + "m (" + flow.DeadheadAssignedMiles
            + " mi) gateZ=" + (destOffset + 205f).ToString("F1") + " destOffset=" + destOffset.ToString("F1"));
        BuildLivedInDetails(root, dest);
        BuildRealProps(root, dest);
        BuildReflectionProbes(root);
        refs.playerSpawn = new GameObject("PlayerSpawn").transform;
        refs.playerSpawn.SetParent(root, false);
        refs.playerSpawn.position = new Vector3(-7.6f, 0.11f, 16.4f);
        refs.playerSpawn.rotation = Quaternion.Euler(0f, 150f, 0f);
        return refs;
    }

    static Texture2D T(string n) { return Resources.Load<Texture2D>("Textures/" + n); }

    static Texture2D shadowBlob;
    static Material shadowMat;

    static void AddContactShadow(Transform parent, Vector3 localPos, Vector2 size)
    {
        if (shadowBlob == null)
        {
            shadowBlob = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            shadowBlob.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float dx = (x - 63.5f) / 63.5f;
                    float dy = (y - 63.5f) / 63.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * 0.72f;
                    px[y * 128 + x] = new Color(0f, 0f, 0f, a);
                }
            shadowBlob.SetPixels32(px);
            shadowBlob.Apply();
            shadowMat = MeshFactory.SignMat("ContactShadow", shadowBlob, Color.white);
        }
        var q = MeshFactory.Quad("ContactShadow", size, localPos, Quaternion.Euler(90f, 0f, 0f), shadowMat, parent);
        q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        q.GetComponent<MeshRenderer>().receiveShadows = false;
    }

    static void LoadMaterials()
    {
        asphaltD = T("asphalt_hq"); concD = T("concrete_hq");
        var asphaltNrm = T("asphalt_hq_n");
        var concNrm = T("concrete_hq_n");
        // Poly Haven CC0 photo scans (asphalt_07, concrete_floor_worn_001; make_ground_ph.py)
        // replace the procedural maps when present.
        if (T("ph_asphalt") != null) { asphaltD = T("ph_asphalt"); asphaltNrm = T("ph_asphalt_nor"); }
        if (T("ph_concrete") != null) { concD = T("ph_concrete"); concNrm = T("ph_concrete_nor"); }
        foreach (var gt in new[] { asphaltD, asphaltNrm, concD, concNrm }) if (gt != null) gt.anisoLevel = 8;

        // 1024 px seamless textures, tiled so one repeat covers roughly 4 m.
        // The bump is deliberately gentle; a strong normal at this tiling turns
        // the apron into a field of glittering gravel.
        asphalt = MeshFactory.TexMat("Asphalt", asphaltD, asphaltNrm, new Vector2(4f, 56f), 0.06f);
        asphalt.color = new Color(1f, 1f, 1f);
        asphalt.SetFloat("_BumpScale", 0.34f);
        yardAsphalt = MeshFactory.TexMat("YardAsphalt", asphaltD, asphaltNrm, new Vector2(5f, 30f), 0.08f);
        yardAsphalt.color = new Color(1.04f, 1.02f, 0.98f);
        yardAsphalt.SetFloat("_BumpScale", 0.34f);
        yardConcrete = MeshFactory.TexMat("YardConcrete", concD, concNrm, new Vector2(64f, 40f), 0.10f);
        yardConcrete.color = new Color(1f, 1f, 1f);
        yardConcrete.SetFloat("_BumpScale", 0.20f);
        lotConcrete = MeshFactory.TexMat("LotConcrete", concD, concNrm, new Vector2(9f, 8f), 0.10f);
        lotConcrete.color = new Color(0.98f, 0.98f, 0.97f);
        lotConcrete.SetFloat("_BumpScale", 0.20f);
        lotAsphalt = MeshFactory.TexMat("LotAsphalt", asphaltD, asphaltNrm, new Vector2(30f, 27f), 0.07f);
        lotAsphalt.color = new Color(1.0f, 1.0f, 1.0f);
        lotAsphalt.SetFloat("_BumpScale", 0.40f);
        dockConcrete = MeshFactory.TexMat("DockConcrete", concD, concNrm, new Vector2(64f, 6f), 0.10f);
        dockConcrete.color = new Color(1f, 1f, 1f);
        dockConcrete.SetFloat("_BumpScale", 0.20f);
        dirt = MeshFactory.TexMat("Dirt", T("dirt_hq"), null, new Vector2(120f, 120f), 0.04f);
        dirt.color = new Color(1.0f, 0.98f, 0.94f);
        grass = MeshFactory.TexMat("Grass", T("grass_hq"), null, new Vector2(3f, 72f), 0.03f);
        grass.color = new Color(0.92f, 0.97f, 0.88f);

        siding = MeshFactory.TexMat("WarehouseSiding", T("warehouse_siding"), T("warehouse_siding_normal"), new Vector2(44f, 12f), 0.30f);
        siding.color = new Color(0.60f, 0.62f, 0.64f);
        warehouseBase = MeshFactory.Mat("WarehouseBase", new Color(0.31f, 0.32f, 0.32f), 0f, 0.10f, T("concrete_floor_02_Diffuse"), null, new Vector2(16f, 2f));

        joint = MeshFactory.Mat("ConcreteJoint", new Color(0.36f, 0.37f, 0.38f), 0f, 0.28f);
        wood = MeshFactory.Mat("PalletWood", new Color(0.45f, 0.33f, 0.19f), 0f, 0.66f);
        concreteDark = MeshFactory.Mat("ConcreteDark", new Color(0.27f, 0.28f, 0.28f), 0f, 0.26f);
        whitePaint = MeshFactory.Mat("WhitePaint", new Color(0.80f, 0.80f, 0.76f), 0.05f, 0.46f);
        offWhite = MeshFactory.Mat("OffWhite", new Color(0.66f, 0.65f, 0.60f), 0.05f, 0.50f);
        roadLine = MeshFactory.Mat("RoadLine", new Color(0.82f, 0.81f, 0.71f), 0f, 0.44f);
        wornYellow = MeshFactory.Mat("WornYellowPaint", new Color(0.74f, 0.60f, 0.24f), 0f, 0.30f);
        wornWhite = MeshFactory.Mat("WornWhitePaint", new Color(0.70f, 0.70f, 0.66f), 0f, 0.30f);
        yellowPaint = MeshFactory.Mat("YellowPaint", new Color(0.89f, 0.63f, 0.08f), 0f, 0.44f);
        metal = MeshFactory.Mat("Metal", new Color(0.36f, 0.39f, 0.42f), 0.78f, 0.34f);
        chrome = MeshFactory.Mat("Chrome", new Color(0.78f, 0.81f, 0.84f), 1f, 0.68f);
        dark = MeshFactory.Mat("Dark", new Color(0.028f, 0.036f, 0.042f), 0.1f, 0.30f);
        glass = MeshFactory.GlassMat("Glass");
        orange = MeshFactory.Mat("Orange", new Color(0.95f, 0.37f, 0.05f), 0.05f, 0.48f);
        orangeGlow = MeshFactory.Mat("OrangeGlow", new Color(1f, 0.35f, 0.02f), 0f, 0.3f);
        orangeGlow.EnableKeyword("_EMISSION"); orangeGlow.SetColor("_EmissionColor", new Color(1f, 0.28f, 0.02f) * 2.4f);
        blue = MeshFactory.Mat("AmazonBlue", new Color(0.020f, 0.075f, 0.155f), 0.3f, 0.40f);
        navy = MeshFactory.Mat("Navy", new Color(0.010f, 0.030f, 0.058f), 0.25f, 0.44f);
        red = MeshFactory.Mat("Red", new Color(0.52f, 0.025f, 0.015f), 0.1f, 0.38f);
        green = MeshFactory.Mat("Foliage", new Color(0.115f, 0.205f, 0.095f), 0f, 0.16f);
        skin = MeshFactory.Mat("Skin", new Color(0.52f, 0.34f, 0.24f), 0f, 0.58f);
        fabric = MeshFactory.Mat("Fabric", new Color(0.085f, 0.10f, 0.115f), 0f, 0.74f);
    }

    static void BuildSky()
    {
        var hdr = Resources.Load<Texture2D>("Textures/dawn_puresky");
        Debug.Log("D2D HDR loaded=" + (hdr != null));
        if (hdr != null)
        {
            var sh = Shader.Find("Skybox/Panoramic");
            if (sh != null)
            {
                var sky = new Material(sh);
                sky.SetTexture("_MainTex", hdr);
                sky.SetFloat("_Exposure", 1.02f);
                sky.SetFloat("_Rotation", 128f);
                RenderSettings.skybox = sky;
            }
        }
        if (hdr == null)
        {
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetColor("_SkyTint", new Color(0.34f, 0.49f, 0.68f));
            sky.SetColor("_GroundColor", new Color(0.12f, 0.11f, 0.10f));
            sky.SetFloat("_AtmosphereThickness", 1.15f);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_Exposure", 1.09f);
            RenderSettings.skybox = sky;
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        // Dawn light is soft and very wraparound. Keep the ambient high enough that
        // shade reads as cool blue-grey instead of black, which was flattening the
        // whole scene into a silhouette.
        RenderSettings.ambientSkyColor = new Color(0.53f, 0.61f, 0.73f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.50f, 0.53f);
        RenderSettings.ambientGroundColor = new Color(0.27f, 0.27f, 0.27f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.00092f;
        RenderSettings.fogColor = new Color(0.74f, 0.76f, 0.76f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 0.65f;
    }

    static void BuildLights(WorldRefs refs)
    {
        var go = new GameObject("Sun");
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        // Early sun is warm but not orange; a saturated key turns white paint beige.
        l.color = new Color(1f, 0.92f, 0.82f);
        l.intensity = 1.18f;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.74f;
        l.shadowResolution = LightShadowResolution.High;
        l.shadowBias = 0.02f;
        // A normal bias stops the low morning sun from making two-sided leaves
        // self-shadow into flickering dark patches as the camera moves.
        l.shadowNormalBias = 0.35f;
        // Low morning sun from the south-west: long shadows across the apron and a
        // warm rim on the truck and trailer instead of flat overhead light.
        go.transform.rotation = Quaternion.Euler(16f, 38f, 0f);
        RenderSettings.sun = l;
        refs.sun = l;

        // broad sky fill from the south, so shade on the truck and building is readable
        var fillGo = new GameObject("SkyFill");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.54f, 0.65f, 0.84f);
        fill.intensity = 0.52f;
        fill.shadows = LightShadows.None;
        fillGo.transform.rotation = Quaternion.Euler(22f, -35f, 0f);

        // A second, cooler bounce from the north keeps the truck's face and the
        // dock fascia from falling into silhouette when we look toward the sun.
        var northGo = new GameObject("NorthBounce");
        var north = northGo.AddComponent<Light>();
        north.type = LightType.Directional;
        north.color = new Color(0.62f, 0.72f, 0.86f);
        north.intensity = 0.30f;
        north.shadows = LightShadows.None;
        northGo.transform.rotation = Quaternion.Euler(24f, 178f, 0f);
    }

    static void BuildGround(Transform root)
    {
        var g = MeshFactory.Box("Ground", new Vector3(1200f, 0.2f, 1200f), new Vector3(0, -0.12f, 200), dirt, root, true);
        g.GetComponent<MeshRenderer>().sharedMaterial = dirt;

        // (The old EastField/WestField dirt boxes were removed: their tops at y 0.13 sat above the
        // lot and yard slabs and hid them, which is why the ground read as one beige plane.)

        // travel center: asphalt lot (x -54..42, z -52..34, top 0.085) with a concrete fuel pad
        // (top 0.105) and a store sidewalk; striping and decals in BuildTravelCenterGround.
        MeshFactory.Box("TruckStopApron", new Vector3(96, 0.10f, 86), new Vector3(-6, 0.035f, -9), lotAsphalt, root, true);
        MeshFactory.Box("TruckStopIsland", new Vector3(30, 0.07f, 26), new Vector3(20, 0.07f, 5), lotConcrete, root, true);
        BuildTravelCenterGround(root);
        // The Amazon yard itself is built under Destination, at the far end of
        // the real deadhead highway (see BuildYardGround), not beside the lot.

        // grass strips along the rural road
        for (int s = -1; s <= 1; s += 2)
            MeshFactory.Box("RoadGrass", new Vector3(7.2f, 0.045f, 208), new Vector3(s * 14.2f, 0.02f, 110), grass, root, false);

        // dirt driveway edges leading into the road
        for (int s = -1; s <= 1; s += 2)
            MeshFactory.Box("RoadShoulder", new Vector3(4.5f, 0.07f, 200), new Vector3(s * 10.5f, 0.02f, 110), dirt, root, false);

        BuildGroundDecals(root);
    }

    // Yard paving lives with the destination so it travels to the end of the
    // highway with the gate and the warehouse.
    static void BuildYardGround(Transform root)
    {
        MeshFactory.Box("AmazonYard", new Vector3(258, 0.14f, 160), new Vector3(25, 0.05f, 296), yardConcrete, root, true);
        MeshFactory.Box("YardDrive", new Vector3(19, 0.025f, 101), new Vector3(8, 0.158f, 267), yardAsphalt, root, true);
        MeshFactory.Box("DockApron", new Vector3(258, 0.03f, 24), new Vector3(25, 0.145f, 317), dockConcrete, root, true);
        MeshFactory.Box("YardLightBand", new Vector3(258, 0.02f, 0.25f), new Vector3(25, 0.17f, 218), roadLine, root, false);
        for (int i = 0; i < 17; i++)
        {
            float jx = -104 + i * 16f;
            if (jx > -2.2f && jx < 18.2f) continue;
            MeshFactory.Box("YardJoint", new Vector3(0.055f, 0.018f, 101), new Vector3(jx, 0.148f, 267), joint, root, false);
        }
        for (int i = 0; i < 8; i++)
            MeshFactory.Box("YardJointX", new Vector3(258, 0.018f, 0.055f), new Vector3(25, 0.148f, 224 + i * 12f), joint, root, false);
        for (int i = 0; i < 8; i++)
            MeshFactory.Box("YardJointX", new Vector3(258, 0.018f, 0.055f), new Vector3(25, 0.148f, 316 + i * 2.2f), joint, root, false);
        Probe(root, "YardReflection", new Vector3(18f, 7f, 260f), new Vector3(250f, 34f, 190f), 128);
    }

    static void BuildHighway(Transform root, float destOffset)
    {
        const float start = 217.5f;
        float end = destOffset + 430f;
        float len = Mathf.Max(1f, end - start);
        var g = new GameObject("Highway").transform; g.SetParent(root, false);
        var hwAsphalt = MeshFactory.TexMat("HighwayAsphalt", asphaltD, T("asphalt_hq_n"), new Vector2(4f, len / 4f), 0.06f);
        hwAsphalt.color = new Color(1f, 1f, 1f);
        hwAsphalt.SetFloat("_BumpScale", 0.34f);
        var hwShoulder = MeshFactory.TexMat("HighwayShoulder", concD, T("concrete_hq_n"), new Vector2(2f, len / 3f), 0.10f);
        var hwDirt = MeshFactory.Mat("HighwayDirt", new Color(0.56f, 0.50f, 0.40f), 0f, 0.06f);
        var centre = MeshFactory.Mat("HighwayCentre", new Color(0.82f, 0.66f, 0.22f), 0f, 0.34f);

        MeshFactory.Box("HighwayGround", new Vector3(640f, 0.2f, len + 900f), new Vector3(0f, -0.14f, start + len / 2f), hwDirt, g, false);
        MeshFactory.Box("HighwaySurface", new Vector3(15f, 0.08f, len), new Vector3(0f, 0.04f, start + len / 2f), hwAsphalt, g, true);
        MeshFactory.Box("HighwayShoulderL", new Vector3(3f, 0.05f, len), new Vector3(-9f, 0.03f, start + len / 2f), hwShoulder, g, true);
        MeshFactory.Box("HighwayShoulderR", new Vector3(3f, 0.05f, len), new Vector3(9f, 0.03f, start + len / 2f), hwShoulder, g, true);
        MeshFactory.Box("HighwayEdgeL", new Vector3(0.16f, 0.02f, len), new Vector3(-6.6f, 0.085f, start + len / 2f), roadLine, g, false);
        MeshFactory.Box("HighwayEdgeR", new Vector3(0.16f, 0.02f, len), new Vector3(6.6f, 0.085f, start + len / 2f), roadLine, g, false);
        MeshFactory.Box("HighwayCentreA", new Vector3(0.14f, 0.02f, len), new Vector3(-0.18f, 0.085f, start + len / 2f), centre, g, false);
        MeshFactory.Box("HighwayCentreB", new Vector3(0.14f, 0.02f, len), new Vector3(0.18f, 0.085f, start + len / 2f), centre, g, false);

        // Delineator posts and mile boards give the eye real motion cues over the
        // long run. Posts are spaced 1 km apart so a 60-mile road does not add
        // thousands of draw calls; the continuous edge and centre lines carry
        // the sense of speed between them.
        int delineator = 0;
        for (float z = start + 250f; z < end; z += 1000f)
        {
            float side = delineator++ % 2 == 0 ? -1f : 1f;
            MeshFactory.Box("Delineator", new Vector3(0.12f, 1.05f, 0.12f), new Vector3(side * 7.8f, 0.60f, z), whitePaint, g, false);
            MeshFactory.Box("DelineatorReflector", new Vector3(0.16f, 0.14f, 0.02f), new Vector3(side * 7.8f, 0.95f, z - 0.07f), orangeGlow, g, false);
        }
        int miles = Mathf.Max(1, Mathf.RoundToInt((end - start) / 1609.344f));
        for (int mi = 1; mi <= miles; mi++)
        {
            float z = 8f + mi * 1609.344f;
            if (z > end - 30f) break;
            MeshFactory.Cyl("MilePost", 0.06f, 1.5f, new Vector3(9.2f, 0.75f, z), metal, g, false);
            MeshFactory.Box("MilePlate", new Vector3(0.78f, 0.34f, 0.05f), new Vector3(9.2f, 1.45f, z - 0.03f), green, g, false);
            var t = MeshFactory.Text3D("MileText", "DFW9  " + (miles - mi + 1) + " MI", new Vector3(9.2f, 1.45f, z - 0.07f), 0.16f, Color.white, g);
            MeshFactory.FaceBack(t);
        }
    }

    static void BuildTravelCenterGround(Transform root)
    {
        var g = new GameObject("TravelCenterGround").transform; g.SetParent(root, false);
        // saw-cut joints on the fuel pad, 5 m slabs
        for (int i = 1; i < 6; i++)
            MeshFactory.Box("PadJoint", new Vector3(0.05f, 0.01f, 26f), new Vector3(5f + i * 5f, 0.103f, 5f), joint, g, false);
        for (int i = 1; i < 6; i++)
            MeshFactory.Box("PadJoint", new Vector3(30f, 0.01f, 0.05f), new Vector3(20f, 0.103f, -8f + i * 26f / 6f), joint, g, false);
        // store sidewalk with its curb
        var walk = MeshFactory.TexMat("Sidewalk", concD, null, new Vector2(8f, 1f), 0.10f);
        walk.color = new Color(1.04f, 1.04f, 1.02f);
        MeshFactory.Box("StoreSidewalk", new Vector3(23.5f, 0.10f, 3.0f), new Vector3(-18f, 0.085f, 1.6f), walk, g, true);
        for (int i = 1; i < 8; i++)
            MeshFactory.Box("WalkJoint", new Vector3(0.04f, 0.01f, 3.0f), new Vector3(-29.75f + i * 23.5f / 8f, 0.133f, 1.6f), joint, g, false);

        // truck parking south of the store: 4.5 m stalls, 30 m long, worn yellow lines
        for (int k = 0; k < 20; k++)
            MeshFactory.Box("TruckStallLine", new Vector3(0.14f, 0.01f, 30f), new Vector3(-44.25f + k * 4.5f, 0.095f, -34f), wornYellow, g, false);
        MeshFactory.Box("TruckStallHead", new Vector3(85.5f, 0.01f, 0.14f), new Vector3(-1.5f, 0.095f, -49f), wornYellow, g, false);
        // white car-stall ends at the store front and a stop bar at the lot exit
        MeshFactory.Box("CarStallHead", new Vector3(15f, 0.01f, 0.12f), new Vector3(-34.5f, 0.095f, 7.0f), wornWhite, g, false);
        MeshFactory.Box("StopBar", new Vector3(6.6f, 0.01f, 0.45f), new Vector3(3.6f, 0.095f, 31.5f), wornWhite, g, false);

        // decals: broad grime, patches, cracks, drips under parked engines, tyre streaks
        Decal(g, "LotGrime", "ground_grime", 96f, 86f, new Vector3(-6f, 0.091f, -9f), 0f, new Color(1f, 1f, 1f, 0.85f));
        Vector3[] patches = { new Vector3(-6, 0, 20), new Vector3(-30, 0, -26), new Vector3(8, 0, -40), new Vector3(30, 0, 26), new Vector3(-46, 0, 2) };
        for (int i = 0; i < patches.Length; i++)
            Decal(g, "AsphaltPatch", "asphalt_patch", 3.2f + (i % 3) * 1.4f, 2.6f + (i % 2) * 1.6f, patches[i] + Vector3.up * 0.094f, i * 37f, new Color(1f, 1f, 1f, 0.75f));
        Vector3[] cracks = { new Vector3(-10, 0, 24), new Vector3(-24, 0, 16), new Vector3(-40, 0, 20), new Vector3(-48, 0, -8),
            new Vector3(-20, 0, -20), new Vector3(-2, 0, -26), new Vector3(16, 0, -44), new Vector3(38, 0, -12),
            new Vector3(36, 0, 24), new Vector3(-36, 0, -46), new Vector3(2, 0, -6), new Vector3(24, 0, -20) };
        for (int i = 0; i < cracks.Length; i++)
            Decal(g, "LotCrack", "crack_a", 7f + (i % 3) * 2f, 7f + (i % 2) * 2f, cracks[i] + Vector3.up * 0.097f, i * 53f, new Color(1f, 1f, 1f, 0.80f));
        for (int k = 0; k < 19; k++)
        {
            float x = -42f + k * 4.5f;
            Decal(g, "StallDrip", "oil_stain", 1.6f, 2.4f, new Vector3(x, 0.099f, -22.5f - (k % 3) * 0.6f), (k * 29) % 360, new Color(1f, 1f, 1f, 0.35f + (k % 4) * 0.07f));
            if (k % 2 == 0)
                Decal(g, "StallDripB", "oil_stain", 1.2f, 1.6f, new Vector3(x + 0.3f, 0.099f, -44.5f + (k % 5) * 0.5f), (k * 41) % 360, new Color(1f, 1f, 1f, 0.30f));
        }
        Decal(g, "TireMarksLot", "tire_marks", 3.2f, 26f, new Vector3(3.5f, 0.098f, 18f), 4f, new Color(0.58f, 0.54f, 0.49f, 0.50f));
        Decal(g, "TireMarksStalls", "tire_marks", 3.0f, 60f, new Vector3(-6f, 0.098f, -15.5f), 90f, new Color(0.58f, 0.54f, 0.49f, 0.42f));
        Decal(g, "TireMarksPump", "tire_marks", 2.8f, 22f, new Vector3(22f, 0.110f, 5f), 0f, new Color(0.58f, 0.54f, 0.49f, 0.34f));
    }

    static void BuildGroundDecals(Transform root)
    {
        var g = new GameObject("GroundDecals").transform; g.SetParent(root, false);
        // Only the travel-center decals stay here. Gate and yard grime are now
        // part of the destination build at the far end of the deadhead.
        Decal(g, "StainPumpA", "oil_stain", 3.0f, 2.4f, new Vector3(16.5f, 0.112f, 6.2f), 22f, new Color(1f, 1f, 1f, 0.80f));
        Decal(g, "StainPumpB", "oil_stain", 2.0f, 1.7f, new Vector3(20.6f, 0.112f, 9.0f), -40f, new Color(1f, 1f, 1f, 0.62f));
        Decal(g, "TireMarksLotB", "tire_marks", 3.0f, 24f, new Vector3(-4f, 0.100f, 20f), 4f, new Color(0.58f, 0.54f, 0.49f, 0.45f));
    }

    static void Decal(Transform parent, string name, string texName, float w, float h, Vector3 pos, float yaw, Color tint)
    {
        var tex = T(texName);
        if (tex == null) return;
        var mat = MeshFactory.SignMat(name + "Mat", tex, tint);
        var q = MeshFactory.Quad(name, new Vector2(w, h), pos, Quaternion.Euler(90f, yaw, 0f), mat, parent);
        q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    static void BuildClouds(Transform root)
    {
        var tex = T("cloud_soft");
        if (tex == null) { Debug.LogWarning("D2D cloud texture missing"); return; }
        var soft = MeshFactory.SignMat("CloudSoft", tex, new Color(1f, 1f, 1f, 0.62f));
        var g = new GameObject("Clouds").transform; g.SetParent(root, false);

        Cloud(g, soft, new Vector3(-220, 95, 420), 260, 88);
        Cloud(g, soft, new Vector3(140, 118, 520), 310, 108);
        Cloud(g, soft, new Vector3(360, 82, 240), 200, 72);
        Cloud(g, soft, new Vector3(-420, 74, 120), 230, 82);
        Cloud(g, soft, new Vector3(60, 100, -330), 270, 92);
        Cloud(g, soft, new Vector3(-260, 82, -430), 230, 86);
        Cloud(g, soft, new Vector3(390, 92, -290), 250, 92);
        Cloud(g, soft, new Vector3(300, 152, 690), 360, 120);

        var wispTex = T("cloud_wisp");
        if (wispTex != null)
        {
            var wisp = MeshFactory.SignMat("CloudWisp", wispTex, new Color(1f, 1f, 1f, 0.72f));
            Cloud(g, wisp, new Vector3(-80, 172, 610), 420, 110);
            Cloud(g, wisp, new Vector3(220, 190, -520), 440, 120);
        }
    }

    static void Cloud(Transform parent, Material mat, Vector3 pos, float w, float h)
    {
        // The HDRI sky already has real clouds; flat cards on top of it read as
        // white ovals (play test 2). They stay only for the procedural fallback.
        if (RenderSettings.skybox != null && RenderSettings.skybox.shader != null && RenderSettings.skybox.shader.name == "Skybox/Panoramic") return;
        var q = MeshFactory.Quad("Cloud", new Vector2(w, h), pos, Quaternion.identity, mat, parent);
        q.AddComponent<SkyBillboard>();
    }

    static void BuildHorizon(Transform root)
    {
        var g = new GameObject("Horizon").transform; g.SetParent(root, false);

        // A Texas-style water tower gives the flat horizon a landmark.
        var wt = new GameObject("WaterTower").transform; wt.SetParent(g, false); wt.position = new Vector3(92, 0, 148);
        for (int i = 0; i < 4; i++)
        {
            float dx = (i % 2 == 0) ? -2.6f : 2.6f;
            float dz = (i < 2) ? -2.6f : 2.6f;
            MeshFactory.Box("TowerLeg", new Vector3(0.34f, 19f, 0.34f), new Vector3(dx, 9.5f, dz), metal, wt, false);
        }
        MeshFactory.Cyl("TowerTank", 4.7f, 5.6f, new Vector3(0, 21.2f, 0), offWhite, wt, false);
        MeshFactory.Cyl("TowerRoof", 4.9f, 1.2f, new Vector3(0, 24.5f, 0), whitePaint, wt, false);
        MeshFactory.Cyl("TowerSkirt", 4.2f, 0.45f, new Vector3(0, 18.3f, 0), blue, wt, false);
        var wtTxt = MeshFactory.Text3D("TowerText", "DFW9", new Vector3(0f, 21.3f, -4.72f), 0.95f, new Color(0.12f, 0.20f, 0.30f), wt);
        MeshFactory.FaceBack(wtTxt);

        // Rows of staged trailers at the edges of the yard.
        for (int i = 0; i < 3; i++) ParkedTrailer(g, new Vector3(-78 + i * 4.1f, 0, 252), 0f, i % 2 == 0);
        for (int i = 0; i < 3; i++) ParkedTrailer(g, new Vector3(94 + i * 4.1f, 0, 250), 0f, i % 2 == 1);

        // stacked containers in the far corners
        ContainerStack(g, new Vector3(-88, 0, 312), 2);
        ContainerStack(g, new Vector3(96, 0, 318), 2);

        // staff cars at the office end
        for (int i = 0; i < 3; i++)
            Car(g, new Vector3(-96 + i * 4.0f, 0, 268 + (i % 2) * 3.5f), 90f + i * 3f,
                i == 0 ? new Color(0.16f, 0.18f, 0.22f) : (i == 1 ? new Color(0.44f, 0.10f, 0.08f) : new Color(0.62f, 0.62f, 0.58f)));

        // more trees along the perimeter to soften the horizon
        for (int i = 0; i < 22; i++)
        {
            float x = -210 + i * 21f;
            float z = 424 + (i % 3) * 15f;
            // The highway runs down the middle of the map; never plant a tree
            // in the lane or the shoulder. 14.5 m = road half-width + shoulder
            // + a small margin.
            if (z > 217.5f && Mathf.Abs(x) < 14.5f) continue;
            Tree(g, new Vector3(x, 0, z), 1.0f + (i % 4) * 0.16f);
        }
        for (int i = 0; i < 22; i++)
        {
            float x = -280 + i * 27f;
            float z = 500 + (i % 4) * 20f;
            if (z > 217.5f && Mathf.Abs(x) < 14.5f) continue;
            Tree(g, new Vector3(x, 0, z), 1.35f + (i % 3) * 0.2f);
        }
        for (int i = 0; i < 20; i++)
        {
            float x = -330 + i * 37f;
            float z = -90 - (i % 4) * 30f;
            if (z > 217.5f && Mathf.Abs(x) < 14.5f) continue;
            Tree(g, new Vector3(x, 0, z), 1.3f + (i % 3) * 0.24f);
        }
        for (int i = 0; i < 8; i++)
            Tree(g, new Vector3(-130 + i * 9f, 0, -58 - (i % 2) * 12f), 0.9f + (i % 3) * 0.2f);
        // low scrub along the fence lines breaks up the flat Texas horizon
        for (int i = 0; i < 26; i++)
        {
            float x = -160 + i * 25f;
            MeshFactory.Sphere("Scrub", 1.4f + (i % 4) * 0.22f, new Vector3(x, 0.55f, 208 + (i % 2) * 2f), green, g);
        }
    }

    static void ParkedTrailer(Transform parent, Vector3 pos, float yaw, bool hasLogo)
    {
        var t = new GameObject("StagedTrailer").transform; t.SetParent(parent, false); t.position = pos; t.rotation = Quaternion.Euler(0, yaw, 0);
        // Sketchfab 53 ft dry van (build_vehicles.py); the boxes below are the fallback.
        if (PropLibrary.Spawn("Trailer53", pos + Vector3.up * PavingY(pos), yaw, 1f, t, true) != null)
        {
            AddContactShadow(t, new Vector3(0f, PavingY(pos) + 0.015f, 0f), new Vector2(3.0f, 16.8f));
            if (hasLogo)
                SignQuad("TrailerLogo", "amazon_logo_navy", 4.8f, 1.8f, new Vector3(1.33f, 3.0f, 1.4f), -90f, t);
            return;
        }
        MeshFactory.Box("Box", new Vector3(2.55f, 2.85f, 16.5f), new Vector3(0, 2.45f, 0), offWhite, t, true);
        MeshFactory.Box("BoxRoof", new Vector3(2.6f, 0.12f, 16.6f), new Vector3(0, 3.92f, 0), whitePaint, t, false);
        MeshFactory.Box("Chassis", new Vector3(1.3f, 0.24f, 14.5f), new Vector3(0, 0.72f, 0), dark, t, false);
        MeshFactory.Box("LandingGear", new Vector3(0.16f, 1.1f, 0.16f), new Vector3(-0.7f, 0.55f, 4.5f), metal, t, false);
        MeshFactory.Box("LandingGear", new Vector3(0.16f, 1.1f, 0.16f), new Vector3(0.7f, 0.55f, 4.5f), metal, t, false);
        for (int s = -1; s <= 1; s += 2)
            for (int i = 0; i < 2; i++)
                MeshFactory.Cyl("Wheel", 0.48f, 0.38f, new Vector3(s * 0.98f, 0.5f, -5.0f - i * 1.2f), dark, t, false).transform.rotation = Quaternion.Euler(0, 0, 90);
        if (hasLogo)
            SignQuad("TrailerLogo", "amazon_logo_navy", 4.8f, 1.8f, new Vector3(1.30f, 3.1f, 1.4f), -90f, t);
    }

    static void ContainerStack(Transform parent, Vector3 pos, int levels)
    {
        var s = new GameObject("ContainerStack").transform; s.SetParent(parent, false);
        s.position = new Vector3(pos.x, SurfaceY(pos), pos.z);
        for (int i = 0; i < levels; i++)
        {
            var mat = (i % 2 == 0) ? blue : offWhite;
            MeshFactory.Box("Container", new Vector3(2.5f, 2.6f, 11.8f), new Vector3(0, 1.30f + i * 2.65f, 0), mat, s, false);
            MeshFactory.Box("ContainerTrim", new Vector3(2.56f, 0.12f, 11.86f), new Vector3(0, 2.60f + i * 2.65f, 0), navy, s, false);
        }
    }

    // Sketchfab cars (build_vehicles.py), picked in turn so the lot mixes models.
    static readonly string[] carModels = { "Car_Camry", "Car_Tahoe", "Car_Silverado", "Car_Fusion" };
    static int carCount;

    // Top of the visible paving at a world point, kept in step with BuildGround:
    // dirt is the base, then the travel-center lot, fuel island, sidewalk, road,
    // Amazon yard, yard drive and dock apron. Used to plant props without
    // relying on physics colliders that may not have synced yet during build.
    static float SurfaceY(Vector3 p)
    {
        float y = -0.02f;
        if (p.x > -54f && p.x < 42f && p.z > -52f && p.z < 34f) y = 0.085f;
        if (p.x > 5f && p.x < 35f && p.z > -8f && p.z < 18f) y = Mathf.Max(y, 0.105f);
        if (p.x > -29.75f && p.x < -6.25f && p.z > 0.1f && p.z < 3.1f) y = Mathf.Max(y, 0.135f);
        if (p.x > -10.5f && p.x < 10.5f && p.z > 12.5f && p.z < 217.5f) y = Mathf.Max(y, 0.055f);
        if (p.x > -7.5f && p.x < 7.5f && p.z > 12.5f && p.z < 217.5f) y = Mathf.Max(y, 0.08f);
        if (p.x > -104f && p.x < 154f && p.z > 216f && p.z < 376f) y = Mathf.Max(y, 0.12f);
        if (p.x > -1.5f && p.x < 17.5f && p.z > 216.5f && p.z < 317.5f) y = Mathf.Max(y, 0.1705f);
        if (p.x > -104f && p.x < 154f && p.z > 305f && p.z < 329f) y = Mathf.Max(y, 0.16f);
        if (p.x > -104f && p.x < 154f && p.z > 217.875f && p.z < 218.125f) y = Mathf.Max(y, 0.18f);
        return y;
    }

    static Vector3 OnGround(float x, float z)
    {
        Vector3 p = new Vector3(x, 0f, z);
        p.y = SurfaceY(p);
        return p;
    }

    // Top of the paving under a parked vehicle: the yard slab or the travel-center apron.
    static float PavingY(Vector3 p) { return SurfaceY(p); }

    static void Car(Transform parent, Vector3 pos, float yaw, Color color)
    {
        var c = new GameObject("Car").transform; c.SetParent(parent, false); c.position = pos; c.rotation = Quaternion.Euler(0, yaw, 0);
        float py = PavingY(pos);
        if (PropLibrary.Spawn(carModels[carCount++ % carModels.Length], pos + Vector3.up * py, yaw, 1f, c, true, color) != null)
        {
            AddContactShadow(c, new Vector3(0f, py + 0.015f, 0f), new Vector2(2.4f, 5.4f));
            return;
        }
        var body = MeshFactory.Mat("CarBody", color, 0.25f, 0.38f);
        MeshFactory.Box("Body", new Vector3(1.8f, 0.55f, 4.2f), new Vector3(0, 0.62f, 0), body, c, false);
        MeshFactory.Box("Cabin", new Vector3(1.55f, 0.55f, 2.0f), new Vector3(0, 1.14f, -0.15f), glass, c, false);
        MeshFactory.Box("CabinRoof", new Vector3(1.50f, 0.10f, 1.7f), new Vector3(0, 1.44f, -0.15f), body, c, false);
        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0) ? -0.82f : 0.82f;
            float z = (i < 2) ? 1.35f : -1.35f;
            MeshFactory.Cyl("Wheel", 0.31f, 0.20f, new Vector3(x, 0.33f, z), dark, c, false).transform.rotation = Quaternion.Euler(0, 0, 90);
        }
    }

    static void BuildTruckStop(Transform root)
    {
        var g = new GameObject("TruckStop").transform; g.SetParent(root, false);

        // ---- main store, left of the truck
        MeshFactory.Box("Store", new Vector3(23, 6.6f, 15), new Vector3(-18, 3.3f, -7.5f), offWhite, g, true);
        MeshFactory.Box("StoreBase", new Vector3(23.4f, 1.1f, 15.4f), new Vector3(-18, 0.55f, -7.5f), concreteDark, g, false);
        MeshFactory.Box("StoreRoofBand", new Vector3(23.5f, 1.0f, 15.5f), new Vector3(-18, 6.4f, -7.5f), navy, g, false);
        MeshFactory.Box("StoreGlass", new Vector3(13.5f, 2.5f, 0.18f), new Vector3(-18, 1.98f, 0.06f), glass, g, false);
        MeshFactory.Box("StoreDoor", new Vector3(2.2f, 2.6f, 0.12f), new Vector3(-12.6f, 1.35f, 0.02f), dark, g, false);
        MeshFactory.Box("StoreCanopy", new Vector3(17.5f, 0.35f, 3.4f), new Vector3(-18, 3.35f, 1.75f), whitePaint, g, false);
        MeshFactory.Box("StoreCanopyFascia", new Vector3(17.6f, 0.55f, 0.18f), new Vector3(-18, 3.25f, 3.45f), orange, g, false);
        SignQuad("TravelCenterSign", "travel_center", 10.4f, 2.55f, new Vector3(-18f, 5.28f, 0.13f), 180f, g);
        MeshFactory.Box("StoreWindowFrame", new Vector3(14.0f, 0.14f, 0.22f), new Vector3(-18, 3.28f, 0.03f), dark, g, false);

        // ---- diesel canopy, right of the truck: Sketchfab canopy (roof, columns, lamps;
        // build_props_sketchfab_rigs.py) with a roof-only collider; the boxes are the fallback.
        float signS = -2.86f, signW = 11.15f;
        if (PropLibrary.Spawn("Canopy", new Vector3(22f, 0f, 7.5f), 0f, 1f, g) != null)
        {
            var roofCol = new GameObject("CanopyRoofCollider"); roofCol.transform.SetParent(g, false);
            roofCol.transform.position = new Vector3(22f, 7.75f, 7.5f);
            roofCol.AddComponent<BoxCollider>().size = new Vector3(21f, 0.75f, 20f);
            signS = -2.53f; signW = 11.47f;   // roof edges at z -2.5 and x 11.5
        }
        else
        {
        for (int i = 0; i < 4; i++)
        {
            float x = 14f + (i % 2) * 16f;
            float z = -1f + (i / 2) * 16f;
            MeshFactory.Cyl("CanopyPost", 0.24f, 7.7f, new Vector3(x, 3.85f, z), chrome, g, false);
        }
        MeshFactory.Box("CanopyRoof", new Vector3(21, 0.75f, 20), new Vector3(22, 7.75f, 7.5f), whitePaint, g, true);
        MeshFactory.Box("CanopyFasciaS", new Vector3(21.4f, 1.35f, 0.35f), new Vector3(22, 7.2f, -2.65f), navy, g, false);
        MeshFactory.Box("CanopyFasciaW", new Vector3(0.35f, 1.35f, 20.4f), new Vector3(11.35f, 7.2f, 7.5f), navy, g, false);
        }
        SignQuad("CanopySignS", "travel_center", 9.8f, 1.9f, new Vector3(22f, 7.26f, signS), 180f, g);
        SignQuad("CanopySignW", "travel_center", 8.6f, 1.7f, new Vector3(signW, 7.26f, 7.5f), 90f, g);

        // pump islands
        for (int i = 0; i < 2; i++)
        {
            float x = 18f + i * 8f;
            MeshFactory.Box("PumpIsland", new Vector3(6.5f, 0.22f, 2.2f), new Vector3(x, 0.11f, 3.0f), concreteDark, g, false);
            // Sketchfab dispenser (build_vehicles.py) on the island; boxes are the fallback.
            if (PropLibrary.Spawn("FuelPump", new Vector3(x, 0.22f, 3.0f), 90f, 1f, g, true) != null) continue;
            MeshFactory.Box("PumpBody", new Vector3(1.25f, 2.15f, 0.85f), new Vector3(x, 1.28f, 3.0f), whitePaint, g, false);
            MeshFactory.Box("PumpScreen", new Vector3(0.62f, 0.48f, 0.05f), new Vector3(x, 1.62f, 3.45f), dark, g, false);
            MeshFactory.Box("PumpScreen2", new Vector3(0.62f, 0.48f, 0.05f), new Vector3(x, 1.62f, 2.55f), dark, g, false);
            MeshFactory.Box("PumpStripe", new Vector3(1.3f, 0.22f, 0.9f), new Vector3(x, 2.28f, 3.0f), orange, g, false);
            MeshFactory.Cyl("PumpHose", 0.035f, 1.45f, new Vector3(x - 0.8f, 1.35f, 3.2f), dark, g, false);
        }

        // ---- travel center pylon near the road
        var py = new GameObject("TravelPylon").transform; py.SetParent(g, false); py.position = new Vector3(10.5f, 0, -11f);
        MeshFactory.Box("PylonPost", new Vector3(0.45f, 8.2f, 0.45f), new Vector3(0, 4.1f, 0), metal, py, false);
        MeshFactory.Box("PylonCabinet", new Vector3(7.0f, 2.55f, 0.75f), new Vector3(0, 9.1f, 0), navy, py, false);
        SignQuad("PylonSign", "travel_center", 6.5f, 1.65f, new Vector3(0f, 9.1f, 0.41f), 180f, py);
        MeshFactory.Box("PylonCap", new Vector3(7.15f, 0.22f, 0.9f), new Vector3(0, 10.5f, 0), orange, py, false);

        // ---- small details around the lot
        for (int i = 0; i < 5; i++)
            Cone(g, new Vector3(-14.5f + (i % 5) * 1.6f, 0, 23.0f + (i / 5) * 3.0f));
        for (int i = 0; i < 4; i++) LightPole(g, new Vector3(-30 + i * 25, 0, -18), 10f);
        for (int i = 0; i < 3; i++) LightPole(g, new Vector3(-26 + i * 24, 0, 25), 9f);

        // Customer parking in front of the store, with painted stalls.
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = OnGround(-42 + i * 3.0f, 9.5f);
            MeshFactory.Box("ParkingLine", new Vector3(0.10f, 0.02f, 5.0f), new Vector3(p.x, p.y + 0.01f, p.z), roadLine, g, false);
        }
        Car(g, new Vector3(-40.5f, 0, 10.2f), 2f, new Color(0.20f, 0.24f, 0.30f));
        Car(g, new Vector3(-34.5f, 0, 10.2f), 358f, new Color(0.55f, 0.12f, 0.08f));
        Car(g, new Vector3(-28.5f, 0, 10.2f), 1f, new Color(0.68f, 0.69f, 0.66f));

        // parked tractor + trailer silhouettes behind the center
        SimpleRig(g, new Vector3(-42, 0, -41), 180f, new Color(0.15f, 0.18f, 0.22f));
        SimpleRig(g, new Vector3(34, 0, -23), 0f, new Color(0.55f, 0.10f, 0.06f));

        // curb along the store front
        MeshFactory.Box("StoreCurb", new Vector3(23.5f, 0.16f, 0.22f), new Vector3(-18, 0.08f, 3.2f), concreteDark, g, false);
        // bins and a bench
        float binY = SurfaceY(new Vector3(-6.2f, 0f, 0.9f));
        MeshFactory.Box("Bin", new Vector3(0.7f, 1.0f, 0.7f), new Vector3(-6.2f, binY + 0.5f, 0.9f), dark, g, true);
        MeshFactory.Box("BinLid", new Vector3(0.8f, 0.08f, 0.8f), new Vector3(-6.2f, binY + 1.04f, 0.9f), metal, g, false);
    }

    static void BuildRoad(Transform root)
    {
        var g = new GameObject("Road").transform; g.SetParent(root, false);
        MeshFactory.Box("RoadSurface", new Vector3(15, 0.08f, 205), new Vector3(0, 0.04f, 115), asphalt, g, true);
        MeshFactory.Box("LeftShoulder", new Vector3(3, 0.05f, 205), new Vector3(-9, 0.03f, 115), concreteDark, g, true);
        MeshFactory.Box("RightShoulder", new Vector3(3, 0.05f, 205), new Vector3(9, 0.03f, 115), concreteDark, g, true);
        MeshFactory.Box("ShoulderLineL", new Vector3(0.16f, 0.02f, 196), new Vector3(-6.6f, 0.095f, 116), roadLine, g, false);
        MeshFactory.Box("ShoulderLineR", new Vector3(0.16f, 0.02f, 196), new Vector3(6.6f, 0.095f, 116), roadLine, g, false);
        for (float z = 24; z < 205; z += 12f)
            MeshFactory.Box("LaneDash", new Vector3(0.16f, 0.02f, 5f), new Vector3(0, 0.095f, z), roadLine, g, false);

        // guard rails along the rural stretch
        for (float z = 36; z < 184; z += 9f)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                MeshFactory.Box("GuardPost", new Vector3(0.12f, 1.0f, 0.12f), new Vector3(s * 10.2f, 0.5f, z), metal, g, true);
                MeshFactory.Box("GuardRail", new Vector3(0.12f, 0.28f, 9), new Vector3(s * 10.2f, 0.85f, z + 4.5f), metal, g, true);
            }
        }

        // trees and telephone poles
        for (int i = 0; i < 22; i++)
        {
            float side = i % 2 == 0 ? -1 : 1;
            float z = 20 + i * 8.2f;
            float x = side * (17 + (i % 4) * 4.5f);
            Tree(g, new Vector3(x, 0, z), 0.8f + (i % 3) * 0.22f);
        }
        for (int i = 0; i < 9; i++)
        {
            var p = new GameObject("UtilityPole").transform; p.SetParent(g, false);
            p.position = new Vector3(14.5f, 0, 27f + i * 21f);
            var poleCol = p.gameObject.AddComponent<CapsuleCollider>();
            poleCol.radius = 0.2f; poleCol.height = 9f; poleCol.center = new Vector3(0f, 4.5f, 0f);
            if (PropLibrary.Spawn("PowerPole", p.position, 90f, 1.45f, p) == null)
            {
                MeshFactory.Cyl("Pole", 0.13f, 9f, new Vector3(0, 4.5f, 0), dark, p, false);
                MeshFactory.Box("CrossArm", new Vector3(2.6f, 0.16f, 0.16f), new Vector3(0, 8.4f, 0), dark, p, false);
            }
            // three sagging conductors between poles
            if (i < 8)
                for (int w = -1; w <= 1; w++)
                    for (int seg = 0; seg < 6; seg++)
                    {
                        float t0 = seg / 6f, t1 = (seg + 1) / 6f;
                        float y0 = 8.25f - 4f * 0.55f * t0 * (1f - t0), y1 = 8.25f - 4f * 0.55f * t1 * (1f - t1);
                        var a = new Vector3(w * 0.75f, y0, t0 * 21f); var b = new Vector3(w * 0.75f, y1, t1 * 21f);
                        var wire = MeshFactory.Box("Wire", new Vector3(0.025f, 0.025f, (b - a).magnitude), (a + b) * 0.5f, dark, p, false);
                        wire.transform.localRotation = Quaternion.LookRotation(b - a);
                    }
        }

        // highway sign with Amazon DFW9
        var hs = new GameObject("HighwaySign").transform; hs.SetParent(g, false); hs.position = new Vector3(12.5f, 0, 126f);
        MeshFactory.Box("SignPost", new Vector3(0.22f, 5.2f, 0.22f), new Vector3(-2.8f, 2.6f, 0), metal, hs, true);
        MeshFactory.Box("SignPost", new Vector3(0.22f, 5.2f, 0.22f), new Vector3(2.8f, 2.6f, 0), metal, hs, true);
        MeshFactory.Box("SignPanel", new Vector3(8.1f, 2.9f, 0.16f), new Vector3(0, 4.2f, 0), green, hs, false);
        SignQuad("HighwaySignTex", "highway_sign", 7.7f, 2.55f, new Vector3(0f, 4.2f, -0.10f), 0f, hs);

        // speed limit 15, set back from the gate lane so it doesn't block the booth
        var sp = new GameObject("SpeedSign").transform; sp.SetParent(g, false); sp.position = new Vector3(-10.4f, 0, 191f);
        MeshFactory.Cyl("Post", 0.07f, 3.3f, new Vector3(0, 1.65f, 0), metal, sp, false);
        var spc = sp.gameObject.AddComponent<CapsuleCollider>(); spc.radius = 0.14f; spc.height = 3.3f; spc.center = new Vector3(0f, 1.65f, 0f);
        MeshFactory.Box("Board", new Vector3(1.15f, 1.55f, 0.08f), new Vector3(0, 3.0f, 0), whitePaint, sp, false);
        // One opaque face only. The old Sprites/Default quad was transparent and
        // double-sided, which let the face paint itself a second time over the
        // white backing and read as "1515".
        var speedTex = T("speed_15");
        if (speedTex != null)
            MeshFactory.Quad("Speed15", new Vector2(1.1f, 1.5f), new Vector3(0f, 3.0f, -0.055f),
                Quaternion.identity, MeshFactory.SignOpaqueMat("Speed15Mat", speedTex, Color.white), sp);
        else Debug.LogWarning("D2D missing texture speed_15");

        // small mile marker
        var mm = new GameObject("MileMarker").transform; mm.SetParent(g, false); mm.position = new Vector3(-9.2f, 0, 160f);
        MeshFactory.Box("Post", new Vector3(0.15f, 1.5f, 0.15f), new Vector3(0, 0.75f, 0), roadLine, mm, true);
        MeshFactory.Box("Plate", new Vector3(0.8f, 0.35f, 0.06f), new Vector3(0, 1.45f, 0), green, mm, false);
        var mt = MeshFactory.Text3D("MileText", "DFW9", new Vector3(0, 1.45f, -0.05f), 0.28f, Color.white, mm);
        MeshFactory.FaceBack(mt);
    }

    static TruckController BuildTruck(GameFlow flow, Transform root, out Transform seat, out Transform exit, out Transform door)
    {
        var prefab = Resources.Load<GameObject>("Models/Freightliner_Cascadia");
        var truckRoot = new GameObject("Freightliner_Cascadia").transform;
        truckRoot.SetParent(root, false);
        // 0.085 lot surface minus the 0.011 tyre-bottom offset measured from the FBX.
        truckRoot.position = new Vector3(0, 0.074f, 8);
        truckRoot.rotation = Quaternion.identity;
        var model = Object.Instantiate(prefab, truckRoot);
        model.name = "TruckVisual";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0, 180, 0);
        model.transform.localScale = Vector3.one;
        ApplyTruckMaterials(model);
        AddContactShadow(truckRoot, new Vector3(0f, 0.19f, -1.65f), new Vector2(3.3f, 10.4f));
        door = FindDeep(model.transform, "DriverDoor");
        Debug.Log("D2D glass shader=" + glass.shader.name + " found=" + (Shader.Find("DockToDrop/Glass") != null));
        UpgradeCabInterior(truckRoot, model, door);

        // The v4 FBX now carries the bumper, air dam, plate, mud flaps, tank
        // straps, door seams and handles, so no duplicate exterior jewellery is
        // added here. The only Unity-side additions are the cab interior and the
        // working lights below.

        seat = new GameObject("Seat").transform; seat.SetParent(truckRoot, false); seat.localPosition = new Vector3(-0.62f, 2.55f, 0.22f);
        exit = new GameObject("ExitPoint").transform; exit.SetParent(truckRoot, false); exit.localPosition = new Vector3(-2.55f, 0.05f, 1.1f);

        var tc = truckRoot.gameObject.AddComponent<TruckController>();
        tc.flow = flow; tc.seat = seat; tc.exitPoint = exit; tc.driverDoor = door; tc.player = flow.Player;
        var rb = truckRoot.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var col = truckRoot.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 1.95f, -1.65f); col.size = new Vector3(2.62f, 3.9f, 9.95f);

        // Fifth wheel between the drive axles: plate, open throat with ramps
        // behind it, pivot brackets, slide and release handle. TrailerRig sets
        // its final height from the trailer's coupler plate.
        var fwMetal = MeshFactory.Mat("FifthWheelSteel", new Color(0.16f, 0.17f, 0.18f), 0.65f, 0.42f);
        var fwBlack = MeshFactory.Mat("FifthWheelThroat", new Color(0.02f, 0.02f, 0.022f), 0.2f, 0.3f);
        var fwGrip = MeshFactory.Mat("FifthWheelGrip", new Color(0.85f, 0.55f, 0.06f), 0.1f, 0.5f);
        var fw = new GameObject("FifthWheel").transform; fw.SetParent(truckRoot, false);
        fw.localPosition = tc.FifthWheelLocal;
        MeshFactory.Box("FWPlate", new Vector3(0.96f, 0.07f, 0.62f), new Vector3(0f, -0.035f, 0.12f), fwMetal, fw, false);
        for (int s = -1; s <= 1; s += 2)
        {
            MeshFactory.BoxRot("FWRamp", new Vector3(0.36f, 0.06f, 0.46f), new Vector3(s * 0.30f, -0.09f, -0.36f), Quaternion.Euler(-14f, 0f, 0f), fwMetal, fw, false);
            MeshFactory.Box("FWBracket", new Vector3(0.10f, 0.20f, 0.30f), new Vector3(s * 0.40f, -0.17f, 0.05f), fwMetal, fw, false);
        }
        MeshFactory.Box("FWThroat", new Vector3(0.12f, 0.072f, 0.30f), new Vector3(0f, -0.035f, -0.06f), fwBlack, fw, false);
        MeshFactory.Box("FWSlide", new Vector3(1.0f, 0.06f, 1.2f), new Vector3(0f, -0.30f, 0f), fwMetal, fw, false);
        MeshFactory.Box("FWHandle", new Vector3(0.42f, 0.025f, 0.025f), new Vector3(-0.68f, -0.10f, 0.02f), fwMetal, fw, false);
        MeshFactory.Box("FWGrip", new Vector3(0.06f, 0.06f, 0.06f), new Vector3(-0.90f, -0.10f, 0.02f), fwGrip, fw, false);
        tc.fifthWheel = fw;
        var airLines = new GameObject("AirLines").AddComponent<AirLines>();
        airLines.transform.SetParent(truckRoot, false);
        airLines.Build(truckRoot);
        tc.lines = airLines;

        var lights = new List<Light>();
        for (int s = -1; s <= 1; s += 2)
        {
            var lo = new GameObject("Headlight"); lo.transform.SetParent(truckRoot, false); lo.transform.localPosition = new Vector3(s * 0.95f, 1.2f, 3.25f);
            var l = lo.AddComponent<Light>(); l.type = LightType.Spot; l.range = 85; l.intensity = 6.5f; l.spotAngle = 52; l.color = new Color(1f, 0.93f, 0.75f); l.shadows = LightShadows.None; l.enabled = false; lights.Add(l);
        }
        tc.SetHeadlights(lights.ToArray());

        var cabLight = new GameObject("CabLight"); cabLight.transform.SetParent(truckRoot, false); cabLight.transform.localPosition = new Vector3(0, 3.12f, 0.35f);
        var cl = cabLight.AddComponent<Light>(); cl.type = LightType.Point; cl.range = 5.0f; cl.intensity = 1.55f; cl.color = new Color(1f, 0.82f, 0.60f); cl.shadows = LightShadows.None;
        // A second, very soft fill in front of the driver keeps the charcoal dash
        // and wheel readable instead of collapsing into a black silhouette.
        var dashFill = new GameObject("DashFill"); dashFill.transform.SetParent(truckRoot, false); dashFill.transform.localPosition = new Vector3(-0.35f, 2.50f, 0.90f);
        var df = dashFill.AddComponent<Light>(); df.type = LightType.Point; df.range = 3.2f; df.intensity = 0.90f; df.color = new Color(1f, 0.88f, 0.72f); df.shadows = LightShadows.None;
        return tc;
    }

    static void UpgradeCabInterior(Transform truckRoot, GameObject model, Transform driverDoor)
    {
        var trim = MeshFactory.Mat("CabTrim", new Color(0.125f, 0.133f, 0.142f), 0.04f, 0.50f);
        var soft = MeshFactory.Mat("CabSoft", new Color(0.170f, 0.176f, 0.184f), 0.02f, 0.72f);
        var headliner = MeshFactory.Mat("CabHeadliner", new Color(0.30f, 0.31f, 0.315f), 0f, 0.88f);
        var rubber = MeshFactory.Mat("CabRubber", new Color(0.028f, 0.030f, 0.032f), 0f, 0.82f);
        var fabric = MeshFactory.Mat("CabSeatFabric", new Color(0.075f, 0.085f, 0.098f), 0f, 0.86f);
        var carpet = MeshFactory.Mat("CabCarpet", new Color(0.050f, 0.055f, 0.060f), 0f, 0.92f);
        var bunkSheet = MeshFactory.Mat("BunkSheet", new Color(0.34f, 0.36f, 0.38f), 0f, 0.90f);
        var chromeTrim = MeshFactory.Mat("CabChrome", new Color(0.72f, 0.75f, 0.78f), 0.90f, 0.78f);

        // ---- floor, seats and sleeper bunk -------------------------
        // v4's FBX deliberately carries no interior, so the cab is furnished
        // here: carpeted floor, air-seat pedestals, driver/passenger seats and
        // the 72-inch sleeper bunk behind them.
        MeshFactory.Box("CabFloor", new Vector3(2.34f, 0.045f, 4.30f), new Vector3(0f, 1.135f, -0.455f), carpet, truckRoot, false);
        // toe board up to the dash, so the front tyre never shows under it
        MeshFactory.Box("Firewall", new Vector3(2.34f, 0.72f, 0.04f), new Vector3(0f, 1.48f, 1.68f), carpet, truckRoot, false);
        MeshFactory.Box("FloorTunnel", new Vector3(0.62f, 0.30f, 0.92f), new Vector3(0f, 1.30f, 0.88f), trim, truckRoot, false);
        for (int s = -1; s <= 1; s += 2)
        {
            float sx = s * 0.62f;
            MeshFactory.Box("SeatPedestal", new Vector3(0.40f, 0.42f, 0.44f), new Vector3(sx, 1.34f, 0.22f), trim, truckRoot, false);
            MeshFactory.Box("SeatCushion", new Vector3(0.56f, 0.16f, 0.56f), new Vector3(sx, 1.56f, 0.24f), fabric, truckRoot, false);
            var back = MeshFactory.Box("SeatBack", new Vector3(0.56f, 0.74f, 0.17f), new Vector3(sx, 1.94f, -0.06f), fabric, truckRoot, false);
            back.transform.localRotation = Quaternion.Euler(-7f, 0f, 0f);
            MeshFactory.Box("SeatHeadrest", new Vector3(0.30f, 0.20f, 0.13f), new Vector3(sx, 2.39f, -0.14f), fabric, truckRoot, false);
            for (int b = -1; b <= 1; b += 2)
            {
                MeshFactory.Box("SeatBolster", new Vector3(0.07f, 0.56f, 0.16f), new Vector3(sx + b * 0.265f, 1.86f, -0.05f), soft, truckRoot, false);
                MeshFactory.Box("SeatCushionBolster", new Vector3(0.07f, 0.09f, 0.50f), new Vector3(sx + b * 0.265f, 1.59f, 0.24f), soft, truckRoot, false);
            }
        }
        MeshFactory.Box("BunkBase", new Vector3(2.10f, 0.14f, 1.32f), new Vector3(0f, 1.48f, -2.02f), trim, truckRoot, false);
        MeshFactory.Box("BunkMattress", new Vector3(2.00f, 0.18f, 1.24f), new Vector3(0f, 1.64f, -2.02f), bunkSheet, truckRoot, false);
        MeshFactory.Box("BunkPillow", new Vector3(0.52f, 0.12f, 0.34f), new Vector3(-0.68f, 1.78f, -2.34f), headliner, truckRoot, false);
        MeshFactory.Box("BunkCurtainRail", new Vector3(2.20f, 0.03f, 0.03f), new Vector3(0f, 3.38f, -1.28f), chromeTrim, truckRoot, false);

        // The v4 shell is hollow and its inside faces carry the Cab_Interior
        // material, so the walls need no skins here.
        for (int s = -1; s <= 1; s += 2)
        {
            var grab = MeshFactory.Cyl("APillarGrab", 0.014f, 0.42f, new Vector3(s * 1.12f, 2.58f, 1.16f), trim, truckRoot, false);
            grab.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
        }

        // ---- shaped cab parts ----------------------------------------
        // Extruded dash sections, a hooded gauge cluster with live needles, an
        // angled centre stack, a dished four-spoke wheel, an overhead storage
        // console and mirror glass. Textures come from make_cab_textures.py.
        // Profiles are (z, y) in truck space; the windshield base is z 1.66, y 2.10.
        var grainTex = T("cab_grain_nor");
        Material Grained(string n, Color c, float sm, float tile = 9f)
        {
            var m = MeshFactory.Mat(n, c, 0f, sm, null, grainTex);
            m.mainTextureScale = new Vector2(tile, tile);
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 0.55f);
            return m;
        }
        Material Panel(string n, string tex, float emit, float sm)
        {
            var t = T(tex);
            var m = MeshFactory.Mat(n, Color.white, 0f, sm, t);
            if (emit > 0f && t != null)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", t);
                m.SetColor("_EmissionColor", Color.white * emit);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }
        Transform Frame(string n, Transform parent, Vector3 pos, float tilt)
        {
            var f = new GameObject(n).transform;
            f.SetParent(parent, false); f.localPosition = pos; f.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            return f;
        }
        Vector2 P(float z, float y) { return new Vector2(z, y); }

        var dashTop = Grained("DashTop", new Color(0.125f, 0.128f, 0.134f), 0.30f);
        var dashLower = Grained("DashLower", new Color(0.235f, 0.238f, 0.245f), 0.26f);
        var binnacle = Grained("Binnacle", new Color(0.095f, 0.098f, 0.104f), 0.34f);
        var stackMat = Grained("CenterStack", new Color(0.135f, 0.138f, 0.145f), 0.42f);
        var overheadMat = Grained("OverheadGrey", new Color(0.30f, 0.30f, 0.295f), 0.20f);
        var visorMat = Grained("VisorFabric", new Color(0.33f, 0.325f, 0.315f), 0.08f, 22f);
        var leather = Grained("WheelLeather", new Color(0.060f, 0.061f, 0.064f), 0.40f, 30f);
        var padMat = Grained("WheelPad", new Color(0.080f, 0.082f, 0.087f), 0.36f);
        var satin = MeshFactory.Mat("SatinSilver", new Color(0.36f, 0.37f, 0.38f), 0.70f, 0.40f);
        var buttonMat = MeshFactory.Mat("CabButton", new Color(0.20f, 0.205f, 0.21f), 0f, 0.45f);
        var mirrorMat = MeshFactory.Mat("MirrorGlass", new Color(0.80f, 0.82f, 0.83f), 1f, 0.97f);
        var yellowKnob = MeshFactory.Mat("ParkBrakeKnob", new Color(0.95f, 0.72f, 0.05f), 0f, 0.55f);
        var redKnob = MeshFactory.Mat("TrailerAirKnob", new Color(0.70f, 0.04f, 0.03f), 0f, 0.55f);
        var clusterMat = Panel("ClusterFace", "cab_cluster", 0.55f, 0.82f);
        var screenMat = Panel("StackScreen", "cab_screen", 0.85f, 0.85f);
        var hvacMat = Panel("HvacPanel", "cab_hvac", 0.10f, 0.35f);
        var switchMat = Panel("SwitchPanel", "cab_switch", 0.10f, 0.35f);
        var overheadFace = Panel("OverheadFace", "cab_overhead", 0f, 0.20f);
        var domeLens = MeshFactory.Mat("DomeLens", new Color(0.95f, 0.92f, 0.85f), 0f, 0.6f);
        domeLens.EnableKeyword("_EMISSION"); domeLens.SetColor("_EmissionColor", new Color(1f, 0.86f, 0.62f) * 0.35f);
        var needleMat = MeshFactory.Mat("NeedleMat", new Color(1f, 0.32f, 0.04f), 0f, 0.4f);
        needleMat.EnableKeyword("_EMISSION"); needleMat.SetColor("_EmissionColor", new Color(1f, 0.28f, 0.02f) * 1.2f);
        var ledGreen = MeshFactory.Mat("CamLed", new Color(0.1f, 0.9f, 0.2f), 0f, 0.5f);
        ledGreen.EnableKeyword("_EMISSION"); ledGreen.SetColor("_EmissionColor", new Color(0.1f, 1f, 0.2f) * 1.5f);

        // Dash: each section is a dark upper piece over a mid-grey lower piece.
        MeshFactory.Extrude("DashDriverUpper", new[] { P(1.66f, 2.10f), P(1.40f, 2.155f), P(1.22f, 2.29f), P(1.08f, 2.345f), P(0.995f, 2.35f),
            P(0.968f, 2.33f), P(0.985f, 2.31f), P(1.13f, 2.30f), P(1.08f, 2.07f), P(1.00f, 2.06f), P(0.965f, 2.035f), P(0.958f, 2.00f),
            P(1.66f, 2.00f) }, -1.20f, -0.22f, binnacle, truckRoot, 35f);
        MeshFactory.Extrude("DashDriverLower", new[] { P(0.958f, 2.00f), P(1.00f, 1.86f), P(1.10f, 1.66f), P(1.20f, 1.44f), P(1.25f, 1.16f),
            P(1.66f, 1.16f), P(1.66f, 2.00f) }, -1.20f, -0.22f, dashLower, truckRoot, 35f);
        MeshFactory.Extrude("DashCenterUpper", new[] { P(1.66f, 2.10f), P(1.35f, 2.15f), P(1.08f, 2.165f), P(1.00f, 2.155f), P(0.968f, 2.13f),
            P(0.962f, 2.09f), P(0.975f, 2.04f), P(1.66f, 2.04f) }, -0.22f, 0.38f, dashTop, truckRoot, 35f);
        MeshFactory.Extrude("DashCenterLower", new[] { P(0.975f, 2.04f), P(1.06f, 1.84f), P(1.15f, 1.60f), P(1.22f, 1.38f), P(1.26f, 1.16f),
            P(1.66f, 1.16f), P(1.66f, 2.04f) }, -0.22f, 0.38f, dashLower, truckRoot, 35f);
        MeshFactory.Extrude("DashPassengerUpper", new[] { P(1.66f, 2.10f), P(1.30f, 2.145f), P(1.02f, 2.155f), P(0.945f, 2.135f), P(0.918f, 2.09f),
            P(0.922f, 2.04f), P(1.66f, 2.04f) }, 0.38f, 1.20f, dashTop, truckRoot, 35f);
        MeshFactory.Extrude("DashPassengerLower", new[] { P(0.922f, 2.04f), P(0.975f, 1.86f), P(1.07f, 1.70f), P(1.25f, 1.50f), P(1.36f, 1.30f),
            P(1.40f, 1.16f), P(1.66f, 1.16f), P(1.66f, 2.04f) }, 0.38f, 1.20f, dashLower, truckRoot, 35f);

        void Vent(Vector3 pos, float tilt)
        {
            var f = Frame("DashVent", truckRoot, pos, tilt);
            MeshFactory.Box("VentBody", new Vector3(0.17f, 0.065f, 0.016f), Vector3.zero, rubber, f, false);
            for (int k = -1; k <= 1; k++)
                MeshFactory.Box("VentSlat", new Vector3(0.155f, 0.005f, 0.018f), new Vector3(0f, k * 0.019f, -0.002f), trim, f, false);
        }
        Vent(new Vector3(-1.06f, 1.928f, 0.973f), -16.7f);
        Vent(new Vector3(0.62f, 2.075f, 0.913f), -4.6f);
        Vent(new Vector3(1.02f, 2.075f, 0.913f), -4.6f);
        var glove = Frame("GloveBoxFrame", truckRoot, new Vector3(0.80f, 1.949f, 0.945f), -16.4f);
        MeshFactory.Box("GloveBoxDoor", new Vector3(0.48f, 0.15f, 0.012f), Vector3.zero, trim, glove, false);
        MeshFactory.Box("GloveBoxHandle", new Vector3(0.13f, 0.018f, 0.02f), new Vector3(0f, 0.058f, -0.010f), satin, glove, false);

        // Gauge cluster in the binnacle face (tilted 12.3 degrees back).
        var cluster = Frame("ClusterFrame", truckRoot, new Vector3(-0.62f, 2.186f, 1.102f), 12.3f);
        MeshFactory.Quad("ClusterFace", new Vector2(0.60f, 0.234f), Vector3.zero, Quaternion.identity, clusterMat, cluster);
        MeshFactory.Quad("SwitchPanel", new Vector2(0.21f, 0.105f), new Vector3(-0.445f, -0.02f, 0f), Quaternion.identity, switchMat, cluster).GetComponent<MeshRenderer>().receiveShadows = true;
        for (int i = 0; i < 2; i++)
        {
            float gx = i == 0 ? -0.130f : 0.130f;
            // TruckController turns these about local Z from rpm and speed
            var piv = new GameObject(i == 0 ? "TachNeedle" : "SpeedoNeedle").transform;
            piv.SetParent(cluster, false);
            piv.localPosition = new Vector3(gx, 0f, -0.004f);
            piv.localRotation = Quaternion.Euler(0f, 0f, 120f);
            MeshFactory.Box("Needle", new Vector3(0.0045f, 0.086f, 0.002f), new Vector3(0f, 0.033f, 0f), needleMat, piv, false);
            MeshFactory.CylRot("NeedleCap", 0.011f, 0.006f, new Vector3(gx, 0f, -0.006f), Quaternion.Euler(90f, 0f, 0f), binnacle, cluster);
        }
        // fifth-wheel lamp between the gauges; TruckController lights it when the jaws lock
        var fwLampMat = MeshFactory.Mat("FifthWheelLampMat", new Color(0.03f, 0.07f, 0.035f), 0f, 0.9f);
        MeshFactory.Quad("FifthWheelLamp", new Vector2(0.026f, 0.013f), new Vector3(0f, -0.088f, -0.003f), Quaternion.identity, fwLampMat, cluster);

        // Centre stack, angled 18 degrees toward the driver.
        var stack = new GameObject("CenterStack").transform;
        stack.SetParent(truckRoot, false);
        stack.localPosition = new Vector3(0.06f, 0f, 0.975f);
        stack.localRotation = Quaternion.Euler(0f, 18f, 0f);
        MeshFactory.Extrude("StackBody", new[] { P(0.25f, 2.135f), P(0.065f, 2.135f), P(0.05f, 2.12f), P(0.05f, 2.105f), P(0.00f, 1.895f),
            P(-0.012f, 1.887f), P(-0.015f, 1.875f), P(-0.065f, 1.665f), P(-0.075f, 1.655f), P(-0.08f, 1.63f), P(-0.06f, 1.46f),
            P(0.00f, 1.30f), P(0.07f, 1.16f), P(0.25f, 1.16f) }, -0.25f, 0.25f, stackMat, stack, 30f);
        var scrF = Frame("ScreenFrame", stack, new Vector3(0f, 2.0f, 0.025f), 13.4f);
        MeshFactory.Quad("StackScreen", new Vector2(0.32f, 0.20f), new Vector3(0f, 0f, -0.003f), Quaternion.identity, screenMat, scrF);
        var hvF = Frame("HvacFrame", stack, new Vector3(0f, 1.77f, -0.04f), 13.4f);
        MeshFactory.Quad("HvacPanel", new Vector2(0.40f, 0.20f), new Vector3(0f, 0f, -0.003f), Quaternion.identity, hvacMat, hvF).GetComponent<MeshRenderer>().receiveShadows = true;
        for (int i = -1; i <= 1; i++)
        {
            MeshFactory.CylRot("HvacKnob", 0.028f, 0.026f, new Vector3(i * 0.125f, 0.019f, -0.016f), Quaternion.Euler(90f, 0f, 0f), rubber, hvF);
            MeshFactory.Box("KnobPointer", new Vector3(0.004f, 0.018f, 0.002f), new Vector3(i * 0.125f, 0.028f, -0.030f), satin, hvF, false);
        }
        // Yellow diamond parking-brake valve and red octagon trailer-air valve.
        var park = MeshFactory.Box("ParkBrakeValve", new Vector3(0.048f, 0.048f, 0.034f), new Vector3(-0.13f, 1.565f, -0.090f), yellowKnob, stack, false);
        park.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var oct = MeshFactory.Lathe("TrailerAirValve", new[] { new Vector2(0f, 0f), new Vector2(0.026f, 0f), new Vector2(0.026f, 0f),
            new Vector2(0.026f, 0.032f), new Vector2(0.026f, 0.032f), new Vector2(0f, 0.032f) }, new Vector3(-0.035f, 1.565f, -0.074f), redKnob, stack, 8);
        oct.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, 22.5f, 0f);

        // Steering wheel: leather rim, oval airbag pad, four dished spokes.
        // TruckController spins WheelPivot about its local Y (the column axis).
        var wheelPivot = new GameObject("WheelPivot").transform;
        wheelPivot.SetParent(truckRoot, false);
        wheelPivot.localPosition = new Vector3(-0.62f, 2.17f, 0.64f);
        wheelPivot.localRotation = Quaternion.Euler(-60f, 0f, 0f);
        MeshFactory.Torus("SteeringWheel", 0.232f, 0.0235f, Vector3.zero, Quaternion.identity, leather, wheelPivot, 48, 14);
        var pad = MeshFactory.Lathe("WheelPad", new[] { new Vector2(0f, -0.075f), new Vector2(0.074f, -0.075f), new Vector2(0.074f, -0.075f),
            new Vector2(0.081f, -0.055f), new Vector2(0.084f, -0.035f), new Vector2(0.080f, -0.018f), new Vector2(0.067f, -0.008f),
            new Vector2(0.042f, -0.003f), new Vector2(0f, -0.001f) }, Vector3.zero, padMat, wheelPivot, 32);
        pad.transform.localScale = new Vector3(1.15f, 1f, 0.92f);
        MeshFactory.Box("PadBadge", new Vector3(0.050f, 0.003f, 0.016f), new Vector3(0f, -0.001f, 0.035f), buttonMat, wheelPivot, false);
        float[] spokeAng = { 90f, -90f, 155f, -155f };
        for (int i = 0; i < 4; i++)
        {
            var sp = new GameObject("SpokePivot").transform;
            sp.SetParent(wheelPivot, false);
            sp.localRotation = Quaternion.Euler(0f, spokeAng[i], 0f);
            var tilt = Quaternion.Euler(-10.5f, 0f, 0f);
            bool side = i < 2;
            MeshFactory.Box("WheelSpoke", new Vector3(side ? 0.058f : 0.036f, 0.016f, 0.165f), new Vector3(0f, -0.021f, 0.152f), padMat, sp, false).transform.localRotation = tilt;
            if (!side) continue;
            MeshFactory.Box("SpokeTrim", new Vector3(0.006f, 0.004f, 0.07f), new Vector3(0f, -0.0058f, 0.18f), satin, sp, false).transform.localRotation = tilt;
            for (int b = -1; b <= 1; b += 2)
                MeshFactory.Box("SpokeButton", new Vector3(0.013f, 0.006f, 0.013f), new Vector3(b * 0.014f, -0.0187f, 0.105f), buttonMat, sp, false).transform.localRotation = tilt;
        }
        // Column, shroud and stalks do not turn with the wheel.
        var colF = new GameObject("ColumnFrame").transform;
        colF.SetParent(truckRoot, false);
        colF.localPosition = wheelPivot.localPosition; colF.localRotation = wheelPivot.localRotation;
        MeshFactory.Box("ColumnShroud", new Vector3(0.115f, 0.20f, 0.085f), new Vector3(0f, -0.185f, -0.012f), binnacle, colF, false);
        MeshFactory.Cyl("SteeringColumn", 0.040f, 0.30f, new Vector3(0f, -0.40f, -0.01f), trim, colF, false);
        MeshFactory.Cyl("ColumnBoot", 0.062f, 0.05f, new Vector3(0f, -0.365f, -0.01f), rubber, colF, false);
        for (int s = -1; s <= 1; s += 2)
        {
            var rotS = Quaternion.Euler(0f, 0f, -s * 78f);
            MeshFactory.Cyl("ColumnStalk", 0.0075f, 0.16f, new Vector3(s * 0.125f, -0.13f, -0.005f), binnacle, colF, false).transform.localRotation = rotS;
            MeshFactory.Cyl("StalkTip", 0.012f, 0.05f, new Vector3(s * 0.228f, -0.108f, -0.005f), binnacle, colF, false).transform.localRotation = rotS;
        }

        // Overhead storage console under the sloped header, kept 6 cm under
        // the roof shell (measured at x = +-0.8) and narrower than the roof
        // corners so it never shows through from outside.
        MeshFactory.Extrude("OverheadConsole", new[] { P(1.06f, 2.975f), P(0.68f, 3.00f), P(0.625f, 3.02f), P(0.60f, 3.055f), P(0.575f, 3.44f),
            P(0.59f, 3.47f), P(0.70f, 3.37f), P(0.80f, 3.26f), P(0.90f, 3.16f), P(1.00f, 3.05f), P(1.06f, 2.995f) }, -0.85f, 0.85f, overheadMat, truckRoot, 35f);
        var ohF = Frame("OverheadFaceFrame", truckRoot, new Vector3(0f, 3.2475f, 0.5875f), -3.7f);
        // unlike Quad's default, the panel takes the roof shadow like the console body
        MeshFactory.Quad("OverheadFace", new Vector2(1.66f, 0.37f), new Vector3(0f, 0f, -0.003f), Quaternion.identity, overheadFace, ohF).GetComponent<MeshRenderer>().receiveShadows = true;
        for (int s = -1; s <= 1; s += 2)
        {
            MeshFactory.Box("DomeLight", new Vector3(0.15f, 0.010f, 0.06f), new Vector3(s * 0.12f, 2.988f, 0.80f), domeLens, truckRoot, false);
            // sun visors stowed flat against the console underside
            var visor = MeshFactory.Box("SunVisor", new Vector3(0.60f, 0.022f, 0.25f), new Vector3(s * 0.46f, 2.9725f, 0.90f), visorMat, truckRoot, false);
            visor.transform.localRotation = Quaternion.Euler(3.8f, 0f, 0f);
            MeshFactory.CylRot("VisorRod", 0.007f, 0.55f, new Vector3(s * 0.46f, 2.968f, 1.03f), Quaternion.Euler(0f, 0f, 90f), trim, truckRoot, false);
        }
        // Fleet dash cam on the windshield (sleeper cabs have no rear-view mirror).
        MeshFactory.Box("DashCam", new Vector3(0.085f, 0.055f, 0.045f), new Vector3(0.10f, 2.94f, 1.15f), binnacle, truckRoot, false);
        MeshFactory.Box("DashCamMount", new Vector3(0.03f, 0.025f, 0.09f), new Vector3(0.10f, 2.955f, 1.21f), binnacle, truckRoot, false);
        MeshFactory.Box("DashCamLed", new Vector3(0.006f, 0.006f, 0.002f), new Vector3(0.125f, 2.952f, 1.127f), ledGreen, truckRoot, false);

        // West-coast mirror glass on the housings' rear faces (measured from the
        // FBX, where they are flat DarkTrim): flat main glass over a convex spot.
        for (int s = -1; s <= 1; s += 2)
        {
            var rot = Quaternion.Euler(0f, s < 0 ? -28f : 27.5f, 0f);
            var c = (s < 0 ? new Vector3(-1.4185f, 0f, 1.249f) : new Vector3(1.401f, 0f, 1.257f)) + rot * new Vector3(0f, 0f, -0.010f);
            var mainGlass = MeshFactory.Quad("MirrorGlass", new Vector2(0.205f, 0.36f), c + new Vector3(0f, 2.51f, 0f), rot, mirrorMat, truckRoot);
            var spotGlass = MeshFactory.Quad("MirrorConvex", new Vector2(0.205f, 0.17f), c + new Vector3(0f, 2.21f, 0f), rot, mirrorMat, truckRoot);
            var mirror = mainGlass.AddComponent<MirrorView>();
            mirror.truck = truckRoot;
            mirror.side = s;
            if (s < 0 && driverDoor != null) { mainGlass.transform.SetParent(driverDoor, true); spotGlass.transform.SetParent(driverDoor, true); }
        }
        var belt = MeshFactory.Box("SeatBelt", new Vector3(0.055f, 1.05f, 0.022f), new Vector3(-1.12f, 2.76f, -0.22f), trim, truckRoot, false);
        belt.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);

        // Door furniture. The FBX doors carry full-height Cab_Interior door cards
        // (inner face at |x| = 1.229), so only the armrest, pull cup and window
        // sill are added. The door window runs from y 2.415 to 3.325.
        MeshFactory.Box("PassengerArmrest", new Vector3(0.10f, 0.075f, 0.50f), new Vector3(1.18f, 1.98f, 0.62f), trim, truckRoot, false);
        MeshFactory.Box("PassengerPullCup", new Vector3(0.050f, 0.045f, 0.20f), new Vector3(1.20f, 2.16f, 1.02f), trim, truckRoot, false);
        MeshFactory.Box("PassengerWindowSill", new Vector3(0.05f, 0.04f, 1.80f), new Vector3(1.21f, 2.40f, 0.40f), soft, truckRoot, false);
        var driverArm = MeshFactory.Box("DriverArmrest", new Vector3(0.10f, 0.075f, 0.50f), new Vector3(-1.18f, 1.98f, 0.62f), trim, truckRoot, false);
        var driverPull = MeshFactory.Box("DriverPullCup", new Vector3(0.050f, 0.045f, 0.20f), new Vector3(-1.20f, 2.16f, 1.02f), trim, truckRoot, false);
        var driverSill = MeshFactory.Box("DriverWindowSill", new Vector3(0.05f, 0.04f, 1.80f), new Vector3(-1.21f, 2.40f, 0.40f), soft, truckRoot, false);
        if (driverDoor != null)
        {
            driverArm.transform.SetParent(driverDoor, true);
            driverPull.transform.SetParent(driverDoor, true);
            driverSill.transform.SetParent(driverDoor, true);
        }

        // The model's original wheel is too low and flat from the new seat height;
        // hide it and use the procedural wheel built above.
        var wheel = FindDeep(model.transform, "SteeringWheel");
        if (wheel != null) wheel.gameObject.SetActive(false);
        var column = FindDeep(model.transform, "SteeringColumn");
        if (column != null) column.gameObject.SetActive(false);
    }

    static void ApplyTruckMaterials(GameObject model)
    {
        // Fleet white is a dielectric clear-coat paint: no metallic term (that tints
        // reflections with the albedo) and a neutral, faintly cool base so the low
        // sun and the tan lot do not push it to cream.
        var white = MeshFactory.Mat("TruckWhite", new Color(0.92f, 0.93f, 0.94f), 0f, 0.80f);
        // The Sketchfab Cascadia's own red: glTF base colour (0.47, 0.03, 0.03)
        // linear, metallic 0.55, as the artist painted it.
        var red = MeshFactory.Mat("TruckRed", new Color(0.71f, 0.19f, 0.19f), 0.55f, 0.70f);
        var pearl = MeshFactory.Mat("TruckPearl", new Color(0.95f, 0.955f, 0.96f), 0f, 0.84f);
        var chromeM = MeshFactory.Mat("TruckChrome", new Color(0.90f, 0.92f, 0.95f), 1f, 0.93f);
        var alum = MeshFactory.Mat("TruckAluminum", new Color(0.62f, 0.64f, 0.66f), 0.88f, 0.66f);
        var darkTrim = MeshFactory.Mat("TruckDarkTrim", new Color(0.035f, 0.040f, 0.046f), 0.05f, 0.42f);
        var grey = MeshFactory.Mat("GreyPlastic", new Color(0.13f, 0.14f, 0.16f), 0.02f, 0.50f);
        var rubber = MeshFactory.Mat("Rubber", new Color(0.010f, 0.011f, 0.012f), 0f, 0.12f);
        var blueAccent = blue;
        var amber = orangeGlow;
        var redLamp = MeshFactory.Mat("RedLamp", new Color(0.62f, 0.015f, 0.01f), 0f, 0.26f);
        redLamp.EnableKeyword("_EMISSION"); redLamp.SetColor("_EmissionColor", new Color(0.8f, 0.01f, 0f) * 1.4f);
        var head = MeshFactory.Mat("Headlamp", new Color(0.92f, 0.92f, 0.85f), 0f, 0.08f);
        head.EnableKeyword("_EMISSION"); head.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.7f) * 1.8f);
        // Inside faces of the hollow shell and the door cards: matte charcoal.
        // low smoothness: the decimated shell caught bright edge highlights at 0.30
        var interior = MeshFactory.Mat("TruckInterior", new Color(0.125f, 0.128f, 0.134f), 0f, 0.12f);
        var seatFab = MeshFactory.Mat("SeatFabric", new Color(0.065f, 0.075f, 0.085f), 0f, 0.80f);
        var stitch = MeshFactory.Mat("Stitch", new Color(0.20f, 0.22f, 0.24f), 0f, 0.8f);
        var plate = MeshFactory.Mat("Plate", new Color(0.85f, 0.85f, 0.80f), 0f, 0.5f);
        var windGlass = MeshFactory.ClearGlassMat("WindshieldGlass");
        var unmatched = new HashSet<string>();
        var texMats = new Dictionary<string, Material>();

        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool isWindshield = r.gameObject.name.ToLowerInvariant().Contains("windshield");
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                string n = m.name.ToLowerInvariant();
                if (n.EndsWith("_tex"))
                {
                    // textured parts of the Sketchfab Cascadia: grille, rims, tires
                    string key = n.Replace("_tex", "");
                    if (!texMats.TryGetValue(key, out var tm))
                    {
                        var tex = Resources.Load<Texture2D>("Textures/cascadia_" + key);
                        tm = MeshFactory.Mat("Truck_" + key, Color.white, key == "rim" ? 0.85f : 0f, key == "tire" ? 0.18f : (key == "rim" ? 0.7f : 0.55f));
                        if (tex != null) tm.mainTexture = tex;
                        texMats[key] = tm;
                    }
                    mats[i] = tm;
                }
                else if (n.Contains("glass") || n.Contains("window")) mats[i] = isWindshield ? windGlass : glass;
                else if (n.Contains("cab_red")) mats[i] = red;
                else if (n.Contains("cab_white")) mats[i] = white;
                else if (n.Contains("cab_pearl")) mats[i] = pearl;
                else if (n.Contains("chrome")) mats[i] = chromeM;
                else if (n.Contains("aluminum") || n.Contains("steel")) mats[i] = alum;
                else if (n.Contains("darktrim")) mats[i] = darkTrim;
                else if (n.Contains("grey") || n.Contains("gray")) mats[i] = grey;
                else if (n.Contains("rubber")) mats[i] = rubber;
                else if (n.Contains("blue")) mats[i] = blueAccent;
                else if (n.Contains("amber")) mats[i] = amber;
                else if (n.Contains("redlamp") || n.Contains("red")) mats[i] = redLamp;
                else if (n.Contains("headlamp") || n.Contains("headlight")) mats[i] = head;
                else if (n.Contains("interior")) mats[i] = interior;
                else if (n.Contains("seatfabric") || n.Contains("fabric")) mats[i] = seatFab;
                else if (n.Contains("stitch")) mats[i] = stitch;
                else if (n.Contains("plate")) mats[i] = plate;
                else if (unmatched.Add(n)) Debug.LogWarning("D2D truck material not remapped: " + m.name + " on " + r.gameObject.name);
            }
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
        }
    }

    static void BuildAmazon(GameFlow flow, Transform root, WorldRefs refs)
    {
        var g = new GameObject("AmazonDFW9").transform; g.SetParent(root, false);

        // ---- fulfillment center shell
        var wall = MeshFactory.Box("FulfillmentCenter", new Vector3(220, 20, 82), new Vector3(25, 10f, 371), siding, g, true);
        wall.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        MeshFactory.Box("WarehouseBase", new Vector3(220.8f, 2.6f, 82.6f), new Vector3(25, 1.3f, 371), warehouseBase, g, false);
        MeshFactory.Box("Roof", new Vector3(221.5f, 0.6f, 83.5f), new Vector3(25, 20.1f, 371), dark, g, false);
        MeshFactory.Box("RoofTrim", new Vector3(222.2f, 0.9f, 84.2f), new Vector3(25, 19.9f, 371), blue, g, false);
        for (int i = 0; i < 5; i++)
        {
            MeshFactory.Box("HVAC", new Vector3(6.5f, 1.7f, 4.5f), new Vector3(-55 + i * 38, 21.2f, 365 + (i % 2) * 12), metal, g, false);
            MeshFactory.Box("HVACCap", new Vector3(6.9f, 0.18f, 4.9f), new Vector3(-55 + i * 38, 22.1f, 365 + (i % 2) * 12), dark, g, false);
        }

        // blue top band and the big navy logo panel
        MeshFactory.Box("TopBlueBand", new Vector3(221, 3.6f, 0.7f), new Vector3(25, 17.9f, 329.75f), blue, g, false);
        MeshFactory.Box("LogoPanel", new Vector3(50, 8.8f, 0.7f), new Vector3(25, 13.6f, 329.78f), navy, g, false);
        SignQuad("AmazonWordmark", "amazon_logo_white", 21.2f, 7.95f, new Vector3(25f, 13.65f, 329.38f), 0f, g);
        var tag = MeshFactory.Text3D("FCName", "FULFILLMENT CENTER  ·  DFW9", new Vector3(25, 8.5f, 329.36f), 0.9f, new Color(0.88f, 0.90f, 0.92f), g);
        MeshFactory.FaceBack(tag);

        // ---- office entrance on the left end
        MeshFactory.Box("OfficeWall", new Vector3(26, 5.8f, 0.8f), new Vector3(-70, 2.9f, 329.85f), offWhite, g, false);
        MeshFactory.Box("OfficeDoor", new Vector3(7.0f, 3.1f, 0.18f), new Vector3(-70, 1.6f, 329.3f), glass, g, false);
        MeshFactory.Box("OfficeFrame", new Vector3(7.4f, 0.22f, 0.3f), new Vector3(-70, 3.25f, 329.2f), dark, g, false);
        MeshFactory.Box("OfficeCanopy", new Vector3(11, 0.35f, 4.2f), new Vector3(-70, 4.2f, 327.5f), whitePaint, g, false);
        SignQuad("OfficeLogo", "amazon_logo_navy", 7.4f, 2.9f, new Vector3(-70f, 5.55f, 329.38f), 0f, g);
        var dfw = MeshFactory.Text3D("DFW9", "DFW9  ·  RELAY", new Vector3(-70, 3.85f, 329.29f), 0.48f, new Color(0.92f, 0.52f, 0.12f), g);
        MeshFactory.FaceBack(dfw);
        float officeStepY = SurfaceY(new Vector3(-70f, 0f, 328.9f));
        for (int i = 0; i < 3; i++)
            MeshFactory.Box("OfficeStep", new Vector3(9, 0.18f, 0.8f), new Vector3(-70, officeStepY + 0.09f + i * 0.16f, 328.9f - i * 0.65f), concreteDark, g, false);

        // ---- dock doors
        MeshFactory.Box("DockCanopy", new Vector3(196, 0.5f, 10.5f), new Vector3(25, 9.1f, 324.2f), whitePaint, g, false);
        MeshFactory.Box("DockCanopyFascia", new Vector3(196.5f, 1.5f, 0.45f), new Vector3(25, 8.6f, 318.95f), blue, g, false);
        for (int i = 0; i < 8; i++)
            MeshFactory.Cyl("DockColumn", 0.22f, 9.0f, new Vector3(-66 + i * 26f, 4.5f, 320.0f), metal, g, false);

        for (int i = 0; i < 14; i++)
        {
            float x = -35 + i * 12.5f;
            MeshFactory.Box("DockDoor", new Vector3(5.4f, 5.8f, 0.35f), new Vector3(x, 3.1f, 329.55f), dark, g, false);
            MeshFactory.Box("DockDoorRib", new Vector3(5.2f, 0.15f, 0.12f), new Vector3(x, 4.8f, 329.33f), metal, g, false);
            MeshFactory.Box("DockDoorRib", new Vector3(5.2f, 0.15f, 0.12f), new Vector3(x, 3.9f, 329.33f), metal, g, false);
            MeshFactory.Box("DockFrameL", new Vector3(0.38f, 6.2f, 0.6f), new Vector3(x - 2.92f, 3.1f, 329.35f), yellowPaint, g, false);
            MeshFactory.Box("DockFrameR", new Vector3(0.38f, 6.2f, 0.6f), new Vector3(x + 2.92f, 3.1f, 329.35f), yellowPaint, g, false);
            MeshFactory.Box("DockBumperL", new Vector3(0.5f, 0.5f, 0.3f), new Vector3(x - 2.4f, 0.7f, 329.1f), dark, g, false);
            MeshFactory.Box("DockBumperR", new Vector3(0.5f, 0.5f, 0.3f), new Vector3(x + 2.4f, 0.7f, 329.1f), dark, g, false);
            var num = MeshFactory.Text3D("DockNum", "A-" + (i + 1).ToString("00"), new Vector3(x, 6.65f, 329.27f), 0.43f, new Color(0.92f, 0.95f, 0.97f), g);
            MeshFactory.FaceBack(num);
            MeshFactory.Box("DockLamp", new Vector3(0.9f, 0.16f, 0.25f), new Vector3(x, 7.45f, 329.18f), orangeGlow, g, false);
            MeshFactory.Box("DockLampHood", new Vector3(0.95f, 0.1f, 0.4f), new Vector3(x, 7.55f, 329.15f), dark, g, false);
            // foam dock shelter and a steel leveller lip at the floor line
            MeshFactory.Box("DockSealTop", new Vector3(6.0f, 0.55f, 0.55f), new Vector3(x, 6.25f, 329.05f), dark, g, false);
            MeshFactory.Box("DockSealL", new Vector3(0.5f, 5.9f, 0.55f), new Vector3(x - 2.9f, 3.35f, 329.05f), dark, g, false);
            MeshFactory.Box("DockSealR", new Vector3(0.5f, 5.9f, 0.55f), new Vector3(x + 2.9f, 3.35f, 329.05f), dark, g, false);
            MeshFactory.Box("DockLeveler", new Vector3(4.8f, 0.06f, 2.2f), new Vector3(x, SurfaceY(new Vector3(x, 0f, 328.0f)) + 0.03f, 328.0f), metal, g, false);
        }

        // wall packs along the facade
        for (int i = 0; i < 6; i++)
        {
            float x = -80 + i * 42f;
            MeshFactory.Box("WallPack", new Vector3(0.8f, 0.3f, 0.3f), new Vector3(x, 10.2f, 329.65f), orangeGlow, g, false);
        }

        // ---- yard fences and trees beyond
        FenceLine(g, new Vector3(-104, 0, 210), new Vector3(-104, 0, 380), 3.1f);
        FenceLine(g, new Vector3(154, 0, 210), new Vector3(154, 0, 380), 3.1f);
        for (int i = 0; i < 18; i++)
        {
            float x = -135 + (i % 9) * 34f;
            float z = i < 9 ? 396 : 430;
            Tree(g, new Vector3(x, 0, z), 0.85f + (i % 3) * 0.25f);
        }

        // ---- target trailer A-14, staged in the yard
        var croot = new GameObject("Trailer_A14").transform; croot.SetParent(g, false);
        // Yard concrete top is 0.12; the trailer wheel bottoms sit 0.01 above its origin.
        croot.position = new Vector3(26, 0.11f, 282);
        // staged nose-out, facing the inbound lane: a tractor swings around in
        // the open lot, lines up with the nose and backs straight under it
        croot.rotation = Quaternion.identity;
        var containerPrefab = Resources.Load<GameObject>("Models/Amazon_53ft_Container");
        var cmodel = Object.Instantiate(containerPrefab, croot);
        cmodel.name = "ContainerVisual";
        cmodel.transform.localPosition = Vector3.zero;
        cmodel.transform.localRotation = Quaternion.identity;
        cmodel.transform.localScale = Vector3.one;
        ApplyContainerMaterials(cmodel);
        // The FBX carries a flat seal tag high on the right door. The gameplay
        // seal is modeled separately on the left lock rod with its cable and
        // hasp, so remove the baked duplicate and leave one readable 482913 tag.
        foreach (var t in cmodel.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && (t.name == "SealTag" || t.name == "SealNumber"))
                Object.Destroy(t.gameObject);
        }
        AddContactShadow(croot, new Vector3(0f, SurfaceY(croot.position) - croot.position.y + 0.015f, 0f), new Vector2(3.5f, 19.6f));
        var ccol = croot.gameObject.AddComponent<BoxCollider>(); ccol.center = new Vector3(0, 2.15f, 0f); ccol.size = new Vector3(3.0f, 4.3f, 18.3f);
        refs.container = croot;
        refs.targetTrailer = croot;

        // Amazon wordmarks on the trailer sides
        SignQuad("TrailerLogoW", "amazon_logo_navy", 5.4f, 2.05f, new Vector3(-1.60f, 3.25f, 0.0f), 90f, croot);
        SignQuad("TrailerLogoE", "amazon_logo_navy", 5.4f, 2.05f, new Vector3(1.60f, 3.25f, 0.0f), -90f, croot);
        SignQuad("TrailerLogoRear", "amazon_logo_navy", 1.55f, 0.60f, new Vector3(0f, 3.86f, 9.27f), 180f, croot);

        // rear conspicuity tape
        for (int i = 0; i < 12; i++)
        {
            float x = -1.38f + i * 0.25f;
            MeshFactory.Box("RearTape", new Vector3(0.22f, 0.11f, 0.015f), new Vector3(x, 1.06f, 9.27f),
                (i % 2 == 0) ? whitePaint : red, croot, false);
        }
        // rear door hardware, lamps, plate and mud flaps
        for (int s = -1; s <= 1; s += 2)
        {
            MeshFactory.Box("LockRod", new Vector3(0.035f, 2.05f, 0.035f), new Vector3(s * 0.62f, 2.08f, 9.30f), metal, croot, false);
            MeshFactory.Box("Hinge", new Vector3(0.14f, 0.10f, 0.06f), new Vector3(s * 1.40f, 2.65f, 9.31f), metal, croot, false);
            MeshFactory.Box("Hinge", new Vector3(0.14f, 0.10f, 0.06f), new Vector3(s * 1.40f, 1.65f, 9.31f), metal, croot, false);
            MeshFactory.Box("RearLamp", new Vector3(0.18f, 0.11f, 0.03f), new Vector3(s * 0.86f, 1.02f, 9.31f), red, croot, false);
            MeshFactory.Box("MudFlap", new Vector3(0.55f, 0.52f, 0.035f), new Vector3(s * 1.00f, 0.36f, 6.55f), dark, croot, false);
        }
        MeshFactory.Box("RearPlate", new Vector3(0.52f, 0.25f, 0.02f), new Vector3(0.25f, 1.34f, 9.31f), whitePaint, croot, false);
        var plateTxt = MeshFactory.Text3D("RearPlateTxt", "TEXAS  7F-4829", new Vector3(0.25f, 1.34f, 9.326f), 0.058f, new Color(0.06f, 0.07f, 0.09f), croot);
        MeshFactory.FaceBack(plateTxt);

        // seal 482913: a small cable seal on the left lock rod, with a readable tag.
        // Real seals are palm-sized, not billboards; the tag hangs just to the side
        // of the rod so the number is never hidden by the hardware.
        var sealPlate = MeshFactory.Mat("SealPlate", new Color(0.95f, 0.68f, 0.06f), 0.25f, 0.42f);
        MeshFactory.Box("SealHasp", new Vector3(0.075f, 0.16f, 0.025f), new Vector3(-0.62f, 1.70f, 9.272f), metal, croot, false);
        MeshFactory.Box("SealPlateEdge", new Vector3(0.205f, 0.125f, 0.012f), new Vector3(-0.30f, 1.55f, 9.286f), dark, croot, false);
        MeshFactory.Box("SealPlate", new Vector3(0.178f, 0.100f, 0.008f), new Vector3(-0.30f, 1.55f, 9.294f), sealPlate, croot, false);
        var sealTxt = MeshFactory.Text3D("SealNumber", "482913", new Vector3(-0.30f, 1.538f, 9.300f), 0.046f, new Color(0.10f, 0.075f, 0.035f), croot);
        var sealTxt2 = MeshFactory.Text3D("SealCaption", "SEAL", new Vector3(-0.30f, 1.588f, 9.300f), 0.018f, new Color(0.10f, 0.075f, 0.035f), croot);
        MeshFactory.FaceBack(sealTxt);
        MeshFactory.FaceBack(sealTxt2);
        var cable = MeshFactory.Cyl("SealCable", 0.006f, 0.33f, new Vector3(-0.46f, 1.65f, 9.284f), metal, croot, false);
        cable.transform.localRotation = Quaternion.Euler(0f, 0f, -104f);
        MeshFactory.Box("SealLock", new Vector3(0.045f, 0.075f, 0.035f), new Vector3(-0.30f, 1.662f, 9.282f), metal, croot, false);

        // inspection points sit just outside the trailer shell so the interaction ray reaches them
        AddInspection(flow, refs, croot, "seal", "INSPECT SEAL", "Check seal 482913 against the paperwork. Photograph the number before you move.", new Vector3(-0.30f, 1.60f, 9.55f), true, new Vector3(0.0f, 0.44f, 0.05f));
        AddInspection(flow, refs, croot, "locks", "CHECK DOOR LOCKS", "Both rear doors must be closed and both lock rods seated. Look for damage to the hinges.", new Vector3(0.85f, 2.25f, 9.55f), false);
        AddInspection(flow, refs, croot, "gear", "CHECK LANDING GEAR", "Landing gear must be down and the feet on solid ground while the trailer is parked.", new Vector3(-1.85f, 1.15f, -5.0f), false);
        AddInspection(flow, refs, croot, "tires", "CHECK TIRES", "Look for cuts, bulges, exposed cord, and loose lug nuts.", new Vector3(1.72f, 0.82f, 5.9f), false);
        AddInspection(flow, refs, croot, "guard", "CHECK UNDER-RIDE GUARD", "The rear under-ride guard and lights must be intact and secure.", new Vector3(0f, 0.95f, 9.75f), false);

        // coupling: kingpin, landing gear, glad hands and articulation
        var rig = croot.gameObject.AddComponent<TrailerRig>();
        rig.Init(flow);
        refs.trailerRig = rig;
        if (refs.truck != null)
        {
            refs.truck.hookTarget = rig;
            refs.truck.SetFifthWheelTop(rig.kingpinLocal.y + 0.025f);
        }
        // painted lead-in lines in front of the nose help line the tractor up
        var yardYellow = MeshFactory.Mat("YardYellow", new Color(0.86f, 0.64f, 0.10f), 0f, 0.3f);
        for (int s = -1; s <= 1; s += 2)
        {
            var lead = MeshFactory.Box("HookLeadIn", new Vector3(0.16f, 0.02f, 9.0f), Vector3.zero, yardYellow, g, false);
            Vector3 lp = croot.TransformPoint(new Vector3(s * 1.62f, 0f, rig.noseLocalZ - 5.0f));
            lp.y = SurfaceY(lp) + 0.012f;
            lead.transform.position = lp;
            lead.transform.rotation = croot.rotation;
        }

        // A-14 yard sign, right beside the staged trailer's left rear corner
        var pylon = new GameObject("A14Pylon").transform; pylon.SetParent(g, false); pylon.position = OnGround(35.0f, 287.0f);
        MeshFactory.Box("PostL", new Vector3(0.14f, 3.2f, 0.14f), new Vector3(-1.15f, 1.60f, 0), metal, pylon, false);
        MeshFactory.Box("PostR", new Vector3(0.14f, 3.2f, 0.14f), new Vector3(1.15f, 1.60f, 0), metal, pylon, false);
        MeshFactory.Box("Board", new Vector3(2.65f, 1.45f, 0.12f), new Vector3(0, 2.80f, 0), navy, pylon, false);
        SignQuad("A14Sign", "dock_a14", 2.18f, 1.33f, new Vector3(0f, 2.80f, -0.08f), 0f, pylon);
        MeshFactory.Box("Beacon", new Vector3(0.55f, 0.13f, 0.22f), new Vector3(0, 3.67f, 0), orangeGlow, pylon, false);

        // unit number repeated on the rear door, where a driver walks up
        MeshFactory.Box("RearA14Plate", new Vector3(0.94f, 0.42f, 0.018f), new Vector3(0.94f, 3.52f, 9.318f), navy, croot, false);
        var rearA14 = MeshFactory.Text3D("RearA14Text", "A-14", new Vector3(0.94f, 3.52f, 9.332f), 0.19f, new Color(0.95f, 0.96f, 0.94f), croot);
        MeshFactory.FaceBack(rearA14);

        // ---- two more staged trailers flank A-14 so the yard reads like a working lot
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -10f : 63f;
            var st = new GameObject("StagedTrailer").transform; st.SetParent(g, false);
            st.position = new Vector3(x, 0.11f, 283);
            st.rotation = Quaternion.Euler(0, 180, 0);
            var sm = Object.Instantiate(containerPrefab, st);
            sm.name = "ContainerVisual";
            sm.transform.localPosition = Vector3.zero;
            sm.transform.localRotation = Quaternion.identity;
            sm.transform.localScale = Vector3.one;
            ApplyContainerMaterials(sm);
            AddContactShadow(st, new Vector3(0f, SurfaceY(st.position) - st.position.y + 0.015f, 0f), new Vector2(3.0f, 16.8f));
            var sc = st.gameObject.AddComponent<BoxCollider>(); sc.center = new Vector3(0, 2.15f, 0); sc.size = new Vector3(3.0f, 4.3f, 18.3f);
            SignQuad("TrailerLogoW", "amazon_logo_navy", 5.4f, 2.05f, new Vector3(-1.60f, 3.25f, 0f), 90f, st);
            SignQuad("TrailerLogoE", "amazon_logo_navy", 5.4f, 2.05f, new Vector3(1.60f, 3.25f, 0f), -90f, st);
        }

        // ---- docked trailers, backed into the doors
        for (int i = 0; i < 5; i++)
        {
            float x = -58 + i * 38f;
            var bg = new GameObject("DockedTrailer").transform; bg.SetParent(g, false);
            bg.position = new Vector3(x, 0.15f, 319.4f);
            bg.rotation = Quaternion.identity;
            var bm = Object.Instantiate(containerPrefab, bg);
            bm.name = "ContainerVisual";
            bm.transform.localPosition = Vector3.zero;
            bm.transform.localRotation = Quaternion.identity;
            bm.transform.localScale = Vector3.one;
            ApplyContainerMaterials(bm);
            AddContactShadow(bg, new Vector3(0f, SurfaceY(bg.position) - bg.position.y + 0.015f, 0f), new Vector2(3.0f, 16.8f));
            var bc = bg.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 2.15f, 0); bc.size = new Vector3(3.0f, 4.3f, 18.3f);
            SignQuad("TrailerLogoW", "amazon_logo_navy", 5.0f, 1.9f, new Vector3(-1.55f, 3.2f, 0f), 90f, bg);
        }

        // ---- yard markings: staged-trailer stalls and the inbound lane
        for (int i = 0; i < 7; i++)
        {
            float x = -24 + i * 18f;
            float ly = SurfaceY(new Vector3(x, 0f, 284f)) + 0.012f;
            MeshFactory.Box("StallLine", new Vector3(0.18f, 0.02f, 25f), new Vector3(x, ly, 284), roadLine, g, false);
        }
        for (int i = 0; i < 9; i++)
        {
            float z = 226 + i * 9f;
            float ly = SurfaceY(new Vector3(8f, 0f, z)) + 0.012f;
            MeshFactory.Box("LaneMark", new Vector3(0.2f, 0.02f, 5f), new Vector3(8f, ly, z), roadLine, g, false);
        }
        // directional arrows toward the trailer
        for (int i = 0; i < 3; i++)
        {
            float z = 232 + i * 12f;
            float ay = SurfaceY(new Vector3(13.2f, 0f, z)) + 0.012f;
            MeshFactory.Box("ArrowShaft", new Vector3(0.4f, 0.02f, 3.4f), new Vector3(13.2f, ay, z), roadLine, g, false);
            MeshFactory.Box("ArrowHeadL", new Vector3(0.35f, 0.02f, 1.6f), new Vector3(12.7f, ay, z + 1.3f), roadLine, g, false).transform.rotation = Quaternion.Euler(0, -38, 0);
            MeshFactory.Box("ArrowHeadR", new Vector3(0.35f, 0.02f, 1.6f), new Vector3(13.7f, ay, z + 1.3f), roadLine, g, false).transform.rotation = Quaternion.Euler(0, 38, 0);
        }

        // pallets and a couple of chocks staged by A-14
        for (int i = 0; i < 3; i++)
        {
            float px = 33.0f + (i % 2) * 1.4f;
            float pz = 275.5f + (i / 2) * 1.3f;
            float baseY = SurfaceY(new Vector3(px, 0f, pz));
            for (int b = 0; b < 4; b++)
                MeshFactory.Box("PalletBoard", new Vector3(1.15f, 0.045f, 0.20f), new Vector3(px, baseY + 0.1225f + b * 0.13f, pz - 0.35f + b * 0.16f), wood, g, true);
            MeshFactory.Box("PalletBlock", new Vector3(0.15f, 0.10f, 0.9f), new Vector3(px - 0.42f, baseY + 0.05f, pz), wood, g, false);
            MeshFactory.Box("PalletBlock", new Vector3(0.15f, 0.10f, 0.9f), new Vector3(px + 0.42f, baseY + 0.05f, pz), wood, g, false);
        }
        // wheel chocks behind the trailer's rear tires, clear of its path when it pulls out
        if (refs.trailerRig != null)
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 cp = refs.trailerRig.transform.TransformPoint(new Vector3(s * 1.55f, 0f, refs.trailerRig.axleLocalZ + 1.35f));
                MeshFactory.Box("Chock", new Vector3(0.4f, 0.22f, 0.26f), new Vector3(cp.x, SurfaceY(cp) + 0.11f, cp.z), orange, g, true);
            }
    }

    static void ApplyContainerMaterials(GameObject model)
    {
        var boxWhite = MeshFactory.Mat("BoxWhite", new Color(0.80f, 0.81f, 0.81f), 0.18f, 0.46f);
        var boxRib = MeshFactory.Mat("BoxRib", new Color(0.72f, 0.73f, 0.74f), 0.15f, 0.50f);
        var boxNavy = navy;
        var boxBlue = blue;
        var steel = metal;
        var chromeM = chrome;
        var darkM = dark;
        var rubber = MeshFactory.Mat("Rubber", new Color(0.012f, 0.013f, 0.014f), 0f, 0.9f);
        var redLamp = MeshFactory.Mat("RedLamp", new Color(0.70f, 0.015f, 0.008f), 0f, 0.3f);
        redLamp.EnableKeyword("_EMISSION"); redLamp.SetColor("_EmissionColor", new Color(0.8f, 0.01f, 0f) * 1.7f);
        var amber = orangeGlow;
        var yellow = MeshFactory.Mat("SealYellow", new Color(0.92f, 0.62f, 0.05f), 0.2f, 0.35f);
        var tape = MeshFactory.Mat("Reflective", new Color(0.95f, 0.95f, 0.90f), 0.1f, 0.25f);
        var refRed = MeshFactory.Mat("ReflectiveRed", new Color(0.75f, 0.04f, 0.02f), 0.1f, 0.3f);
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i]; if (m == null) continue;
                string n = m.name.ToLowerInvariant();
                if (n.Contains("box_white")) mats[i] = boxWhite;
                else if (n.Contains("box_rib")) mats[i] = boxRib;
                else if (n.Contains("box_navy")) mats[i] = boxNavy;
                else if (n.Contains("box_blue")) mats[i] = boxBlue;
                else if (n.Contains("steel")) mats[i] = steel;
                else if (n.Contains("chrome")) mats[i] = chromeM;
                else if (n.Contains("dark")) mats[i] = darkM;
                else if (n.Contains("rubber")) mats[i] = rubber;
                else if (n.Contains("red")) mats[i] = redLamp;
                else if (n.Contains("amber")) mats[i] = amber;
                else if (n.Contains("sealyellow")) mats[i] = yellow;
                else if (n.Contains("reflectivered")) mats[i] = refRed;
                else if (n.Contains("reflective")) mats[i] = tape;
            }
            r.sharedMaterials = mats;
            r.receiveShadows = true;
        }
    }

    static void BuildGate(GameFlow flow, Transform root, WorldRefs refs)
    {
        var g = new GameObject("Gate").transform; g.SetParent(root, false);

        // ---- gantry over the entrance
        float gantryY = SurfaceY(new Vector3(-9.6f, 0f, 205f));
        MeshFactory.Box("GantryPostL", new Vector3(0.65f, 8.3f, 0.65f), new Vector3(-9.6f, gantryY + 4.15f, 205), metal, g, true);
        MeshFactory.Box("GantryPostR", new Vector3(0.65f, 8.3f, 0.65f), new Vector3(9.6f, gantryY + 4.15f, 205), metal, g, true);
        MeshFactory.Box("GantryBeam", new Vector3(20.2f, 0.6f, 0.5f), new Vector3(0, 7.8f, 205), metal, g, false);
        MeshFactory.Box("GantryBeam2", new Vector3(20.2f, 0.25f, 0.35f), new Vector3(0, 7.35f, 205), dark, g, false);
        MeshFactory.Box("GateSignBacker", new Vector3(12.6f, 5.0f, 0.4f), new Vector3(0, 9.85f, 205), navy, g, false);
        SignQuad("AmazonGateSign", "amazon_gate", 11.9f, 4.61f, new Vector3(0f, 9.85f, 204.75f), 0f, g);
        for (int i = 0; i < 3; i++)
            MeshFactory.Box("GateLamp", new Vector3(0.5f, 0.16f, 0.3f), new Vector3(-4 + i * 4, 12.58f, 204.9f), orangeGlow, g, false);

        // ---- guard booth, close to the lane and facing the arriving driver
        MeshFactory.Box("GuardBooth", new Vector3(5.2f, 3.8f, 5.2f), new Vector3(11.6f, 1.9f, 201.4f), offWhite, g, true);
        MeshFactory.Box("BoothBase", new Vector3(5.5f, 0.5f, 5.5f), new Vector3(11.6f, 0.25f, 201.4f), concreteDark, g, false);
        MeshFactory.Box("BoothRoof", new Vector3(5.9f, 0.35f, 5.9f), new Vector3(11.6f, 3.95f, 201.4f), dark, g, false);
        MeshFactory.Box("BoothRoofLip", new Vector3(6.3f, 0.14f, 6.3f), new Vector3(11.6f, 4.18f, 201.4f), metal, g, false);
        MeshFactory.Box("BoothGlassFront", new Vector3(4.2f, 1.55f, 0.12f), new Vector3(11.6f, 2.35f, 198.75f), glass, g, false);
        MeshFactory.Box("BoothGlassSide", new Vector3(0.12f, 1.55f, 4.0f), new Vector3(8.95f, 2.35f, 201.4f), glass, g, false);
        MeshFactory.Box("BoothFrame", new Vector3(4.4f, 0.16f, 0.2f), new Vector3(11.6f, 3.16f, 198.76f), dark, g, false);
        MeshFactory.Box("BoothSill", new Vector3(4.4f, 0.18f, 0.3f), new Vector3(11.6f, 1.52f, 198.8f), dark, g, false);
        MeshFactory.Box("BoothLamp", new Vector3(0.9f, 0.16f, 0.28f), new Vector3(11.6f, 4.10f, 198.85f), orangeGlow, g, false);
        MeshFactory.Box("BoothDesk", new Vector3(3.8f, 0.12f, 0.1f), new Vector3(11.6f, 1.6f, 199.35f), dark, g, false);
        MeshFactory.Box("BoothRadio", new Vector3(0.38f, 0.22f, 0.26f), new Vector3(9.9f, 1.72f, 199.5f), dark, g, false);
        MeshFactory.Box("BoothLampGlow", new Vector3(0.22f, 0.10f, 0.18f), new Vector3(10.2f, 1.78f, 199.45f), orangeGlow, g, false);
        MeshFactory.Box("BoothTrafficRed", new Vector3(0.22f, 0.22f, 0.18f), new Vector3(9.2f, 3.42f, 198.72f), red, g, false);
        MeshFactory.Box("BoothTrafficGreen", new Vector3(0.22f, 0.22f, 0.18f), new Vector3(9.2f, 3.12f, 198.72f), green, g, false);
        var guard = new GameObject("Guard").transform; guard.SetParent(g, false); guard.position = new Vector3(11.6f, 1.2f, 200.6f);
        MeshFactory.Cyl("GuardBody", 0.32f, 1.1f, new Vector3(0, 0.55f, 0), blue, guard, false);
        MeshFactory.Sphere("GuardHead", 0.19f, new Vector3(0, 1.3f, 0), skin, guard);
        MeshFactory.Box("GuardCap", new Vector3(0.44f, 0.10f, 0.36f), new Vector3(0, 1.47f, 0), dark, guard, false);
        var gb = MeshFactory.Text3D("SecurityText", "SECURITY  ·  GATE 1", new Vector3(11.6f, 3.55f, 198.64f), 0.24f, new Color(0.95f, 0.6f, 0.2f), g);
        MeshFactory.FaceBack(gb);
        var chk = MeshFactory.Text3D("CheckInText", "ALL DRIVERS CHECK IN", new Vector3(11.6f, 0.78f, 198.68f), 0.19f, new Color(0.10f, 0.16f, 0.20f), g);
        MeshFactory.FaceBack(chk);
        // Warm booth light so the check-in window reads as occupied at dawn.
        var boothLamp = new GameObject("BoothLight"); boothLamp.transform.SetParent(g, false); boothLamp.transform.position = new Vector3(11.6f, 2.55f, 200.7f);
        var bl = boothLamp.AddComponent<Light>(); bl.type = LightType.Point; bl.range = 5.6f; bl.intensity = 1.25f;
        bl.color = new Color(1f, 0.78f, 0.50f); bl.shadows = LightShadows.None;

        // Guard stands on the shoulder beside the stop line, clear of both the
        // 14 m roadway and the booth footprint, angled toward the driver's window.
        GuardFigure(g, OnGround(8.35f, 197.5f), 205f);

        // ---- barrier arm
        var pivot = new GameObject("BarrierPivot").transform; pivot.SetParent(g, false); pivot.position = new Vector3(8.55f, 1.3f, 207.2f);
        // Solid collider: the closed arm has to stop a truck and the chase
        // camera, and it rotates out of the way with the pivot when opened.
        MeshFactory.Box("BarrierArm", new Vector3(16.8f, 0.18f, 0.18f), new Vector3(-8.4f, 0, 0), whitePaint, pivot, true);
        for (int i = 0; i < 8; i++)
            MeshFactory.Box("Stripe", new Vector3(0.9f, 0.22f, 0.22f), new Vector3(-15.4f + i * 2.05f, 0, 0), orange, pivot, false);
        float barrierY = SurfaceY(new Vector3(8.55f, 0f, 207.2f));
        MeshFactory.Box("BarrierSupport", new Vector3(0.55f, 1.55f, 0.55f), new Vector3(8.55f, barrierY + 0.775f, 207.2f), metal, g, false);
        MeshFactory.Box("BarrierLight", new Vector3(0.28f, 0.28f, 0.28f), new Vector3(8.55f, barrierY + 1.69f, 207.2f), orangeGlow, g, false);
        var gc = g.gameObject.AddComponent<GateController>(); gc.arm = pivot; refs.gate = gc;

        // ---- stop line
        MeshFactory.Box("StopLine", new Vector3(14.2f, 0.03f, 0.55f), new Vector3(0, SurfaceY(new Vector3(0f, 0f, 196.5f)) + 0.015f, 196.5f), roadLine, g, false);
        for (int i = 0; i < 8; i++)
        {
            float cx = -7 + i * 2f;
            MeshFactory.Box("Crosswalk", new Vector3(4.5f, 0.03f, 0.45f), new Vector3(cx, SurfaceY(new Vector3(cx, 0f, 199.2f)) + 0.015f, 199.2f), roadLine, g, false);
        }

        // ---- fence on both sides of the gate
        FenceLine(g, new Vector3(-110, 0, 206.5f), new Vector3(-9.6f, 0, 206.5f), 2.9f);
        FenceLine(g, new Vector3(9.6f, 0, 206.5f), new Vector3(155, 0, 206.5f), 2.9f);
        FenceLine(g, new Vector3(155, 0, 206.5f), new Vector3(155, 0, 220), 2.9f);
        FenceLine(g, new Vector3(-110, 0, 206.5f), new Vector3(-110, 0, 220), 2.9f);
        // fence gate sign
        // (The old floating GateDirection panel sat north of the barrier with its
        // back to the driver, so it appeared as a mirrored Amazon board over the
        // warehouse. The main gantry sign already identifies the gate.)

        // ---- waypoint anchor
        // The HUD waypoint is a screen-space marker anchored to this fixed point
        // above the gantry. It is deliberately not parented to the barrier arm:
        // that transform swings, which is why the old "96 m" label used to ride
        // the arm instead of staying over the gate.
        var gateWp = new GameObject("GateWaypoint").transform;
        gateWp.SetParent(g, false);
        gateWp.position = new Vector3(0f, 6.2f, 205f);
        refs.gateWaypoint = gateWp;

        // ---- trigger before the arm
        var trig = new GameObject("GateTrigger"); trig.transform.SetParent(g, false); trig.transform.position = new Vector3(0, 2, 188);
        var bc = trig.AddComponent<BoxCollider>(); bc.size = new Vector3(15, 5.5f, 10); bc.isTrigger = true;
        var gt = trig.AddComponent<GateTrigger>(); gt.flow = flow; refs.gateTrigger = gt;

        // ---- perimeter fence along the yard
        FenceLine(g, new Vector3(-110, 0, 206.5f), new Vector3(-110, 0, 380), 3.1f);
        FenceLine(g, new Vector3(155, 0, 206.5f), new Vector3(155, 0, 380), 3.1f);
        // Close the back of the yard. Without this a driver could leave through
        // either side of the warehouse even though the front and sides were fenced.
        FenceLine(g, new Vector3(-110, 0, 380), new Vector3(155, 0, 380), 3.1f);
        for (int i = 0; i < 10; i++)
            Tree(g, new Vector3(-120 + (i % 5) * 8, 0, 208 + (i / 5) * 6), 0.9f);
    }

    static void GuardFigure(Transform parent, Vector3 pos, float yaw)
    {
        var hiVis = MeshFactory.Mat("HiVis", new Color(0.86f, 0.94f, 0.10f), 0f, 0.62f);
        var trousers = MeshFactory.Mat("GuardTrousers", new Color(0.055f, 0.065f, 0.08f), 0f, 0.78f);
        var reflect = MeshFactory.Mat("VestStripe", new Color(0.92f, 0.94f, 0.90f), 0.15f, 0.35f);

        var g = new GameObject("GateGuard").transform; g.SetParent(parent, false);
        g.position = pos; g.rotation = Quaternion.Euler(0, yaw, 0);
        var col = g.gameObject.AddComponent<CapsuleCollider>();
        col.radius = 0.30f; col.height = 1.85f; col.center = new Vector3(0f, 0.93f, 0f);
        for (int s = -1; s <= 1; s += 2)
        {
            MeshFactory.Box("Boot", new Vector3(0.20f, 0.16f, 0.32f), new Vector3(s * 0.14f, 0.09f, 0.06f), dark, g, false);
            MeshFactory.Box("Leg", new Vector3(0.20f, 0.72f, 0.24f), new Vector3(s * 0.14f, 0.48f, 0f), trousers, g, false);
            MeshFactory.Box("Arm", new Vector3(0.15f, 0.56f, 0.18f), new Vector3(s * 0.35f, 1.10f, 0f), trousers, g, false);
            MeshFactory.Sphere("Hand", 0.075f, new Vector3(s * 0.35f, 0.82f, 0.02f), skin, g);
        }
        MeshFactory.Box("Vest", new Vector3(0.52f, 0.62f, 0.29f), new Vector3(0f, 1.18f, 0f), hiVis, g, false);
        MeshFactory.Box("VestFrontPanel", new Vector3(0.34f, 0.62f, 0.025f), new Vector3(0f, 1.18f, 0.158f), hiVis, g, false);
        MeshFactory.Box("StripeH1", new Vector3(0.46f, 0.055f, 0.30f), new Vector3(0f, 1.30f, 0.01f), reflect, g, false);
        MeshFactory.Box("StripeH2", new Vector3(0.46f, 0.055f, 0.30f), new Vector3(0f, 1.04f, 0.01f), reflect, g, false);
        MeshFactory.Box("StripeV1", new Vector3(0.055f, 0.60f, 0.025f), new Vector3(-0.15f, 1.18f, 0.17f), reflect, g, false);
        MeshFactory.Box("StripeV2", new Vector3(0.055f, 0.60f, 0.025f), new Vector3(0.15f, 1.18f, 0.17f), reflect, g, false);
        MeshFactory.Cyl("Neck", 0.075f, 0.12f, new Vector3(0f, 1.53f, 0f), skin, g, false);
        MeshFactory.Sphere("Head", 0.155f, new Vector3(0f, 1.67f, 0f), skin, g);
        MeshFactory.Cyl("HardHat", 0.185f, 0.13f, new Vector3(0f, 1.79f, 0f), whitePaint, g, false);
        MeshFactory.Box("HardHatBrim", new Vector3(0.40f, 0.035f, 0.40f), new Vector3(0f, 1.735f, 0.04f), whitePaint, g, false);
        // clipboard in the left hand
        MeshFactory.Box("Clipboard", new Vector3(0.30f, 0.38f, 0.025f), new Vector3(0.34f, 1.05f, 0.17f), dark, g, false);
        MeshFactory.Box("ClipPaper", new Vector3(0.25f, 0.32f, 0.008f), new Vector3(0.34f, 1.06f, 0.185f), whitePaint, g, false);
    }

    static void BuildReflectionProbes(Transform root)
    {
        // The yard probe travels with the destination; this root only owns the
        // tight stop probe around the truck at the travel center.
        var g = new GameObject("ReflectionProbes").transform; g.SetParent(root, false);
        Probe(g, "StopReflection", new Vector3(0f, 5f, 6f), new Vector3(90f, 18f, 70f), 128);
    }

    static void Probe(Transform parent, string name, Vector3 pos, Vector3 size, int resolution)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = pos;
        var rp = go.AddComponent<ReflectionProbe>();
        rp.mode = ReflectionProbeMode.Realtime;
        rp.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        rp.size = size;
        rp.resolution = resolution;
        rp.hdr = true;
        rp.intensity = 1f;
        rp.importance = 1;
        rp.nearClipPlane = 0.25f;
        rp.farClipPlane = 500f;
        rp.RenderProbe();
    }

    static void BuildYardProps(Transform root)
    {
        var g = new GameObject("YardProps").transform; g.SetParent(root, false);
        for (int i = 0; i < 5; i++) LightPole(g, new Vector3(-70 + i * 40, 0, 238), 14f);
        for (int i = 0; i < 5; i++) LightPole(g, new Vector3(-50 + i * 42, 0, 318), 14f);
        for (int i = 0; i < 12; i++)
            Cone(g, new Vector3(43 + (i % 6) * 7, 0, 256 + (i / 6) * 6));
        // bins and bollards at the office
        float dumpY = SurfaceY(new Vector3(-83f, 0f, 325f));
        MeshFactory.Box("Dumpster", new Vector3(2.2f, 1.5f, 1.4f), new Vector3(-83, dumpY + 0.75f, 325), green, g, true);
        MeshFactory.Box("DumpsterLid", new Vector3(2.3f, 0.16f, 1.5f), new Vector3(-83, dumpY + 1.58f, 325), dark, g, false);
        float bollardY = SurfaceY(new Vector3(-58f, 0f, 326f));
        for (int i = 0; i < 4; i++)
            MeshFactory.Cyl("Bollard", 0.16f, 1.2f, new Vector3(-58 + i * 2.4f, bollardY + 0.6f, 326), yellowPaint, g, true);
        // parked pickup in the staff lot
        var car = new GameObject("Pickup").transform; car.SetParent(g, false);
        car.position = new Vector3(-92f, SurfaceY(new Vector3(-92f, 0f, 238f)), 238f); car.rotation = Quaternion.Euler(0, 12, 0);
        MeshFactory.Box("CarBody", new Vector3(1.9f, 0.8f, 4.8f), new Vector3(0, 0.75f, 0), dark, car, true);
        MeshFactory.Box("CarCab", new Vector3(1.7f, 0.75f, 2.0f), new Vector3(0, 1.45f, -0.3f), glass, car, false);
        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0) ? -0.9f : 0.9f;
            float z = (i < 2) ? 1.5f : -1.5f;
            MeshFactory.Cyl("CarWheel", 0.33f, 0.22f, new Vector3(x, 0.34f, z), dark, car, false).transform.rotation = Quaternion.Euler(0, 0, 90);
        }
    }

    // Realistic CC0 props from Poly Haven (see THIRD_PARTY_NOTICES.md): planted
    // beds around the store, street furniture, barriers and scrub along the
    // fence lines. Positions are fixed so every run looks the same.
    static void BuildRealProps(Transform root, Transform yard)
    {
        if (!PropLibrary.Has("Shrub_A")) return;
        var g = new GameObject("RealProps").transform; g.SetParent(root, false);
        var yg = new GameObject("DestinationProps").transform;
        yg.SetParent(yard != null ? yard : root, false);
        string[] shrubs = { "Shrub_A", "Bush_A", "Shrub_B", "Bush_B" };
        // planted beds along both store side walls
        for (int i = 0; i < 6; i++)
        {
            PropLibrary.Spawn(shrubs[i % 4], OnGround(-5.9f, -1.5f - i * 2.3f), i * 61f, 0.75f + (i % 3) * 0.1f, g);
            PropLibrary.Spawn(shrubs[(i + 1) % 4], OnGround(-30.2f, -1.5f - i * 2.3f), i * 47f, 0.75f + (i % 2) * 0.12f, g);
        }
        PropLibrary.Spawn("Hydrant", OnGround(-4.6f, 1.2f), 20f, 1f, g, true);
        PropLibrary.Spawn("TrashCan", OnGround(-10.6f, 1.0f), 15f, 1f, g, true);
        PropLibrary.Spawn("TrashCan", OnGround(-25.4f, 1.0f), 200f, 1f, g, true);
        PropLibrary.Spawn("UtilityBox", OnGround(-29.9f, -15.6f), 180f, 1f, g, true);
        PropLibrary.Spawn("UtilityBox", OnGround(-29.2f, -15.6f), 180f, 1f, g, true);
        Vector3 tyre = OnGround(-31.2f, -12.5f);
        for (int i = 0; i < 4; i++)
            PropLibrary.Spawn("Tyre", new Vector3(tyre.x, tyre.y + 0.16f * i + 0.08f, tyre.z), i * 40f, 1.6f, g).transform.rotation = Quaternion.Euler(90f, i * 37f, 0f);
        // jersey barriers closing off the west edge of the lot
        for (int i = 0; i < 8; i++)
            PropLibrary.Spawn("JerseyBarrier", OnGround(-36.5f, 4f + i * 3.3f), 90f + (i % 2) * 2f, 2.0f, g, true);
        // yard: boxes by the dumpster, a covered car in the staff lot
        Vector3 box = OnGround(-81.2f, 326.4f);
        for (int i = 0; i < 5; i++)
            PropLibrary.Spawn("CardboardBox", new Vector3(box.x + (i % 3) * 0.55f, box.y + (i / 3) * 0.34f, box.z), i * 23f, 1.4f, yg);
        PropLibrary.Spawn("CoveredCar", OnGround(-96.5f, 245f), 8f, 1.05f, yg, true);
        // scrub along the yard fence and the road verges
        for (int i = 0; i < 18; i++)
        {
            float z = 212f + i * 9.5f;
            PropLibrary.Spawn(shrubs[i % 4], OnGround(-107f + (i % 3) * 0.8f, z), i * 53f, 0.9f + (i % 4) * 0.15f, yg);
            PropLibrary.Spawn(shrubs[(i + 2) % 4], OnGround(158f - (i % 3) * 0.8f, z + 4f), i * 71f, 0.9f + (i % 3) * 0.18f, yg);
        }
        for (int i = 0; i < 24; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            PropLibrary.Spawn(shrubs[i % 4], OnGround(side * (12.5f + (i % 5) * 1.7f), 34f + i * 6.3f), i * 29f, 0.8f + (i % 3) * 0.2f, g);
        }
    }

    // Small things that say people work here: last night's puddles, a lit store,
    // pump-island clutter, signage at reading distance, pallets and chocks at the
    // docks. Everything is static, collider-free unless you could walk into it,
    // and kept low-key so it never competes with the truck or the trailer.
    static void BuildLivedInDetails(Transform root, Transform yard)
    {
        var g = new GameObject("LivedIn").transform; g.SetParent(root, false);
        var yg = new GameObject("DestinationDetails").transform;
        yg.SetParent(yard != null ? yard : root, false);
        var puddle = MeshFactory.Mat("Puddle", new Color(0.045f, 0.050f, 0.056f), 0f, 0.96f);
        var wood = MeshFactory.Mat("PalletWood", new Color(0.46f, 0.36f, 0.24f), 0f, 0.22f);
        var shelf = MeshFactory.Mat("StoreShelf", new Color(0.20f, 0.21f, 0.23f), 0.1f, 0.4f);
        var storeLight = MeshFactory.Mat("StoreInteriorLight", new Color(0.86f, 0.84f, 0.78f), 0f, 0.2f);
        storeLight.EnableKeyword("_EMISSION"); storeLight.SetColor("_EmissionColor", new Color(1f, 0.93f, 0.80f) * 0.85f);
        var neonRed = MeshFactory.Mat("NeonRed", new Color(0.9f, 0.08f, 0.04f), 0f, 0.3f);
        neonRed.EnableKeyword("_EMISSION"); neonRed.SetColor("_EmissionColor", new Color(1f, 0.10f, 0.04f) * 1.6f);
        var dockGreen = MeshFactory.Mat("DockLightGreen", new Color(0.1f, 0.8f, 0.2f), 0f, 0.3f);
        dockGreen.EnableKeyword("_EMISSION"); dockGreen.SetColor("_EmissionColor", new Color(0.1f, 1f, 0.25f) * 1.3f);
        var dockRed = MeshFactory.Mat("DockLightRed", new Color(0.8f, 0.06f, 0.04f), 0f, 0.3f);
        dockRed.EnableKeyword("_EMISSION"); dockRed.SetColor("_EmissionColor", new Color(1f, 0.08f, 0.03f) * 1.3f);
        var signWhite = new Color(0.92f, 0.93f, 0.92f);

        // ---- drying puddles: soft-edged dark wet patches (the old mirror discs read as
        // hard blue ellipses once the lot was no longer hidden under the dirt boxes)
        void Puddle(Transform parent, Vector3 p, float w, float l, float yaw)
        {
            p.y = SurfaceY(p) + 0.003f;
            if (T("wet_patch") != null)
            {
                Decal(parent, "WetPatch", "wet_patch", w * 1.35f, l * 1.35f, p + Vector3.up * 0.014f, yaw, new Color(1f, 1f, 1f, 0.9f));
                return;
            }
            var d = MeshFactory.Cyl("Puddle", 0.5f, 0.004f, p, puddle, parent, false, 28);
            d.transform.localScale = new Vector3(w, 0.004f, l);
            d.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            d.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        Puddle(g, new Vector3(-4.5f, 0.088f, 14.5f), 2.6f, 1.5f, 20f);   // in the walk to the truck
        Puddle(g, new Vector3(6.2f, 0.088f, 19.0f), 1.6f, 1.0f, -35f);
        Puddle(g, new Vector3(-9.0f, 0.088f, 5.5f), 3.2f, 1.8f, 75f);
        Puddle(g, new Vector3(2.5f, 0.088f, -6.0f), 4.0f, 2.2f, 10f);
        Puddle(g, new Vector3(-24f, 0.088f, 6.0f), 2.2f, 1.2f, 60f);
        Puddle(yg, new Vector3(12.5f, 0.123f, 238f), 3.0f, 1.6f, 5f);      // yard
        Puddle(yg, new Vector3(-12f, 0.123f, 270f), 4.4f, 2.0f, -15f);
        Puddle(yg, new Vector3(30f, 0.123f, 282f), 2.4f, 1.4f, 40f);
        Puddle(yg, new Vector3(4f, 0.163f, 309f), 3.6f, 1.7f, 85f);       // dock apron

        // ---- the store is open: lit interior behind the glass, shelf silhouettes
        // The store is a solid block, so the "interior" is a lit panel on the
        // window face with shelf and counter silhouettes laid over it.
        MeshFactory.Box("StoreInterior", new Vector3(13.2f, 2.3f, 0.01f), new Vector3(-18f, 1.98f, 0.156f), storeLight, g, false);
        for (int i = 0; i < 5; i++)
        {
            MeshFactory.Box("StoreShelf", new Vector3(1.7f, 1.05f, 0.01f), new Vector3(-23.6f + i * 2.5f, 1.36f, 0.163f), shelf, g, false);
            for (int k = 0; k < 3; k++)
                MeshFactory.Box("ShelfEdge", new Vector3(1.7f, 0.025f, 0.01f), new Vector3(-23.6f + i * 2.5f, 1.12f + k * 0.32f, 0.169f), metal, g, false);
        }
        MeshFactory.Box("StoreCounter", new Vector3(2.0f, 0.55f, 0.01f), new Vector3(-12.9f, 1.10f, 0.163f), shelf, g, false);
        for (int i = 0; i < 4; i++)
            MeshFactory.Box("WindowMullion", new Vector3(0.07f, 2.3f, 0.02f), new Vector3(-24.6f + i * 4.4f, 1.98f, 0.175f), dark, g, false);
        var open = MeshFactory.Text3D("OpenSign", "OPEN 24 HRS", new Vector3(-21.5f, 2.75f, 0.17f), 0.20f, new Color(1f, 0.22f, 0.10f), g);
        MeshFactory.FaceBack(open);
        // thin neon underline beneath the sign
        MeshFactory.Box("OpenSignTube", new Vector3(1.6f, 0.025f, 0.025f), new Vector3(-21.5f, 2.58f, 0.19f), neonRed, g, false);
        var hours = MeshFactory.Text3D("DoorHours", "SHOWERS  LAUNDRY  SCALES", new Vector3(-12.6f, 2.86f, 0.10f), 0.06f, signWhite, g);
        MeshFactory.FaceBack(hours);

        // ice chest and propane cage along the store front
        float iceY = SurfaceY(new Vector3(-26.5f, 0f, 1.0f));
        MeshFactory.Box("IceChest", new Vector3(1.6f, 1.25f, 0.8f), new Vector3(-26.5f, iceY + 0.625f, 1.0f), whitePaint, g, true);
        var ice = MeshFactory.Text3D("IceWord", "ICE", new Vector3(-26.5f, iceY + 0.865f, 1.41f), 0.16f, new Color(0.10f, 0.32f, 0.62f), g);
        MeshFactory.FaceBack(ice);
        float propaneY = SurfaceY(new Vector3(-29.0f, 0f, 1.0f));
        MeshFactory.Box("PropaneCage", new Vector3(1.4f, 1.6f, 0.75f), new Vector3(-29.0f, propaneY + 0.8f, 1.0f), metal, g, true);
        for (int i = 0; i < 4; i++)
            MeshFactory.Cyl("PropaneTank", 0.15f, 0.48f, new Vector3(-29.45f + i * 0.30f, propaneY + 0.24f, 1.0f), whitePaint, g, false);
        float benchY = SurfaceY(new Vector3(-9.0f, 0f, 1.2f));
        MeshFactory.Box("Bench", new Vector3(1.8f, 0.08f, 0.45f), new Vector3(-9.0f, benchY + 0.46f, 1.2f), wood, g, true);
        for (int s = -1; s <= 1; s += 2)
            MeshFactory.Box("BenchLeg", new Vector3(0.06f, 0.42f, 0.40f), new Vector3(-9.0f + s * 0.8f, benchY + 0.21f, 1.2f), metal, g, false);

        // ---- pump islands: pump numbers, squeegee buckets, trash cans, idling sign
        for (int i = 0; i < 2; i++)
        {
            float x = 18f + i * 8f;
            bool realPump = PropLibrary.Has("FuelPump");
            var num = MeshFactory.Text3D("PumpNumber", (7 + i).ToString(), new Vector3(x, 2.05f, 3.43f), 0.22f, new Color(0.08f, 0.10f, 0.14f), g);
            MeshFactory.FaceBack(num);
            if (realPump) num.transform.position = new Vector3(x, 2.66f, 3.0f);
            var diesel = MeshFactory.Text3D("PumpDiesel", "DIESEL  #2", new Vector3(x, 1.20f, 3.43f), 0.07f, new Color(0.08f, 0.45f, 0.18f), g);
            MeshFactory.FaceBack(diesel);
            if (realPump) Object.Destroy(diesel);
            float pumpY = SurfaceY(new Vector3(x, 0f, 3.5f));
            MeshFactory.Cyl("SqueegeeBucket", 0.17f, 0.42f, new Vector3(x + 1.7f, pumpY + 0.21f, 3.55f), green, g, false);
            var stick = MeshFactory.Cyl("SqueegeeStick", 0.014f, 0.9f, new Vector3(x + 1.72f, pumpY + 0.53f, 3.55f), metal, g, false);
            stick.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
            MeshFactory.Cyl("PumpTrash", 0.26f, 0.95f, new Vector3(x - 1.9f, pumpY + 0.475f, 3.6f), dark, g, true);
            MeshFactory.Cyl("PumpTrashLid", 0.29f, 0.06f, new Vector3(x - 1.9f, pumpY + 0.965f, 3.6f), metal, g, false);
        }
        var idlePost = new GameObject("NoIdlingSign").transform; idlePost.SetParent(g, false); idlePost.position = OnGround(12.6f, -0.4f);
        MeshFactory.Box("SignPost", new Vector3(0.07f, 2.3f, 0.07f), new Vector3(0, 1.15f, 0), metal, idlePost, false);
        MeshFactory.Box("SignPlate", new Vector3(0.62f, 0.78f, 0.03f), new Vector3(0, 2.0f, 0), whitePaint, idlePost, false);
        var idle = MeshFactory.Text3D("SignText", "NO IDLING\nOVER\n5 MIN", new Vector3(0f, 2.0f, 0.02f), 0.075f, new Color(0.62f, 0.06f, 0.04f), idlePost);
        MeshFactory.FaceBack(idle);

        // tyre streaks out of the pump lane toward the road
        Decal(g, "TireMarksPumps", "tire_marks", 3.0f, 22f, new Vector3(22f, SurfaceY(new Vector3(22f, 0f, 6f)) + 0.014f, 6f), 0f, new Color(0.58f, 0.54f, 0.49f, 0.32f));
        Decal(g, "TireMarksLot", "tire_marks", 3.0f, 18f, new Vector3(0f, SurfaceY(new Vector3(0f, 0f, 22f)) + 0.014f, 22f), 8f, new Color(0.58f, 0.54f, 0.49f, 0.36f));

        // ---- yard: pallets, chocks and dock lights at the doors
        void PalletStack(Vector3 p, int layers, float yaw)
        {
            var s = new GameObject("PalletStack").transform; s.SetParent(yg, false); s.position = p; s.rotation = Quaternion.Euler(0f, yaw, 0f);
            for (int k = 0; k < layers; k++)
            {
                float y = 0.075f + k * 0.15f;
                for (int b = -1; b <= 1; b++)
                    MeshFactory.Box("PalletRunner", new Vector3(0.10f, 0.09f, 1.2f), new Vector3(b * 0.45f, y - 0.03f, 0f), wood, s, false);
                for (int b = 0; b < 5; b++)
                    MeshFactory.Box("PalletBoard", new Vector3(1.0f, 0.022f, 0.14f), new Vector3(0f, y + 0.026f, -0.5f + b * 0.25f), wood, s, false);
            }
            s.gameObject.AddComponent<BoxCollider>().center = new Vector3(0f, layers * 0.075f, 0f);
            s.GetComponent<BoxCollider>().size = new Vector3(1.0f, layers * 0.15f, 1.2f);
        }
        PalletStack(new Vector3(-52f, 0.16f, 326.5f), 8, 0f);
        PalletStack(new Vector3(-50.6f, 0.16f, 326.4f), 5, 4f);
        PalletStack(new Vector3(-51.3f, 0.16f, 324.8f), 3, 87f);
        PalletStack(new Vector3(134f, 0.16f, 326f), 6, -6f);

        for (int i = 0; i < 14; i++)
        {
            float x = -35 + i * 12.5f;
            bool occupied = (i % 3 != 1);
            // dock signal light beside each door: red while a trailer is being worked
            MeshFactory.Box("DockLightBox", new Vector3(0.30f, 0.55f, 0.12f), new Vector3(x + 3.45f, 2.1f, 329.0f), dark, yg, false);
            MeshFactory.Box("DockLight", new Vector3(0.20f, 0.20f, 0.04f), new Vector3(x + 3.45f, occupied ? 2.24f : 1.97f, 328.93f), occupied ? dockRed : dockGreen, yg, false);
            // a wheel chock left on the apron, as they always are
            if (i % 2 == 0)
            {
                var chock = MeshFactory.Box("WheelChock", new Vector3(0.25f, 0.18f, 0.35f), new Vector3(x - 1.2f + (i % 4) * 0.3f, 0.25f, 322.5f - (i % 3) * 0.8f), yellowPaint, yg, false);
                chock.transform.rotation = Quaternion.Euler(0f, i * 23f, 0f);
            }
        }
        var dockNo = MeshFactory.Text3D("DockSpeed", "DOCK AREA  5 MPH", new Vector3(-58f, 2.3f, 326.95f), 0.13f, new Color(0.10f, 0.10f, 0.10f), yg);
        MeshFactory.FaceBack(dockNo);
        MeshFactory.Box("DockSpeedPlate", new Vector3(2.4f, 0.42f, 0.03f), new Vector3(-58f, 2.3f, 327.0f), yellowPaint, yg, false);
    }

    static void AddInspection(GameFlow flow, WorldRefs refs, Transform container, string id, string title, string details, Vector3 pos, bool isSeal, Vector3 markerOffset = default(Vector3))
    {
        var go = new GameObject("Inspect_" + id);
        go.transform.SetParent(container, false);
        go.transform.localPosition = pos;
        var bc = go.AddComponent<BoxCollider>(); bc.size = Vector3.one * 0.62f; bc.isTrigger = true;
        var ip = go.AddComponent<InspectionPoint>();
        ip.Setup(id, title, details);
        var it = go.AddComponent<Interactable>();
        it.id = id; it.prompt = title; it.oneShot = false;
        it.onInteract.AddListener(() => flow.OnInspect(ip));

        var tag = new GameObject("InspectionTag");
        tag.transform.SetParent(go.transform, false);
        tag.transform.localPosition = new Vector3(0f, 0.58f, 0f) + markerOffset;
        MeshFactory.Box("TagStem", new Vector3(0.016f, 0.30f, 0.016f), new Vector3(0f, -0.24f, 0f), orange, tag.transform, false);
        MeshFactory.Box("TagBorder", new Vector3(0.215f, 0.265f, 0.012f), Vector3.zero, orange, tag.transform, false);
        MeshFactory.Box("TagFace", new Vector3(0.182f, 0.232f, 0.016f), new Vector3(0f, 0f, 0.006f), whitePaint, tag.transform, false);
        var num = MeshFactory.Text3D("TagNumber", (refs.inspections.Count + 1).ToString(),
            new Vector3(0f, 0f, 0.02f), 0.15f, new Color(0.05f, 0.09f, 0.14f), tag.transform);
        num.fontStyle = FontStyle.Bold;
        ip.SetMarker(tag.transform);
        if (isSeal)
        {
            var pt = go.AddComponent<PhotoTarget>();
            pt.id = "seal"; pt.label = "SEAL 482913";
            refs.sealTarget = pt;
        }
        refs.inspections.Add(ip);
    }

    static void SignQuad(string name, string texName, float w, float h, Vector3 pos, float yaw, Transform parent)
    {
        var tex = T(texName);
        if (tex == null) { Debug.LogWarning("D2D missing texture " + texName); return; }
        var mat = MeshFactory.SignMat(name + "Mat", tex, Color.white);
        MeshFactory.Quad(name, new Vector2(w, h), pos, Quaternion.Euler(0, yaw, 0), mat, parent);
    }

    static Material concreteFooting;
    static void LightPole(Transform parent, Vector3 pos, float height)
    {
        pos.y = SurfaceY(pos);
        var p = new GameObject("LightPole").transform; p.SetParent(parent, false); p.position = pos;
        var poleCol = p.gameObject.AddComponent<CapsuleCollider>();
        poleCol.radius = 0.18f; poleCol.height = Mathf.Max(2f, height); poleCol.center = new Vector3(0f, Mathf.Max(1f, height) * 0.5f, 0f);
        // Sketchfab Gardco shoebox pole (9 m, origin at the foot; head lands on +X at yaw 180).
        // The cylinder pole with the Poly Haven flood light is the fallback.
        float s = height / 9f;
        Vector3 lampPos = new Vector3(1.75f, height - 0.25f, 0f);
        if (PropLibrary.Spawn("LotLight", pos, 180f, s, p) != null)
            lampPos = new Vector3(0.45f * s, 8.8f * s, 0f);
        else
        {
        MeshFactory.Cyl("Pole", 0.14f, height, new Vector3(0, height / 2, 0), metal, p, false);
        MeshFactory.Box("Arm", new Vector3(2.0f, 0.12f, 0.16f), new Vector3(0.9f, height, 0), metal, p, false);
        MeshFactory.Cyl("Footing", 0.32f, 0.55f, new Vector3(0, 0.27f, 0), concreteFooting ?? (concreteFooting = MeshFactory.Mat("Footing", new Color(0.55f, 0.54f, 0.51f), 0f, 0.12f)), p, false);
        // Poly Haven CC0 flood light on the arm; the old orange box stays only as a fallback
        var head = PropLibrary.Spawn("SecurityLight", p.TransformPoint(new Vector3(1.75f, height - 0.35f, 0)), p.eulerAngles.y + 90f, 1.7f, p);
        if (head == null)
        {
            MeshFactory.Box("Lamp", new Vector3(1.1f, 0.14f, 0.42f), new Vector3(1.75f, height - 0.12f, 0), orangeGlow, p, false);
            MeshFactory.Box("LampHood", new Vector3(1.2f, 0.08f, 0.5f), new Vector3(1.75f, height - 0.02f, 0), dark, p, false);
        }
        }
        if (height >= 13f)
        {
            var lgo = new GameObject("YardLamp");
            lgo.transform.SetParent(p, false);
            lgo.transform.localPosition = lampPos;
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point; l.range = 21f; l.intensity = 0.55f;
            l.color = new Color(1f, 0.72f, 0.40f); l.shadows = LightShadows.None;
        }
    }

    static Material coneOrange, coneWhite, coneBase;
    static void Cone(Transform parent, Vector3 pos)
    {
        // A 28-inch highway cone: square rubber base, tapered PVC body with two
        // reflective collars, slightly worn. Replaces the old stacked cylinders.
        pos.y = SurfaceY(pos);
        var c = new GameObject("Cone").transform; c.SetParent(parent, false); c.position = pos;
        c.rotation = Quaternion.Euler(0f, (pos.x * 37f + pos.z * 11f) % 90f, 0f);
        var coneCol = c.gameObject.AddComponent<BoxCollider>();
        coneCol.center = new Vector3(0f, 0.36f, 0f); coneCol.size = new Vector3(0.34f, 0.72f, 0.34f);
        if (coneOrange == null)
        {
            coneOrange = MeshFactory.Mat("ConePVC", new Color(0.95f, 0.30f, 0.04f), 0f, 0.55f);
            coneWhite = MeshFactory.Mat("ConeCollar", new Color(0.86f, 0.87f, 0.85f), 0.1f, 0.7f);
            coneBase = MeshFactory.Mat("ConeBase", new Color(0.06f, 0.06f, 0.06f), 0f, 0.15f);
        }
        MeshFactory.Box("Base", new Vector3(0.38f, 0.035f, 0.38f), new Vector3(0, 0.0175f, 0), coneBase, c, false);
        MeshFactory.Lathe("Body", new[] { new Vector2(0.155f, 0.035f), new Vector2(0.12f, 0.24f), new Vector2(0.085f, 0.45f), new Vector2(0.045f, 0.68f), new Vector2(0.03f, 0.715f), new Vector2(0.0f, 0.716f) }, Vector3.zero, coneOrange, c);
        MeshFactory.Lathe("CollarA", new[] { new Vector2(0.108f, 0.33f), new Vector2(0.087f, 0.46f) }, Vector3.zero, coneWhite, c);
        MeshFactory.Lathe("CollarB", new[] { new Vector2(0.076f, 0.52f), new Vector2(0.064f, 0.59f) }, Vector3.zero, coneWhite, c);
    }

    static void Tree(Transform parent, Vector3 pos, float scale)
    {
        pos.y = SurfaceY(pos);
        if (PropLibrary.SpawnTree(pos, scale, parent) != null) return;
        var t = new GameObject("Tree").transform; t.SetParent(parent, false); t.position = pos; t.localScale = Vector3.one * scale;
        MeshFactory.Cyl("Trunk", 0.20f, 3.9f, new Vector3(0, 1.95f, 0), dark, t, false);
        // a cluster of flattened crowns reads as a broadleaf canopy instead of a lollipop
        MeshFactory.Sphere("CrownA", 2.15f, new Vector3(0, 4.9f, 0), green, t).transform.localScale = new Vector3(4.3f, 2.9f, 4.1f);
        MeshFactory.Sphere("CrownB", 1.65f, new Vector3(1.15f, 4.2f, 0.45f), green, t).transform.localScale = new Vector3(3.3f, 2.4f, 3.1f);
        MeshFactory.Sphere("CrownC", 1.55f, new Vector3(-1.05f, 4.25f, -0.35f), green, t).transform.localScale = new Vector3(3.1f, 2.3f, 3.0f);
        MeshFactory.Sphere("CrownD", 1.35f, new Vector3(0.25f, 5.45f, 0.65f), green, t).transform.localScale = new Vector3(2.7f, 2.0f, 2.6f);
        MeshFactory.Sphere("CrownE", 1.25f, new Vector3(-0.55f, 3.75f, 0.85f), green, t).transform.localScale = new Vector3(2.5f, 1.9f, 2.4f);
    }

    static void FenceLine(Transform parent, Vector3 a, Vector3 b, float height)
    {
        Vector3 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.5f) return;
        float baseY = SurfaceY(a);
        var g = new GameObject("Fence").transform; g.SetParent(parent, false); g.position = new Vector3(a.x, baseY, a.z); g.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        int posts = Mathf.Max(1, Mathf.RoundToInt(len / 6f));
        for (int i = 0; i <= posts; i++)
        {
            float t = i / (float)posts;
            // Sample each post so the fence follows road, shoulder and yard without sinking.
            float py = SurfaceY(g.TransformPoint(new Vector3(t * len, 0f, 0f))) - baseY;
            MeshFactory.Cyl("FencePost", 0.055f, height, new Vector3(t * len, py + height / 2, 0), metal, g, false);
        }
        for (int i = 0; i < 3; i++)
            MeshFactory.Box("FenceRail", new Vector3(len, 0.035f, 0.035f), new Vector3(len / 2, 0.45f + i * (height - 0.7f) / 2f, 0), metal, g, false);
        // One solid panel for the whole run: a truck that drifts off the road or
        // cuts the gate corner meets the fence instead of passing through it.
        var fenceCol = g.gameObject.AddComponent<BoxCollider>();
        fenceCol.center = new Vector3(len / 2f, height * 0.5f, 0f);
        // Thick enough that a fast cab cannot tunnel through the panel between
        // two frames, and thick enough for the camera sphere sweep to see it.
        fenceCol.size = new Vector3(len, height, 0.45f);
    }

    static void SimpleRig(Transform parent, Vector3 pos, float yaw, Color bodyColor)
    {
        var g = new GameObject("ParkedRig").transform; g.SetParent(parent, false); g.position = pos; g.rotation = Quaternion.Euler(0, yaw, 0);
        // Sketchfab tractor (build_props_sketchfab_rigs.py) with the 53 ft van behind it
        // (build_vehicles.py). Both face local +Z with the hood at z = +3.3 like the boxes
        // below (the fallback).
        Vector3 fwd = g.forward;
        if (PropLibrary.Has("Rig_Logistics") && PropLibrary.Has("Trailer53"))
        {
            PropLibrary.Spawn("Rig_Logistics", pos - fwd * 0.8f + Vector3.up * PavingY(pos), yaw, 1f, g, true);
            PropLibrary.Spawn("Trailer53", pos - fwd * 11.0f + Vector3.up * PavingY(pos), yaw, 1f, g, true);
            return;
        }
        var body = MeshFactory.Mat("RigBody", bodyColor, 0.2f, 0.4f);
        MeshFactory.Box("Cab", new Vector3(2.5f, 2.6f, 2.4f), new Vector3(0, 1.7f, 0), body, g, true);
        MeshFactory.Box("Hood", new Vector3(2.3f, 1.3f, 2.2f), new Vector3(0, 1.3f, 2.2f), body, g, false);
        MeshFactory.Box("Sleeper", new Vector3(2.45f, 2.3f, 2.0f), new Vector3(0, 1.9f, -2.1f), body, g, false);
        MeshFactory.Box("Trailer", new Vector3(2.6f, 2.9f, 14.8f), new Vector3(0, 2.4f, -11f), offWhite, g, false);
        for (int i = 0; i < 3; i++)
            MeshFactory.Cyl("Wheel", 0.52f, 0.44f, new Vector3((i % 2 == 0 ? -1.2f : 1.2f), 0.52f, -2f - (i / 2) * 4.2f), dark, g, false).transform.rotation = Quaternion.Euler(0, 0, 90);
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }
}
