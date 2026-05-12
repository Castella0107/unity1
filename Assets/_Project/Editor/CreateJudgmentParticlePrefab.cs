using UnityEditor;
using UnityEngine;
using System.IO;

public static class CreateJudgmentParticlePrefab
{
    [MenuItem("Tools/Create JudgmentParticle Prefab")]
    public static void Create()
    {
        // Ensure the Effects directory exists
        string dir = "Assets/_Project/Prefabs/Effects";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Effects");

        // Root GO
        var go = new GameObject("JudgmentParticle");
        var ps = go.AddComponent<ParticleSystem>();

        // Disable renderer auto-added looping by default
        var renderer = go.GetComponent<ParticleSystemRenderer>();

        // ── Main module ────────────────────────────────────────────────────
        var main          = ps.main;
        main.duration     = 0.4f;
        main.loop         = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f);
        main.startSpeed    = new ParticleSystem.MinMaxCurve(2.5f);
        main.startSize     = new ParticleSystem.MinMaxCurve(0.15f);
        main.gravityModifierMultiplier = -0.5f;
        main.stopAction   = ParticleSystemStopAction.Disable;
        main.maxParticles  = 200;

        // ── Emission module ────────────────────────────────────────────────
        var emission  = ps.emission;
        emission.enabled      = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
        var burst = new ParticleSystem.Burst(0f, 18);
        emission.SetBursts(new ParticleSystem.Burst[] { burst });

        // ── Shape module ───────────────────────────────────────────────────
        var shape     = ps.shape;
        shape.enabled     = true;
        shape.shapeType   = ParticleSystemShapeType.Hemisphere;
        shape.radius      = 0.1f;

        // ── Size over lifetime ─────────────────────────────────────────────
        var sizeMod   = ps.sizeOverLifetime;
        sizeMod.enabled = true;
        var sizeAnim = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        sizeMod.size  = new ParticleSystem.MinMaxCurve(1f, sizeAnim);

        // ── Color over lifetime (alpha fade) ──────────────────────────────
        var colorMod  = ps.colorOverLifetime;
        colorMod.enabled = true;
        var gradient  = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]  { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[]  { new GradientAlphaKey(1f, 0f),         new GradientAlphaKey(0f, 1f) });
        colorMod.color = new ParticleSystem.MinMaxGradient(gradient);

        // ── Renderer ──────────────────────────────────────────────────────
        renderer.sortingOrder = 100;
        // Use Particles/Standard material with additive blend if available,
        // else fall back to default-particle
        var additiveMat = new Material(Shader.Find("Particles/Standard Unlit"));
        if (additiveMat.shader != null && additiveMat.shader.isSupported)
        {
            additiveMat.SetFloat("_Mode", 4); // Additive
            additiveMat.SetInt("_BlendOp",   0);
            additiveMat.SetInt("_SrcBlend",  1);
            additiveMat.SetInt("_DstBlend",  1);
            renderer.material = additiveMat;
        }

        // ── Save as prefab ─────────────────────────────────────────────────
        string prefabPath = dir + "/JudgmentParticle.prefab";
        bool success;
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath, out success);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (success)
            Debug.Log("[CreateJudgmentParticlePrefab] Created at " + prefabPath);
        else
            Debug.LogError("[CreateJudgmentParticlePrefab] Failed to save prefab");
    }
}
