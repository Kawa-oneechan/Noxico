if (Realm ~= "Nox") then return end

local jadaBoard = Board.PickBoard(BoardType.Wild, GetBiomeByName("Woods"), -1);

jadaBoard.Name = "Jadah's Camp"
jadaBoard.ID = "jadahlair"
jadaBoard.Music = "set://Home"
jadaBoard.BoardType = BoardType.Special
jadaBoard.RemoveToken("encounters")
jadaBoard.AddToken("encounters").AddToken("stock")
jadaBoard.MakeTarget()

jadaBoard.MergeBitmap("lairhouse.png", "lairhouse.tml");

-- needs more furniture
-- todo 0x149 seat
-- todo 0x14a chest
-- todo 0x14b closet
