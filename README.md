# SapTap Mod for Vintage Story

A realistic sap collection mod that adds craftable sap taps and bucket collection system for harvesting sap from pine trees.

## Features

- **Craftable Sap Taps**: Made from 3 copper ingots
- **Realistic Collection**: Attach empty buckets to taps and wait for sap to collect
- **Tree Limitations**: Maximum 2 taps per tree to prevent over-harvesting
- **Seasonal Variation**: Collection rates vary by season
  - Spring: 1.5x collection rate (best time)
  - Summer: 1.0x normal rate
  - Fall: 0.5x reduced rate
  - Winter: 0.25x minimal rate
- **Pine Tree Requirement**: Taps can only be placed on pine tree logs

## How to Use

1. Craft a sap tap using 3 copper ingots in a crafting grid
2. Find a pine tree (grown or placed logs work)
3. Place the sap tap against the tree trunk
4. Right-click the tap with an empty bucket to attach it
5. Wait for the bucket to fill (takes about 1 in-game day at normal rate)
6. Right-click again to retrieve the filled bucket

## Collection Rate

- Base rate: 1 liter per in-game day (20 game hours)
- Full bucket: 10 liters total
- Time to fill: ~10 in-game days (varies by season)

## Installation

1. Build the mod using `dotnet build`
2. Copy the resulting zip file to your Vintage Story mods folder
3. Restart the game

## TODO / Future Development

- [ ] Add visible bucket rendering when attached to tap
- [ ] Implement tree health system (taps affect tree health)
- [ ] Add custom textures for the sap tap
- [ ] Create proper sap liquid item (currently uses resin placeholder)
- [ ] Add sound effects for bucket attachment/removal
- [ ] Add particle effects for dripping sap

## Technical Notes

- Uses BlockEntity for state persistence
- Implements seasonal modifiers from game calendar
- Tree tap counting searches connected log blocks
- Placement validation ensures only pine logs with proper conditions
