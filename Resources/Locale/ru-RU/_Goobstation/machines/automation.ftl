# Роботизированная рука

signal-port-name-input-machine = Предмет: Входная машина
signal-port-description-input-machine = Слот автоматизации для извлечения предметов из машины, вместо того чтобы брать их с пола.

signal-port-name-output-machine = Предмет: Выходная машина
signal-port-description-output-machine = Слот автоматизации для вставки предметов в машину, вместо того чтобы класть их на пол.

signal-port-name-item-moved = Предмет перемещён
signal-port-description-item-moved = Сигнальный порт, который срабатывает после перемещения предмета этой рукой.

signal-port-name-automation-slot-filter = Предмет: Слот фильтра
signal-port-description-automation-slot-filter = Слот автоматизации для фильтра машины автоматизации.

# Измельчитель реагентов

signal-port-name-automation-slot-beaker = Предмет: Слот стакана
signal-port-description-automation-slot-beaker = Слот автоматизации для стакана машины, работающей с жидкостями.

signal-port-name-automation-slot-input = Предмет: Входные предметы
signal-port-description-automation-slot-input = Слот автоматизации для хранения входных предметов машины.

# Упаковщик плат

signal-port-name-automation-slot-board = Предмет: Слот платы
signal-port-description-automation-slot-board = Слот автоматизации для платы упаковщика.

signal-port-name-automation-slot-materials = Предмет: Хранилище материалов
signal-port-description-automation-slot-materials = Слот автоматизации для вставки материалов в хранилище машины.

# Утилизатор

signal-port-name-flush = Смыв
signal-port-description-flush = Сигнальный порт для управления механизмом смыва утилизатора.

signal-port-name-eject = Извлечь
signal-port-description-eject = Сигнальный порт для извлечения содержимого утилизатора.

signal-port-name-ready = Готов
signal-port-description-ready = Сигнальный порт, который срабатывает после полного набора давления в утилизаторе.

# Контейнер для хранения

signal-port-name-automation-slot-storage = Предмет: Хранилище
signal-port-description-automation-slot-storage = Слот автоматизации для инвентаря контейнера хранения.

signal-port-name-storage-inserted = Вставлено
signal-port-description-storage-inserted = Сигнальный порт, который срабатывает после вставки предмета в контейнер хранения.

signal-port-name-storage-removed = Удалено
signal-port-description-storage-removed = Сигнальный порт, который срабатывает после удаления предмета из контейнера хранения.

# Факс-машина

signal-port-name-automation-slot-paper = Предмет: Бумага
signal-port-description-automation-slot-paper = Слот автоматизации для лотка бумаги факс-машины.

signal-port-name-fax-copy = Копировать факс
signal-port-description-fax-copy = Сигнальный порт для копирования бумаги факс-машины.

# Конструктор / Интерактор

signal-port-name-machine-start = Запуск
signal-port-description-machine-start = Сигнальный порт для однократного запуска машины.

signal-port-name-machine-autostart = Автозапуск
signal-port-description-machine-autostart = Сигнальный порт для управления автоматическим запуском после завершения.

signal-port-name-machine-started = Запущено
signal-port-description-machine-started = Сигнальный порт, который срабатывает после запуска машины.

signal-port-name-machine-completed = Завершено
signal-port-description-machine-completed = Сигнальный порт, который срабатывает после завершения работы машины.

signal-port-name-machine-failed = Ошибка
signal-port-description-machine-failed = Сигнальный порт, который срабатывает при неудачном запуске машины.

# Интерактор

signal-port-name-automation-slot-tool = Предмет: Инструмент
signal-port-description-automation-slot-tool = Слот автоматизации для инструмента, удерживаемого интерактором.

# Автодок

signal-port-name-automation-slot-autodoc-hand = Предмет: Рука автодока
signal-port-description-automation-slot-autodoc-hand = Слот автоматизации для органа/части и т.д., удерживаемой автодоком из инструкций STORE ITEM / GRAB ITEM.

# Газовый баллон

signal-port-name-automation-slot-gas-tank = Предмет: Газовый баллон
signal-port-description-automation-slot-gas-tank = Слот автоматизации для газового баллона.

# Радиационный коллектор

signal-port-name-rad-empty = Пусто
signal-port-description-rad-empty = Сигнальный порт установлен в HIGH, если баллон отсутствует или давление ниже 33%, в противном случае LOW.

signal-port-name-rad-low = Низкий
signal-port-description-rad-low = Сигнальный порт установлен в HIGH, если давление баллона ниже 66%, в противном случае LOW.

signal-port-name-rad-full = Полный
signal-port-description-rad-full = Сигнальный порт установлен в HIGH, если давление баллона выше 66%, в противном случае LOW.