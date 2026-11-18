using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using SLZ.Marrow;
using SLZ.Marrow.Warehouse;
using SLZ.Rig;
using SLZ.Combat;

[assembly: MelonInfo(typeof(ImpactFrame.ImpactFrameMod), "ImpactFrame", "1.0.0", "gdono")]
[assembly: MelonGame("Stress Level Zero", "Bonelab")]

namespace ImpactFrame
{
    public class ImpactFrameMod : MelonMod
    {
        // Configuration fields
        public static bool Enabled = true;
        public static float ImpactChance = 0.3f; // 30% chance
        public static Color ImpactColor = new Color(1f, 0.2f, 0.2f, 1f); // Red by default
        public static float ImpactDuration = 0.1f; // Duration in seconds
        public static float ImpactIntensity = 0.5f; // How strong the effect is

        // Internal state
        private static readonly Harmony _harmony = new Harmony("gdono.impactframe");
        private static MelonPreferences_Category _category;

        public override void OnInitializeMelon()
        {
            // Create preferences category
            _category = MelonPreferences.CreateCategory("ImpactFrame");

            // Register preferences with default values
            _category.CreateEntry("Enabled", Enabled, "Enable/Disable the impact frame mod");
            _category.CreateEntry("ImpactChance", ImpactChance, "Chance (0-1) for impact frame to trigger on hit");
            _category.CreateEntry("ImpactColorR", ImpactColor.r, "Impact frame color - Red component (0-1)");
            _category.CreateEntry("ImpactColorG", ImpactColor.g, "Impact frame color - Green component (0-1)");
            _category.CreateEntry("ImpactColorB", ImpactColor.b, "Impact frame color - Blue component (0-1)");
            _category.CreateEntry("ImpactColorA", ImpactColor.a, "Impact frame color - Alpha component (0-1)");
            _category.CreateEntry("ImpactDuration", ImpactDuration, "Duration of impact frame in seconds");
            _category.CreateEntry("ImpactIntensity", ImpactIntensity, "Intensity of the impact effect (0-1)");

            // Load saved preferences
            LoadPreferences();

            // Apply harmony patches
            try
            {
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                MelonLogger.Msg("[ImpactFrame] Harmony patches applied successfully!");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ImpactFrame] Failed to apply patches: {ex}");
            }

            MelonLogger.Msg("[ImpactFrame] Mod initialized!");
        }

        public override void OnDeinitializeMelon()
        {
            _harmony.UnpatchAll("gdono.impactframe");
            MelonLogger.Msg("[ImpactFrame] Mod deinitialized.");
        }

        private static void LoadPreferences()
        {
            Enabled = _category.GetEntry<bool>("Enabled").Value;
            ImpactChance = Mathf.Clamp01(_category.GetEntry<float>("ImpactChance").Value);

            float r = Mathf.Clamp01(_category.GetEntry<float>("ImpactColorR").Value);
            float g = Mathf.Clamp01(_category.GetEntry<float>("ImpactColorG").Value);
            float b = Mathf.Clamp01(_category.GetEntry<float>("ImpactColorB").Value);
            float a = Mathf.Clamp01(_category.GetEntry<float>("ImpactColorA").Value);
            ImpactColor = new Color(r, g, b, a);

            ImpactDuration = Mathf.Max(0.01f, _category.GetEntry<float>("ImpactDuration").Value);
            ImpactIntensity = Mathf.Clamp01(_category.GetEntry<float>("ImpactIntensity").Value);

            MelonLogger.Msg($"[ImpactFrame] Config loaded - Enabled: {Enabled}, Chance: {ImpactChance}, Color: {ImpactColor}, Duration: {ImpactDuration}s, Intensity: {ImpactIntensity}");
        }

        // Public method to reload config at runtime (e.g., via console command)
        public static void ReloadConfig()
        {
            LoadPreferences();
        }

        // Called when config changes in-game
        [HarmonyPatch(typeof(MelonPreferences), nameof(MelonPreferences.Save))]
        private static class MelonPreferencesSavePatch
        {
            static void Postfix()
            {
                LoadPreferences();
            }
        }
    }

    // Patch for melee weapon hits on NPCs/Players
    [HarmonyPatch(typeof(CombatManager), "OnHit")]
    private static class CombatManagerOnHitPatch
    {
        static bool Prefix(CombatManager __instance, HitData hitData)
        {
            if (!ImpactFrameMod.Enabled) return true;

            // Check if the hit was from a bone/melee weapon
            if (hitData.weapon != null && IsBoneWeapon(hitData.weapon))
            {
                TryTriggerImpactFrame(hitData);
            }

            return true; // Continue original method
        }

        private static bool IsBoneWeapon(GameObject weapon)
        {
            if (weapon == null) return false;

            // Check for bone weapon by name or component
            string name = weapon.name.ToLower();
            return name.Contains("bone") ||
                   name.Contains("skull") ||
                   weapon.GetComponent<BoneWeapon>() != null;
        }

        private static void TryTriggerImpactFrame(HitData hitData)
        {
            // Check if target is NPC or Player
            RigManager targetRig = hitData.targetRig;
            if (targetRig == null) return;

            bool isPlayer = targetRig.isPlayer;
            bool isNPC = targetRig.gameObject.GetComponent<NPC>() != null ||
                         targetRig.gameObject.GetComponentInParent<NPC>() != null;

            if (!isPlayer && !isNPC) return;

            // Roll chance
            if (UnityEngine.Random.value > ImpactFrameMod.ImpactChance) return;

            // Trigger impact frame effect
            TriggerImpactFrameEffect(targetRig, hitData);
        }

        private static void TriggerImpactFrameEffect(RigManager targetRig, HitData hitData)
        {
            // Create a brief visual effect - screen flash / time slow / particle
            MelonCoroutines.Start(DoImpactFrame(targetRig, hitData));
        }

        private static System.Collections.IEnumerator DoImpactFrame(RigManager targetRig, HitData hitData)
        {
            // Store original time scale
            float originalTimeScale = Time.timeScale;

            // Apply time slow effect
            Time.timeScale = 1f - ImpactFrameMod.ImpactIntensity;
            Time.fixedDeltaTime = Time.timeScale * 0.02f;

            // Create visual flash on target
            var flashRenderer = targetRig.gameObject.AddComponent<ImpactFlashRenderer>();
            flashRenderer.Initialize(ImpactFrameMod.ImpactColor, ImpactFrameMod.ImpactDuration);

            // Wait for duration
            yield return new WaitForSecondsRealtime(ImpactFrameMod.ImpactDuration);

            // Restore time scale
            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = 0.02f;

            // Cleanup
            if (flashRenderer != null)
                UnityEngine.Object.Destroy(flashRenderer);
        }
    }

    // Simple renderer component for the impact flash effect
    public class ImpactFlashRenderer : MonoBehaviour
    {
        private Color _flashColor;
        private float _duration;
        private float _timer;
        private Material _originalMaterial;
        private Renderer _renderer;
        private Material _flashMaterial;

        public void Initialize(Color color, float duration)
        {
            _flashColor = color;
            _duration = duration;
            _timer = duration;

            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
            {
                _originalMaterial = _renderer.material;
                _flashMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _flashMaterial.color = _flashColor;
                _flashMaterial.EnableKeyword("_EMISSION");
                _flashMaterial.SetColor("_EmissionColor", _flashColor * 2f);
            }
        }

        private void Update()
        {
            if (_renderer == null || _flashMaterial == null) return;

            _timer -= Time.unscaledDeltaTime;
            float t = 1f - (_timer / _duration);

            // Pulse effect
            float pulse = Mathf.Sin(t * Mathf.PI);
            Color currentColor = Color.Lerp(Color.white, _flashColor, pulse * ImpactFrameMod.ImpactIntensity);

            _renderer.material.color = currentColor;

            if (_timer <= 0f)
            {
                if (_originalMaterial != null)
                    _renderer.material = _originalMaterial;
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            if (_flashMaterial != null)
                Destroy(_flashMaterial);
        }
    }

    // Console commands for runtime config
    [HarmonyPatch(typeof(Console), "Initialize")]
    private static class ConsoleInitializePatch
    {
        static void Postfix()
        {
            RegisterCommands();
        }
    }

    private static void RegisterCommands()
    {
        // Note: Bonelab uses a custom console system.
        // These would need to be registered with SLZ's console if available
        MelonLogger.Msg("[ImpactFrame] Console commands registered (if console system detected)");
    }
}