# ChooseStartOperatives

Мод для [Quasimorph](https://store.steampowered.com/app/2059170/Quasimorph/) — окно выбора
стартовых оперативников и классов при создании новой игры, сразу после подтверждения сложности.

Игра по умолчанию даёт фиксированный набор (6 наёмников / 4 класса, меньше на высоких
сложностях, случайно — на максимальной). Мод открывает окно выбора: любые из всех 17 профилей
наёмников и любых из 14 классов, количество по-прежнему ограничено настройками выбранной
сложности. Интерфейс собран из клонов нативных элементов игры — кнопки, рамки, шрифты и звуки
родные.

## Возможности

- Выбор стартовых оперативников из всех 17 разблокируемых профилей (без `_boss`/`_custom`).
- Выбор стартовых классов из всех 14 классов.
- Лимиты выбора следуют пресету сложности (`StartingMercCount` 6/4/2, `StartingClassesCount`
  4/2/2); кастомные пресеты клампятся до размера пула.
- Предзаполнение — ванильный исход выбранного пресета (фиксированные списки или случайный
  ролл при включённых `RndMercsAtStart`/`RndClassesAtStart`).
- Кнопки «Случайно» и «По умолчанию» у каждого списка, счётчик выбора, «Отмена»/Esc
  возвращает к экрану сложности.
- Тултипы при наведении: родная карточка профиля оперативника (`BuildMercenaryTooltip` в
  режиме без наёмника — статы и талант-перк) и список перков класса.
- Локализация RU/EN (сырые строки по `Localization.Instance.CurrentLang`).

## Сборка

Требуется .NET SDK 8+ и установленная игра.

```bash
dotnet build -c Release          # путь к игре по умолчанию D:\Steam\steamapps\common\Quasimorph
dotnet build -c Release -p:GameDir="C:\Path\To\Quasimorph"
```

Ссылки на `Assembly-CSharp.dll`, `0Harmony.dll` и модули Unity (включая
`Unity.TextMeshPro.dll`, `UnityEngine.InputLegacyModule.dll`) берутся из папки игры
(`Quasimorph_Data\Managed`). Внешних NuGet-зависимостей, кроме reference assemblies, нет.

## Установка

Скопируйте `bin/Release/ChooseStartOperatives.dll` и `modmanifest.json` в

```
%USERPROFILE%\AppData\LocalLow\Magnum Scriptum Ltd\Quasimorph\LocalUserPresets\ChooseStartOperatives\
```

и перезапустите игру. Мод включён по умолчанию (меню Mods внутри игры).

## Steam Workshop

- Элемент Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3788001973 (id `3788001973`).
- Папка загрузки: `publish\` (DLL + манифест + `thumbnail.png`).
- Обновление: пересобрать, скопировать `bin/Release/ChooseStartOperatives.dll` в `publish\`, затем в игровой консоли
  `mod_updateworkshopitem 3788001973 D:\modding\ChooseStartOperatives\publish FALSE` (TRUE — заодно обновить превью).
- Превью генерируется скриптом `D:\modding\make_workshop_preview.py` (квадрат 640×640 в стиле
  игры, фиолетовый треугольник-подпись).

## Как это устроено

- Harmony-префикс на `MainMenuGameMode.DifficultyScreenOnStartGame` (private): вместо
  немедленного показа интро открывается окно выбора; по подтверждению оригинальный метод
  вызывается повторно через кешированный `MethodInfo` (флаг `_proceed` пускает префикс насквозь).
- Harmony-префикс на `MercenarySystem.FillStartMercsAndClasses` (единственная точка создания
  стартового ростера): тело ванильного метода воспроизводится с выбранными списками,
  генерация — через публичный `MercenarySystem.CloneMercenary`.
- Окно строится кодом: клон `CommonButton` с экрана сложности (у `HotkeyButton`-доноров
  компонент подменяется на чистый `CommonButton` с переносом сериализованных полей, панель
  хоткеев освобождается через приватный `GameKeyPanel.FreePanels`), жёлтая рамка выбора —
  клон `_yellowBorder` со слота сохранений, фон/рамка — спрайты `ConfirmDialogWindow`.
- Подписи клонов: `SetRawCaption` чистит тег только у кнопки — у `LocalizableLabel` нужно
  рефлексией стереть `_label`, иначе его `Start()` на следующем кадре вернёт текст донора.
- Слой в иерархии канваса: окно вставляется перед верхнеуровневым предком тултипов
  `TooltipFactory` — выше всех экранов, но ниже тултипов.

Подробный справочник по моддингу Quasimorph — в `D:\modding\QM-MODDING-GUIDE.md`.
