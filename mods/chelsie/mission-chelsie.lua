if (Realm ~= "Nox") then return end

local chelBoard = Board.PickBoard(BoardType.Wild, GetBiomeByName("Woods"), -1);

chelBoard.Drain()
chelBoard.Name = "Chelsie's Lair"
chelBoard.ID = "chelsielair"
chelBoard.Music = "set://Home"
chelBoard.BoardType = BoardType.Special
chelBoard.RemoveToken("encounters")
chelBoard.AddToken("encounters").AddToken("stock")
chelBoard.MakeTarget()

chelBoard.MergeBitmap("lairhouse.png", "lairhouse.txt");

--local Chelsie = BoardChar(Character.GetUnique("chelsie"))
--Chelsie.XPosition = 36;
--Chelsie.YPosition = 20;
--chelBoard.Entities.Add(Chelsie);
--Chelsie.ParentBoard = chelBoard;

local bed = Clutter()
bed.Glyph = 0x147
bed.XPosition = 53
bed.YPosition = 27
bed.ForegroundColor = Color.Black
bed.BackgroundColor = Color.FromArgb(86, 63, 44)
bed.ID = "Bed_Chelsie"
bed.Name = "Bed"
bed.Description = "This is Chelsie's bed. Position yourself over it and press Enter to use it."
bed.ParentBoard = chelBoard
chelBoard.Entities.Add(bed)

local table = Clutter()
table.Glyph = 0x148
table.XPosition = 50
table.YPosition = 22
table.ForegroundColor = Color.Black
table.BackgroundColor = Color.FromArgb(86, 63, 44)
table.ID = "Table_Chelsie"
table.Name = "Table"
table.Description = "This is Chelsie's table."
table.ParentBoard = chelBoard
chelBoard.Entities.Add(table)

-- needs more furniture
-- todo 0x149 seat
-- todo 0x14a chest
-- todo 0x14b closet
