-- Example script to place the character defined in uniques.tml.patch somewhere nice.

-- Break early if we're doing Seradevar, which is not a nice place.
if (Realm ~= "Nox") then return end

-- We can do simple debug output to console.
print("Hello, world!");

-- Pick out a Town board. Don't care about the biome, or max water.
local urtaBoard = Board.PickBoard(BoardType.Town, -1, -1);

-- Generate our character and incorporate them.
local urta = BoardChar(Character.GetUnique("urta"))

-- Set them up in the town we picked earlier.
-- (TODO: PlaceCharacter function? -- Kawa)
urta.XPosition = 40;
urta.YPosition = 25;
urtaBoard.Entities.Add(urta);
urta.ParentBoard = urtaBoard;
