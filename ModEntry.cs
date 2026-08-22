using System;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace ChooseStartOperatives
{
    /// <summary>
    /// Мод «Choose Start Operatives»: окно выбора стартовых наёмников и классов
    /// при создании новой игры. Количество выбираемого ограничено настройками
    /// выбранной сложности.
    /// </summary>
    public static class ModEntry
    {
        public const string ModName = "ChooseStartOperatives";

        private static bool _harmonyApplied;

        [Hook(ModHookType.AfterBootstrap)]
        public static void OnAfterBootstrap(IModContext context)
        {
            try
            {
                ApplyHarmonyPatches();
                Debug.Log("[" + ModName + "] Загружен: перехват старта новой игры активен.");
            }
            catch (Exception ex)
            {
                Debug.Log("[" + ModName + "] Ошибка инициализации: " + ex);
            }
        }

        private static void ApplyHarmonyPatches()
        {
            if (_harmonyApplied)
            {
                return;
            }
            _harmonyApplied = true;
            new Harmony(ModName).PatchAll(typeof(ModEntry).Assembly);
            Debug.Log("[" + ModName + "] Harmony-патчи применены.");
        }
    }
}
