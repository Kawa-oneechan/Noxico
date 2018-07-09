-- Example script to place the character defined in uniques.tml.patch somewhere nice.

-- Break early if we're doing Seradevar, which is not a nice place.
if (Realm ~= "Nox") then return end

-- We can do simple debug output to console.
print("Hello, world!");

-- Pick out a Town board. Don't care about the biome, or max water.
local urtaBoard = Board.PickBoard(BoardType.Town, -1, -1);
-- Generate our character and incorporate them.
urtaBoard.PlaceCharacter(Character.GetUnique("urta"), 40, 25);
-- Maps are 80x50, so this is right in the middle, damn the barriers.
