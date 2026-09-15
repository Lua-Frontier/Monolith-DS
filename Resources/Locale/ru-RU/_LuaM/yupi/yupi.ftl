yupi-program-name = ЮПИ Переводы
yupi-bank-name = PromGradBank
yupi-no-account = Банковский счёт недоступен.

yupi-ui-own-code = Ваш ЮПИ:
yupi-ui-copy-tooltip = Скопировать код
yupi-ui-balance = Баланс:
yupi-ui-low-rate-remaining = Льготный лимит:
yupi-ui-credits = { $amount } кр.
yupi-ui-target-code = Код получателя:
yupi-ui-amount = Сумма:
yupi-ui-send = Отправить
yupi-ui-send-cooldown = Отправить через { $time }
yupi-ui-commission-hint =
    Комиссия { $low }% в пределах лимита, { $high }% сверх него.
    Лимит восстанавливается в течение 30 минут.
yupi-ui-commission-preview = Комиссия: { $commission } кр. Получателю дойдёт: { $net } кр.

yupi-transfer-sent = Перевод { $amount } кр. на счёт { $code } выполнен. Комиссия: { $commission } кр., получателю дошло { $net } кр.
yupi-transfer-received = Входящий перевод ЮПИ со счёта { $code }: { $amount } кр.{ $hasSavings ->
    [yes] {" "}Ещё { $savings } кр. ушло в сбережения.
   *[no] {""}
}

yupi-error-invalid-amount = Неверная сумма.
yupi-error-invalid-target = Счёт с таким кодом не найден.
yupi-error-self-transfer = Нельзя переводить самому себе.
yupi-error-target-unavailable = Получатель сейчас недоступен.
yupi-error-insufficient-funds = Недостаточно средств.
yupi-error-cooldown = Следующий перевод будет доступен через минуту.
yupi-error-disabled = Переводы временно недоступны.
