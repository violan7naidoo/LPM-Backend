# Game Configuration Files

This directory contains JSON configuration files for each slot game. Each game can have its own unique configuration.

## File Naming Convention

Configuration files should be named `{GameId}.json` where `GameId` is the identifier used to load the game (e.g., `SnowKingdom.json`).

## Configuration Structure

### Required Fields

- **gameName**: Display name of the game
- **numReels**: Number of reels (columns) in the game grid
- **numRows**: Number of rows in the game grid
- **wildSymbol**: String name of the wild symbol (must exist in `symbols`)
- **scatterSymbol**: String name of the scatter symbol (must exist in `symbols`)
- **freeSpinsAwarded**: Number of free spins awarded when scatter triggers
- **betAmounts**: Array of valid bet amounts
- **scatterPayout**: Dictionary mapping scatter count to multiplier (e.g., `{"3": 5, "4": 20}`)
- **symbols**: Dictionary of all symbols in the game (see below)
- **reelStrips**: Array of arrays, one per reel, containing symbol names in order
- **paylines**: Array of arrays, each inner array represents a payline with row indices for each reel

### Symbols Configuration

**IMPORTANT**: The `symbols` object can contain **ANY number of symbols**. There is no fixed limit. Each game can have:
- 5 symbols (fruit machine)
- 13 symbols (current SnowKingdom game)
- 20+ symbols (complex games)
- Any other number

Each symbol entry in the `symbols` object must have:
- **name**: The symbol identifier (must match the key and references in `reelStrips`)
- **payout**: Dictionary mapping count of symbols (3, 4, 5, 6, etc.) to payout multiplier
- **image**: Path to the symbol image (for frontend display)

### Example: Minimal Game (5 symbols)

```json
{
  "gameName": "FruitMachine",
  "numReels": 5,
  "numRows": 3,
  "wildSymbol": "WILD",
  "scatterSymbol": "SCATTER",
  "freeSpinsAwarded": 10,
  "betAmounts": [1, 2, 5],
  "scatterPayout": {
    "3": 5,
    "4": 15,
    "5": 50
  },
  "symbols": {
    "WILD": {
      "name": "WILD",
      "payout": {"3": 20, "4": 50, "5": 100},
      "image": "/images/symbols/Wild.png"
    },
    "SCATTER": {
      "name": "SCATTER",
      "payout": {},
      "image": "/images/symbols/Scatter.png"
    },
    "CHERRY": {
      "name": "CHERRY",
      "payout": {"3": 5, "4": 15, "5": 40},
      "image": "/images/symbols/Cherry.png"
    },
    "LEMON": {
      "name": "LEMON",
      "payout": {"3": 3, "4": 10, "5": 30},
      "image": "/images/symbols/Lemon.png"
    },
    "ORANGE": {
      "name": "ORANGE",
      "payout": {"3": 2, "4": 8, "5": 20},
      "image": "/images/symbols/Orange.png"
    }
  },
  "reelStrips": [
    ["CHERRY", "LEMON", "ORANGE", "CHERRY", "WILD"],
    ["LEMON", "ORANGE", "CHERRY", "LEMON", "SCATTER"],
    ["ORANGE", "CHERRY", "LEMON", "ORANGE", "CHERRY"],
    ["CHERRY", "ORANGE", "LEMON", "CHERRY", "ORANGE"],
    ["LEMON", "CHERRY", "ORANGE", "LEMON", "CHERRY"]
  ],
  "paylines": [
    [0, 0, 0, 0, 0],
    [1, 1, 1, 1, 1],
    [2, 2, 2, 2, 2]
  ]
}
```

## Validation Rules

The system automatically validates:
1. At least one symbol must be defined
2. Wild and scatter symbols must exist in the symbols dictionary
3. All symbols referenced in `reelStrips` must exist in the `symbols` dictionary
4. Grid dimensions must match the number of reels and rows specified

## Creating a New Game

1. Create a new JSON file: `{YourGameId}.json`
2. Copy the structure from `SnowKingdom.json` as a template
3. Modify the symbols, reel strips, paylines, and other settings
4. Ensure all symbol names in `reelStrips` match keys in the `symbols` object
5. The game will be automatically loaded when requested with that `GameId`

## Notes

- Symbol names are case-sensitive
- Payout tables can have any count values (3, 4, 5, 6, etc.) - not limited to specific counts
- Reel strips can be any length - they wrap around when generating the grid
- Each game is completely independent - you can have games with different symbol counts, grid sizes, and rules

