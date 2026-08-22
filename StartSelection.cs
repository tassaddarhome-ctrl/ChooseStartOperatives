using System;
using System.Collections.Generic;
using System.Linq;
using MGSC;
using UnityEngine;

namespace ChooseStartOperatives
{
    /// <summary>
    /// Состояние выбора наёмников и классов для стартовой новой игры.
    /// Заполняется окном выбора (<see cref="SelectionWindow"/>), применяется
    /// префиксом на MercenarySystem.FillStartMercsAndClasses.
    /// </summary>
    public static class StartSelection
    {
        /// <summary>Все профили наёмников, доступные для выбора (без _boss/_custom).</summary>
        public static List<string> MercPool { get; private set; }

        /// <summary>Все классы, доступные для выбора (без _custom).</summary>
        public static List<string> ClassPool { get; private set; }

        /// <summary>Лимит наёмников из выбранной сложности (StartingMercCount).</summary>
        public static int MercLimit { get; private set; }

        /// <summary>Лимит классов из выбранной сложности (StartingClassesCount).</summary>
        public static int ClassLimit { get; private set; }

        /// <summary>Выбранные профили наёмников (порядок = порядок ростера).</summary>
        public static List<string> SelectedMercs { get; } = new List<string>();

        /// <summary>Выбранные классы.</summary>
        public static List<string> SelectedClasses { get; } = new List<string>();

        /// <summary>Есть отложенный выбор, ждущий применения при старте новой игры.</summary>
        public static bool Pending { get; private set; }

        /// <summary>
        /// Готовит пулы и лимиты под выбранный пресет сложности.
        /// Возвращает false, если конфиги игры ещё не загружены.
        /// </summary>
        public static bool Prepare(DifficultyPreset preset)
        {
            try
            {
                MercPool = Data.MercenaryProfiles.Ids
                    .Where(id => !id.EndsWith("_boss") && !id.EndsWith("_custom"))
                    .ToList();
                ClassPool = Data.MercenaryClasses.Ids
                    .Where(id => !id.EndsWith("_custom"))
                    .ToList();
            }
            catch (Exception)
            {
                return false;
            }

            if (MercPool.Count == 0 || ClassPool.Count == 0)
            {
                return false;
            }

            // Ваниль ограничивает счётчики размером дефолтных списков; кастомный
            // пресет может задать больше — клампим до размера полного пула.
            MercLimit = Mathf.Clamp(Mathf.RoundToInt(preset.StartingMercCount), 1, MercPool.Count);
            ClassLimit = Mathf.Clamp(Mathf.RoundToInt(preset.StartingClassesCount), 1, ClassPool.Count);
            return true;
        }

        /// <summary>Ванильный набор наёмников для пресета (фиксированный или случайный ролл).</summary>
        public static List<string> VanillaMercs(DifficultyPreset preset)
        {
            if (preset.RndMercsAtStart)
            {
                return RollRandom(MercPool, MercLimit);
            }
            return Data.Global.StartMercenaries.Take(MercLimit).ToList();
        }

        /// <summary>Ванильный набор классов для пресета (фиксированный или случайный ролл).</summary>
        public static List<string> VanillaClasses(DifficultyPreset preset)
        {
            if (preset.RndClassesAtStart)
            {
                return RollRandom(ClassPool, ClassLimit);
            }
            return Data.Global.StartClasses.Take(ClassLimit).ToList();
        }

        /// <summary>Случайные count разных элементов пула (свой Фишер–Йетс, порядок случайный).</summary>
        public static List<string> RollRandom(List<string> pool, int count)
        {
            List<string> shuffled = new List<string>(pool);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                string tmp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = tmp;
            }
            return shuffled.Take(Mathf.Min(count, shuffled.Count)).ToList();
        }

        /// <summary>Фиксирует выбор как отложенный к применению при старте новой игры.</summary>
        public static void Commit(List<string> mercs, List<string> classes)
        {
            SelectedMercs.Clear();
            SelectedMercs.AddRange(mercs);
            SelectedClasses.Clear();
            SelectedClasses.AddRange(classes);
            Pending = true;
        }

        /// <summary>Забирает отложенный выбор (однократно).</summary>
        public static bool TryConsume(List<string> mercs, List<string> classes)
        {
            if (!Pending)
            {
                return false;
            }
            Pending = false;
            mercs.AddRange(SelectedMercs);
            classes.AddRange(SelectedClasses);
            return true;
        }

        public static string MercName(string profileId)
        {
            return Localization.Get("spec." + profileId + ".name");
        }

        public static string ClassName(string classId)
        {
            return Localization.Get("class." + classId + ".name");
        }
    }
}
