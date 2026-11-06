# How Stylemeter scores

Every bonus has base points, listed in [BONUSES.md](BONUSES.md). Multipliers turn them into the points that fill the
meter, and a full meter moves up through eight ranks. This page explains each part.

## 1. Points formula

```
points = base x thread x height x boss x points multiplier
```

| Factor | Value | More |
|---|---|---|
| Base | The bonus's own points | [BONUSES.md](BONUSES.md) |
| Thread | x1.5, x1, x0.5 or x0, by how fresh the move is | [3. Thread tension](#3-thread-tension) |
| Height | x1 to x2, grows with every action in the air | [4. Height](#4-height) |
| Boss | x2 for an action against a boss | |
| Points multiplier | x0.25 to x3, set in Options > Stylemeter | |

Not every bonus takes every multiplier:

| Kind | Examples | Thread | Height | Boss |
|---|---|---|---|---|
| Actions | Hits, kills and what they earn: KILL, AIRSHOT, RIPOSTE | yes | yes | yes |
| Moves | Clashes, parries, binds, pogo chains, near misses | no | yes | yes |
| Flat | Kill streaks, UNTOUCHABLE, rank goals such as CLUTCH, arenas, boss fight results, HANG TIME | no | no | no |

The points multiplier applies to all of them. It scales the points but not the drain, so below x1 the ranks are
harder to reach and above x1 easier. The menu sets it in steps of 0.25.

For example, SHORYUKEN has 80 base points. On a taut thread (x1.5), as the third action of a jump (x1.2), against an
ordinary enemy and with the points multiplier at x1, it pays 80 x 1.5 x 1.2 = 144.

Plain hits score silently: their points fill the meter without a line in the feed, which shows only what is worth
seeing. They are actions, so every multiplier applies.

| Plain hit | Points |
|---|---|
| Needle, Hornet on the ground | 10 |
| Needle, Hornet in the air | 16 |
| Down slash on an enemy (pogo) | 20 |
| Tool | 20 |
| Silk skill | 30 |
| Clawline pulling Hornet in | 20 |

The first hit of Clawline is not silent: it shows as HOOKED.

## 2. Ranks and the meter

| Rank | Meter size | Drain per second | A full meter lasts |
|---|---|---|---|
| DREAMER | 200 | 7.2 | 28 s |
| CHRYSALIS | 300 | 9.1 | 33 s |
| BELLSONG | 400 | 11 | 36 s |
| ASCENDED | 500 | 13 | 38 s |
| SUBLIME | 700 | 26 | 27 s |
| SSERENADE | 850 | 32.5 | 26 s |
| SSSILKSTORM | 1000 | 44 | 23 s |
| SILKSONG | 1500 | 300 | 5 s |

The last column is how long a full meter lasts with no new points, once the drain starts.

- The first points start a combo at DREAMER, with the meter already a quarter full.
- A full meter moves up one rank. The points left over carry into the next meter, which starts at least a quarter
  full.
- An empty meter moves down one rank, to three quarters of the lower meter. Emptying DREAMER ends the combo.
- On SILKSONG the meter stops at full.
- The drain waits 2 s after the last points, so the meter holds while Hornet keeps scoring. SILKSONG has no pause.
- Nothing drains while the game is paused, in cutscenes and dialogues, during scene transitions and at a bench.
- Death ends the combo, and so does quitting to the menu.

SILKSONG drains 300 points a second from the first moment, so a full meter melts in 5 s. It stays only while Hornet
keeps scoring about as fast. Holding it pays:

- Every 5 s on SILKSONG gives Hornet one silk, and the feed shows SILK. Dropping below SILKSONG starts the count
  again. The silk can be turned off in the config file with `Meter.SilkReward`.
- Every 10 s on SILKSONG the feed shows HOLDING THE NOTE, a line without points.

## 3. Thread tension

Repeating one move gets dull, so its points fade. Every source of hits has its own thread, with a tension from 0 to
10. All threads start at 10.

| State | Tension | Multiplier |
|---|---|---|
| TAUT | 7.5 to 10 | x1.5 |
| WORN | 5 to 7.5 | x1 |
| FRAYED | 1 to 5 | x0.5 |
| SNAPPED | 0 to 1 | x0 |

The sources, each with a thread of its own:

- the needle on the ground: side and up slashes standing;
- the needle in the air: side and up slashes in a jump;
- the needle down: down slashes and pogos;
- special needle attacks: dash attacks, charged slashes and crest attacks;
- each attack tool;
- each silk skill;
- Clawline.

Clashes, parries, binds, movement, streaks and fight results have no thread and never wear.

Every scoring hit wears its thread:

| Hit | Wear |
|---|---|
| Needle | 0.4 |
| Pogo | 0.3 |
| Tool or Clawline | 0.6 |
| Silk skill | 0.75 |
| A kill, on top of the hit | 0.5 |

- The thread of the source loses 0.7 x wear. Every other thread gains 0.5 x wear.
- Against a boss the wear is x1.5.
- During a combo every thread also mends by 0.1 a second. 10 s after a combo ends, all threads are back at 10.

About ten needle hits in a row on the ground take that thread from TAUT to WORN. A pogo, a tool or a skill in between
mends it.

How low a thread can go depends on how many sources Hornet has: the four needle sources, each equipped attack tool
and silk skill, and Clawline once she has it. With five or more, a thread can snap. With only the four needle
sources, it stops at FRAYED.

The line under the meter shows the thread of the last source used: taut, worn, frayed or snapped.

## 4. Height

Hornet spends much of a fight in the air, so height grows with actions, not with time.

- Every scoring action in the air (a hit, a clash, a parry, a near miss, a bind) raises the height multiplier by 0.1,
  up to x2.
- Each action scores at the height reached before it: the first action of a jump at x1, the second at x1.1, the
  eleventh and later at x2.
- Landing resets it to x1, a quarter second later: a hit right as Hornet lands still counts as in the air.
- Clinging to or sliding down a wall keeps it without raising it.
- Bounces (pogos, mushrooms, Clawline) are not landings.
- Height multiplies actions and moves, not flat bonuses.

The line under the meter shows it, for example: height x1.4.

## 5. Damage and death

- Every hit on Hornet costs one whole rank, however many masks it takes. The meter drops to three quarters of the
  rank below. A hit on DREAMER ends the combo.
- Spikes, acid, lava and pits count as hits.
- Soft damage over time from the surroundings, such as frost, does not cost a rank: each tick takes 50 points off the
  meter.
- Any damage breaks the NEEDLEWORK and DEFLECT chains, restarts the count for UNTOUCHABLE and spoils PERFECT in an
  arena. A hit in a boss fight also loses FLAWLESS VICTORY.
- A hit that Magnetite Dice cancel costs nothing and pays LOADED DICE.
- An explosion that hits Hornet shows AW MAN, and three hits within 5 s show GIT GUD. Both are jokes without points.
- Death ends the combo. Dying in a boss fight loses the result of that fight.

## 6. Multikills

Kills that follow each other within half a second make a multikill:

| Kills | Bonus | Points |
|---|---|---|
| 2 | DUET | 25 |
| 3 | TRIO | 50 |
| 4 and more | CHOIR xN | 100 for each kill from the fourth |

Kill streaks count kills over several seconds:

| Kills | Within | Bonus | Points |
|---|---|---|---|
| 2 | 4 s | DOUBLE KILL | 50 |
| 3 | 8 s | TRIPLE KILL | 100 |
| 4 | 15 s | ULTRAKILL | 200 |
| 5 | 20 s | RAMPAGE | 300 |

- The best step that holds pays, and each step pays once per streak.
- A kill that makes no step starts a new streak.
- The windows count back from each kill: kills at 0, 5 and 7 s give TRIPLE KILL at once, with no DOUBLE KILL.
- Multikills are actions and kill streaks are flat. One kill can give both.

## 7. Combinations

Besides single actions, the mod keeps track of the last few seconds and pays for combinations:

- A sequence in a short window: RIPOSTE (a kill within 1 s of a clash or parry), GET OVER HERE! (a kill within 1 s of
  a Clawline pull), SET PIECE (a clash or parry, a pogo and a kill, all within 2 s).
- A mix of sources on one enemy: MIXED MEDIA, PATCHWORK and TAPESTRY pay for a kill after hits from 2, 3 or 4 and
  more sources.
- A mix of moves in a short time: MEDLEY (needle, pogo, tool, silk skill and clash within 5 s) and WOMBO COMBO (clash,
  pogo, silk skill and tool within 3 s, crowned by a CHOIR).
- A mix over a whole combo: TOOLBOX (every equipped attack tool) and SILK ROAD (three silk skills).
- Chains that grow with every step: POGO, DEFLECT, NEEDLEWORK and UNTOUCHABLE.
- Rhythm: ON BEAT, for four hits at an even beat.
- Hornet's state: one mask left (LAST STAND, STITCH IN TIME, CLUTCH), a crest's state (BLOODLUST in rage), the tools
  she wears (GLASS CANNON).
- The place: a boss fight (x2 and the result of the fight) or an arena (CURTAIN CALL, PERFECT).

## 8. The feed

The feed under the meter shows the latest bonuses.

- The newest line is on top. A line lives 3 s after its last update and fades out in its last half second. Up to six
  lines show at once.
- When the same bonus comes again while its line is on top, the line counts up instead of adding a new one: CLASH x3.
- Chains (POGO, DEFLECT, CHOIR, NEEDLEWORK, UNTOUCHABLE) carry their own count and keep one line, which moves to the
  top with every step, even if other lines came in between.
- Plain hits have no line.
- The colour of a line follows the base points of its bonus:

| Base points | Colour |
|---|---|
| Below 100 | cream |
| 100 to 199 | gold |
| 200 to 299 | rose |
| 300 and more | red |
| SILK | light gold |

## 9. How bonuses stack

- A kill gets exactly one kill type, the most specific one: SHORYUKEN or UPPERCUT for an up slash, DEATH FROM ABOVE or
  PIN DROP for a down slash, DASH SLASH or DRILL BIT for a dash attack, GREAT SLASH or SUSPENDED for a charged slash,
  WHIPLASH and ROOTED for the Witch crest, REELED IN for Clawline, the kill bonus of a skill or tool (SKEWERED,
  PINNED and so on), SHREDDED or IMPALED for a tool without one, and KILL for anything else.
- An enemy that Hornet hit in the last 3 s and that then dies on spikes, in lava, in acid, in a trap or in someone
  else's explosion gets that as its kill type: SPIKED, MELTED, DISSOLVED, TRAPPED or KABOOM.
- Every other bonus that applies stacks on top of the kill type: AIRSHOT, CRITICAL HIT, CLEAN CUT, OVERSTITCHED,
  RIPOSTE, LAST STAND, the elements (SEARED, TOXIC, SHOCKING, KABOOM, FLAMBÉ), the enemy (BIG KILL, WINGCLIPPED,
  SEVERED, BIG GAME), the crest (TROPHY, BLOODLUST, SPELLBOUND), the distance (PIN-POINT, HADOUKEN), the mix of
  sources, multikills, kill streaks and so on.
- AIRSHOT is not given for a down slash kill: that hit is in the air anyway.
- MIXED MEDIA, PATCHWORK and TAPESTRY count all needle attacks as one source, so the mix needs a tool, a silk skill or
  Clawline.
- One action has one thread and one height: the hit and everything it earns share them.
- A kill by Hornet's damage over time (poison, fire or lightning she left on an enemy) is still hers. The kill type
  comes from her last hit and the element from the ticks; lightning ticks give LIVE WIRE. Bonuses for where Hornet is
  at that moment (AIRSHOT, CRITICAL HIT, RIPOSTE and the like) do not apply, since the tick can come seconds later.
- One throw of a tool or one cast of a skill covers its hits for 3 s. SHISH KEBAB, HAT TRICK, TRIFECTA, RICOCHET,
  FULL SPIN, RAGE QUIT, NAILED IT and the like pay at most once per throw.
- Some bonuses pay once per combo: TOOLBOX, SILK ROAD, TRAP CARD, NO COST TOO GREAT, CLUTCH, ZERO TO HERO,
  NO STRINGS ATTACHED, AGAINST ALL ODDS, GLASS CANNON. A tool used up to 3 s before the first points counts as part
  of the combo.
- A boss fight runs from the boss's title card until the boss dies. Hits and moves against the boss count x2 during
  the fight. The result comes once at the end, flat: the named win (FINALE for a boss without one), and
  FLAWLESS VICTORY, SHOWSTOPPER, JACKPOT!, GREAT ENEMY FELLED, STUDENT BECOMES THE MASTER or UNFORGIVEN when they
  apply.
- Passive bonuses do not hold the meter: HOLDING THE NOTE has no points, and UNTOUCHABLE pays only if Hornet made at
  least 10 scoring actions in those 30 s.
- A bonus switched off in the menu gives no points and no line. Everything else from the same action still counts.
