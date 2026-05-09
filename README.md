# Launch_M&B2_by_Lelbry

GUI-лаунчер для **Mount & Blade II: Bannerlord** с балансными правками.

## Возможности (MVP)

- **Расширение лимита отряда (+50)** — увеличивает максимальный размер отряда главного героя на 50.
- **Полный лут с врагов** — повышает шанс дропа экипировки убитых юнитов до 100%.
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

## Структура

```
Launcher/                       — Avalonia GUI
Module/LelbryBalanceFixes/      — игровой мод (Harmony-патчи)
  ├── SubModule.xml
  ├── manifest.json             — метаданные правок (источник правды для UI)
  └── src/
      ├── LelbryBalanceFixesSubModule.cs
      ├── BalanceFixesRegistry.cs
      └── Fixes/
          ├── PartySizeBoostFix.cs
          └── FullLootFix.cs
```

## Как добавить новую правку

1. В `Module/LelbryBalanceFixes/src/Fixes/` создать класс, имплементирующий `IBalanceFix` с атрибутом `[BalanceFix(id, title, description)]`.
2. В `manifest.json` добавить запись с тем же `id`.
3. Пересобрать (`dotnet build`).

Лаунчер автоматически подхватит новую правку из манифеста.

## Логи

- Лаунчер: `%APPDATA%/Launch_M-B2_by_Lelbry/launcher.json` (конфиг).
- Мод: `%USERPROFILE%/Documents/Mount and Blade II Bannerlord/Logs/LelbryBalanceFixes.log`.

## Лицензия

TBD.
