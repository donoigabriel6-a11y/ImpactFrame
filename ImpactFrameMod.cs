using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using SLZ.Marrow;
using SLZ.Marrow.Warehouse;
using SLZ.Rig;

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
        private static readonly HarmonyLib.Harmony _harmony = new HarmonyLib.Harmony("gdono.impactframe");
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
            _harmony.UnpatchSelf();
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
        static class MelonPreferencesSavePatch
        {
            static void Postfix()
            {
                LoadPreferences();
            }
        }
    }

    // Patch for melee weapon hits on NPCs/Players
    // Using reflection to access Assembly-CSharp types
    [HarmonyPatch("CombatManager", "OnHit")]
    static class CombatManagerOnHitPatch
    {
        static bool Prefix(object __instance, object hitData)
        {
            if (!ImpactFrameMod.Enabled) return true;

            // Check if the hit was from a bone/melee weapon using reflection
            var weapon = hitData?.GetType().GetProperty("weapon")?.GetValue(hitData);
            if (weapon != null && IsBoneWeapon(weapon as GameObject))
            {
                TryTriggerImpactFrame(hitData);
            }

            return true; // Continue original method
        }

        private static bool IsBoneWeapon(GameObject weapon)
        {
            if (weapon == null) return false;

            // Check for bone weapon by name
            string name = weapon.name.ToLower();
            return name.Contains("bone") ||
                   name.Contains("skull") ||
                   name.Contains("femur") ||
                   name.Contains("humerus") ||
                   name.Contains("rib") ||
                   name.Contains("spine");
        }

        private static void TryTriggerImpactFrame(object hitData)
        {
            // Get targetRig using reflection
            var targetRig = hitData?.GetType().GetProperty("targetRig")?.GetValue(hitData);
            if (targetRig == null) return;

            // Check if target is NPC or Player using reflection
            var rigManager = targetRig as RigManager;
            if (rigManager == null) return;

            bool isPlayer = false;
            bool isNPC = false;

            // Use reflection to check properties
            var isPlayerProp = rigManager.GetType().GetProperty("isPlayer");
            if (isPlayerProp != null)
                isPlayer = (bool)(isPlayerProp.GetValue(rigManager) ?? false);

            // Check for NPC component via reflection
            var npcType = Type.GetType("NPC, Assembly-CSharp");
            if (npcType != null)
            {
                isNPC = rigManager.gameObject.GetComponent(npcType) != null ||
                        rigManager.gameObject.GetComponentInParent(npcType) != null;
            }

            if (!isPlayer && !isNPC) return;

            // Roll chance
            if (UnityEngine.Random.value > ImpactFrameMod.ImpactChance) return;

            // Trigger impact frame effect
            TriggerImpactFrameEffect(rigManager);
        }

        private static void TriggerImpactFrameEffect(RigManager targetRig)
        {
            // Create a brief visual effect - screen flash / time slow / particle
            MelonCoroutines.Start(DoImpactFrame(targetRig));
        }

        private static System.Collections.IEnumerator DoImpactFrame(RigManager targetRig)
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
            float pulse = Mathf.Sin(t * (float)Math.PI);
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
}