# Bald Bot
A self-playing Super Auto Pets AI

![image](https://user-images.githubusercontent.com/3793509/180692239-e722d01b-7dd9-412a-9cae-97b5a4d0487b.png)

## Installation
1. Download and install [MelonLoader v0.5.4 Open-Beta](https://github.com/LavaGang/MelonLoader/releases/tag/v0.5.4) into the Super Auto Pets directory
2. Copy  [ClassLibrary1.dll](https://github.com/Hollings/BaldBot/releases) into the Super Auto Pets `Mods` directory
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
