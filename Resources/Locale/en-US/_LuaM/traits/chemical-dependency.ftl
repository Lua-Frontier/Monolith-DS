trait-luam-chemical-dependency-name = Chemical Dependency
trait-luam-chemical-dependency-desc = Every round you depend on a random chemical that can be brewed: a medicine, a narcotic or a poison. Every 10 units of it hold withdrawal off for 15 minutes. Without a dose withdrawal sets in: you shake, fall, drop things and can not hold back your groans. Withdrawal can be sat out: after 2 minutes it lets go until the next bout. A health analyzer shows which chemical you need. Naltrexone holds withdrawal off while it works, and a 150-unit course cures the dependency; every dose of the chemical takes the treatment back by 25%.
chemical-dependency-craving = You start to crave your usual dose.
chemical-dependency-withdrawal-start = Withdrawal hits you. Your body needs a dose right now!
chemical-dependency-relief = The withdrawal fades.
chemical-dependency-relapse = The craving grows stronger again: the treatment slips back.
chemical-dependency-nicotine-withdrawal = Your throat scratches, you badly want a smoke.
chemical-dependency-endured = You sat out the withdrawal. It lets go for now.
chemical-dependency-withdrawal-1 = Your whole body aches.
chemical-dependency-withdrawal-2 = Your hands won't listen.
chemical-dependency-withdrawal-3 = You shiver and sweat at the same time.
health-analyzer-window-entity-dependency-text = Dependency:
addiction-warning = It is getting harder to stop: { $addiction } is pulling you in.
addiction-acquired = Looks like you got hooked. Addiction: { $addiction }.
addiction-cured = The craving lets go. You got over your addiction: { $addiction }.
addiction-heavy = You are in too deep. Your addiction got heavy: { $addiction }. It will not pass on its own anymore, only a full course of treatment helps.
addiction-name-alcohol = alcohol
addiction-name-nicotine = nicotine
addiction-name-desoxyephedrine = desoxyephedrine
addiction-name-stimulants = hyperzine
addiction-name-ephedrine = ephedrine
addiction-name-space-drugs = space drugs
addiction-name-fentanyl = fentanyl
addiction-name-omnizine = omnizine
health-analyzer-window-addiction-recovery = { $addiction } (treatment { $percent }%)
health-analyzer-window-addiction-heavy = { $addiction }, heavy
reagent-effect-guidebook-treat-chemical-dependency =
    { $chance ->
        [1] Holds
       *[other] hold
    } withdrawal off and cures chemical dependencies
