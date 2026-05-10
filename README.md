# Launch_M&B2_by_Lelbry

GUI-лаунчер для **Mount & Blade II: Bannerlord** с балансными правками.

## Возможности

- **Расширение лимита отряда** — добавляет настраиваемый бонус к лимиту главного отряда (по умолчанию +50, регулируется в Live Tuning).
- **Полный лут с врагов** — повышает шанс дропа экипировки убитых юнитов до 100%.
- **Live Tuning** — менять параметры в работающей игре, без перезапуска. Жмёшь «Применить» в лаунчере → мод подхватывает значение через секунду.
- **Подсказка по текущему лимиту** — лаунчер показывает реальные цифры из игры (бойцов в отряде / лимит, разбивка база+бонус).
- Включение/выключение каждой правки галочкой в GUI.
- Запуск игры напрямую (`Bannerlord.exe`) или через Steam.

## Стек

- Лаунчер: **C# / .NET 6 / Avalonia 11**
- Игровой мод: **C# / .NET Framework 4.7.2 / Harmony 2.3**, ставится в `Modules/LelbryBalanceFixes/`

## Сборка

Требуется .NET 6 SDK и установленные шаблоны Avalonia 11:

```bash
dotnet new --install Avalonia.Templates::11.0.10  # один раз
dotnet build Launch_M-B2_by_Lelbry.sln
```

## Запуск

```bash
dotnet run --project Launcher/Launcher.csproj
```

В окне:
1. Проверь путь до игры (по умолчанию подхватится из стандартной установки Steam).
2. Поставь галочки на нужных правках.
3. Выбери способ запуска.
4. Нажми **«Запустить Bannerlord»**.

Лаунчер создаст/обновит `<game>/Modules/LelbryBalanceFixes/`, запишет туда `enabled.json` со списком включённых правок и стартует игру.

## Live Tuning (тюнинг без перезапуска)

В нижней панели лаунчера — слайдер бонуса к лимиту отряда:
- Кнопки `-100 -50 -20` / `+20 +50 +100` для быстрых корректировок.
- Поле ручного ввода (диапазон −200…+1000).
- Кнопка `Сбросить (=0)` возвращает поведение к ванили (плюс ноль).
- Чекбокс «Полный лут с врагов» — отдельный тумблер.
- Кнопка **«Применить к игре»** записывает `live.json` в папку мода. Если игра запущена — мод подхватит значение через ~1 секунду через `FileSystemWatcher`. Если ещё не запущена — значение применится при следующем запуске.

Под слайдером — подсказка с актуальным состоянием игры: `В игре сейчас: X / Y (база A + бонус B)`. Если игра не запущена — `Игра не запущена`.

## Структура

```
Launcher/                       — Avalonia GUI
Module/LelbryBalanceFixes/      — игровой мод (Harmony-патчи)
  ├── SubModule.xml
  ├── manifest.json             — метаданные правок (источник правды для UI)
  └── src/
      ├── LelbryBalanceFixesSubModule.cs
      ├── BalanceFixesRegistry.cs
      ├── LiveConfig.cs                — hot-reload конфига через FileSystemWatcher
      ├── LiveStatusReporterBehavior.cs — пишет live-status.json для лаунчера
      └── Fixes/
          ├── PartySizeBoostFix.cs
          └── FullLootFix.cs
```

**Файлы обмена между лаунчером и модом** (живут в `<game>/Modules/LelbryBalanceFixes/`):
- `enabled.json` — список включённых правок (лаунчер пишет, мод читает на старте).
- `live.json` — текущие значения параметров (лаунчер пишет, мод читает в realtime).
- `live-status.json` — текущее состояние игры (мод пишет, лаунчер читает в realtime).

## Как добавить новую правку

1. В `Module/LelbryBalanceFixes/src/Fixes/` создать класс, имплементирующий `IBalanceFix` с атрибутом `[BalanceFix(id, title, description)]`.
2. В `manifest.json` добавить запись с тем же `id`.
3. Пересобрать (`dotnet build`).

Лаунчер автоматически подхватит новую правку из манифеста.

## Логи

- Лаунчер: `%APPDATA%/Launch_M-B2_by_Lelbry/launcher.json` (конфиг).
- Мод: `%USERPROFILE%/Documents/Mount and Blade II Bannerlord/Logs/LelbryBalanceFixes.log`.

## Вид версии приложений

v.0.1.0 
<img width="1119" height="778" alt="image" src="https://github.com/user-attachments/assets/23b9f7b0-83de-4f0b-8b82-cb5611050177" />

