using DecaGames.RotMG.Objects.Map.Data;
using DG.Tweening.Core.Easing;
using GameSDK;
using GameSDK.ModHost;
using GameSDK.ModHost.Patching;
using Global;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace reflection_inspector
{
    /// <summary>
    /// Main mod entry point.
    /// </summary>
    [Mod("paocxss.reflection_inspector", "reflection_inspector", "1.0.0", Author = "paocxss", Description = "Reflection Inspector for mods (no ESP)")]
    public class Mod : ModBase
    {
        

        //defined classes


        //player objects
        GameObject playerObj = null;
        GameObject playerParent = null;
        FKALGHJIADI playerClass = null;
        ViewHandler playerVH = null;
        LKHPPBEGNOM realPlayerCLass = null;
        Transform playerTransform = null;
        KJMONHENJEN playerClassPositions = null;


        private static ViewHandler cachePlayerVH;



        bool foundPlayer = false;
        int logThrottle = 0;
        // Keep delegates/handles alive so they aren't GC'd
        private static ModLogger s_logger;

        /// <summary>Called once when the mod is loaded.</summary>
        public override void OnLoad()
        {
            Logger.Info("MyMod loaded!");
            ImGuiWindow.Register(Logger);

            // store logger for static detours
            s_logger = Logger;

            // Cole temporariamente no OnLoad() para listar todos os métodos disponíveis
            foreach (var m in typeof(ImGui).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
                Logger.Info($"ImGui.{m.Name}({string.Join(", ", System.Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name + " " + p.Name))})");

            // hookProjectileMethod();
        }

        /// <summary>Called every frame.</summary>
        public override void OnUpdate()
        {

            if (realPlayerCLass == null)
            {
                logThrottle++;
                bool shouldLog = (logThrottle % 300 == 1); // Log every 300 frames to avoid spamming logs, but still get updates if player appears later
                //bool shouldLog = false;

                playerParent = GameObject.Find("Player");
                if (playerParent == null || !playerParent.IsValid)
                {
                    if (shouldLog) Logger.Warning("Player parent not found");
                    return;
                }

                var transform = playerParent.transform;
                if (transform == null || !transform.IsValid)
                {
                    if (shouldLog) Logger.Warning("Player parent transform is null");
                    return;
                }

                var childTransform = transform.GetChild(1); // Child 1 should always be the player obj
                if (childTransform == null || !childTransform.IsValid)
                {
                    if (shouldLog) Logger.Warning("GetChild(1) returned null");
                    return;
                }

                playerTransform = childTransform;


                playerObj = childTransform.gameObject;
                if (playerObj == null || !playerObj.IsValid)
                {
                    if (shouldLog) Logger.Warning("Child gameObject is null");
                    return;
                }

                

                playerVH = playerObj.GetComponent<ViewHandler>();
                if (playerVH == null || !playerVH.IsValid)
                {
                    if (shouldLog) Logger.Warning("ViewHandler not found on child");
                    return;
                }
                cachePlayerVH = playerVH;

                var entity = playerVH.destroyEntity;
                if (entity == null || !entity.IsValid)
                {
                    if (shouldLog) Logger.Warning("destroyEntity is null");
                    return;
                }

                playerClass = entity.Cast<FKALGHJIADI>();
                if (playerClass == null || !playerClass.IsValid)
                {
                    if (shouldLog) Logger.Warning("Cast to FKALGHJIADI failed");
                    return;
                }

                realPlayerCLass = playerClass.Cast<LKHPPBEGNOM>();
                if (realPlayerCLass == null || !realPlayerCLass.IsValid)
                {
                    if (shouldLog) Logger.Warning("Cast to LKHPPBEGNOM failed");
                    return;
                }


                playerClassPositions = playerClass.Cast<KJMONHENJEN>();
                if (playerClassPositions == null || !playerClassPositions.IsValid)
                {
                    if (shouldLog) Logger.Warning("Cast to KJMONHENJEN failed");
                    return;
                }

                ReflectionInvoker.RegisterInstance("playerClass", playerClass);
                ReflectionInvoker.RegisterInstance("realPlayerClass", realPlayerCLass);
                ReflectionInvoker.RegisterInstance("playerPositions", playerClassPositions);

                Logger.Info("Player class found!");
                int health = GetIntFromMember(realPlayerCLass, "ABCPKBGJPEP");
                string name = GetStringFromMember(realPlayerCLass, "DPGEBOCBKEF");
                FoundPlayer(health, name);
            }
            if (foundPlayer && playerObj != null && playerObj.IsValid)
            {
                /*
                try
                {
                    // Pega o campo DGNPJNFGFPE via reflection
                    //var type = playerClassPositions.GetType();

                    var objProps = GetIL2CppMember<ObjectProperties>(realPlayerCLass, "KKENJFFDMPO");
                    float radius = 0.45f * objProps.collisionRadiusMultiplier;

                    Vector3 screenPos = ESP.WorldToScreenPoint(cachePlayerVH.transform.position);
                    SpriteRenderer MOMHMNDNAGE = GetSpriteRenderer(realPlayerCLass, "MOMHMNDNAGE");

                    Vector3 center = playerTransform.position;
                    center.y += radius; // Ajusta o centro para ficar na cabeça do jogador, considerando o raio de colisão

                    Vector3 topLeft = ESP.WorldToScreenPoint(new Vector3 { x = center.x - radius, y = center.y + radius, z = center.z });
                    Vector3 topRight = ESP.WorldToScreenPoint(new Vector3 { x = center.x + radius, y = center.y + radius, z = center.z });
                    Vector3 bottomLeft = ESP.WorldToScreenPoint(new Vector3 { x = center.x - radius, y = center.y - radius, z = center.z });
                    Vector3 bottomRight = ESP.WorldToScreenPoint(new Vector3 { x = center.x + radius, y = center.y - radius, z = center.z });

                    G_Fields.boxTL = new System.Numerics.Vector2(topLeft.x, topLeft.y);
                    G_Fields.boxTR = new System.Numerics.Vector2(topRight.x, topRight.y);
                    G_Fields.boxBL = new System.Numerics.Vector2(bottomLeft.x, bottomLeft.y);
                    G_Fields.boxBR = new System.Numerics.Vector2(bottomRight.x, bottomRight.y);



                    //Logger.Info($"Player screen X position: {screenPos.x}"); // Loga a posição na tela
                    //Logger.Info($"Player screen Y position: {screenPos.y}"); // Loga a posição no mundo


                    if (ESP.IsOnScreen(screenPos))
                    {
                        G_Fields.n_screen_position = screenPos;
                        G_Fields.n_visible = true;
                    }
                    else
                    {
                        G_Fields.n_visible = false;
                    }

                    ImGui.DrawCircle(new System.Numerics.Vector2(screenPos.x, screenPos.y), 10, ImGui.ColorToU32(new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f))); // Desenha um círculo vermelho na posição do jogador

                }
                catch (Exception e)
                {
                    Logger.Warning($"Erro ao pegar posição: {e.Message}");
                }*/
            }
        }


      
        

        /*
        [Patch("", "HBEAKBIHANL")]
        [PatchMethod("CGCMCPAMNPK", 1)]
        public static class TakeDamagePatch
        {
            [Prefix]
            public static bool Prefix(IntPtr __instance, ref DecaGames.RotMG.Objects.Map.Data.ViewHandler BIEALKBCLIP)
            {
                
                //s_logger.Info($"HHCCBONIIOM: {HHCCBONIIOM}, KLHOFENGJNM: {KLHOFENGJNM}, FFFFKPDHEFP: {FFFFKPDHEFP}, GHEBEMMJLDJ: {GHEBEMMJLDJ}, CFJBHEKKLNF: {CFJBHEKKLNF}, JCADLABDPIO: {JCADLABDPIO}, AFCNMCJIKFD: {AFCNMCJIKFD}, KDAJOMOFMJB: {KDAJOMOFMJB}, KCHJBMCNIIA: {KCHJBMCNIIA}, PBGHBKMHACI: {PBGHBKMHACI}");
                //s_logger.Info($"TakeDamagePatch Prefix - instance: 0x{__instance.ToInt64():X}, healthComp: {JGMBPFJEGAH}, damage: {KPKIICOBBIM}, armorPiercing: {LJNABPCBPHM}, hitType: {OEPKCIMBKKD}, time: {CPHPBDFBPFP}");
                s_logger.Info($"TakeDamagePatch Prefix - instance: 0x{__instance.ToInt64():X}, ViewHandler: {BIEALKBCLIP}");

                if (BIEALKBCLIP == cachePlayerVH)
                {
                    s_logger.Info("Player mentioned!");
                }
                return false;
            }
        }*/

        public void FoundPlayer(int health, string playerName)
        {
            if (foundPlayer) return;
            foundPlayer = true;
            G_Fields.n_health = health;
            G_Fields.n_name = playerName;



            Logger.Info($"Player health: {health}");
        }



        private T GetIL2CppMember<T>(object obj, string memberName) where T : class
        {
            if (obj == null) return null;
            var type = obj.GetType();

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var val = field.GetValue(obj);
                if (val is T t) return t;

                if (val != null)
                {
                    var ptrProp = val.GetType().GetProperty("Pointer", BindingFlags.Instance | BindingFlags.Public);
                    if (ptrProp != null)
                    {
                        IntPtr ptr = (IntPtr)ptrProp.GetValue(val);
                        if (ptr != IntPtr.Zero)
                            return (T)Activator.CreateInstance(typeof(T), ptr);
                    }
                }
            }

            var prop = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
            {
                var val = prop.GetValue(obj);
                if (val is T t) return t;
            }

            Logger.Warning($"Could not access '{memberName}' on {type.FullName}");
            return null;
        }

        private SpriteRenderer GetSpriteRenderer(object obj, string memberName)
        {
            if (obj == null) return null;
            var type = obj.GetType();

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var val = field.GetValue(obj);
                if (val is SpriteRenderer sr) return sr;

                // Fallback: tenta cast via ponteiro IL2Cpp
                if (val != null)
                {
                    var ptrProp = val.GetType().GetProperty("Pointer", BindingFlags.Instance | BindingFlags.Public);
                    if (ptrProp != null)
                    {
                        IntPtr ptr = (IntPtr)ptrProp.GetValue(val);
                        return ptr != IntPtr.Zero ? new SpriteRenderer(ptr) : null;
                    }
                }
            }

            var prop = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
            {
                var val = prop.GetValue(obj);
                if (val is SpriteRenderer sr) return sr;
            }

            Logger.Warning($"Could not access SpriteRenderer '{memberName}' on {type.FullName}");
            return null;
        }

        private int GetIntFromMember(object obj, string memberName)
        {
            if (obj == null) return 0;
            var type = obj.GetType();

            var prop = type.GetProperty(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (prop != null)
            {
                var val = prop.GetValue(obj);
                if (val is int i) return i;
                if (val is int?) return ((int?)val) ?? 0;
            }

            var field = type.GetField(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                var val = field.GetValue(obj);
                if (val is int i) return i;
                if (val is int?) return ((int?)val) ?? 0;
            }

            Logger.Warning($"Could not access int member '{memberName}' on type {type.FullName}");
            return 0;
        }


        private string GetStringFromMember(object obj, string memberName)
        {
            if (obj == null) return null;
            var type = obj.GetType();

            var prop = type.GetProperty(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (prop != null)
            {
                var val = prop.GetValue(obj);
                if (val is string s) return s;
                return val?.ToString();
            }

            var field = type.GetField(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                var val = field.GetValue(obj);
                if (val is string s) return s;
                return val?.ToString();
            }

            Logger.Warning($"Could not access string member '{memberName}' on type {type.FullName}");
            return null;
        }

     

        

        // unmanaged delegate signatures for hooking
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr DetourStaticDelegate(IntPtr p0);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr DetourInstanceDelegate(IntPtr instancePtr, IntPtr p0);
    }
}