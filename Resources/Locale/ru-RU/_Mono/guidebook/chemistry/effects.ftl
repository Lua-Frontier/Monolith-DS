health-scale-display =
    { $deltasign ->
        [-1] урон { $kind } уменьшен в [color=green]{ $amount }[/color] раз
         [0] урон { $kind } умножен на { $amount }
         [1] урон { $kind } увеличен в [color=red]{ $amount }[/color] раз
        *[other] урон { $kind } умножен на { $amount }
    }

reagent-effect-guidebook-health-scale =
    { $chance ->
        [1] Умножает текущий { $changes }
       *[other] С шансом { $chance }% умножает текущий { $changes }
    }

reagent-effect-guidebook-claws-growth =
    { $chance ->
        [1] Отращивает
        *[other] отращивает
    } когти в { $amount } раз быстрее во время метаболизма

reagent-effect-guidebook-claws-growth-suppression =
    { $chance ->
        [1] Подавляет
        *[other] подавляет
    } рост когтей.