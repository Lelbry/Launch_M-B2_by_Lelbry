# MODDING_NOTES.md

Заметки по модификации Mount & Blade II: Bannerlord 1.3.15, накопленные при работе над `LelbryBalanceFixes`. Сюда писать всё, что узнаём про API игры, чтобы следующие правки шли сразу по правильным точкам.

> **Цель файла:** при появлении новой идеи правки баланса (например, «больше денег с торговли» или «снизить износ оружия») сразу видеть — что патчить, какие модели существуют, и какие подводные камни уже известны.

---

## Главное правило: модели подменяются по режиму игры

Bannerlord для каждого «компонента игры» (party size, battle rewards, trade, tournament и т.д.) имеет **абстрактную модель** в `TaleWorlds.CampaignSystem.ComponentInterfaces` и **набор реализаций**. Активная реализация регистрируется загруженным модулем:

| Модуль игрока | Какая модель активна |
|---|---|
| Только Native + SandBoxCore + Sandbox | `Default*Model` |
| + StoryMode | `StoryMode*Model` (для классической кампании) |
| + NavalDLC | `NavalDLC*Model` (часто заменяет Default в Sandbox-режиме) |

**Следствие:** патчить одну `DefaultXModel` — значит работать только в одном из режимов. В Sandbox с NavalDLC или в Campaign со StoryMode наша правка молча игнорируется.

### Решение №1 — патчить consumer-side property
Если есть свойство уровня `PartyBase` / `MobileParty`, через которое идут все читатели — патчить его. Пример: `PartyBase.PartySizeLimit` (int) вызывается UI/AI/save-кодом независимо от того, какая модель зарегистрирована.

### Решение №2 — рефлексия по всем подклассам
```csharp
var baseType = typeof(BattleRewardModel);
foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
    Type[] types;
    try { types = asm.GetTypes(); } catch { continue; }
    foreach (var t in types) {
        if (t == null || t.IsAbstract || t.IsInterface) continue;
        if (!baseType.IsAssignableFrom(t)) continue;
        var m = AccessTools.Method(t, "MethodName");
        if (m == null || m.DeclaringType != t) continue;
        harmony.Patch(m, postfix: ...);
    }
}
```
Покрывает Default + StoryMode + NavalDLC + любой будущий мод со своей моделью. **`m.DeclaringType != t` важно** — иначе AccessTools.Method возвращает наследованный метод, и мы патчим один и тот же метод многократно.

### Решение №3 — патчить call site (точку вызова)
Иногда правильная цель — не модель, а сам метод-цикл. Пример: `MapEvent.LootCasualtyCharacter` для лута (не `*BattleRewardModel.*`). Найти такие точки можно только IL-инспекцией.

---

## Известные точки патчинга (по версии 1.3.15)

### Party size

| Цель | Назначение | Заметка |
|---|---|---|
| `PartyBase.PartySizeLimit` (int getter) | **Рекомендую** для боста лимита. Видят все consumer'ы. | Не использует Explainer внутри — независимая ветка. |
| `PartyBase.PartySizeLimitExplainer` (ExplainedNumber getter) | Только для тултипа с разбивкой бонусов. | Не пропагируется в int property. |
| `*PartySizeLimitModel.GetPartyMemberSizeLimit(PartyBase, bool)` | Уровень модели. | Default / StoryMode / NavalDLC — патчить все. |

Реализации модели:
- `TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel`
- `StoryMode.GameComponents.StoryModePartySizeLimitModel`
- `NavalDLC.GameComponents.NavalDLCPartySizeLimitModel`

### Лут с врагов

| Цель | Назначение | Заметка |
|---|---|---|
| `MapEvent.LootCasualtyCharacter(CharacterObject, MapEventParty, MapEventParty, float aiTradePenalty, int maxLootedItemsPerBodyForMainParty, ItemRoster)` | **Главная цель** — параметр `maxLootedItemsPerBodyForMainParty` это per-body cap. Vanilla = 1. | Прямой контроль количества лута. Patch как Prefix с `ref int`. |
| `*BattleRewardModel.GetLootedItemFromTroop(CharacterObject character, float targetValue)` | Per-iteration выбор предмета. | `targetValue` идёт по убыванию в цикле — первая итерация запрашивает дорогое. |
| `*BattleRewardModel.GetExpectedLootedItemValueFromCasualty(Hero, CharacterObject)` | **НЕ цикл лута.** Используется AI для предсказаний (engage decisions). Патчить бесполезно. |  |

Реализации `BattleRewardModel`:
- `TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel`
- `StoryMode.GameComponents.StoryModeBattleRewardModel`
- `NavalDLC.GameComponents.NavalDLCBattleRewardModel`

Все методы `DefaultBattleRewardModel` (для будущих правок):
- `CalculateRenownGain`, `CalculateInfluenceGain`, `CalculateMoraleGainVictory`
- `CalculateGoldLossAfterDefeat`, `CalculatePlunderedGoldAmountFromDefeatedParty`
- `GetLootedItemFromTroop`, `GetExpectedLootedItemValueFromCasualty`
- `GetLootGoldChances`, `GetLootMemberChancesForWinnerParties`
- `GetLootPrisonerChances`, `GetLootItemChancesForWinnerParties`, `GetLootCasualtyChances`
- `GetBannerLootChanceFromDefeatedHero`, `GetBannerRewardForWinningMapEvent`
- Naval-only: `CalculateShipDamageAfterDefeat`, `DistributeDefeatedPartyShipsAmongWinners`, `GetSunkenShipMoraleEffect`, `GetFigureheadLoot`, `PartyLootShipScore`

### MapEvent — методы лута и реконсиляции
- `LootDefeatedPartyGold`, `LootDefeatedPartyMembers`, `LootDefeatedPartyPrisoners`
- `LootDefeatedPartyItems`, `LootDefeatedPartyCasualties`, `LootDefeatedPartyShips`
- `GetMemberRosterReceivingLootShare`, `GetPrisonerRosterReceivingLootShare`, `GetItemRosterReceivingLootShare`
- `ItemRosterForPlayerLootShare`
- `FindWinnerPartyToGetCurrentLootObjectBasedOnChances`

---

## Harmony подводные камни

### 1. Параметры биндятся по имени
Имя параметра в нашем Prefix/Postfix должно **точно** совпадать с именем в IL игры:

```csharp
// ✗ молча не сработает
public static void Prefix(ref float lootAmount) { ... }

// ✓ — реальное имя в Bannerlord 1.3.15
public static void Prefix(ref float targetValue) { ... }
```

Если имя не совпадёт — Harmony в Apply() кидает `Parameter "X" not found in method...`. **Всегда логировать ошибки в `IBalanceFix.Apply` и проверять log после первого запуска.**

Узнать настоящие имена — через `System.Reflection.Metadata.PEReader` (см. `tools/inspect/`) или ILSpy.

### 2. Patch property getter — через `AccessTools.PropertyGetter`
```csharp
// для int / ExplainedNumber-property:
var target = AccessTools.PropertyGetter(typeof(PartyBase), nameof(PartyBase.PartySizeLimit));
```

### 3. Один метод — один Postfix на иерархию
`AccessTools.Method(t, "X")` возвращает наследованный метод, если тип не override'ит. Чтобы избежать двойных применений, проверять `m.DeclaringType == t`.

### 4. ThrownExceptions ReactiveCommand
В лаунчере: без `cmd.ThrownExceptions.Subscribe(...)` любое необработанное исключение в команде валит UI-процесс. Всегда подписываться.

### 5. Avalonia CommandParameter — string
В XAML `CommandParameter="50"` приходит как **строка**. Команда должна принимать `string` и парсить (см. `LiveTuningViewModel.AdjustPartySizeCommand`). `ReactiveCommand<int, Unit>` упадёт с InvalidCastException.

---

## Module loading & lifecycle

### Порядок вызовов MBSubModuleBase
1. `OnSubModuleLoad()` — мод загружен, игра ещё в процессе старта. Здесь стартуем `LiveConfig` (FileSystemWatcher) и применяем Harmony patches.
2. `OnSubModuleActivated()` — модуль активирован.
3. `OnBeforeInitialModuleScreenSetAsRoot()` — главное меню готово.
4. `OnGameStart(Game, IGameStarter)` — игрок начал/загрузил игру. **Здесь регистрировать `CampaignBehaviorBase` через `(starter as CampaignGameStarter).AddBehavior(...)`.**
5. `OnGameInitializationFinished(Game)` — мир инициализирован.

### CampaignBehaviorBase events
В `RegisterEvents()` подписываемся на:
- `CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, handler)` — кампания загружена.
- `CampaignEvents.OnPartySizeChangedEvent` — изменения отряда (рекрут, потери).
- `CampaignEvents.QuarterHourlyTickEvent` / `HourlyTickEvent` — периодика (каждые 15/60 in-game минут ≈ 30 секунд / минуты real time на скорости 1).

### ⚠ Не блокировать game thread в RegisterEvents и OnSessionLaunched
Эти точки вызываются **во время load-screen**. Если внутри них что-то делает синхронный тяжёлый I/O или дёргает несовершенно проинициализированные геттеры (типа `MainParty.PartySizeLimit` через нашу же модель) — экран загрузки виснет навечно.

**Pattern:** event handler обновляет in-memory `volatile` snapshot из примитивов; отдельный `System.Threading.Timer` флашит snapshot в файл с background thread. См. `LiveStatusReporterBehavior.cs`.

---

## Module mismatch на старых сейвах

Когда игрок добавляет новый модуль к сейву, который раньше его не содержал, Bannerlord показывает диалог «Несовпадение модулей». Если согласиться — игра пытается реконсилировать сейв с новым набором модулей. Для **больших** сейвов это либо очень долго, либо вешает экран загрузки навсегда.

**Это не баг нашего мода.** Это поведение Bannerlord — повторяется и с пустым модулем без патчей, лишь бы он попал в список модулей сейва.

**Решение для пользователя:**
- Использовать кнопку «Запустить без мода (старые сейвы)» в лаунчере для старых кампаний.
- Включать наш мод только при старте новых кампаний.

В коде: `GameLauncher.LaunchWithoutOurMod` исключает `LelbryBalanceFixes` из списка модулей и снимает галочку в `LauncherData.xml`.

---

## Module list — правильный список

### Каноничные ID модулей в Bannerlord 1.3.15
**Регистр имеет значение.** Brutal pitfall: имя папки и `<Id>` могут отличаться:

| Папка | `<Id>` в SubModule.xml | Display Name |
|---|---|---|
| `Native` | `Native` | Native |
| `SandBoxCore` | `SandBoxCore` | SandBox Core |
| **`SandBox`** | **`Sandbox`** | Sandbox |
| `StoryMode` | `StoryMode` | StoryMode |
| `BirthAndDeath` | `BirthAndDeath` | Birth and Aging Options |
| `CustomBattle` | `CustomBattle` | CustomBattle |
| `FastMode` | `FastMode` | Fast Mode |
| `NavalDLC` | `NavalDLC` | War Sails |
| `Multiplayer` | `Multiplayer` | (multiplayer-only) |

**Только `SandBox` папка → `Sandbox` ID** ломает интуицию. Если в команде запуска передать `Sandbox` (lowercase) — игра поймёт. Если передать `SandBox` — модуль молча пропустится, и зависимые от него моды (включая наш) не загрузятся.

### Источник правды для списка модулей
`%USERPROFILE%/Documents/Mount and Blade II Bannerlord/Configs/LauncherData.xml` — XML с тем, что игрок включил в TaleWorlds Launcher. Лаунчер обязан читать этот файл и передавать тот же список (плюс наш мод в конец) — иначе ломаются сейвы, использующие моды, которые мы не учли (BirthAndDeath, NavalDLC и т.п.).

См. `Launcher/Services/LauncherDataReader.cs`.

---

## Live config (hot-reload без перезапуска)

### Архитектура

```
┌──────────────┐  пишет live.json    ┌────────────────┐  читает (FSW)  ┌──────────┐
│   Лаунчер    │ ──────────────────> │ <game>/Modules │ ─────────────> │   Мод    │
│  (Apply btn) │                     │ /Lelbry.../    │                │ LiveCfg  │
└──────────────┘                     └────────────────┘                └──────────┘
       ▲                                     │
       │ читает (FSW)                        │ пишет live-status.json
       │                                     ▼
   ┌───────────────────┐                ┌──────────────────────┐
   │ live-status.json  │ <───────────── │ LiveStatusReporter   │
   └───────────────────┘  Timer 3s      │ (CampaignBehavior)    │
                                        └──────────────────────┘
```

### Правила
- **Атомарная запись:** `File.WriteAllText(path + ".tmp", ...)` → `File.Move(tmp, path, overwrite: true)`. Иначе FSW поймает половину файла и парсер упадёт.
- **Debounce:** на FSW-ивенты ставить таймер ~80–100ms — несколько событий на одну запись.
- **Volatile поля:** `LiveConfig` хранит значения в `volatile int` / `volatile bool` для безопасного чтения с любого потока (Harmony Postfix может бежать в любом контексте).
- **Min-clamp:** для отрицательных бонусов следить, чтобы итоговый расчёт не уехал в недопустимые значения (см. `PartySizeBoostFix.MinTotal = 5`).

---

## Инструменты разведки

### `tools/inspect/` (локально в `/tmp/inspect`)
Console-app на System.Reflection.Metadata.PEReader. Запускается как:
```bash
dotnet run --no-build -- <path-to-dll> [TypeNameContains [MethodNameContains]]
```
Пример:
```bash
dotnet run --no-build -- "...TaleWorlds.CampaignSystem.dll" "PartySize" "GetPartyMember"
```
Печатает сигнатуры методов с **именами параметров** (важно для Harmony bind by name).

### Поиск всех реализаций абстрактного класса
```bash
for mod in Native SandBoxCore Sandbox StoryMode BirthAndDeath FastMode NavalDLC; do
  for path in "$GAME/Modules/$mod/bin/Win64_Shipping_Client"/*.dll; do
    [ -f "$path" ] || continue
    dotnet run --no-build -- "$path" "BaseTypeName" 2>&1 | grep "^TYPE.*$KeywordName" | head -3
  done
done
```

---

## Идеи для следующих правок (cherry-pick)

Кандидаты, которые упоминал пользователь или которые часто просят:

### Уже реализовано
- ✅ Party size limit (`party_size_boost`)
- ✅ Loot quantity per body (`full_loot`)

### В очереди (нужно изучить точки)
- 🔲 **Опыт за бой** — `CombatXpModel`, `*Model.GetXpFromHit`. Глянуть `DefaultCombatXpModel` в CampaignSystem.
- 🔲 **Влияние клана / renown** — `BattleRewardModel.CalculateRenownGain`, `BattleRewardModel.CalculateInfluenceGain`. Уже знаем модели.
- 🔲 **Цены в торговле** — `MarketBalancingModel`, `TradeRumorsModel`. Не изучал.
- 🔲 **Награды турниров** — `TournamentModel.GetTournamentPrize`. Не изучал.
- 🔲 **Зарплаты войск** — `PartyWageModel.GetTotalWage`. Не изучал.
- 🔲 **Доход поселений** — `SettlementEconomyModel.*`. Не изучал.
- 🔲 **Лимит рекрутов в деревнях** — `VolunteerModel.GetDailyVolunteerProductionProbability`. Не изучал.
- 🔲 **Скорость путешествия** — `PartySpeedCalculatingModel`. Не изучал.

Для каждой — повторить алгоритм:
1. Найти абстрактную модель в `TaleWorlds.CampaignSystem.ComponentInterfaces`.
2. Через рефлексию найти **все** наследники (Default + StoryMode + NavalDLC + ?).
3. Понять, есть ли consumer-side property (как `PartyBase.PartySizeLimit`) или call-site метод (как `MapEvent.LootCasualtyCharacter`) — патчить там, если можно.
4. Иначе — патчить все наследники модели.
5. Добавить параметр в `LiveConfig` + `LiveTuningConfig` + UI в Live Tuning панели.

---

## Чек-лист «как добавить новый IBalanceFix»

1. **Разведка** — найти точку патчинга через `tools/inspect`. Записать в этот файл секцию с найденными классами/методами.
2. **Параметр** — если правка регулируемая, добавить поле в:
   - `Module/.../src/LiveConfig.cs` (volatile field + accessor + parsing в Reload)
   - `Launcher/Models/LiveTuningConfig.cs` (POCO field)
   - `Launcher/ViewModels/LiveTuningViewModel.cs` (property с RaiseAndSetIfChanged + IsDirty)
   - `Launcher/Views/MainWindow.axaml` (UI блок)
3. **Класс правки** — `Module/.../src/Fixes/MyFix.cs`:
   - `[BalanceFix(id, title, description)]`
   - `IBalanceFix.Apply(Harmony)` — рефлексией найти все целевые методы, попатчить.
   - Логировать `ModLog.Info("MyFix: patched X")` на каждое успешное применение.
   - Использовать try/catch вокруг тела Postfix/Prefix — никогда не давать исключению уйти в game thread.
4. **manifest.json** — добавить запись с тем же id (для UI лаунчера).
5. **Сборка + smoke test** — `dotnet build`, запустить лаунчер, проверить лог: видны ли строки `Applied fix 'my_fix'` и `MyFix: patched ...` без ERROR-строк.
