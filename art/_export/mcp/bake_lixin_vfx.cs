using UnityEditor;
using UnityEngine;

public static class BakeLixinVfx
{
    public static string Run()
    {
        const string dir = "Assets/Bundle/Vfx";
        if (!AssetDatabase.IsValidFolder("Assets/Bundle"))
            AssetDatabase.CreateFolder("Assets", "Bundle");
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Bundle", "Vfx");

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mesh = cube.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(cube);

        var mat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (mat == null)
        {
            var sh = Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Legacy Shaders/Particles/Additive")
                     ?? Shader.Find("Sprites/Default");
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, dir + "/VfxParticle.mat");
        }

        MakeGamble(dir + "/fx_lixin_gamble.prefab", mesh, mat);
        MakeCrunch(dir + "/fx_lixin_crunch.prefab", mesh, mat);
        MakeOverwork(dir + "/fx_lixin_overwork.prefab", mesh, mat);
        MakeBuffAtk(dir + "/fx_buff_atk.prefab", mesh, mat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "baked fx_lixin_gamble/crunch/overwork + fx_buff_atk";
    }

    static GameObject NewFx(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<ParticleSystem>();
        return go;
    }

    static void Finish(GameObject go, ParticleSystem ps, Mesh mesh, Material mat, int order)
    {
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = mesh;
        r.sharedMaterial = mat;
        r.alignment = ParticleSystemRenderSpace.View;
        r.minParticleSize = 0f;
        r.maxParticleSize = 4f;
        r.sortingOrder = order;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        var main = ps.main;
        main.playOnAwake = true;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        var col = ps.collision;
        col.enabled = false;
    }

    static void Save(GameObject go, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    static void MakeGamble(string path, Mesh mesh, Material mat)
    {
        var go = NewFx("fx_lixin_gamble");
        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.35f;
        main.loop = false;
        main.startLifetime = 0.42f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 4.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.283f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.62f, 0.22f, 1f),
            new Color(1f, 0.55f, 0.12f));
        main.maxParticles = 28;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 16f;
        sh.radius = 0.04f;

        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.z = new ParticleSystem.MinMaxCurve(1.2f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.7f, 0.3f, 1f), 0f),
                new GradientColorKey(new Color(1f, 0.35f, 0.15f), 0.55f),
                new GradientColorKey(new Color(1f, 0.85f, 0.2f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        Finish(go, ps, mesh, mat, 42);
        Save(go, path);
    }

    static void MakeCrunch(string path, Mesh mesh, Material mat)
    {
        var go = NewFx("fx_lixin_crunch");
        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 4f;
        main.loop = true;
        main.startLifetime = 0.55f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.283f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.25f, 0.95f, 1f),
            new Color(0.9f, 0.95f, 1f));
        main.maxParticles = 40;
        main.stopAction = ParticleSystemStopAction.None;

        var em = ps.emission;
        em.rateOverTime = 28f;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Hemisphere;
        sh.radius = 0.35f;

        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.y = new ParticleSystem.MinMaxCurve(1.6f);
        vol.space = ParticleSystemSimulationSpace.World;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.2f, 1f, 0.85f), 0f),
                new GradientColorKey(new Color(0.7f, 0.85f, 1f), 1f)
            },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        Finish(go, ps, mesh, mat, 41);
        Save(go, path);
    }

    static void MakeOverwork(string path, Mesh mesh, Material mat)
    {
        var go = NewFx("fx_lixin_overwork");
        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = 0.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.283f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.95f, 0.12f, 0.18f),
            new Color(0.35f, 0.05f, 0.08f));
        main.maxParticles = 20;
        main.gravityModifier = 0.35f;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.18f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.25f, 0.2f), 0f),
                new GradientColorKey(new Color(0.2f, 0.05f, 0.08f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        Finish(go, ps, mesh, mat, 43);
        Save(go, path);
    }

    static void MakeBuffAtk(string path, Mesh mesh, Material mat)
    {
        var go = NewFx("fx_buff_atk");
        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.6f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.283f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.2f),
            new Color(1f, 0.55f, 0.1f));
        main.maxParticles = 16;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Hemisphere;
        sh.radius = 0.28f;

        Finish(go, ps, mesh, mat, 40);
        Save(go, path);
    }
}
