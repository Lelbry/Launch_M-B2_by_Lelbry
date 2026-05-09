# Lelbry M&B2 Bannerlord Balance Launcher

## Что это за проект

GUI-лаунчер для **Mount & Blade II: Bannerlord**, который правит "несправедливости" баланса игры через собственный игровой мод. Лаунчер позволяет включать/выключать конкретные правки галочками и запускает игру с активным модом.

Автор: **Lelbry** (GitHub).

## Стек

- **Лаунчер:** C# / .NET 8 / Avalonia (кросс-платформенный XAML-GUI)
- **Игровой мод:** C# class library, использующая `TaleWorlds.*.dll` API + Harmony для патчинга
- **Деплой мода:** лаунчер собирает мод и копирует в `Modules/LelbryBalanceFixes/` рядом с игрой

## Структура репозитория

```
Launch_M&B2_by_Lelbry/
├── CLAUDE.md                  # этот файл
├── README.md
├── .gitignore
├── Launcher/                  # Avalonia GUI (.csproj)
│   └── ...
└── Module/                    # игровой мод
    └── LelbryBalanceFixes/
        ├── SubModule.xml
        ├── LelbryBalanceFixes.csproj
        └── src/
```

## Где находится игра

Игра установлена по пути:
```
E:\Top Programs\Steam\steamapps\common\Mount & Blade II Bannerlord\
```

Эта папка добавлена как дополнительная рабочая директория в сессии — её содержимое можно читать (DLL для референса, Native XML для примеров), но **не модифицировать напрямую**. Все правки идут через отдельный модуль в `Modules/LelbryBalanceFixes/`.

Ключевые подпапки игры:
- `bin/Win64_Shipping_Client/` — игровые DLL (`TaleWorlds.CampaignSystem.dll`, `TaleWorlds.Core.dll` и др.) — нужны как ссылки при сборке мода
- `Modules/Native/ModuleData/` — XML с базовым балансом (примеры)
- `Modules/SandBox/`, `Modules/SandBoxCore/`, `Modules/StoryMode/` — основные кампанийные модули

Версия игры (`package_info.txt`): Compile Changeset 110062, env PC@v1.3.4.

## Запланированные правки баланса

1. **Полный лут с врагов после битвы** — сейчас падает только небольшая часть экипировки. Нужно патчить логику `LootCollector` / `MapEventSide` так, чтобы трофеи включали всё снаряжение убитых юнитов.
2. **+50 к лимиту отряда по кнопке** — расширение party size cap. Реализуется патчем `PartyBase.PartySizeLimit` или через `MobilePartyExtensions`.
3. *(будут добавляться)*

Каждая правка — отдельный `IPatch` / Harmony-патч с собственным toggle, читаемым из конфига лаунчера (`LelbryBalanceFixes/config.json` рядом с модом).

## Договорённости (как работаем)

- **Язык общения:** русский.
- **Деструктивные действия** (force-push, удаление веток, перезапись чужих файлов в папке игры) — только с явного подтверждения.
- **Файлы игры в `E:\...\Bannerlord\`** редактировать нельзя кроме папки `Modules/LelbryBalanceFixes/` после её создания.
- **Коммиты** делаем только когда пользователь явно об этом просит.
- **В сообщениях коммитов и PR не указывать Claude** — никаких `Co-Authored-By: Claude`, ссылок на Claude Code, упоминаний помощника. Автор коммитов — только Lelbry.
- **GitHub репо:** публичный, владелец `Lelbry`.

## Полезные ссылки и заметки

- Bannerlord modding API references: TaleWorlds.* DLL в `bin/Win64_Shipping_Client/` (декомпилировать через ILSpy/dnSpy при необходимости).
- Harmony (для рантайм-патчинга): `Lib.Harmony` NuGet.
- Стандартный пример структуры мода: `Modules/Native/SubModule.xml` (формат манифеста).
