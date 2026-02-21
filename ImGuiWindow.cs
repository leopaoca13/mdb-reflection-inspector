using System;
using GameSDK.ModHost;
using GameSDK.ModHost.ImGui;

// Alias System.Numerics vector types for ImGui (avoids ambiguity with UnityEngine)
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace MyMod
{
    /// <summary>
    /// ImGui window for your mod.
    /// Registered in Mod.OnLoad() and drawn every frame while visible.
    /// </summary>
    public static class ImGuiWindow
    {
        private static int _callbackId;
        private static bool _windowOpen = true;

        /// <summary>Register the ImGui draw callback. Call this from OnLoad().</summary>
        public static void Register(ModLogger logger)
        {
            _callbackId = ImGuiManager.RegisterCallback(
                "MyMod",
                Draw,
                enabled: true);

            logger.Info($"ImGui window registered (callback #{_callbackId})");
        }

        /// <summary>Unregister the ImGui draw callback. Call this from OnUnload().</summary>
        public static void Unregister()
        {
            ImGuiManager.RemoveCallback(_callbackId);
        }

        private static void Draw()
        {
            if (!_windowOpen) return;

            ImGui.SetNextWindowSize(new Vector2(400, 300), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("MyMod", ref _windowOpen))
            {
                ImGui.Text("Hello from MyMod!");
                ImGui.Separator();

                // Add your ImGui widgets here
                if (ImGui.Button("Click Me"))
                {
                    // Handle button click
                }
            }
            ImGui.End();
        }
    }
}
