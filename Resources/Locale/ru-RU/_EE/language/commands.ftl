command-list-langs-desc = Показать языки, на которых ваша сущность может говорить в данный момент.
command-list-langs-help = Использование: {$command}

command-saylang-desc = Отправить сообщение на определённом языке. Для выбора языка можно использовать его название или позицию в списке языков.
command-saylang-help = Использование: {$command} <id языка> <сообщение>. Пример: {$command} TauCetiBasic "Hello World!". Пример: {$command} 1 "Hello World!"

command-language-select-desc = Выбрать текущий язык вашей сущности. Можно использовать название языка или его позицию в списке.
command-language-select-help = Использование: {$command} <id языка>. Пример: {$command} 1. Пример: {$command} TauCetiBasic

command-language-spoken = Разговорные:
command-language-understood = Понимаемые:
command-language-current-entry = {$id}. {$language} - {$name} (текущий)
command-language-entry = {$id}. {$language} - {$name}

command-language-invalid-number = Номер языка должен быть от 0 до {$total}. Или используйте название языка.
command-language-invalid-language = Язык {$id} не существует или вы не можете на нём говорить.

# Toolshed

command-description-language-add = Добавляет новый язык для сущности в конвейере. Два последних аргумента указывают, должен ли язык быть разговорным/понимаемым. Пример: 'self language:add "Canilunzt" true true'
command-description-language-rm = Удаляет язык у сущности в конвейере. Работает аналогично language:add. Пример: 'self language:rm "TauCetiBasic" true true'
command-description-language-lsspoken = Показывает все языки, на которых сущность может говорить. Пример: 'self language:lsspoken'
command-description-language-lsunderstood = Показывает все языки, которые сущность может понимать. Пример: 'self language:lssunderstood'

command-description-translator-addlang = Добавляет целевой язык для сущности-переводчика в конвейере. Подробнее см. language:add.
command-description-translator-rmlang = Удаляет целевой язык у сущности-переводчика в конвейере. Подробнее см. language:rm.
command-description-translator-addrequired = Добавляет обязательный язык для сущности-переводчика в конвейере. Пример: 'ent 1234 translator:addrequired "TauCetiBasic"'
command-description-translator-rmrequired = Удаляет обязательный язык у сущности-переводчика в конвейере. Пример: 'ent 1234 translator:rmrequired "TauCetiBasic"'
command-description-translator-lsspoken = Показывает все разговорные языки для сущности-переводчика в конвейере. Пример: 'ent 1234 translator:lsspoken'
command-description-translator-lsunderstood = Показывает все понимаемые языки для сущности-переводчика в конвейере. Пример: 'ent 1234 translator:lssunderstood'
command-description-translator-lsrequired = Показывает все обязательные языки для сущности-переводчика в конвейере. Пример: 'ent 1234 translator:lsrequired'

command-language-error-this-will-not-work = Это не сработает.
command-language-error-not-a-translator = Сущность {$entity} не является переводчиком.