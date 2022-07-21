# Bald Bot
A self-playing Super Auto Pets AI

## Installation
1. Download and install [MelonLoader v0.5.4 Open-Beta](https://github.com/LavaGang/MelonLoader/releases/tag/v0.5.4) into the Super Auto Pets directory
2. Copy  `ClassLibrary1.dll` into the Super Auto Pets `Mods` directory
3. Run the game

## Usage
If the mod has been loaded, it will automatically perform actions during your turn. In its current state, the bot is dumb and will do actions in this order:
1. Merge owned pets
2. Buy the first pet if there is an empty slot
3. Upgrade pets if possible
4. Reroll and repeat steps 2 and 3 until money is gone
5. Click the "End Turn" button

## To Do
1. Keep a record of all enemy teams seen and use the data to make better buying and placement decisions
2. Skip battle sequence
3. Massive code cleanup