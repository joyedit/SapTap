# SapTap — a Vintage Story mod

Adds sap taps (spiles): small metal tubes you hammer into a resin-oozing log, with a hook
underneath that an ordinary wooden bucket hangs from. Leave it a while, come back, take the
bucket down and keep the resin.

Requires Vintage Story 1.22 or newer.

## The tap

A tap is modelled as a real spile: a pointed, tapered tip that goes into the bark, a collar
that seats against the trunk, a tube with a hammered band, a drip lip at the outer end, and a
hook rod with an upturned barb beneath it.

The bucket is not part of the tap model. When you hang one, the block entity tesselates the
actual vanilla `woodbucket` mesh and drops it onto the hook, so what you see is the same
bucket you were holding — bail, staves, iron rings and all.

## Crafting

Shapeless, in any crafting grid — the three items can go in any slots, in any order:

    hammer  +  metal plate or metal sheet  +  chisel   ->  4 sap taps of that metal

Either stock works: the smithed **metal plate** item (`metalplate-<metal>`) or the **metal sheet**
block (`metalsheet-<metal>-<side>`). They look almost identical in the inventory. Only the plate
has a vanilla crafting route — sheets are loot and worldgen only — so the plate is the repeatable
survival option.

Working the stock needs a hammer **and** chisel at least as hard as the metal being worked.
**Copper hammers and chisels cannot make any tap** — they share tool tier 2 with gold and silver,
which is not enough to cut plate.

| Stock metal | Tools needed |
| ----------- | ------------ |
| Copper, tin/bismuth/black bronze, brass, gold, silver, lead, bismuth, molybdochalkos | tin, bismuth or black bronze, iron, meteoric iron or steel |
| Iron, meteoric iron | iron, meteoric iron or steel |
| Steel | steel |

The hammer and chisel need not be the same metal, and they are not consumed — each craft costs
8 durability off both.

## Use

1. Find a log that is oozing resin — vanilla pine and acacia grow these.
2. Right-click the **side** of that log with a tap in hand. The tap goes onto the trunk instead
   of the log's usual resin-harvesting interaction.
3. Right-click the tap with an empty wooden bucket to hang it.
4. Wait. Looking at the tap shows how many litres have collected and the current season's yield.
5. Right-click with an empty hand to take the bucket back, along with one resin per litre.

Breaking a tap that has a bucket on it drops both the bucket and whatever sap it held.

A tree accepts at most **two taps**. Connected trunk blocks are found through the tree felling
group the game already assigns to grown trees, so the limit is per tree, not per resin patch.

## Collection rate

Base rate is 0.5 litres per in-game hour into a 10 litre bucket, scaled by the season the game
calendar reports at the tap's position (hemisphere-aware):

| Season | Yield | In-game hours to fill |
| ------ | ----- | --------------------- |
| Spring | 1.5x  | ~13 (about half a day) |
| Summer | 1.0x  | 20 |
| Autumn | 0.5x  | 40 |
| Winter | 0.25x | 80 (about 3.3 days) |

## Building and deploying

    ./deploy.sh

Builds the mod, packages `SapTap-<version>.zip` (version read from `modinfo.json`) and moves it
into `~/.config/VintagestoryData/Mods`, removing older SapTap zips first.

`SapTap.csproj` points at the game install through `VINTAGE_STORY_PATH`.

## Layout

| Path | What it is |
| ---- | ---------- |
| `src/BlockSapTap.cs` | Placement rules, tree/tap counting, selection boxes, interaction help |
| `src/BlockEntitySapTap.cs` | Sap accumulation, bucket handling, bucket mesh |
| `assets/saptap/shapes/block/saptap.json` | The spile, authored for the `north` variant |
| `assets/saptap/blocktypes/saptap.json` | Metal and side variants, shape rotation, transforms |
| `assets/saptap/recipes/grid/saptap.json` | The two grid recipes |
| `assets/saptap/lang/en.json` | Names and messages |

### Orientation convention

The `side` variant names the direction from the tap **towards** the tree. The shape is drawn for
`north` (bark plane at z=0, tube pointing south) and rotated with `rotateY` 0/90/180/270 for
north/west/south/east — the same mapping vanilla's toolrack uses, where `rotateY` +90 takes
south to east to north to west.

## Ideas not done yet

- [ ] Render the sap level rising inside the hanging bucket
- [ ] A proper sap liquid instead of handing out resin items
- [ ] Dripping particles at the drip lip
- [ ] Tree health: heavily tapped trees yielding less over time
