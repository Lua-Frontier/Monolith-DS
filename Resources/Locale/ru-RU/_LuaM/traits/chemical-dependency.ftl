trait-luam-chemical-dependency-name = Зависимость от химикатов
trait-luam-chemical-dependency-desc = Каждый раунд вы зависимы от случайного химиката, который можно сварить: лекарства, наркотика или яда. Каждые 10 единиц препарата сдерживают ломку на 15 минут. Без дозы начинается ломка: вас трясёт, вы падаете, роняете вещи и не можете сдержать стонов. Ломку можно перетерпеть: через 2 минуты она отступает до следующего приступа. Какой химикат нужен, покажет анализатор здоровья. Налтрексон снимает ломку, пока действует, а курс в 150 единиц избавляет от зависимости; каждая доза химиката откатывает лечение на 25%.
chemical-dependency-craving = Вас начинает тянуть к привычной дозе.
chemical-dependency-withdrawal-start = Вас накрывает ломка. Организму срочно нужна доза!
chemical-dependency-relief = Ломка отступает.
chemical-dependency-relapse = Тяга снова крепнет: лечение откатывается.
chemical-dependency-nicotine-withdrawal = Горло дерёт, страшно хочется закурить.
chemical-dependency-endured = Вы перетерпели ломку. Пока отпустило.
chemical-dependency-withdrawal-1 = Всё тело ломит.
chemical-dependency-withdrawal-2 = Руки не слушаются.
chemical-dependency-withdrawal-3 = Вас бросает то в жар, то в холод.
health-analyzer-window-entity-dependency-text = Зависимость:
addiction-warning = Вам всё труднее остановиться: { $addiction } затягивает.
addiction-acquired = Кажется, вы подсели. Зависимость: { $addiction }.
addiction-cured = Тяга отпускает. Вы избавились от зависимости: { $addiction }.
addiction-heavy = Вы слишком глубоко увязли. Зависимость стала тяжёлой: { $addiction }. Перетерпеть её уже не выйдет, нужен полный курс лечения.
addiction-name-alcohol = алкоголь
addiction-name-nicotine = никотин
addiction-name-desoxyephedrine = дезоксиэфедрин
addiction-name-stimulants = гиперзин
addiction-name-ephedrine = эфедрин
addiction-name-space-drugs = космический мираж
addiction-name-fentanyl = фентанил
addiction-name-omnizine = омнизин
health-analyzer-window-addiction-recovery = { $addiction } (лечение { $percent }%)
health-analyzer-window-addiction-heavy = { $addiction }, тяжёлая
reagent-effect-guidebook-treat-chemical-dependency =
    { $chance ->
        [1] Снимает
       *[other] снимают
    } ломку и лечит зависимости от химикатов
