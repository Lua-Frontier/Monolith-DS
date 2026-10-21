gas-deposit-drill-no-resources = Здесь нечего бурить!
gas-deposit-drill-system-examined = Экстрактор настроен на [color={$statusColor}]{PRESSURE($pressure)}[/color].
gas-deposit-drill-system-examined-amount = Экстрактор сообщает что {
    $value ->
        [0] [color={$statusColor}]почти ничего[/color] не осталось.
        *[other] [color={$statusColor}]{GASQUANTITY($value)}[/color] осталось.
    }
gas-deposit-extraction-rate = скорость извлечения

gas-deposit-drill-system-examined-yield = Экстрактор сообщает, что осталось [color={ $statusColor }]{ NATURALFIXED($yield, 1) }%[/color]{
    $hitMinimum ->
        [false] запасов.
        *[other] запасов, и достигнуты глубокие резервы.
    }