if (Realm ~= "Nox") then return end

local jadaBoard = Board.PickBoard(BoardType.Wild, GetBiomeByName("Woods"), -1);

jadaBoard.Drain()
jadaBoard.Name = "Jadah's Camp"
jadaBoard.ID = "jadahlair"
jadaBoard.Music = "set://Home"
jadaBoard.BoardType = BoardType.Special
jadaBoard.RemoveToken("encounters")
jadaBoard.AddToken("encounters").AddToken("stock")
jadaBoard.MakeTarget()

jadaBoard.MergeBitmap("lairhouse.png", "lairhouse.tml");

--local Jadah = BoardChar(Character.GetUnique("jadah"))
--Jadah.XPosition = 36;
--Jadah.YPosition = 20;
--jadaBoard.Entities.Add(Jadah);
--Jadah.ParentBoard = jadaBoard;

local bed = jadaBoard.PlaceEntity(Clutter(), 53, 27)
bed.Glyph = 0x147
bed.ForegroundColor = Color.Black
bed.BackgroundColor = Color.FromArgb(86, 63, 44)
bed.ID = "Bed_Jadah"
bed.Name = "Bed"
bed.Description = "This is Jadah's bed. Position yourself over it and press Enter to use it."

local table = jadaBoard.PlaceEntity(Clutter(), 50, 22)
table.Glyph = 0x148
table.ForegroundColor = Color.Black
table.BackgroundColor = Color.FromArgb(86, 63, 44)
table.ID = "Table_Jadah"
table.Name = "Table"
table.Description = "This is Jadah's table."

-- needs more furniture
-- todo 0x149 seat
-- todo 0x14a chest
-- todo 0x14b closet
