<p align="center"> <img alt="Monolith DS" height="300" src="https://github.com/Lua-Frontier/Monolith-DS/blob/master/Resources/Textures/_LuaM/logogit.png?raw=true" /></p>

![Alt](https://repobeats.axiom.co/api/embed/cfba1806606b5f66bb0bd62eebcd8941d9008e31.svg "Repobeats analytics image")

Monolith-DS — это форк [Monolith](https://github.com/Monolith-Station/Monolith)/[Space Station 14](https://github.com/space-wizards/space-station-14), работающий на движке [Robust Toolbox](https://github.com/space-wizards/RobustToolbox), написанном на C#.

Это основной репозиторий Monolith-DS, являющийся форком Monolith.

## Ссылки

[Discord](https://discord.gg/PFMc8FENR) | [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/) | [Вики](https://wiki.deadspace14.net/Фронтир:Заглавная_страница)


## Документация

На официальном сайте с [документацией](https://docs.spacestation14.io/) имеется вся необходимая информация о контенте SS14, движке, дизайне игры и многом другом. Также имеется много информации для начинающих разработчиков.

## Участие в разработке

Если вы желаете внести свой вклад в сборку проекта, мы рады принять участию любого человека. Заходите в Discord, если хотите помочь. Не бойтесь просить о помощи!
Перед созданием PR, убедитесь, что ваши изменения и соответствуют [рекомендациям по PR](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html).

## Сборка
Обратитесь к руководству Space Wizards по настройке среды разработки для получения общей информации, но имейте в виду, что ввиду использования Einstein Engines некоторые инструкции могут быть неприменимы.
Ниже предоставляется несколько инструкций для запуска сборки.

### Зависимости

> - Git
> - .NET SDK 10.0

### Запуск сборки

> 1. Склонируйте этот репозиторий локально.
> 2. Запустите `RUN_THIS.py` для инициализации подмодулей и скачивания движка.
> 3. Откройте консоль в директории проекта.
> 4. Соберите проект с помощью `dotnet build`.

[Более подробная инструкция по запуску проекта.](https://docs.spacestation14.com/en/general-development/setup.html)


## Лицензия

Смотрите заголовки REUSE для получения подробной информации о лицензировании каждого файла и конкретных лицензиях, под которыми предоставляется вклад. В основном весь код в этой кодовой базе распространяется под лицензией под GNU Affero General Public License 3.0.

По умолчанию, оригинальный код, внесенный в кодовую базу Monolith после коммита 04d8ce483f638320d1b85a7aaacdf01442757363, распространяется под Mozilla Public License версии 2.0 с удаленным Приложением B. Подробнее в `LICENSE-MPL.txt`.

Контент, внесенный в этот репозиторий после коммита [2fca06eaba205ae6fe3aceb8ae2a0594f0effee0](https://github.com/new-frontiers-14/frontier-station-14/commit/2fca06eaba205ae6fe3aceb8ae2a0594f0effee0), лицензирован под GNU Affero General Public License версии 3.0, если не указано иное. Подробнее в `LICENSE-AGPLv3.txt`.

Контент, внесенный в этот репозиторий до коммита [2fca06eaba205ae6fe3aceb8ae2a0594f0effee0](https://github.com/new-frontiers-14/frontier-station-14/commit/2fca06eaba205ae6fe3aceb8ae2a0594f0effee0), лицензирован под лицензией MIT, если не указано иное. Подробнее в `LICENSE-MPL.txt`.

[2fca06eaba205ae6fe3aceb8ae2a0594f0effee0](https://github.com/new-frontiers-14/frontier-station-14/commit/2fca06eaba205ae6fe3aceb8ae2a0594f0effee0) был создан 1 июля 2024 года в 16:04 UTC.

Большинство ассетов лицензированы под CC-BY-SA 3.0, если не указано иное. Ассеты имеют свою лицензию и информацию об авторских правах в файле метаданных. [Пример](https://github.com/space-wizards/space-station-14/blob/master/Resources/Textures/Objects/Tools/crowbar.rsi/meta.json).

Обратите внимание, что некоторые ассеты лицензированы под некоммерческой лицензией CC-BY-NC-SA 3.0 или аналогичными некоммерческими лицензиями, и их необходимо будет удалить, если вы хотите использовать этот проект в коммерческих целях.


## Атрибуция

При заимствовании контента из других форков мы организуем его в соответствующие подкаталоги для удобства отслеживания авторства и минимизации конфликтов при слиянии.

Контент в этих подкаталогах происходит из соответствующих форков и может содержать изменения. Эти изменения обозначены комментариями вокруг/на измененных строках.

| Подкаталог | Название форка | Репозиторий форка | Лицензия |
|--------------|-----------|-----------------|---------|
| `_CD` | Cosmatic Drift | https://github.com/cosmatic-drift-14/cosmatic-drift | MIT |
| `_CorvaxNext` | Corvax Next | https://github.com/space-syndicate/space-station-14-next | AGPL 3.0 |
| `_Cresent` | Hullrot | https://github.com/Sector-Crescent/Hullrot| AGPL 3.0 |
| `_DeadSpace` | Мёртвый Космос | https://github.com/dead-space-server/space-station-14-fobos | Custom |
| `_DV` | Delta-V | https://github.com/DeltaV-Station/Delta-v | AGPL 3.0 |
| `_EE` **или** `_EinsteinEngines` | Einstein Engines | https://github.com/Simple-Station/Einstein-Engines | AGPL 3.0 |
| `_EstacaoPirata` | Estacao Pirata | https://github.com/Day-OS/estacao-pirata-14 | AGPL 3.0 |
| `_FarHorizons` | Far Horizons  | https://github.com/Far-Horizons-SS14/Far-Horizons-SS14 | MIT |
| `_Funkystation` | Funky Station | https://github.com/funky-station/funky-station | AGPL 3.0 |
| `_Goobstation` **или** `_Shitmed` | Goob Station | https://github.com/Goob-Station/Goob-Station | AGPL 3.0 |
| `_Harmony` | Harmony | https://github.com/ss14-harmony/ss14-harmony | AGPL 3.0 |
| `_Impstation` | Impstation | https://github.com/impstation/imp-station-14 | AGPL 3.0 |
| `_Lua` | LuaCorp | https://github.com/Lua-Frontier/sector-frontier-14 | AGPL 3.0 |
| `_LuaM` | Monolith DS | https://github.com/Lua-Frontier/Monolith-DS | AGPL 3.0 |
| `_Moffstation` | Moff Station | https://github.com/moff-station/moff-station-14 | MIT |
| `_Mono` | Monolith | https://github.com/Monolith-Station/Monolith | AGPL 3.0 |
| `_NF` | Frontier Station | https://github.com/new-frontiers-14/frontier-station-14 | AGPL 3.0 |
| `_Obelisk` | Obelisk | https://github.com/Obelisk-Sector/Obelisk | AGPL 3.0 |
| `_RMC14` | RMC-14 | https://github.com/RMC-14/RMC-14 | MIT |
| `_SCP` | Fire Station | https://github.com/space-sunrise/project-fire | MIT |
| `_SimpleStation` | Simple Station | https://github.com/Gary412/SimpleStation14 | MIT |
| `_StarLight` | Star Light | https://github.com/ss14Starlight/space-station-14 | MIT |
| `Nyanotrasen` | Nyanotrasen | https://github.com/Nyanotrasen/Nyanotrasen | MIT |


Дополнительные репозитории, из которых были перенесены функции без создания подкаталогов, перечислены ниже.

| Название форка | Репозиторий форка | Лицензия |
|-----------|-----------------|---------|
| Space Station 14 | https://github.com/space-wizards/space-station-14 | MIT |

Обратите внимание, что подкатлоги могут отсутствовать, ввиду ошибок контрибьютеров оригинального репозитория [Monolith](https://github.com/Monolith-Station/Monolith). 
