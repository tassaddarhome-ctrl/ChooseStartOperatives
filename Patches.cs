using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace ChooseStartOperatives
{
    /// <summary>
    /// Перехват подтверждения сложности: вместо немедленного показа интро
    /// открываем окно выбора стартовых наёмников и классов. По подтверждению
    /// окна вызываем оригинальный метод (интро → GameStarted → ProcessStartGame).
    /// </summary>
    [HarmonyPatch(typeof(MainMenuGameMode), "DifficultyScreenOnStartGame")]
    internal static class MainMenuGameMode_DifficultyScreenOnStartGame_Patch
    {
        private static readonly MethodInfo Original = AccessTools.Method(
            typeof(MainMenuGameMode), "DifficultyScreenOnStartGame");

        /// <summary>true внутри повторного вызова оригинала после подтверждения окна.</summary>
        private static bool _proceed;

        private static bool Prefix(MainMenuGameMode __instance, int slot, DifficultyPreset preset)
        {
            if (_proceed)
            {
                return true;
            }

            if (!StartSelection.Prepare(preset))
            {
                Debug.Log("[ChooseStartOperatives] Пулы пусты (конфиги не загружены?) — старт без выбора.");
                return true;
            }

            Debug.Log("[ChooseStartOperatives] Перехват старта новой игры (slot " + slot
                + ", mercs " + StartSelection.MercLimit + ", classes " + StartSelection.ClassLimit + ").");
            if (!SelectionWindow.Show(__instance, slot, preset, ProceedWithOriginal))
            {
                // Окно не построилось — стартуем обычным порядком.
                return true;
            }
            return false;
        }

        private static void ProceedWithOriginal(MainMenuGameMode instance, int slot, DifficultyPreset preset)
        {
            _proceed = true;
            try
            {
                Original.Invoke(instance, new object[] { slot, preset });
            }
            finally
            {
                _proceed = false;
            }
        }
    }

    /// <summary>
    /// Применение выбора: вместо фиксированных/случайных стартовых списков игры
    /// используем выбранные игроком профили и классы. Тело ванильного метода
    /// воспроизводится с нашими списками (CloneMercenary — публичный, вся
    /// логика генерации ванильная).
    /// </summary>
    [HarmonyPatch(typeof(MercenarySystem), "FillStartMercsAndClasses")]
    internal static class MercenarySystem_FillStartMercsAndClasses_Patch
    {
        private static bool Prefix(
            SpaceTime spaceTime,
            MagnumProjects magnumProjects,
            MagnumProgression magnumProgression,
            Difficulty difficulty,
            Mercenaries mercenaries,
            PerkFactory perkFactory)
        {
            List<string> mercs = new List<string>();
            List<string> classes = new List<string>();
            if (!StartSelection.TryConsume(mercs, classes))
            {
                return true;
            }

            foreach (string profileId in mercs)
            {
                mercenaries.UnlockedMercenaries.Add(profileId);
                MercenarySystem.CloneMercenary(
                    spaceTime, magnumProjects, magnumProgression, mercenaries,
                    profileId, cloneInstant: true, difficulty, perkFactory);
            }
            foreach (string classId in classes)
            {
                mercenaries.UnlockedClasses.Add(classId);
            }

            Debug.Log("[ChooseStartOperatives] Стартовый выбор применён: mercs=["
                + string.Join(", ", mercs.ToArray()) + "], classes=["
                + string.Join(", ", classes.ToArray()) + "].");
            return false;
        }
    }
}
