using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

[assembly: MelonInfo(typeof(ImpactFrame.ImpactFrameMod), "ImpactFrame", "2.2.0", "gdono")]
[assembly: MelonGame("Stress Level Zero", "Bonelab")]

namespace ImpactFrame
{
    public class ImpactFrameMod : MelonMod
    {
        // ========== QUEST DEFAULTS ==========
        private const bool IS_QUEST = true;
        private const int DEFAULT_PARTICLE_CAP = 30;
        private const float DEFAULT_TIME_SCALE = 0.08f;
        private const float DEFAULT_FLASH_INTENSITY = 1.2f;
        private const float DEFAULT_SHAKE_MAGNITUDE = 0.06f;
        private const float DEFAULT_SOUND_VOLUME = 0.5f;
        private const float DEFAULT_HIT_MARKER_SIZE = 0.2f;
        public const int DEFAULT_COMBO_FONT_SIZE = 32;
        private const float DEFAULT_KILL_SCALE = 0.2f;

        // ========== CONFIGURATION ==========
        public static bool Enabled = true;
        public static float ImpactChance = 0.4f;
        public static bool RequireBoneWeapon = true;
        public static bool TriggerOnNPCs = true;
        public static bool TriggerOnPlayers = true;
        public static float MinDamageThreshold = 3f;
        public static float Cooldown = 0.3f;

        [Flags]
        public enum Effects { None = 0, TimeStop = 1, TimeSlow = 2, ScreenFlash = 4, ScreenShake = 8, Particles = 16, Sound = 32, HitMarker = 64, All = 127 }
        public static Effects ActiveEffects = Effects.All & ~Effects.Particles & ~Effects.ScreenShake; // Conservative defaults for Quest

        public static float TimeScale = 0.08f;
        public static float Duration = 0.1f;
        public static float EaseInTime = 0.015f;
        public static float EaseOutTime = 0.05f;

        public static Color FlashColor = new Color(1f, 0.15f, 0.05f, 1f);
        public static float FlashIntensity = 1.2f;
        public static bool FlashTargetOnly = true;

        public static float ShakeMagnitude = 0.06f;
        public static float ShakeRoughness = 8f;
        public static float ShakeFadeIn = 0.01f;
        public static float ShakeFadeOut = 0.04f;

        public static bool EnableParticles = false;
        public static int ParticleCount = 10;
        public static float ParticleSize = 0.04f;
        public static float ParticleLifetime = 0.25f;
        public static float ParticleSpeed = 2f;
        public static Color ParticleColor = new Color(1f, 0.5f, 0.1f, 1f);
        public static Color ParticleColorEnd = new Color(1f, 0.1f, 0.05f, 0f);

        public static bool EnableSound = true;
        public static float SoundVolume = 0.5f;
        public static float SoundPitch = 1.3f;

        public static bool ShowHitMarker = true;
        public static float HitMarkerSize = 0.18f;
        public static float HitMarkerDuration = 0.12f;
        public static Color HitMarkerColor = new Color(1f, 1f, 1f, 1f);
        public static float HitMarkerThickness = 0.015f;

        public static bool EnableCombo = true;
        public static float ComboWindow = 1.2f;
        public static int MaxComboCount = 50;
        public static bool ShowComboUI = true;

        public static bool KillSlowMo = true;
        public static float KillSlowMoDuration = 0.3f;
        public static float KillSlowMoScale = 0.2f;

        // UI State
        public static bool ShowConfigUI = false;
        private static int _uiTab = 0;
        private static Vector2 _scrollPos;
        private static Rect _windowRect = new Rect(50, 50, 400, 500);
        private static bool _uiInitialized = false;

        // ========== INTERNAL STATE ==========
        private static readonly HarmonyLib.Harmony _harmony = new HarmonyLib.Harmony("gdono.impactframe");
        private static MelonPreferences_Category _category;
        private static float _lastTriggerTime = -999f;
        public static int ComboCount = 0;
        private static float _lastHitTime = -999f;
        private static Camera _mainCamera;
        private static GameObject _particleSystem;
        private static AudioSource _audioSource;
        private static bool _isEffectActive = false;
        private static Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

        // Cached reflection types
        public static Type _rigManagerType;
        public static Type _npcType;
        public static PropertyInfo _isPlayerProperty;
        public static PropertyInfo _healthProperty;

        // Debug
        public static int _hitCount = 0;
        public static int _triggerCount = 0;
        public static string _lastDebugMsg = "";

        public override void OnInitializeMelon()
        {
            _category = MelonPreferences.CreateCategory("ImpactFrame");
            RegisterPreferences();
            LoadPreferences();
            InitializeReflection();
            SetupHarmony();
            MelonLogger.Msg("[ImpactFrame] Quest v2.2 initialized!");
            _lastDebugMsg = "Mod initialized - Press trigger+grip to open config";
        }

        private static void InitializeReflection()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "Assembly-CSharp")
                {
                    _rigManagerType = asm.GetType("RigManager");
                    _npcType = asm.GetType("NPC");
                    break;
                }
            }

            if (_rigManagerType != null)
            {
                _isPlayerProperty = _rigManagerType.GetProperty("isPlayer");
                _healthProperty = _rigManagerType.GetProperty("health");
            }
        }

        private void RegisterPreferences()
        {
            _category.CreateEntry("Enabled", Enabled, "Master toggle");
            _category.CreateEntry("ImpactChance", ImpactChance, "Trigger chance (0-1)");
            _category.CreateEntry("RequireBoneWeapon", RequireBoneWeapon, "Only trigger on bone weapons");
            _category.CreateEntry("TriggerOnNPCs", TriggerOnNPCs, "Trigger on NPC hits");
            _category.CreateEntry("TriggerOnPlayers", TriggerOnPlayers, "Trigger on player hits");
            _category.CreateEntry("MinDamageThreshold", MinDamageThreshold, "Minimum damage to trigger");
            _category.CreateEntry("Cooldown", Cooldown, "Cooldown between triggers (seconds)");
            _category.CreateEntry("ActiveEffects", (int)ActiveEffects, "Bitmask: 1=TimeStop,2=TimeSlow,4=Flash,8=Shake,16=Particles,32=Sound,64=HitMarker");
            _category.CreateEntry("TimeScale", TimeScale, "Time scale during effect");
            _category.CreateEntry("Duration", Duration, "Effect duration (seconds)");
            _category.CreateEntry("EaseInTime", EaseInTime, "Ease in duration");
            _category.CreateEntry("EaseOutTime", EaseOutTime, "Ease out duration");
            _category.CreateEntry("FlashColorR", FlashColor.r, "Flash red (0-1)");
            _category.CreateEntry("FlashColorG", FlashColor.g, "Flash green (0-1)");
            _category.CreateEntry("FlashColorB", FlashColor.b, "Flash blue (0-1)");
            _category.CreateEntry("FlashColorA", FlashColor.a, "Flash alpha (0-1)");
            _category.CreateEntry("FlashIntensity", FlashIntensity, "Flash intensity multiplier");
            _category.CreateEntry("FlashTargetOnly", FlashTargetOnly, "Flash only target vs full screen");
            _category.CreateEntry("ShakeMagnitude", ShakeMagnitude, "Shake magnitude");
            _category.CreateEntry("ShakeRoughness", ShakeRoughness, "Shake roughness");
            _category.CreateEntry("ShakeFadeIn", ShakeFadeIn, "Shake fade in");
            _category.CreateEntry("ShakeFadeOut", ShakeFadeOut, "Shake fade out");
            _category.CreateEntry("EnableParticles", EnableParticles, "Enable hit particles (costly)");
            _category.CreateEntry("ParticleCount", ParticleCount, "Particles per hit");
            _category.CreateEntry("ParticleSize", ParticleSize, "Particle size");
            _category.CreateEntry("ParticleLifetime", ParticleLifetime, "Particle lifetime");
            _category.CreateEntry("ParticleSpeed", ParticleSpeed, "Particle initial speed");
            _category.CreateEntry("ParticleColorR", ParticleColor.r, "Particle start red");
            _category.CreateEntry("ParticleColorG", ParticleColor.g, "Particle start green");
            _category.CreateEntry("ParticleColorB", ParticleColor.b, "Particle start blue");
            _category.CreateEntry("ParticleColorA", ParticleColor.a, "Particle start alpha");
            _category.CreateEntry("EnableSound", EnableSound, "Enable hit sound");
            _category.CreateEntry("SoundVolume", SoundVolume, "Sound volume (0-1)");
            _category.CreateEntry("SoundPitch", SoundPitch, "Sound pitch multiplier");
            _category.CreateEntry("ShowHitMarker", ShowHitMarker, "Show hit marker");
            _category.CreateEntry("HitMarkerSize", HitMarkerSize, "Hit marker size (world units)");
            _category.CreateEntry("HitMarkerDuration", HitMarkerDuration, "Hit marker duration");
            _category.CreateEntry("HitMarkerColorR", HitMarkerColor.r, "Hit marker red");
            _category.CreateEntry("HitMarkerColorG", HitMarkerColor.g, "Hit marker green");
            _category.CreateEntry("HitMarkerColorB", HitMarkerColor.b, "Hit marker blue");
            _category.CreateEntry("HitMarkerColorA", HitMarkerColor.a, "Hit marker alpha");
            _category.CreateEntry("HitMarkerThickness", HitMarkerThickness, "Hit marker line thickness");
            _category.CreateEntry("EnableCombo", EnableCombo, "Enable combo system");
            _category.CreateEntry("ComboWindow", ComboWindow, "Combo window (seconds)");
            _category.CreateEntry("MaxComboCount", MaxComboCount, "Max combo count");
            _category.CreateEntry("ShowComboUI", ShowComboUI, "Show combo counter on screen");
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
            ParticleCount = Mathf.Clamp(_category.GetEntry<int>("ParticleCount").Value, 0, 30);
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
            MaxComboCount = Mathf.Clamp(_category.GetEntry<int>("MaxComboCount").Value, 1, 99);
            ShowComboUI = _category.GetEntry<bool>("ShowComboUI").Value;

            KillSlowMo = _category.GetEntry<bool>("KillSlowMo").Value;
            KillSlowMoDuration = Mathf.Max(0.01f, _category.GetEntry<float>("KillSlowMoDuration").Value);
            KillSlowMoScale = Mathf.Clamp01(_category.GetEntry<float>("KillSlowMoScale").Value);

            MelonLogger.Msg($"[ImpactFrame] Config loaded - Enabled: {Enabled}, Chance: {ImpactChance}, Effects: {ActiveEffects}");
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
        }

        private static void CleanupResources()
        {
            if (_particleSystem != null) UnityEngine.Object.Destroy(_particleSystem);
            if (_audioSource != null) UnityEngine.Object.Destroy(_audioSource);
            _originalMaterials.Clear();
        }

        // ========== HELPER METHODS ==========
        private static object GetPropertyValue(object obj, string propertyName)
        {
            if (obj == null) return null;
            var prop = obj.GetType().GetProperty(propertyName);
            return prop?.GetValue(obj);
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

        // ========== CORE PATCH ==========
        [HarmonyPatch("CombatManager", "OnHit")]
        static class CombatManagerOnHitPatch
        {
            static bool Prefix(object __instance, object hitData)
            {
                if (!Enabled) return true;
                if (Time.unscaledTime - _lastTriggerTime < Cooldown) return true;
                if (UnityEngine.Random.value > ImpactChance) return true;

                var weapon = GetPropertyValue(hitData, "weapon") as GameObject;
                if (RequireBoneWeapon && weapon != null && !IsBoneWeapon(weapon)) return true;

                var damage = GetPropertyValue(hitData, "damage");
                if (damage is float dmg && dmg < MinDamageThreshold) return true;

                var targetRig = GetPropertyValue(hitData, "targetRig");
                if (targetRig == null) return true;

                if (_rigManagerType == null || !_rigManagerType.IsInstanceOfType(targetRig)) return true;

                bool isPlayer = false;
                if (_isPlayerProperty != null)
                    isPlayer = (bool)(_isPlayerProperty.GetValue(targetRig) ?? false);

                bool isNPC = false;
                if (_npcType != null)
                {
                    var go = targetRig as GameObject;
                    if (go != null)
                    {
                        var getComponent = typeof(GameObject).GetMethod("GetComponent", new[] { typeof(string) });
                        var getComponentInParent = typeof(GameObject).GetMethod("GetComponentInParent", new[] { typeof(string) });
                        string npcTypeName = _npcType.FullName;
                        isNPC = getComponent.Invoke(go, new object[] { npcTypeName }) != null ||
                                getComponentInParent.Invoke(go, new object[] { npcTypeName }) != null;
                    }
                }

                if ((isPlayer && !TriggerOnPlayers) || (isNPC && !TriggerOnNPCs)) return true;
                if (!isPlayer && !isNPC) return true;

                _lastTriggerTime = Time.unscaledTime;
                UpdateCombo();

                bool isKill = false;
                if (_healthProperty != null)
                {
                    float health = (float)(_healthProperty.GetValue(targetRig) ?? 100f);
                    if (health <= 0) isKill = true;
                }

                _hitCount++;
                _triggerCount++;
                _lastDebugMsg = $"Hit #{_hitCount} - Triggered! (Weapon: {weapon?.name ?? "none"})";

                MelonCoroutines.Start(DoImpactEffect(targetRig, hitData, isPlayer, isNPC, isKill));
                return true;
            }

            private static object GetPropertyValue(object obj, string propertyName)
            {
                if (obj == null) return null;
                var prop = obj.GetType().GetProperty(propertyName);
                return prop?.GetValue(obj);
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
        private static IEnumerator DoImpactEffect(object targetRig, object hitData, bool isPlayer, bool isNPC, bool isKill)
        {
            if (_isEffectActive) yield break;
            _isEffectActive = true;

            InitializeResources();
            float startTime = Time.unscaledTime;
            float endTime = startTime + Duration;
            float originalTimeScale = Time.timeScale;
            float originalFixedDelta = Time.fixedDeltaTime;

            Vector3 hitPos = ((GameObject)targetRig).transform.position + Vector3.up * 1.5f;
            var hitPoint = GetPropertyValue(hitData, "hitPoint");
            if (hitPoint is Vector3 hp) hitPos = hp;

            if ((ActiveEffects & Effects.ScreenFlash) != 0) StartTargetFlash(targetRig);

            var shakeRoutine = ((ActiveEffects & Effects.ScreenShake) != 0) ? MelonCoroutines.Start(DoScreenShake()) : null;
            var particleRoutine = (EnableParticles && (ActiveEffects & Effects.Particles) != 0) ? MelonCoroutines.Start(DoParticles(hitPos)) : null;
            var soundRoutine = (EnableSound && (ActiveEffects & Effects.Sound) != 0) ? MelonCoroutines.Start(PlayHitSound(hitPos)) : null;
            var markerRoutine = (ShowHitMarker && (ActiveEffects & Effects.HitMarker) != 0) ? MelonCoroutines.Start(ShowHitMarkerAt(hitPos)) : null;

            bool isTimeStop = (ActiveEffects & Effects.TimeStop) != 0;
            bool isTimeSlow = (ActiveEffects & Effects.TimeSlow) != 0 || !isTimeStop;

            while (Time.unscaledTime < endTime)
            {
                float t = (Time.unscaledTime - startTime) / Duration;
                float curveValue = EaseCurve(t);

                if (isTimeStop)
                    Time.timeScale = Mathf.Lerp(1f, TimeScale, curveValue);
                else if (isTimeSlow)
                    Time.timeScale = Mathf.Lerp(1f, TimeScale, curveValue);

                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                UpdateTargetFlash(targetRig, t);
                yield return null;
            }

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
        private static void StartTargetFlash(object targetRig)
        {
            var go = targetRig as GameObject;
            if (go == null) return;

            var renderers = go.GetComponentsInChildren<Renderer>();
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

        private static void UpdateTargetFlash(object targetRig, float t)
        {
            if (!FlashTargetOnly) return;

            float flash = Mathf.Sin(t * (float)Math.PI) * FlashIntensity;
            Color emissionColor = FlashColor * flash * 2f;

            var go = targetRig as GameObject;
            if (go == null) return;

            var renderers = go.GetComponentsInChildren<Renderer>();
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

        private static void EndTargetFlash(object targetRig)
        {
            foreach (var kvp in _originalMaterials)
            {
                if (kvp.Key != null) kvp.Key.materials = kvp.Value;
            }
            _originalMaterials.Clear();
        }

        // ========== RESOURCES ==========
        private static void InitializeResources()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;

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
            int length = (int)(sampleRate * 0.1f);
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
            yield return new WaitForSecondsRealtime(0.12f);
            UnityEngine.Object.Destroy(clip);
        }

        private static IEnumerator ShowHitMarkerAt(Vector3 worldPos)
        {
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

    // ========== CONFIG UI (OnGUI) ==========
    public class ConfigUI : MonoBehaviour
    {
        private Rect _windowRect = new Rect(50, 50, 400, 550);
        private Vector2 _scrollPos;
        private int _tab = 0;
        private string[] _tabs = { "General", "Effects", "Visuals", "Advanced", "Debug" };
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private bool _stylesInitialized = false;

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize = 28;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.alignment = TextAnchor.MiddleCenter;
            _titleStyle.normal.textColor = Color.white;

            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = 20;
            _buttonStyle.fixedHeight = 40;

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 20;
            _labelStyle.normal.textColor = Color.white;

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!ImpactFrameMod.ShowConfigUI) return;
            InitStyles();

            _windowRect = GUI.Window(9999, _windowRect, (GUI.WindowFunction)DrawWindow, "");
        }

        private void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0, 0, 400, 40));

            GUILayout.Space(10);
            GUILayout.Label("IMPACT FRAME v2.2", _titleStyle);
            GUILayout.Space(5);
            GUILayout.Label("Quest Config Menu", _labelStyle);
            GUILayout.Space(10);

            // Tabs
            _tab = GUILayout.Toolbar(_tab, _tabs, _buttonStyle);
            GUILayout.Space(10);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            switch (_tab)
            {
                case 0: DrawGeneral(); break;
                case 1: DrawEffects(); break;
                case 2: DrawVisuals(); break;
                case 3: DrawAdvanced(); break;
                case 4: DrawDebug(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.Space(10);

            if (GUILayout.Button("Save & Close", _buttonStyle))
            {
                ImpactFrameMod.ShowConfigUI = false;
            }
        }

        private void DrawGeneral()
        {
            GUILayout.Label("General Settings", _labelStyle);
            GUILayout.Space(5);

            ImpactFrameMod.Enabled = GUILayout.Toggle(ImpactFrameMod.Enabled, "Enabled", _buttonStyle);
            GUILayout.Space(5);

            ImpactFrameMod.ImpactChance = Slider("Impact Chance", ImpactFrameMod.ImpactChance, 0f, 1f);
            ImpactFrameMod.Cooldown = Slider("Cooldown (s)", ImpactFrameMod.Cooldown, 0f, 2f);
            ImpactFrameMod.RequireBoneWeapon = GUILayout.Toggle(ImpactFrameMod.RequireBoneWeapon, "Require Bone Weapon", _buttonStyle);
            ImpactFrameMod.TriggerOnNPCs = GUILayout.Toggle(ImpactFrameMod.TriggerOnNPCs, "Trigger on NPCs", _buttonStyle);
            ImpactFrameMod.TriggerOnPlayers = GUILayout.Toggle(ImpactFrameMod.TriggerOnPlayers, "Trigger on Players", _buttonStyle);
            ImpactFrameMod.MinDamageThreshold = Slider("Min Damage", ImpactFrameMod.MinDamageThreshold, 0f, 50f);
            ImpactFrameMod.Duration = Slider("Duration (s)", ImpactFrameMod.Duration, 0.05f, 1f);
            ImpactFrameMod.TimeScale = Slider("Time Scale", ImpactFrameMod.TimeScale, 0f, 1f);
        }

        private void DrawEffects()
        {
            GUILayout.Label("Active Effects (bitmask)", _labelStyle);
            GUILayout.Space(5);

            int effects = (int)ImpactFrameMod.ActiveEffects;
            bool timeStop = (effects & 1) != 0;
            bool timeSlow = (effects & 2) != 0;
            bool flash = (effects & 4) != 0;
            bool shake = (effects & 8) != 0;
            bool particles = (effects & 16) != 0;
            bool sound = (effects & 32) != 0;
            bool hitMarker = (effects & 64) != 0;

            timeStop = GUILayout.Toggle(timeStop, "Time Stop (1)", _buttonStyle);
            timeSlow = GUILayout.Toggle(timeSlow, "Time Slow (2)", _buttonStyle);
            flash = GUILayout.Toggle(flash, "Screen Flash (4)", _buttonStyle);
            shake = GUILayout.Toggle(shake, "Screen Shake (8)", _buttonStyle);
            particles = GUILayout.Toggle(particles, "Particles (16)", _buttonStyle);
            sound = GUILayout.Toggle(sound, "Sound (32)", _buttonStyle);
            hitMarker = GUILayout.Toggle(hitMarker, "Hit Marker (64)", _buttonStyle);

            int newEffects = 0;
            if (timeStop) newEffects |= 1;
            if (timeSlow) newEffects |= 2;
            if (flash) newEffects |= 4;
            if (shake) newEffects |= 8;
            if (particles) newEffects |= 16;
            if (sound) newEffects |= 32;
            if (hitMarker) newEffects |= 64;
            ImpactFrameMod.ActiveEffects = (ImpactFrameMod.Effects)newEffects;

            GUILayout.Space(10);
            GUILayout.Label($"Current Bitmask: {newEffects}", _labelStyle);
        }

        private void DrawVisuals()
        {
            GUILayout.Label("Flash Settings", _labelStyle);
            GUILayout.Space(5);

            ImpactFrameMod.FlashIntensity = Slider("Flash Intensity", ImpactFrameMod.FlashIntensity, 0f, 3f);
            ImpactFrameMod.FlashTargetOnly = GUILayout.Toggle(ImpactFrameMod.FlashTargetOnly, "Flash Target Only", _buttonStyle);

            ImpactFrameMod.FlashColor = ColorField("Flash Color", ImpactFrameMod.FlashColor);

            GUILayout.Space(10);
            GUILayout.Label("Shake Settings", _labelStyle);
            ImpactFrameMod.ShakeMagnitude = Slider("Shake Magnitude", ImpactFrameMod.ShakeMagnitude, 0f, 0.3f);
            ImpactFrameMod.ShakeRoughness = Slider("Roughness", ImpactFrameMod.ShakeRoughness, 0f, 30f);

            GUILayout.Space(10);
            GUILayout.Label("Hit Marker", _labelStyle);
            ImpactFrameMod.ShowHitMarker = GUILayout.Toggle(ImpactFrameMod.ShowHitMarker, "Show Hit Marker", _buttonStyle);
            ImpactFrameMod.HitMarkerSize = Slider("Size", ImpactFrameMod.HitMarkerSize, 0.05f, 1f);
            ImpactFrameMod.HitMarkerColor = ColorField("Color", ImpactFrameMod.HitMarkerColor);
        }

        private void DrawAdvanced()
        {
            GUILayout.Label("Easing", _labelStyle);
            ImpactFrameMod.EaseInTime = Slider("Ease In", ImpactFrameMod.EaseInTime, 0f, 0.2f);
            ImpactFrameMod.EaseOutTime = Slider("Ease Out", ImpactFrameMod.EaseOutTime, 0f, 0.2f);

            GUILayout.Space(10);
            GUILayout.Label("Combo System", _labelStyle);
            ImpactFrameMod.EnableCombo = GUILayout.Toggle(ImpactFrameMod.EnableCombo, "Enable Combo", _buttonStyle);
            ImpactFrameMod.ShowComboUI = GUILayout.Toggle(ImpactFrameMod.ShowComboUI, "Show Combo UI", _buttonStyle);
            ImpactFrameMod.ComboWindow = Slider("Combo Window", ImpactFrameMod.ComboWindow, 0.5f, 5f);
            ImpactFrameMod.MaxComboCount = (int)Slider("Max Combo", ImpactFrameMod.MaxComboCount, 10, 200);

            GUILayout.Space(10);
            GUILayout.Label("Kill Slow-Mo", _labelStyle);
            ImpactFrameMod.KillSlowMo = GUILayout.Toggle(ImpactFrameMod.KillSlowMo, "Enabled", _buttonStyle);
            ImpactFrameMod.KillSlowMoDuration = Slider("Duration", ImpactFrameMod.KillSlowMoDuration, 0.1f, 2f);
            ImpactFrameMod.KillSlowMoScale = Slider("Time Scale", ImpactFrameMod.KillSlowMoScale, 0f, 1f);

            GUILayout.Space(10);
            GUILayout.Label("Performance (Quest)", _labelStyle);
            ImpactFrameMod.EnableParticles = GUILayout.Toggle(ImpactFrameMod.EnableParticles, "Particles (costly)", _buttonStyle);
            ImpactFrameMod.EnableSound = GUILayout.Toggle(ImpactFrameMod.EnableSound, "Sound", _buttonStyle);
        }

        private void DrawDebug()
        {
            GUILayout.Label("Debug Info", _labelStyle);
            GUILayout.Space(5);

            GUILayout.Label($"Hits: {ImpactFrameMod._hitCount}", _labelStyle);
            GUILayout.Label($"Triggers: {ImpactFrameMod._triggerCount}", _labelStyle);
            GUILayout.Label($"Combo: {ImpactFrameMod.ComboCount}", _labelStyle);
            GUILayout.Label($"TimeScale: {Time.timeScale:F3}", _labelStyle);
            GUILayout.Label($"Camera: {Camera.main != null}", _labelStyle);
            GUILayout.Label($"RigManagerType: {ImpactFrameMod._rigManagerType != null}", _labelStyle);
            GUILayout.Label($"NPCType: {ImpactFrameMod._npcType != null}", _labelStyle);

            GUILayout.Space(10);
            GUILayout.Label("Last Event:", _labelStyle);
            GUILayout.Label(ImpactFrameMod._lastDebugMsg, _labelStyle);

            GUILayout.Space(10);
            if (GUILayout.Button("Test Flash (1s)", _buttonStyle))
            {
                MelonCoroutines.Start(TestFlash());
            }
            if (GUILayout.Button("Test Shake (1s)", _buttonStyle))
            {
                MelonCoroutines.Start(TestShake());
            }
            if (GUILayout.Button("Test Sound", _buttonStyle))
            {
                MelonCoroutines.Start(TestSound());
            }
            if (GUILayout.Button("Reset Stats", _buttonStyle))
            {
                ImpactFrameMod._hitCount = 0;
                ImpactFrameMod._triggerCount = 0;
                ImpactFrameMod.ComboCount = 0;
            }
        }

        private IEnumerator TestFlash()
        {
            if (Camera.main == null) yield break;
            var renderers = Camera.main.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.materials != null)
                {
                    foreach (var mat in r.materials)
                    {
                        if (mat != null)
                        {
                            mat.EnableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", Color.red * 2f);
                        }
                    }
                }
            }
            yield return new WaitForSecondsRealtime(1f);
            foreach (var r in renderers)
            {
                if (r.materials != null)
                {
                    foreach (var mat in r.materials)
                    {
                        if (mat != null) mat.SetColor("_EmissionColor", Color.black);
                    }
                }
            }
        }

        private IEnumerator TestShake()
        {
            if (Camera.main == null) yield break;
            var cam = Camera.main.transform;
            var orig = cam.localPosition;
            float t = 0f;
            while (t < 1f)
            {
                cam.localPosition = orig + new Vector3(
                    (Mathf.PerlinNoise(Time.unscaledTime * 20, 0) - 0.5f) * 0.1f,
                    (Mathf.PerlinNoise(0, Time.unscaledTime * 20) - 0.5f) * 0.1f,
                    0
                );
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            cam.localPosition = orig;
        }

        private IEnumerator TestSound()
        {
            if (Camera.main == null) yield break;
            var audio = Camera.main.GetComponent<AudioSource>() ?? Camera.main.gameObject.AddComponent<AudioSource>();
            audio.spatialBlend = 0f;
            int sampleRate = 44100;
            int len = sampleRate / 10;
            float[] samples = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = Mathf.Sin(2 * Mathf.PI * 800 * t) * Mathf.Exp(-t * 20) * 0.3f;
            }
            var clip = AudioClip.Create("Test", len, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audio.PlayOneShot(clip);
            yield return new WaitForSecondsRealtime(0.15f);
            UnityEngine.Object.Destroy(clip);
        }

        private float Slider(string label, float value, float min, float max)
        {
            GUILayout.Label($"{label}: {value:F2}", _labelStyle);
            return GUILayout.HorizontalSlider(value, min, max);
        }

        private Color ColorField(string label, Color color)
        {
            GUILayout.Label(label, _labelStyle);
            float r = Slider("R", color.r, 0f, 1f);
            float g = Slider("G", color.g, 0f, 1f);
            float b = Slider("B", color.b, 0f, 1f);
            float a = Slider("A", color.a, 0f, 1f);
            return new Color(r, g, b, a);
        }
    }

    // Auto-add ConfigUI to main camera
    [HarmonyPatch(typeof(Camera), "OnPostRender")]
    static class CameraPatch
    {
        static void Postfix(Camera __instance)
        {
            if (__instance == Camera.main && __instance.GetComponent<ConfigUI>() == null)
                __instance.gameObject.AddComponent<ConfigUI>();
        }
    }

    // Input handler for opening config
    public class InputHandler : MonoBehaviour
    {
        private float _gripPressTime = 0f;
        private float _triggerPressTime = 0f;

        private void Update()
        {
            // Check for OVRInput (Quest controllers)
            try
            {
                var ovrInputType = Type.GetType("OVRInput, Assembly-CSharp");
                if (ovrInputType != null)
                {
                    var getMethod = ovrInputType.GetMethod("Get", new[] { typeof(int) });
                    var getDownMethod = ovrInputType.GetMethod("GetDown", new[] { typeof(int) });
                    var getUpMethod = ovrInputType.GetMethod("GetUp", new[] { typeof(int) });

                    // Button indices: 1=Trigger, 2=Grip, 4=Start/Menu
                    const int trigger = 1;
                    const int grip = 2;
                    const int menu = 4;

                    // Check if both grip and trigger held
                    bool gripHeld = (bool)(getMethod?.Invoke(null, new object[] { grip }) ?? false);
                    bool triggerHeld = (bool)(getMethod?.Invoke(null, new object[] { trigger }) ?? false);
                    bool menuPressed = (bool)(getDownMethod?.Invoke(null, new object[] { menu }) ?? false);

                    if (menuPressed)
                    {
                        ImpactFrameMod.ShowConfigUI = !ImpactFrameMod.ShowConfigUI;
                    }
                    else if (gripHeld && triggerHeld)
                    {
                        // Both held - could use for something else
                    }
                }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Camera), "OnPostRender")]
    static class InputPatch
    {
        static void Postfix(Camera __instance)
        {
            if (__instance == Camera.main && __instance.GetComponent<InputHandler>() == null)
                __instance.gameObject.AddComponent<InputHandler>();
        }
    }
}