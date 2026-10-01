yupi-program-name = YUPI Transfers
yupi-bank-name = PromGradBank
yupi-no-account = Bank account unavailable.

yupi-ui-own-code = Your YUPI:
yupi-ui-copy-tooltip = Copy code
yupi-ui-balance = Balance:
yupi-ui-low-rate-remaining = Low-rate allowance:
yupi-ui-credits = { $amount } cr.
yupi-ui-target-code = Recipient code:
yupi-ui-amount = Amount:
yupi-ui-send = Send
yupi-ui-send-cooldown = Send in { $time }
yupi-ui-commission-hint =
    Commission is { $low }% within the allowance and { $high }% above it.
    The allowance recovers over 30 minutes.
yupi-ui-commission-preview = Commission: { $commission } cr. Recipient gets: { $net } cr.

yupi-transfer-sent = Sent { $amount } cr. to account { $code }. Commission: { $commission } cr., recipient got { $net } cr.
yupi-transfer-received = Incoming YUPI transfer from account { $code }: { $amount } cr.{ $hasSavings ->
    [yes] {" "}Another { $savings } cr. went to savings.
   *[no] {""}
}

yupi-error-invalid-amount = Invalid amount.
yupi-error-invalid-target = No account with that code was found.
yupi-error-self-transfer = You cannot transfer to yourself.
yupi-error-target-unavailable = The recipient is currently unavailable.
yupi-error-insufficient-funds = Insufficient funds.
yupi-error-cooldown = Your next transfer will be available in a minute.
yupi-error-disabled = Transfers are temporarily unavailable.
