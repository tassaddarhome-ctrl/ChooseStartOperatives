using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using MGSC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooseStartOperatives
{
    /// <summary>
    /// Модальное окно выбора стартовых наёмников и классов, открывается между
    /// подтверждением сложности и интро. Всё строится кодом из клонов нативных
    /// элементов: кнопки — DifficultyScreen, рамка/затемнение — ConfirmDialogWindow,
    /// жёлтая рамка выбора — SavedSlotPanel.
    /// </summary>
    public class SelectionWindow : MonoBehaviour
    {
        private const string RootName = "ChooseStartOperativesWindow";

        /// <summary>Отладочный дамп иерархии окна в Player.log (для доводки вёрстки).</summary>
        private const bool DumpUi = true;

        private static SelectionWindow _instance;

        private static readonly FieldInfo StartBtnField = GetField(typeof(DifficultyScreen), "_startBtn");
        private static readonly FieldInfo CustomizeBtnField = GetField(typeof(DifficultyScreen), "_customizeBtn");
        private static readonly FieldInfo BackBtnField = GetField(typeof(DifficultyScreen), "_backBtn");
        private static readonly FieldInfo YellowBorderField = GetField(typeof(SavedSlotPanel), "_yellowBorder");

        private MainMenuGameMode _mode;
        private int _slot;
        private DifficultyPreset _preset;
        private Action<MainMenuGameMode, int, DifficultyPreset> _onConfirm;

        private readonly List<string> _mercs = new List<string>();
        private readonly List<string> _classes = new List<string>();
        private bool _tabIsMercs = true;

        private CommonButton _tabMercsButton;
        private CommonButton _tabClassesButton;
        private GameObject _tabMercsBorder;
        private GameObject _tabClassesBorder;
        private Transform _mercsRoot;
        private Transform _classesRoot;
        private LocalizableLabel _counterLabel;
        private CommonButton _startButton;
        private LocalizableLabel _titleLabel;

        private readonly List<Row> _mercRows = new List<Row>();
        private readonly List<Row> _classRows = new List<Row>();

        private float _rowH;
        private float _rowW;
        private float _gap = 3f;

        private class Row
        {
            public string Id;
            public CommonButton Button;
            public GameObject Border;
        }

        /// <summary>
        /// Создаёт окно. Возвращает false, если окно построить не удалось —
        /// тогда игра стартует обычным порядком.
        /// </summary>
        public static bool Show(
            MainMenuGameMode mode,
            int slot,
            DifficultyPreset preset,
            Action<MainMenuGameMode, int, DifficultyPreset> onConfirm)
        {
            if (_instance != null)
            {
                // Повторный клик «Старт» при открытом окне — игнорируем.
                return true;
            }

            DifficultyScreen difficulty = UI.Get<DifficultyScreen>();
            if (difficulty == null)
            {
                Debug.LogWarning("[ChooseStartOperatives] DifficultyScreen не найден — старт без окна.");
                return false;
            }

            CommonButton donorStart = StartBtnField.GetValue(difficulty) as CommonButton;
            CommonButton donorCustomize = CustomizeBtnField.GetValue(difficulty) as CommonButton;
            CommonButton donorBack = BackBtnField.GetValue(difficulty) as CommonButton;
            if (donorStart == null || donorCustomize == null || donorBack == null)
            {
                Debug.LogWarning("[ChooseStartOperatives] Кнопки-доноры не найдены — старт без окна.");
                return false;
            }

            Canvas canvas = difficulty.GetComponentInParent<Canvas>();
            Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
            if (rootCanvas == null)
            {
                Debug.LogWarning("[ChooseStartOperatives] Canvas не найден — старт без окна.");
                return false;
            }

            GameObject root = new GameObject(RootName, typeof(RectTransform));
            SelectionWindow window = root.AddComponent<SelectionWindow>();
            window._mode = mode;
            window._slot = slot;
            window._preset = preset;
            window._onConfirm = onConfirm;
            root.transform.SetParent(rootCanvas.transform, worldPositionStays: false);
            PlaceInHierarchy((RectTransform)root.transform, rootCanvas, difficulty.transform);
            _instance = window;

            try
            {
                window.Build(rootCanvas, donorStart, donorCustomize, donorBack);
            }
            catch (Exception ex)
            {
                Debug.Log("[ChooseStartOperatives] Ошибка построения окна: " + ex);
                CancelWindow();
                return false;
            }
            return true;
        }

        public static void CancelWindow()
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
            }
        }

        private static readonly FieldInfo SimpleTooltipField = GetField(typeof(TooltipFactory), "_simpleTextTooltip");
        private static readonly FieldInfo PropertiesTooltipField = GetField(typeof(TooltipFactory), "_tooltip");

        /// <summary>
        /// Порядок в иерархии канваса: окно — выше всех экранов, но ниже слоя
        /// тултипов (иначе родные тултипы строк рисуются под затемнением окна).
        /// Вставляемся ровно перед верхнеуровневым предком тултипа, если он
        /// стоит позже экрана сложности; иначе — последним ребёнком.
        /// </summary>
        private static void PlaceInHierarchy(RectTransform windowRect, Canvas rootCanvas, Transform anyScreen)
        {
            Transform tooltipLayer = FindTopLevelAncestor(GetTooltipAnchor(), rootCanvas.transform);
            if (tooltipLayer != null)
            {
                Transform screenLayer = FindTopLevelAncestor(anyScreen, rootCanvas.transform);
                if (screenLayer == null || tooltipLayer.GetSiblingIndex() > screenLayer.GetSiblingIndex())
                {
                    windowRect.SetSiblingIndex(tooltipLayer.GetSiblingIndex());
                    Debug.Log("[ChooseStartOperatives] Окно вставлено перед слоем тултипов '"
                        + tooltipLayer.name + "' (sib " + tooltipLayer.GetSiblingIndex() + ").");
                    return;
                }
            }
            windowRect.SetAsLastSibling();
        }

        private static Transform GetTooltipAnchor()
        {
            TooltipFactory factory = SingletonMonoBehaviour<TooltipFactory>.Instance;
            if (factory == null)
            {
                return null;
            }
            Component simple = SimpleTooltipField != null
                ? SimpleTooltipField.GetValue(factory) as Component
                : null;
            if (simple != null)
            {
                return simple.transform;
            }
            Component rich = PropertiesTooltipField != null
                ? PropertiesTooltipField.GetValue(factory) as Component
                : null;
            return rich != null ? rich.transform : null;
        }

        /// <summary>Поднимается от объекта до его верхнеуровневого предка под root (null, если не под root).</summary>
        private static Transform FindTopLevelAncestor(Transform t, Transform root)
        {
            if (t == null)
            {
                return null;
            }
            while (t.parent != null && t.parent != root)
            {
                t = t.parent;
            }
            return t.parent == root ? t : null;
        }

        // ------------------------------------------------------------------
        // Построение UI.
        // ------------------------------------------------------------------

        private void Build(Canvas rootCanvas, CommonButton donorStart, CommonButton donorCustomize, CommonButton donorBack)
        {
            RectTransform canvasRect = (RectTransform)rootCanvas.transform;
            float canvasW = canvasRect.rect.width;
            float canvasH = canvasRect.rect.height;

            // Высота нативной кнопки — базовая единица вёрстки (дизайн-пиксели канваса).
            RectTransform donorRect = (RectTransform)donorStart.transform;
            _rowH = donorRect.rect.height;

            RectTransform rootRect = (RectTransform)transform;
            StretchFull(rootRect);

            // Затемнение фона: цвет — сэмпл с затемнения ConfirmDialogWindow.
            Image dim = NewImage("Dim", rootRect);
            StretchFull((RectTransform)dim.transform);
            dim.color = HarvestDimColor();
            dim.raycastTarget = true;

            // Панель.
            RectTransform panel = NewRect("Panel", rootRect);
            float panelW = Mathf.Round(canvasW * 0.78f);
            float panelH = Mathf.Round(canvasH * 0.92f);
            Center(panel, new Vector2(panelW, panelH));

            Image frame = NewImage("Frame", panel);
            StretchFull((RectTransform)frame.transform);
            Sprite frameSprite = HarvestFrameSprite();
            if (frameSprite != null)
            {
                frame.sprite = frameSprite;
                frame.type = Image.Type.Sliced;
                frame.color = HarvestFrameColor();
            }
            else
            {
                frame.color = new Color32(0x14, 0x16, 0x14, 255);
            }

            float margin = 5f;
            float titleH = _rowH;
            float tabsH = _rowH + 2f;
            float footerH = _rowH + 4f;

            // Заголовок.
            _titleLabel = CloneLabel(donorStart, panel, "Title");
            Place((RectTransform)_titleLabel.transform,
                new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, titleH), new Vector2(margin, -margin));
            _titleLabel.SetRawText(L("Стартовый отряд", "Starting Squad"));
            _titleLabel.Text.alignment = TextAlignmentOptions.MidlineLeft;

            // Счётчик текущего списка.
            _counterLabel = CloneLabel(donorStart, panel, "Counter");
            Place((RectTransform)_counterLabel.transform,
                new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, titleH), new Vector2(-margin, -margin));
            _counterLabel.SetRawText("");
            _counterLabel.Text.alignment = TextAlignmentOptions.MidlineRight;
            _counterLabel.Text.color = Colors.AltGreen;

            // Табы.
            RectTransform tabs = NewRect("Tabs", panel);
            Place(tabs,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, tabsH), new Vector2(margin, -(margin + titleH)));

            float tabW = Mathf.Round((panelW - margin * 2f - _gap) * 0.35f);
            _tabMercsButton = CloneButton(donorCustomize, tabs, "TabMercs");
            _tabMercsBorder = AddSelectionBorder(_tabMercsButton.transform);
            Place((RectTransform)_tabMercsButton.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(tabW, _rowH), Vector2.zero);
            _tabMercsButton.SetRawCaption(L("Наёмники", "Operatives"));
            NormalizeCaption(_tabMercsButton, TextAlignmentOptions.Center, 4f);
            _tabMercsButton.OnClick += delegate { SwitchTab(true); };

            _tabClassesButton = CloneButton(donorCustomize, tabs, "TabClasses");
            _tabClassesBorder = AddSelectionBorder(_tabClassesButton.transform);
            RectTransform classesTabRect = (RectTransform)_tabClassesButton.transform;
            Place(classesTabRect,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(tabW, _rowH), new Vector2(tabW + _gap, 0f));
            _tabClassesButton.SetRawCaption(L("Классы", "Classes"));
            NormalizeCaption(_tabClassesButton, TextAlignmentOptions.Center, 4f);
            _tabClassesButton.OnClick += delegate { SwitchTab(false); };

            // Область списков.
            RectTransform content = NewRect("Content", panel);
            StretchBetween(content,
                margin,
                margin + titleH + tabsH + 2f,
                margin,
                margin + footerH);
            float contentW = content.rect.width;
            const int Columns = 3;
            _rowW = Mathf.Floor((contentW - _gap * (Columns - 1)) / Columns);
            float contentH = content.rect.height;

            _mercsRoot = NewRect("MercList", content).transform;
            StretchFull((RectTransform)_mercsRoot.transform);
            BuildRows(_mercsRoot, StartSelection.MercPool, _mercRows, isClass: false, donor: donorCustomize,
                offsetY: GridOffsetY(contentH, StartSelection.MercPool.Count));

            _classesRoot = NewRect("ClassList", content).transform;
            StretchFull((RectTransform)_classesRoot.transform);
            BuildRows(_classesRoot, StartSelection.ClassPool, _classRows, isClass: true, donor: donorCustomize,
                offsetY: GridOffsetY(contentH, StartSelection.ClassPool.Count));

            // Футер.
            RectTransform footer = NewRect("Footer", panel);
            Place(footer,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.5f),
                new Vector2(0f, footerH), new Vector2(-margin, margin));

            float btnW = Mathf.Round((panelW - margin * 2f - _gap * 3f) * 0.25f);
            CommonButton random = CloneButton(donorCustomize, footer, "RandomBtn");
            PlaceButton(random, btnW, 0);
            random.SetRawCaption(L("Случайно", "Random"));
            random.OnClick += delegate { RollCurrentTab(); };

            CommonButton def = CloneButton(donorCustomize, footer, "DefaultBtn");
            PlaceButton(def, btnW, 1);
            def.SetRawCaption(L("По умолчанию", "Default"));
            def.OnClick += delegate { RestoreCurrentTab(); };

            CommonButton cancel = CloneButton(donorBack, footer, "CancelBtn");
            PlaceButton(cancel, btnW, 2);
            cancel.SetRawCaption(L("Отмена", "Cancel"));
            cancel.OnClick += delegate { CancelWindow(); };

            _startButton = CloneButton(donorStart, footer, "StartBtn");
            PlaceButton(_startButton, btnW, 3);
            _startButton.SetRawCaption(L("Начать", "Start"));
            _startButton.OnClick += delegate { Confirm(); };

            // Предзаполнение — ванильный исход для выбранного пресета.
            _mercs.AddRange(StartSelection.VanillaMercs(_preset));
            _classes.AddRange(StartSelection.VanillaClasses(_preset));
            SwitchTab(true);

            if (DumpUi)
            {
                Dump(transform);
            }
        }

        /// <summary>
        /// Тултипы строк: наёмнику — родной тултип профиля (BuildMercenaryTooltip
        /// умеет работать без живого Mercenary: статы + талант-перк), классу —
        /// простой тултип с перечнем перков линейки.
        /// </summary>
        private static void AttachRowTooltip(GameObject target, string id, bool isClass)
        {
            try
            {
                if (isClass)
                {
                    MercenaryClassRecord record = Data.MercenaryClasses.GetRecord(id);
                    RowTooltip.Attach(target,
                        delegate
                        {
                            SingletonMonoBehaviour<TooltipFactory>.Instance.ShowSimpleTextTooltip(BuildClassTooltipText(record));
                        },
                        delegate
                        {
                            SingletonMonoBehaviour<TooltipFactory>.Instance.HideSimpleTextTooltip();
                        });
                }
                else
                {
                    MercenaryProfileRecord profile = Data.MercenaryProfiles.GetRecord(id);
                    if (profile != null)
                    {
                        RowTooltip.Attach(target,
                            delegate
                            {
                                SingletonMonoBehaviour<TooltipFactory>.Instance.BuildMercenaryTooltip(null, profile);
                            },
                            delegate
                            {
                                SingletonMonoBehaviour<TooltipFactory>.Instance.HideTooltip();
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[ChooseStartOperatives] Не удалось повесить тултип: " + ex.Message);
            }
        }

        private static string BuildClassTooltipText(MercenaryClassRecord record)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Localization.Get("class." + record.Id + ".name").WrapInColor(Colors.AltGreen));
            foreach (string perkId in record.PerkIds)
            {
                sb.AppendLine("- " + Localization.Get("perk." + FormatHelper.ClearPerkGrades(perkId) + ".name"));
            }
            return sb.ToString();
        }

        /// <summary>Смещение сетки вниз, чтобы список стоял по центру области контента.</summary>
        private float GridOffsetY(float contentH, int count)
        {
            const int Columns = 3;
            int rows = (count + Columns - 1) / Columns;
            float gridH = rows * (_rowH + _gap) - _gap;
            return Mathf.Max(0f, (contentH - gridH) * 0.5f);
        }

        private void BuildRows(Transform listRoot, List<string> pool, List<Row> rows, bool isClass, CommonButton donor, float offsetY)
        {
            const int Columns = 3;
            for (int i = 0; i < pool.Count; i++)
            {
                string id = pool[i];
                CommonButton button = CloneButton(donor, listRoot, isClass ? "cls_" + id : "merc_" + id);

                int col = i % Columns;
                int row = i / Columns;
                Place((RectTransform)button.transform,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(_rowW, _rowH),
                    new Vector2(col * (_rowW + _gap), -offsetY - row * (_rowH + _gap)));

                float textLeft = 5f;
                if (isClass)
                {
                    Sprite icon = ClassIcon(id);
                    if (icon != null)
                    {
                        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        iconGo.transform.SetParent(button.transform, false);
                        RectTransform iconRect = (RectTransform)iconGo.transform;
                        float iconSize = _rowH - 4f;
                        Place(iconRect,
                            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(iconSize, iconSize), new Vector2(5f + iconSize * 0.5f, 0f));
                        Image iconImage = iconGo.GetComponent<Image>();
                        iconImage.sprite = icon;
                        iconImage.preserveAspect = true;
                        iconImage.raycastTarget = false;
                        textLeft = 5f + iconSize + 2f;
                    }
                }

                button.SetRawCaption(isClass ? StartSelection.ClassName(id) : StartSelection.MercName(id));
                NormalizeCaption(button, TextAlignmentOptions.MidlineLeft, textLeft);

                GameObject border = AddSelectionBorder(button.transform);

                Row item = new Row { Id = id, Button = button, Border = border };
                rows.Add(item);

                AttachRowTooltip(button.gameObject, id, isClass);

                string captured = id;
                button.OnClick += delegate { Toggle(captured); };
            }
        }

        // ------------------------------------------------------------------
        // Логика выбора.
        // ------------------------------------------------------------------

        private void Toggle(string id)
        {
            bool isMercs = CurrentIsMercs;
            List<string> selected = isMercs ? _mercs : _classes;
            int limit = isMercs ? StartSelection.MercLimit : StartSelection.ClassLimit;

            if (selected.Contains(id))
            {
                selected.Remove(id);
            }
            else if (selected.Count < limit)
            {
                selected.Add(id);
            }
            else
            {
                // Лимит исчерпан — отказ со звуком.
                SingletonMonoBehaviour<SoundController>.Instance.PlayUiSound(
                    SingletonMonoBehaviour<SoundsStorage>.Instance.EmptyAttack);
                return;
            }

            RefreshSelectionVisuals();
        }

        private bool CurrentIsMercs
        {
            get { return _tabIsMercs; }
        }

        private void SwitchTab(bool mercs)
        {
            _tabIsMercs = mercs;
            _mercsRoot.gameObject.SetActive(mercs);
            _classesRoot.gameObject.SetActive(!mercs);
            _tabMercsButton.Select(mercs);
            _tabClassesButton.Select(!mercs);
            if (_tabMercsBorder != null) _tabMercsBorder.SetActive(mercs);
            if (_tabClassesBorder != null) _tabClassesBorder.SetActive(!mercs);
            RefreshSelectionVisuals();
        }

        private void RollCurrentTab()
        {
            if (CurrentIsMercs)
            {
                _mercs.Clear();
                _mercs.AddRange(StartSelection.RollRandom(StartSelection.MercPool, StartSelection.MercLimit));
            }
            else
            {
                _classes.Clear();
                _classes.AddRange(StartSelection.RollRandom(StartSelection.ClassPool, StartSelection.ClassLimit));
            }
            RefreshSelectionVisuals();
        }

        private void RestoreCurrentTab()
        {
            if (CurrentIsMercs)
            {
                _mercs.Clear();
                _mercs.AddRange(StartSelection.VanillaMercs(_preset));
            }
            else
            {
                _classes.Clear();
                _classes.AddRange(StartSelection.VanillaClasses(_preset));
            }
            RefreshSelectionVisuals();
        }

        private void RefreshSelectionVisuals()
        {
            RefreshList(_mercRows, _mercs, StartSelection.MercLimit);
            RefreshList(_classRows, _classes, StartSelection.ClassLimit);

            bool isMercs = CurrentIsMercs;
            int count = isMercs ? _mercs.Count : _classes.Count;
            int limit = isMercs ? StartSelection.MercLimit : StartSelection.ClassLimit;
            _counterLabel.SetRawText(string.Format(
                L("Выбрано: {0} из {1}", "Selected: {0} of {1}"), count, limit));

            // Начать: нужен хотя бы один наёмник и один класс (туториал берёт UnlockedClasses[0]).
            _startButton.SetInteractable(_mercs.Count >= 1 && _classes.Count >= 1);
        }

        private void RefreshList(List<Row> rows, List<string> selected, int limit)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                bool on = selected.Contains(row.Id);
                if (row.Border != null)
                {
                    row.Border.SetActive(on);
                }
                // Подсветка фона выбранной строки (pressed-спрайт) — дублирует рамку.
                row.Button.Select(on);
            }
        }

        private void Confirm()
        {
            StartSelection.Commit(_mercs, _classes);
            Action<MainMenuGameMode, int, DifficultyPreset> confirm = _onConfirm;
            MainMenuGameMode mode = _mode;
            int slot = _slot;
            DifficultyPreset preset = _preset;
            Destroy(gameObject);
            confirm(mode, slot, preset);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelWindow();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ------------------------------------------------------------------
        // Доноры и примитивы UI.
        // ------------------------------------------------------------------

        private static Sprite ClassIcon(string classId)
        {
            MercenaryClassRecord record = Data.MercenaryClasses.GetRecord(classId);
            MercenaryClassDescriptor descriptor = record != null
                ? record.ContentDescriptor as MercenaryClassDescriptor
                : null;
            return descriptor != null ? descriptor.SmallIcon : null;
        }

        /// <summary>
        /// Клон кнопки-донора без чужой функциональности: подписки не копируются
        /// сами, тултип срезаем, а HotkeyButton (кнопки экрана сложности) подменяем
        /// на чистый CommonButton — он тянет GameKeyPanel с иконками хоткеев из
        /// общего пула и подписки на смену режима ввода.
        /// </summary>
        private static CommonButton CloneButton(CommonButton donor, Transform parent, string name)
        {
            GameObject go = UnityEngine.Object.Instantiate(donor.gameObject, parent);
            go.name = name;

            CommonButton button = go.GetComponent<CommonButton>();
            HotkeyButton hotkey = button as HotkeyButton;
            if (hotkey != null)
            {
                FreeKeyPanel(hotkey);
                // DestroyImmediate вызывает OnDisable: HotkeyButton отпишется
                // от событий InputController, Navigable — от навигации.
                UnityEngine.Object.DestroyImmediate(hotkey);
                button = go.AddComponent<CommonButton>();
                CopyButtonFields(hotkey, button);
            }

            // У клона подписи остаётся тег донора: LocalizableLabel.Start на
            // следующем кадре перезапишет текст по этому тегу («Тонкая
            // настройка» и т.п.). Чистим, чтобы жил наш сырой текст.
            MakeRawLabel(button.CaptionLabel);

            HintTooltipHandler tooltip = go.GetComponent<HintTooltipHandler>();
            if (tooltip != null)
            {
                UnityEngine.Object.Destroy(tooltip);
            }
            return button;
        }

        private static readonly FieldInfo LabelTagField = GetField(typeof(LocalizableLabel), "_label");

        /// <summary>Стирает тег локализации у клона подписи — иначе Start() вернёт текст донора.</summary>
        private static void MakeRawLabel(LocalizableLabel label)
        {
            if (label != null && LabelTagField != null)
            {
                LabelTagField.SetValue(label, string.Empty);
            }
        }

        private static readonly FieldInfo GameKeyPanelField = GetField(typeof(HotkeyButton), "_gameKeyPanel");

        /// <summary>Возвращает взятые из пула иконки хоткеев и удаляет панель с клона.</summary>
        private static void FreeKeyPanel(HotkeyButton button)
        {
            try
            {
                GameKeyPanel panel = GameKeyPanelField.GetValue(button) as GameKeyPanel;
                if (panel == null)
                {
                    return;
                }
                typeof(GameKeyPanel).GetMethod("FreePanels",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.Invoke(panel, null);
                UnityEngine.Object.DestroyImmediate(panel.gameObject);
            }
            catch (Exception ex)
            {
                Debug.Log("[ChooseStartOperatives] Не удалось убрать панель хоткеев: " + ex.Message);
            }
        }

        /// <summary>Сериализованные визуальные поля CommonButton, переносимые при подмене компонента.</summary>
        private static readonly string[] CopiedButtonFields =
        {
            "_interactable", "_captionLabel", "captionText", "background",
            "normalBgSprite", "hoverBgSprite", "pressedBgSprite", "disabledBgSprite",
            "normalCaptionColor", "hoverCaptionColor", "pressedCaptionColor", "disabledCaptionColor"
        };

        private static void CopyButtonFields(CommonButton source, CommonButton target)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (int i = 0; i < CopiedButtonFields.Length; i++)
            {
                FieldInfo field = typeof(CommonButton).GetField(CopiedButtonFields[i], flags);
                if (field != null)
                {
                    field.SetValue(target, field.GetValue(source));
                }
            }
        }

        private static LocalizableLabel CloneLabel(CommonButton donorButton, Transform parent, string name)
        {
            LocalizableLabel source = donorButton.CaptionLabel;
            if (source == null)
            {
                return null;
            }
            GameObject go = UnityEngine.Object.Instantiate(source.gameObject, parent);
            go.name = name;
            LocalizableLabel label = go.GetComponent<LocalizableLabel>();
            MakeRawLabel(label);
            return label;
        }

        /// <summary>Жёлтая рамка-выделение — клон рамки слота сохранений.</summary>
        private GameObject AddSelectionBorder(Transform parent)
        {
            Image donor = FindYellowBorderDonor();
            if (donor == null)
            {
                return null;
            }
            GameObject go = UnityEngine.Object.Instantiate(donor.gameObject, parent);
            go.name = "SelBorder";
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-1f, -1f);
            rect.offsetMax = new Vector2(1f, 1f);
            rect.localScale = Vector3.one;
            go.SetActive(false);
            return go;
        }

        private static Image _yellowBorderDonor;

        private static Image FindYellowBorderDonor()
        {
            if (_yellowBorderDonor != null)
            {
                return _yellowBorderDonor;
            }
            try
            {
                ManageSavesScreen saves = UI.Get<ManageSavesScreen>();
                if (saves == null)
                {
                    return null;
                }
                SavedSlotPanel[] panels = saves.GetComponentsInChildren<SavedSlotPanel>(true);
                for (int i = 0; i < panels.Length; i++)
                {
                    Image border = YellowBorderField.GetValue(panels[i]) as Image;
                    if (border != null)
                    {
                        _yellowBorderDonor = border;
                        return border;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[ChooseStartOperatives] Рамка-донор не найдена: " + ex.Message);
            }
            return null;
        }

        /// <summary>Крупный Sliced-спрайт ConfirmDialogWindow — рамка панели.</summary>
        private static Sprite _frameSprite;
        private static Color _frameColor = new Color32(0x14, 0x16, 0x14, 255);
        private static Color _dimColor = new Color32(0, 0, 0, 220);

        private static Sprite HarvestFrameSprite()
        {
            HarvestDialogVisuals();
            return _frameSprite;
        }

        private static Color HarvestFrameColor()
        {
            HarvestDialogVisuals();
            return _frameColor;
        }

        private static Color HarvestDimColor()
        {
            HarvestDialogVisuals();
            return _dimColor;
        }

        private static void HarvestDialogVisuals()
        {
            if (_frameSprite != null)
            {
                return;
            }
            try
            {
                ConfirmDialogWindow dialog = UI.Get<ConfirmDialogWindow>();
                if (dialog == null)
                {
                    return;
                }
                Image[] images = dialog.GetComponentsInChildren<Image>(true);
                Image bestSliced = null;
                float bestArea = 0f;
                Image bestPlain = null;
                float bestPlainArea = 0f;
                for (int i = 0; i < images.Length; i++)
                {
                    Image image = images[i];
                    if (image == null)
                    {
                        continue;
                    }
                    RectTransform rect = image.transform as RectTransform;
                    if (rect == null)
                    {
                        continue;
                    }
                    float area = rect.rect.width * rect.rect.height;
                    if (image.type == Image.Type.Sliced && image.sprite != null && area > bestArea)
                    {
                        bestSliced = image;
                        bestArea = area;
                    }
                    if (image.sprite == null && image.color.a < 1f && area > bestPlainArea)
                    {
                        bestPlain = image;
                        bestPlainArea = area;
                    }
                }
                if (bestSliced != null)
                {
                    _frameSprite = bestSliced.sprite;
                    _frameColor = bestSliced.color;
                    Debug.Log("[ChooseStartOperatives] Рамка окна: спрайт '" + _frameSprite.name
                        + "' цвет #" + ColorUtility.ToHtmlStringRGBA(_frameColor));
                }
                if (bestPlain != null)
                {
                    _dimColor = bestPlain.color;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[ChooseStartOperatives] Не удалось взять спрайты у ConfirmDialogWindow: " + ex.Message);
            }
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        /// <summary>Растянуть внутри родителя с отступами: слева/справа/снизу/сверху.</summary>
        private static void StretchBetween(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 anchoredPosition)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            rect.localScale = Vector3.one;
        }

        private void PlaceButton(CommonButton button, float width, int indexFromLeft)
        {
            Place((RectTransform)button.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(width, _rowH),
                new Vector2(width * 0.5f + indexFromLeft * (width + _gap), 0f));
            NormalizeCaption(button, TextAlignmentOptions.Center, 4f);
        }

        /// <summary>Подпись клонированной кнопки: растянуть с отступом и задать выравнивание.</summary>
        private static void NormalizeCaption(CommonButton button, TextAlignmentOptions alignment, float leftPadding)
        {
            LocalizableLabel label = button.CaptionLabel;
            if (label == null)
            {
                return;
            }
            RectTransform rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(leftPadding, 0f);
            rect.offsetMax = new Vector2(-4f, 0f);
            rect.localScale = Vector3.one;
            label.Text.alignment = alignment;
            label.Text.enableWordWrapping = false;
        }

        private static string L(string ru, string en)
        {
            return Localization.Instance != null
                && Localization.Instance.CurrentLang == Localization.Lang.Russian
                ? ru
                : en;
        }

        private static FieldInfo GetField(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        // ------------------------------------------------------------------
        // Отладочный дамп.
        // ------------------------------------------------------------------

        private static void Dump(Transform root)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[ChooseStartOperatives] ===== WINDOW DUMP =====");
                DumpNode(root, sb, 0, 5);
                Debug.Log(sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.Log("[ChooseStartOperatives] Ошибка дампа: " + ex.Message);
            }
        }

        private static void DumpNode(Transform t, StringBuilder sb, int depth, int maxDepth)
        {
            string pad = new string(' ', depth * 2);
            sb.Append(pad + t.name + " [sib:" + t.GetSiblingIndex() + " act:" + t.gameObject.activeSelf + "]");
            RectTransform rt = t as RectTransform;
            if (rt != null)
            {
                sb.Append(" rect=" + rt.rect.width.ToString("F0") + "x" + rt.rect.height.ToString("F0")
                    + " pos=(" + rt.anchoredPosition.x.ToString("F0") + "," + rt.anchoredPosition.y.ToString("F0") + ")"
                    + " anchor=(" + rt.anchorMin.x.ToString("F2") + "," + rt.anchorMin.y.ToString("F2") + ")-("
                    + rt.anchorMax.x.ToString("F2") + "," + rt.anchorMax.y.ToString("F2") + ")");
            }
            List<string> comps = new List<string>();
            Component[] components = t.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component comp = components[i];
                if (comp == null)
                {
                    comps.Add("<null>");
                    continue;
                }
                string typeName = comp.GetType().Name;
                Image image = comp as Image;
                if (image != null)
                {
                    string sprite = image.sprite != null ? image.sprite.name : "null";
                    comps.Add(typeName + "{" + sprite + " " + image.type + " #" + ColorUtility.ToHtmlStringRGBA(image.color) + "}");
                }
                else
                {
                    comps.Add(typeName);
                }
            }
            sb.AppendLine(" [" + string.Join(", ", comps.ToArray()) + "]");
            if (depth < maxDepth)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    DumpNode(t.GetChild(i), sb, depth + 1, maxDepth);
                }
            }
        }
    }
}
