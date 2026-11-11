# CLAUDE.md

Краткая техническая справка по билду Monolith-DS для Claude Code.

## Что это

Русскоязычный форк Space Station 14 на базе Monolith (линия Frontier / New Frontiers). Движок — сабмодуль `RobustToolbox` (официальные теги space-wizards, сейчас линия v291). Основная ветка — `master`.

## Сборка и запуск

```bash
python RUN_THIS.py                      # один раз после клонирования (сабмодули)
dotnet build --configuration DebugOpt   # .NET SDK 10 (global.json)
./runserver.bat
./runclient.bat
```

Собранное лежит в `bin/`. `bin/Content.Server/server_config.toml` перезаписывается копией из `RobustToolbox/Robust.Server/server_config.toml` при её изменении, поэтому свои cvars после обновления движка нужно вернуть.

## Тесты

```bash
dotnet test Content.Tests/Content.Tests.csproj -- NUnit.ConsoleOut=0
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName!~ShipyardTest"
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName~ShipyardTest"
runTestsYAML.bat                        # YAML-линтер, лог в Scripts/logs
```

- CI собирает с `/p:WarningsAsErrors=nullable`.
- `stackalloc` в клиентском коде валит `SandboxTest` (ILVerify) — используйте массив.
- Тесты, которые запускают полный раунд, могут падать из-за случайно загруженной POI-карты: ошибки ищите в StdOut trx-лога (`--logger trx`).
- `NoShipyardShipArbitrage` чувствителен к случайным спавнерам: цену шаттла ставить с запасом над максимальной оценкой.
- Тесты, упавшие по таймауту, при запущенном рядом своём сервере или клиенте — перезапустите на свободной машине.
- В `PoolManager.Cvars.cs` перечислены cvars для тестового пула; новый cvar, который перезапускает или выключает сервер в конце раунда, нужно выключить там же.

## Структура

- `Content.Shared` / `Content.Server` / `Content.Client` — основной код; плюс проекты `Content.Goobstation.*`.
- Код других форков лежит в папках с префиксом: `_Mono`, `_NF`, `_Goobstation`, `_DeadSpace` и т.д.
- Собственный код LuaCorp — папки `_Lua` и `_LuaM`; **новое добавляется в `_LuaM`** (`Content.*/_LuaM`, `Resources/Prototypes/_LuaM`, `Resources/Audio/_LuaM` …).
- Локализация: основная `Resources/Locale/ru-RU` (автопереводы прототипов — `ru-RU/ss14-ru/prototypes/...`), запасная `en-US`.

## Правила

- **`RobustToolbox/` не редактировать.** Разрешено только переключить сабмодуль на официальный тег space-wizards (после `checkout` — `git submodule sync --recursive` и `git submodule update --init --recursive` внутри него).
- Маркировка изменений — по [MARKERS.md](MARKERS.md). Мелкие правки в чужих файлах помечаются `// LuaM` или блоком `// LuaM-start` … `// LuaM-end` (в YAML — `# LuaM`).
- Служебные карты (например, карта настольных игр) нужно защищать от очистки `_Mono/Cleanup`: `EnsureComp<CleanupImmuneComponent>(mapUid)`, иначе сущности на карте удаляются.

## Ловушки движка (RT 291)

- На клиенте `SetWorldPosition` / `SetLocalPosition` / `SetLocalRotation` запускают интерполяцию и оставляют устаревшую цель `NextPosition`/`NextRotation`. При ручном перемещении каждый кадр (перетаскивание, клиентские призраки) используйте `SetLocalPositionNoLerp`, иначе объект дёргается и потом уезжает к старой цели.
- Свет с тенями, центр которого попал внутрь окклюдера (`IsLightEmbeddedInOccluder`), не рисуется — не ставьте `offset` ровно на край тайла.
- `OccluderComponent` принимает `polygon`; у диагональных стен он обязателен.

## Windows

- Рабочая копия в CRLF (`core.autocrlf=true`). `sed -i` в Git Bash переписывает файл в LF — правьте через Edit или Python с `newline=''`.
- `.ftl` хранятся в CRLF с BOM.
- `.github/mapchecker/mapchecker.py` на Windows запускать с `PYTHONUTF8=1`.
- В ru-RU сообщение может ссылаться (`{ ent-X.desc }`) только на ru-ключи: en-US — отдельный бандл.
