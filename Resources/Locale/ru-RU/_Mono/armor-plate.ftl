armor-plate-break = Ваша {$plateName} разбилась вдребезги!
armor-plate-examine-with-plate = Установлена [color=yellow]{$plateName}[/color]. Прочность: [color={$durabilityColor}]{$percent}%[/color]
armor-plate-examine-with-plate-simple = Установлена [color=yellow]{$plateName}[/color].
armor-plate-examine-no-plate = Бронепластина не установлена.
armor-plate-examine-no-storage = Нет отсека для бронепластин.

armor-plate-examinable-verb-text = Характеристики пластины
armor-plate-examinable-verb-message = Просмотр характеристик защиты и прочности.

armor-plate-attributes-examine = Эта бронепластина:
armor-plate-initial-durability = Рассчитана на [color=yellow]{ $durability }[/color] стандартных единиц урона.

armor-plate-item-durability = Прочность: [color={$durabilityColor}]{$percent}%[/color]

armor-plate-gait-speed = скорость
armor-plate-gait-walk = скорость ходьбы
armor-plate-gait-sprint = скорость бега

armor-plate-speed-display =
    { $stringClause ->
         [1] Увеличивает вашу {$gait} на [color=yellow]{$speedPercent}%[/color].
         [-1] Уменьшает вашу {$gait} на [color=yellow]{$speedPercent}%[/color].
        *[other] Не должно быть такого значения скорости!
    }

armor-plate-ratios-display =
    { $stringClause ->
        [1] [color=cyan]Поглощает[/color] [color=yellow]{$ratioPercent}%[/color] урона типа [color=yellow]{$dmgType}[/color]
        [-1] [color=fuchsia]Усиливает[/color] урон типа [color=yellow]{$dmgType}[/color] на [color=yellow]{$ratioPercent}%[/color]
        [0] Не влияет на урон типа [color=yellow]{$dmgType}[/color]
       *[other] Для {$dmgType} не должно быть такого значения поглощения!
    }
armor-plate-stamina-value = Наносит [color=yellow]{$multiplier}%[/color] {$sources} урона как урон по выносливости.

armor-plate-multiplier-display = и вычитает [color=yellow]{ $multiplier }%[/color] от исходного значения урона из прочности.
armor-plate-multiplier-none = и не повреждает пластину.
armor-plate-stamina-source-absorb = [color=cyan]Поглощённого[/color]
armor-plate-stamina-concat = и
armor-plate-stamina-source-amplified = [color=fuchsia]Усиленного[/color]
armor-plate-stamina-source-raw = [color=red]Всего входящего[/color]