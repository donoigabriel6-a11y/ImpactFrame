using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;
using SLZ.Marrow;
using SLZ.Rig;

[assembly: MelonInfo(typeof(ImpactFrame.ImpactFrameMod), "ImpactFrame", "2.0.0", "gdono")]
[assembly: MelonGame("Stress Level Zero", "Bonelab")]

namespace ImpactFrame
{
    public class ImpactFrameMod : MelonMod
    {
        // ========== CONFIGURATION ==========
        public static bool Enabled = true;

        // Trigger Settings
        public static float ImpactChance = 0.35f;
        public static bool RequireBoneWeapon = true;
        public static bool TriggerOnNPCs = true;
        public static bool TriggerOnPlayers = true;
        public static float MinDamageThreshold = 5f;
        public static float Cooldown = 0.4f;

        // Effect Type (bitmask)
        [Flags]
        public enum Effects { None = 0, TimeStop = 1, TimeSlow = 2, ScreenFlash = 4, ScreenShake = 8, Particles = 16, Sound = 32, HitMarker = 64, All = 127 }
        public static Effects ActiveEffects = Effects.All;

        // Time Settings
        public static float TimeScale = 0.02f;
        public static float Duration = 0.15f;
        public static float EaseInTime = 0.02f;
        public static float EaseOutTime = 0.08f;

        // Screen Flash (via target renderer emission)
        public static Color FlashColor = new Color(1f, 0.1f, 0.1f, 1f);
        public static float FlashIntensity = 2f;
        public static bool FlashTargetOnly = true;

        // Screen Shake
        public static float ShakeMagnitude = 0.12f;
        public static float ShakeRoughness = 12f;
        public static float ShakeFadeIn = 0.01f;
        public static float ShakeFadeOut = 0.08f;

        // Particles
        public static bool EnableParticles = true;
        public static int ParticleCount = 25;
        public static float ParticleSize = 0.06f;
        public static float ParticleLifetime = 0.4f;
        public static float ParticleSpeed = 3f;
        public static Color ParticleColor = new Color(1f, 0.4f, 0.1f, 1f);
        public static Color ParticleColorEnd = new Color(1f, 0.1f, 0.05f, 0f);

        // Sound
        public static bool EnableSound = true;
        public static float SoundVolume = 0.7f;
        public static float SoundPitch = 1.2f;

        // Hit Marker (using simple LineRenderer in world space)
        public static bool ShowHitMarker = true;
        public static float HitMarkerSize = 0.3f;
        public static float HitMarkerDuration = 0.18f;
        public static Color HitMarkerColor = new Color(1f, 1f, 1f, 1f);
        public static float HitMarkerThickness = 0.02f;

        // Combo System
        public static bool EnableCombo = true;
        public static float ComboWindow = 1.5f;
        public static int MaxComboCount = 99;
        public static bool ShowComboUI = true;

        // Slow-mo on kill
        public static bool KillSlowMo = true;
        public static float KillSlowMoDuration = 0.5f;
        public static float KillSlowMoScale = 0.1f;

        // ========== INTERNAL STATE ==========
        private static readonly HarmonyLib.Harmony _harmony = new HarmonyLib.Harmony("gdono.impactframe");
        private static MelonPreferences_Category _category;
        private static float _lastTriggerTime = -999f;
        public static int ComboCount = 0;  // Public for UI access
        private static float _lastHitTime = -999f;
        private static Camera _mainCamera;
        private static GameObject _particleSystem;
        private static AudioSource _audioSource;
        private static bool _isEffectActive = false;
        private static Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

        public override void OnInitializeMelon()
        {
            _category = MelonPreferences.CreateCategory("ImpactFrame");
            RegisterPreferences();
            LoadPreferences();
            SetupHarmony();
            MelonLogger.Msg("[ImpactFrame] Mod initialized v2.0 - Better Impact Frames!");
        }

        private void RegisterPreferences()
        {
            // General
            _category.CreateEntry("Enabled", Enabled, "Master toggle");
            _category.CreateEntry("ImpactChance", ImpactChance, "Trigger chance (0-1)");
            _category.CreateEntry("RequireBoneWeapon", RequireBoneWeapon, "Only trigger on bone weapons");
            _category.CreateEntry("TriggerOnNPCs", TriggerOnNPCs, "Trigger on NPC hits");
            _category.CreateEntry("TriggerOnPlayers", TriggerOnPlayers, "Trigger on player hits");
            _category.CreateEntry("MinDamageThreshold", MinDamageThreshold, "Minimum damage to trigger");
            _category.CreateEntry("Cooldown", Cooldown, "Cooldown between triggers (seconds)");

            // Effects (bitmask)
            _category.CreateEntry("ActiveEffects", (int)ActiveEffects, "Bitmask: 1=TimeStop,2=TimeSlow,4=Flash,8=Shake,16=Particles,32=Sound,64=HitMarker");

            // Time
            _category.CreateEntry("TimeScale", TimeScale, "Time scale during effect (0=pause, 0.02=near stop)");
            _category.CreateEntry("Duration", Duration, "Effect duration (seconds)");
            _category.CreateEntry("EaseInTime", EaseInTime, "Ease in duration");
            _category.CreateEntry("EaseOutTime", EaseOutTime, "Ease out duration");

            // Screen Flash
            _category.CreateEntry("FlashColorR", FlashColor.r, "Flash red (0-1)");
            _category.CreateEntry("FlashColorG", FlashColor.g, "Flash green (0-1)");
            _category.CreateEntry("FlashColorB", FlashColor.b, "Flash blue (0-1)");
            _category.CreateEntry("FlashColorA", FlashColor.a, "Flash alpha (0-1)");
            _category.CreateEntry("FlashIntensity", FlashIntensity, "Flash intensity multiplier");
            _category.CreateEntry("FlashTargetOnly", FlashTargetOnly, "Flash only target vs full screen");

            // Screen Shake
            _category.CreateEntry("ShakeMagnitude", ShakeMagnitude, "Shake magnitude");
            _category.CreateEntry("ShakeRoughness", ShakeRoughness, "Shake roughness");
            _category.CreateEntry("ShakeFadeIn", ShakeFadeIn, "Shake fade in");
            _category.CreateEntry("ShakeFadeOut", ShakeFadeOut, "Shake fade out");

            // Particles
            _category.CreateEntry("EnableParticles", EnableParticles, "Enable hit particles");
            _category.CreateEntry("ParticleCount", ParticleCount, "Particles per hit");
            _category.CreateEntry("ParticleSize", ParticleSize, "Particle size");
            _category.CreateEntry("ParticleLifetime", ParticleLifetime, "Particle lifetime");
            _category.CreateEntry("ParticleSpeed", ParticleSpeed, "Particle initial speed");
            _category.CreateEntry("ParticleColorR", ParticleColor.r, "Particle start red");
            _category.CreateEntry("ParticleColorG", ParticleColor.g, "Particle start green");
            _category.CreateEntry("ParticleColorB", ParticleColor.b, "Particle start blue");
            _category.CreateEntry("ParticleColorA", ParticleColor.a, "Particle start alpha");

            // Sound
            _category.CreateEntry("EnableSound", EnableSound, "Enable hit sound");
            _category.CreateEntry("SoundVolume", SoundVolume, "Sound volume (0-1)");
            _category.CreateEntry("SoundPitch", SoundPitch, "Sound pitch multiplier");

            // Hit Marker
            _category.CreateEntry("ShowHitMarker", ShowHitMarker, "Show hit marker");
            _category.CreateEntry("HitMarkerSize", HitMarkerSize, "Hit marker size (world units)");
            _category.CreateEntry("HitMarkerDuration", HitMarkerDuration, "Hit marker duration");
            _category.CreateEntry("HitMarkerColorR", HitMarkerColor.r, "Hit marker red");
            _category.CreateEntry("HitMarkerColorG", HitMarkerColor.g, "Hit marker green");
            _category.CreateEntry("HitMarkerColorB", HitMarkerColor.b, "Hit marker blue");
            _category.CreateEntry("HitMarkerColorA", HitMarkerColor.a, "Hit marker alpha");
            _category.CreateEntry("HitMarkerThickness", HitMarkerThickness, "Hit marker line thickness");

            // Combo
            _category.CreateEntry("EnableCombo", EnableCombo, "Enable combo system");
            _category.CreateEntry("ComboWindow", ComboWindow, "Combo window (seconds)");
            _category.CreateEntry("MaxComboCount", MaxComboCount, "Max combo count");
            _category.CreateEntry("ShowComboUI", ShowComboUI, "Show combo counter on screen");

            // Kill Slow-mo
            _category.CreateEntry("KillSlowMo", KillSlowMo, "Extra slow-mo on kill");
            _category.CreateEntry("KillSlowMoDuration", KillSlowMoDuration, "Kill slow-mo duration");
            _category.CreateEntry("KillSlowMoScale", KillSlowMoScale, "Kill slow-mo time scale");
        }

        private static void LoadPreferences()
        {
            Enabled = _category.GetEntry<bool>("Enabled").Value;
            ImpactChance = Mathf.Clamp01(_category.GetEntry<float>("ImpactChance").Value);
            RequireBoneWeapon = _category.GetEntry<bool>("RequireBoneWeapon").Value;
            TriggerOnNPCs = _category.GetEntry<bool>("TriggerOnNPCs").Value;
            TriggerOnPlayers = _category.GetEntry<bool>("TriggerOnPlayers").Value;
            MinDamageThreshold = Mathf.Max(0, _category.GetEntry<float>("MinDamageThreshold").Value);
            Cooldown = Mathf.Max(0, _category.GetEntry<float>("Cooldown").Value);

            ActiveEffects = (Effects)Mathf.Clamp(_category.GetEntry<int>("ActiveEffects").Value, 0, 127);

            TimeScale = Mathf.Clamp01(_category.GetEntry<float>("TimeScale").Value);
            Duration = Mathf.Max(0.01f, _category.GetEntry<float>("Duration").Value);
            EaseInTime = Mathf.Max(0, _category.GetEntry<float>("EaseInTime").Value);
            EaseOutTime = Mathf.Max(0, _category.GetEntry<float>("EaseOutTime").Value);

            FlashColor = new Color(
                Mathf.Clamp01(_category.GetEntry<float>("FlashColorR").Value),
                Mathf.Clamp01(_category.GetEntry<float>("FlashColorG").Value),
                Mathf.Clamp01(_category.GetEntry<float>("FlashColorB").Value),
                Mathf.Clamp01(_category.GetEntry<float>("FlashColorA").Value)
            );
            FlashIntensity = Mathf.Max(0, _category.GetEntry<float>("FlashIntensity").Value);
            FlashTargetOnly = _category.GetEntry<bool>("FlashTargetOnly").Value;

            ShakeMagnitude = Mathf.Max(0, _category.GetEntry<float>("ShakeMagnitude").Value);
            ShakeRoughness = Mathf.Max(0, _category.GetEntry<float>("ShakeRoughness").Value);
            ShakeFadeIn = Mathf.Max(0, _category.GetEntry<float>("ShakeFadeIn").Value);
            ShakeFadeOut = Mathf.Max(0, _category.GetEntry<float>("ShakeFadeOut").Value);

            EnableParticles = _category.GetEntry<bool>("EnableParticles").Value;
            ParticleCount = Mathf.Clamp(_category.GetEntry<int>("ParticleCount").Value, 0, 200);
            ParticleSize = Mathf.Max(0.01f, _category.GetEntry<float>("ParticleSize").Value);
            ParticleLifetime = Mathf.Max(0.01f, _category.GetEntry<float>("ParticleLifetime").Value);
            ParticleSpeed = Mathf.Max(0, _category.GetEntry<float>("ParticleSpeed").Value);
            ParticleColor = new Color(
                Mathf.Clamp01(_category.GetEntry<float>("ParticleColorR").Value),
                Mathf.Clamp01(_category.GetEntry<float>("ParticleColorG").Value),
                Mathf.Clamp01(_category.GetEntry<float>("ParticleColorB").Value),
                Mathf.Clamp01(_category.GetEntry<float>("ParticleColorA").Value)
            );

            EnableSound = _category.GetEntry<bool>("EnableSound").Value;
            SoundVolume = Mathf.Clamp01(_category.GetEntry<float>("SoundVolume").Value);
            SoundPitch = Mathf.Max(0.1f, _category.GetEntry<float>("SoundPitch").Value);

            ShowHitMarker = _category.GetEntry<bool>("ShowHitMarker").Value;
            HitMarkerSize = Mathf.Max(0.01f, _category.GetEntry<float>("HitMarkerSize").Value);
            HitMarkerDuration = Mathf.Max(0.01f, _category.GetEntry<float>("HitMarkerDuration").Value);
            HitMarkerColor = new Color(
                Mathf.Clamp01(_category.GetEntry<float>("HitMarkerColorR").Value),
                Mathf.Clamp01(_category.GetEntry<float>("HitMarkerColorG").Value),
                Mathf.Clamp01(_category.GetEntry<float>("HitMarkerColorB").Value),
                Mathf.Clamp01(_category.GetEntry<float>("HitMarkerColorA").Value)
            );
            HitMarkerThickness = Mathf.Max(0.001f, _category.GetEntry<float>("HitMarkerThickness").Value);

            EnableCombo = _category.GetEntry<bool>("EnableCombo").Value;
            ComboWindow = Mathf.Max(0.1f, _category.GetEntry<float>("ComboWindow").Value);
            MaxComboCount = Mathf.Clamp(_category.GetEntry<int>("MaxComboCount").Value, 1, 999);
            ShowComboUI = _category.GetEntry<bool>("ShowComboUI").Value;

            KillSlowMo = _category.GetEntry<bool>("KillSlowMo").Value;
            KillSlowMoDuration = Mathf.Max(0.01f, _category.GetEntry<float>("KillSlowMoDuration").Value);
            KillSlowMoScale = Mathf.Clamp01(_category.GetEntry<float>("KillSlowMoScale").Value);

            MelonLogger.Msg($"[ImpactFrame] Config loaded - Effects: {ActiveEffects}, Chance: {ImpactChance}, Duration: {Duration}s, TimeScale: {TimeScale}");
        }

        public static void ReloadConfig() => LoadPreferences();

        [HarmonyPatch(typeof(MelonPreferences), nameof(MelonPreferences.Save))]
        static class MelonPreferencesSavePatch
        {
            static void Postfix() => LoadPreferences();
        }

        private void SetupHarmony()
        {
            try
            {
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                MelonLogger.Msg("[ImpactFrame] Harmony patches applied!");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ImpactFrame] Patch failed: {ex}");
            }
        }

        public override void OnDeinitializeMelon()
        {
            _harmony.UnpatchSelf();
            CleanupResources();
            MelonLogger.Msg("[ImpactFrame] Mod deinitialized.");
        }

        private static void CleanupResources()
        {
            if (_particleSystem != null) UnityEngine.Object.Destroy(_particleSystem);
            if (_audioSource != null) UnityEngine.Object.Destroy(_audioSource);
            _originalMaterials.Clear();
        }

        // ========== CORE PATCH ==========
        [HarmonyPatch("CombatManager", "OnHit")]
        static class CombatManagerOnHitPatch
        {
            static bool Prefix(object __instance, object hitData)
            {
                if (!Enabled) return true;
                if (Time.unscaledTime - _lastTriggerTime < Cooldown) return true;
                if (UnityEngine.Random.value > ImpactChance) return true;

                var weapon = hitData?.GetType().GetProperty("weapon")?.GetValue(hitData) as GameObject;
                if (RequireBoneWeapon && weapon != null && !IsBoneWeapon(weapon)) return true;

                var damage = hitData?.GetType().GetProperty("damage")?.GetValue(hitData);
                if (damage is float dmg && dmg < MinDamageThreshold) return true;

                var targetRig = hitData?.GetType().GetProperty("targetRig")?.GetValue(hitData);
                if (targetRig == null) return true;

                var rigManager = targetRig as RigManager;
                if (rigManager == null) return true;

                bool isPlayer = false;
                var isPlayerProp = rigManager.GetType().GetProperty("isPlayer");
                if (isPlayerProp != null) isPlayer = (bool)(isPlayerProp.GetValue(rigManager) ?? false);

                bool isNPC = false;
                var npcType = Type.GetType("NPC, Assembly-CSharp");
                if (npcType != null)
                {
                    isNPC = rigManager.gameObject.GetComponent(npcType) != null ||
                            rigManager.gameObject.GetComponentInParent(npcType) != null;
                }

                if ((isPlayer && !TriggerOnPlayers) || (isNPC && !TriggerOnNPCs)) return true;
                if (!isPlayer && !isNPC) return true;

                _lastTriggerTime = Time.unscaledTime;
                UpdateCombo();

                // Check if this is a killing blow
                bool isKill = false;
                var healthProp = targetRig.GetType().GetProperty("health");
                if (healthProp != null)
                {
                    float health = (float)(healthProp.GetValue(targetRig) ?? 100f);
                    if (health <= 0) isKill = true;
                }

                MelonCoroutines.Start(DoImpactEffect(rigManager, hitData, isPlayer, isNPC, isKill));
                return true;
            }

            private static bool IsBoneWeapon(GameObject weapon)
            {
                if (weapon == null) return false;
                string name = weapon.name.ToLower();
                return name.Contains("bone") || name.Contains("skull") || name.Contains("femur") ||
                       name.Contains("humerus") || name.Contains("rib") || name.Contains("spine") ||
                       name.Contains("clavicle") || name.Contains("scapula") || name.Contains("pelvis") ||
                       name.Contains("radius") || name.Contains("ulna") || name.Contains("tibia") ||
                       name.Contains("fibula") || name.Contains("vertebra");
            }

            private static void UpdateCombo()
            {
                if (!EnableCombo) return;
                if (Time.unscaledTime - _lastHitTime <= ComboWindow)
                    ComboCount = Mathf.Min(ComboCount + 1, MaxComboCount);
                else
                    ComboCount = 1;
                _lastHitTime = Time.unscaledTime;
            }
        }

        // ========== DEATH PATCH FOR KILL SLOW-MO ==========
        [HarmonyPatch("NPC", "Die")]
        static class NPCCOnDiePatch
        {
            static void Postfix()
            {
                if (KillSlowMo && Enabled)
                    MelonCoroutines.Start(DoKillSlowMo());
            }
        }

        // ========== IMPACT EFFECT ==========
        private static IEnumerator DoImpactEffect(RigManager targetRig, object hitData, bool isPlayer, bool isNPC, bool isKill)
        {
            if (_isEffectActive) yield break;
            _isEffectActive = true;

            InitializeResources();
            float startTime = Time.unscaledTime;
            float endTime = startTime + Duration;
            float originalTimeScale = Time.timeScale;
            float originalFixedDelta = Time.fixedDeltaTime;

            // Capture hit position
            Vector3 hitPos = targetRig.transform.position + Vector3.up * 1.5f;
            var hitPointProp = hitData?.GetType().GetProperty("hitPoint");
            if (hitPointProp != null && hitPointProp.GetValue(hitData) is Vector3 hp)
                hitPos = hp;

            // Flash target renderers
            if ((ActiveEffects & Effects.ScreenFlash) != 0)
                StartTargetFlash(targetRig);

            // Trigger sub-effects
            var shakeRoutine = ((ActiveEffects & Effects.ScreenShake) != 0) ? MelonCoroutines.Start(DoScreenShake()) : null;
            var particleRoutine = (EnableParticles && (ActiveEffects & Effects.Particles) != 0) ? MelonCoroutines.Start(DoParticles(hitPos)) : null;
            var soundRoutine = (EnableSound && (ActiveEffects & Effects.Sound) != 0) ? MelonCoroutines.Start(PlayHitSound(hitPos)) : null;
            var markerRoutine = (ShowHitMarker && (ActiveEffects & Effects.HitMarker) != 0) ? MelonCoroutines.Start(ShowHitMarkerAt(hitPos)) : null;

            // Time manipulation loop
            bool isTimeStop = (ActiveEffects & Effects.TimeStop) != 0;
            bool isTimeSlow = (ActiveEffects & Effects.TimeSlow) != 0 || !isTimeStop;

            while (Time.unscaledTime < endTime)
            {
                float t = (Time.unscaledTime - startTime) / Duration;
                float curveValue = EaseCurve(t);

                if (isTimeStop)
                {
                    Time.timeScale = Mathf.Lerp(1f, TimeScale, curveValue);
                }
                else if (isTimeSlow)
                {
                    Time.timeScale = Mathf.Lerp(1f, TimeScale, curveValue);
                }

                Time.fixedDeltaTime = 0.02f * Time.timeScale;

                UpdateTargetFlash(targetRig, t);
                yield return null;
            }

            // Restore
            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDelta;
            EndTargetFlash(targetRig);

            _isEffectActive = false;
        }

        private static IEnumerator DoKillSlowMo()
        {
            float startTime = Time.unscaledTime;
            float endTime = startTime + KillSlowMoDuration;
            float originalTimeScale = Time.timeScale;
            float originalFixedDelta = Time.fixedDeltaTime;

            while (Time.unscaledTime < endTime)
            {
                float t = (Time.unscaledTime - startTime) / KillSlowMoDuration;
                float curveValue = EaseCurve(t);
                Time.timeScale = Mathf.Lerp(originalTimeScale, KillSlowMoScale, curveValue);
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                yield return null;
            }

            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDelta;
        }

        private static float EaseCurve(float t)
        {
            if (t < EaseInTime / Duration) return t * Duration / EaseInTime;
            if (t > 1f - EaseOutTime / Duration) return 1f - (1f - t) * Duration / EaseOutTime;
            return 1f;
        }

        // ========== TARGET FLASH ==========
        private static void StartTargetFlash(RigManager targetRig)
        {
            var renderers = targetRig.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (!_originalMaterials.ContainsKey(r))
                    _originalMaterials[r] = r.materials;

                Material[] mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null)
                    {
                        mats[i] = new Material(mats[i]);
                        mats[i].EnableKeyword("_EMISSION");
                    }
                }
                r.materials = mats;
            }
        }

        private static void UpdateTargetFlash(RigManager targetRig, float t)
        {
            if (!FlashTargetOnly) return;

            float flash = Mathf.Sin(t * (float)Math.PI) * FlashIntensity;
            Color emissionColor = FlashColor * flash * 2f;

            var renderers = targetRig.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.materials != null)
                {
                    foreach (var mat in r.materials)
                    {
                        if (mat != null && mat.IsKeywordEnabled("_EMISSION"))
                            mat.SetColor("_EmissionColor", emissionColor);
                    }
                }
            }
        }

        private static void EndTargetFlash(RigManager targetRig)
        {
            foreach (var kvp in _originalMaterials)
            {
                if (kvp.Key != null)
                    kvp.Key.materials = kvp.Value;
            }
            _originalMaterials.Clear();
        }

        // ========== RESOURCES ==========
        private static void InitializeResources()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;

            // Particle system
            if (_particleSystem == null && EnableParticles && (ActiveEffects & Effects.Particles) != 0)
            {
                _particleSystem = new GameObject("ImpactFrameParticles");
                var ps = _particleSystem.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.1f;
                main.loop = false;
                main.startLifetime = ParticleLifetime;
                main.startSize = ParticleSize;
                main.startColor = new ParticleSystem.MinMaxGradient(ParticleColor, ParticleColorEnd);
                main.startSpeed = ParticleSpeed;
                main.gravityModifier = 0f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = ps.emission;
                emission.rateOverTime = 0f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.05f;
            }

            // Audio source
            if (_audioSource == null && EnableSound && _mainCamera != null)
            {
                _audioSource = _mainCamera.gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 0f;
                _audioSource.playOnAwake = false;
                _audioSource.volume = SoundVolume;
            }
        }

        // ========== SUB-EFFECTS ==========
        private static IEnumerator DoScreenShake()
        {
            if (_mainCamera == null) yield break;

            Vector3 originalPos = _mainCamera.transform.localPosition;
            float elapsed = 0f;
            float dur = Duration;

            while (elapsed < dur)
            {
                float t = elapsed / dur;
                float fade = 1f;
                if (t < ShakeFadeIn / dur) fade = t * dur / ShakeFadeIn;
                else if (t > 1f - ShakeFadeOut / dur) fade = (1f - t) * dur / ShakeFadeOut;

                float magnitude = ShakeMagnitude * fade;
                Vector3 offset = new Vector3(
                    (Mathf.PerlinNoise(Time.unscaledTime * ShakeRoughness, 0) - 0.5f) * 2,
                    (Mathf.PerlinNoise(0, Time.unscaledTime * ShakeRoughness) - 0.5f) * 2,
                    0
                ) * magnitude;

                _mainCamera.transform.localPosition = originalPos + offset;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _mainCamera.transform.localPosition = originalPos;
        }

        private static IEnumerator DoParticles(Vector3 position)
        {
            if (_particleSystem == null) yield break;
            _particleSystem.transform.position = position;
            var ps = _particleSystem.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(ParticleColor, ParticleColorEnd);
            main.startSize = ParticleSize;
            main.startLifetime = ParticleLifetime;
            main.startSpeed = ParticleSpeed;
            ps.Emit(ParticleCount);
            yield return new WaitForSecondsRealtime(ParticleLifetime);
        }

        private static IEnumerator PlayHitSound(Vector3 position)
        {
            if (_audioSource == null) yield break;

            int sampleRate = 44100;
            int length = (int)(sampleRate * 0.12f);
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(1000f * SoundPitch, 150f * SoundPitch, t);
                float env = Mathf.Exp(-t * 15f) * (1f - t);
                samples[i] = Mathf.Sin(2 * (float)Math.PI * freq * t) * env * SoundVolume * 0.4f;
            }
            var clip = AudioClip.Create("ImpactHit", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            _audioSource.pitch = SoundPitch;
            _audioSource.PlayOneShot(clip);
            yield return new WaitForSecondsRealtime(0.15f);
            UnityEngine.Object.Destroy(clip);
        }

        private static IEnumerator ShowHitMarkerAt(Vector3 worldPos)
        {
            // Create a simple world-space crosshair using LineRenderers
            GameObject marker = new GameObject("HitMarker");
            marker.transform.position = worldPos;

            GameObject[] lines = new GameObject[4];
            LineRenderer[] lrs = new LineRenderer[4];

            for (int i = 0; i < 4; i++)
            {
                lines[i] = new GameObject("Line" + i);
                lines[i].transform.SetParent(marker.transform);
                lrs[i] = lines[i].AddComponent<LineRenderer>();
                lrs[i].material = new Material(Shader.Find("Sprites/Default"));
                lrs[i].startColor = HitMarkerColor;
                lrs[i].endColor = HitMarkerColor;
                lrs[i].startWidth = HitMarkerThickness;
                lrs[i].endWidth = HitMarkerThickness;
                lrs[i].positionCount = 2;
                lrs[i].useWorldSpace = true;

                float angle = i * 90f * (float)Math.PI / 180f;
                float offset = HitMarkerSize * 0.2f;
                float len = HitMarkerSize * 0.5f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                lrs[i].SetPosition(0, worldPos + dir * offset);
                lrs[i].SetPosition(1, worldPos + dir * len);
            }

            float elapsed = 0f;
            while (elapsed < HitMarkerDuration)
            {
                float t = elapsed / HitMarkerDuration;
                float alpha = Mathf.Lerp(1f, 0f, t * t);
                Color c = HitMarkerColor; c.a = alpha;
                foreach (var lr in lrs)
                {
                    lr.startColor = c; lr.endColor = c;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            UnityEngine.Object.Destroy(marker);
        }
    }

    // ========== COMBO UI (simple OnGUI) ==========
    public class ComboDisplay : MonoBehaviour
    {
        private GUIStyle _style;
        private GUIStyle _shadowStyle;

        private void OnGUI()
        {
            if (!ImpactFrameMod.EnableCombo || !ImpactFrameMod.ShowComboUI) return;
            if (ImpactFrameMod.ComboCount <= 1) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label);
                _style.fontSize = 42;
                _style.fontStyle = FontStyle.Bold;
                _style.alignment = TextAnchor.UpperCenter;
                _style.normal.textColor = Color.white;

                _shadowStyle = new GUIStyle(_style);
                _shadowStyle.normal.textColor = new Color(0, 0, 0, 0.8f);
            }

            int combo = ImpactFrameMod.ComboCount;
            string text = "x" + combo + " COMBO";
            if (combo >= 5) text = "x" + combo + " COMBO!";
            if (combo >= 10) text = "x" + combo + " COMBO!!";
            if (combo >= 20) text = "x" + combo + " UNSTOPPABLE!!!";
            if (combo >= 50) text = "x" + combo + " LEGENDARY!!!!";

            float x = Screen.width * 0.5f;
            float y = 120f;

            // Shadow
            GUI.Label(new Rect(x - 100 + 2, y + 2, 200, 60), new GUIContent(text), _shadowStyle);
            // Main
            GUI.Label(new Rect(x - 100, y, 200, 60), new GUIContent(text), _style);
        }
    }

    // Auto-add ComboDisplay to main camera
    [HarmonyPatch(typeof(Camera), "OnPostRender")]
    static class CameraPatch
    {
        static void Postfix(Camera __instance)
        {
            if (__instance == Camera.main && __instance.GetComponent<ComboDisplay>() == null)
                __instance.gameObject.AddComponent<ComboDisplay>();
        }
    }
}