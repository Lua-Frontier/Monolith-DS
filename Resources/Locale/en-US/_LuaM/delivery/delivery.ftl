delivery-program-name = ColossDelivery

job-name-delivery-courier = Courier
job-description-delivery-courier = Deliver ColossDelivery orders to the people of the sector. Pick up parcels from the delivery telepad on Colossus Central and bring them to the customers.

delivery-order-placed = Order #{ $id } placed. A courier will be notified.
delivery-notify-new-order = New order #{ $id } for { $customer }. Courier payout: { $payout } cr.
delivery-order-taken = You took order #{ $id }.
delivery-order-expired = Delivery time for order #{ $id } ran out. The order is cancelled and the customer is refunded.
delivery-parcel-wrong-customer = The bag won't open: this is not your order.
delivery-parcel-opened = You open the delivery bag.
delivery-courier-paid = Order delivered! You received { $amount } cr.
delivery-courier-none = not assigned

delivery-parcel-examine-order = Order [color=#F5C518]#{ $id }[/color].
delivery-parcel-examine-customer = Recipient: [color=white]{ $name }[/color].
delivery-parcel-examine-courier = Courier: [color=white]{ $name }[/color].
delivery-parcel-examine-time = Time until cancellation: [color=orange]{ $time }[/color].

delivery-error-take = This order is already taken.
delivery-error-unavailable = The delivery service is currently unavailable.
delivery-error-no-account = Account unavailable.
delivery-error-no-id = Insert an ID card into your PDA to place an order.
delivery-error-too-many-items = An order can contain at most 30 items.
delivery-error-too-cheap = The minimum order is 5,000 cr.
delivery-error-too-many-orders = You already have too many active orders.
delivery-error-insufficient-funds = Insufficient funds.
delivery-error-invalid-cart = The cart is invalid.

delivery-ui-maintenance = MAINTENANCE
delivery-ui-account-unavailable = ACCOUNT UNAVAILABLE
delivery-ui-balance = Balance: { $amount } cr.
delivery-ui-search = Search items...
delivery-ui-clear = Clear
delivery-ui-order = Order
delivery-ui-my-orders = My orders
delivery-ui-no-orders = No active orders.
delivery-ui-courier-profile = Courier profile
delivery-ui-courier-stats = Delivered: { $delivered } · Earned: { $earned } cr.
delivery-ui-active-orders = Active orders
delivery-ui-cart-summary = Items: { $count }/{ $max } · Total: [color={ $color }]{ $total } cr.[/color] (min. { $min } cr.)
delivery-ui-price = { $amount } cr.
delivery-ui-order-title = Order #{ $id } · { $customer }
delivery-ui-status-waiting = Status: waiting for a courier
delivery-ui-status-yours = Status: taken by you
delivery-ui-status-taken = Status: taken by { $courier }
delivery-ui-order-money = Cost: { $total } cr.
delivery-ui-order-money-courier = Cost: { $total } cr. · Your cut: { $payout } cr.
delivery-ui-order-position = Bag: X { $x }, Y { $y }
delivery-ui-order-position-unknown = Bag: position unknown
delivery-ui-item = { $name } ×{ $count }
delivery-ui-items-more = {" "}and { $count } more
delivery-ui-time-left = Time left: { $time }
delivery-ui-take = Take order
delivery-courier-paid-cash = Order delivered! You received { $amount } cr in cash.
delivery-ui-maintenance-hint = No couriers on shift. Check back later.
delivery-ui-account-unavailable-hint = Banking is unavailable for this character.
delivery-ui-catalog-loading = Loading catalog...
delivery-ui-page = Page { $page }/{ $pages } · items: { $count }
delivery-ui-cart-empty = The cart is empty.
delivery-ui-nothing-found = Nothing found.
delivery-ui-cash-payout = Payouts: cash

delivery-category-all = All items
delivery-category-cart = Cart
delivery-category-favorites = Favorites
delivery-sort-name = By name
delivery-sort-price-ascending = Price: low to high
delivery-sort-price-descending = Price: high to low
delivery-ui-favorites-empty = No favorites yet. Mark an item with the star.
delivery-ui-favorite-add = Add to favorites
delivery-ui-favorite-remove = Remove from favorites
delivery-category-engineering = Engineering
delivery-category-salvage = Salvage
delivery-category-fuel = Fuel
delivery-category-medical = Medical
delivery-category-science = Science
delivery-category-weapons = Weapons & gear
delivery-category-botany = Botany
delivery-category-kitchen = Kitchen
delivery-category-food = Food
delivery-category-drinks = Drinks
delivery-category-leisure = Leisure
delivery-category-clothing = Clothing
delivery-category-other = Other
delivery-order-in-transit = Courier { $courier } took order #{ $id }.
delivery-error-courier-too-many = You already have { $max } active orders. Deliver them first.
delivery-ui-tab-catalog = Catalog
delivery-ui-tab-orders = My orders ({ $count })
delivery-ui-status-in-transit = Status: courier { $courier } is on the way
delivery-ui-status-reassign = Status: courier left, waiting for a new one
delivery-ui-order-position-pending = Parcel: appears on the telepad once taken
delivery-ui-courier-active = Orders taken: { $count }/{ $max }
delivery-category-exclusive = ColossDelivery exclusives

flavor-complex-luam-kvass = like bread kvass
reagent-name-luam-kvass = kvass
reagent-desc-luam-kvass = A refreshing live-fermented bread kvass. Sweet and sour, lightly fizzy.


loadout-group-courier-kit = Courier kit

delivery-courier-welcome-content =
    {"["}head=2]ColossDelivery[/head]
    {"["}italic]Fast. Warm. Right into your hands.[/italic]

    Dear courier,

    Thank you for choosing ColossDelivery! If you are reading this letter, your shift has already started. Welcome to the team.

    {"["}bold]Your workplace[/bold]
    The mail office on "Colossus Central". The delivery telepad that receives parcels stands there. Your direct supervisor is the Station Representative of "Colossus Central", bring all work questions to them.

    {"["}bold]How to work[/bold]
    1. Open the ColossDelivery app on your PDA. It lists every open order from the residents of the sector.
    2. Take an order right away. While you hesitate, another courier will grab it.
    3. As soon as you accept an order, the parcel appears on the telepad in the mail office. You can carry at most two orders at once.
    4. The delivery bag has a pinpointer that points at the customer.
    5. Hand the bag to the customer. Only they can open it, with their ID card.

    {"["}bold]Deadlines[/bold]
    You have 30 minutes from the moment the order was placed. If time runs out, the bag self-destructs, the customer gets a refund and you get nothing.

    {"["}bold]Payment[/bold]
    You receive 30% of the order total up to 50,000 and 10% of everything above. The money reaches your account as soon as the customer opens the bag. Only the courier who accepted the order is paid.

    {"["}bold]Equipment[/bold]
    The thermobag, uniform and service blaster are issued to you for the shift. The blaster is for self-defense only.

    At the end of your shift, return all equipment, including the blaster, to the mail office. Loss of or damage to company property is subject to a fine.

    Smooth routes and generous tips!

    Sincerely,
    ColossDelivery HR department
