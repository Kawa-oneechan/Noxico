if (realm ~= "Nox") then return end
local myBoard, town
while 1 do
	local t = PickBoard(BoardType.Town, -1, -1)
	local b = GetBoard(t.ToEast)
	print("b.GetToken('Biome').Value == " .. b.GetToken("biome").Value)
	if (b == nil) or (b.BoardType == BoardType.Town) or (b.GetToken("biome").Value == 0) then
		b = GetBoard(t.ToWest)
	end
	if (b == nil) or (b.BoardType == BoardType.Town) or (b.GetToken("biome").Value == 0) then
		b = GetBoard(t.ToNorth)
	end
	if (b == nil) or (b.BoardType == BoardType.Town) or (b.GetToken("biome").Value == 0) then
		b = GetBoard(t.ToSouth)
	end
	if (not (b == nil) or (b.BoardType == BoardType.Town) or (b.GetToken("biome").Value == 0)) then
		myBoard = b
		MakeBoardTarget(t)
		town = t
		break
	end
end

myBoard.Drain()
myBoard.Name = "Home"
myBoard.ID = "home"
myBoard.Music = "set://Home"
myBoard.BoardType = BoardType.Special
myBoard.RemoveToken("encounters")
myBoard.AddToken("encounters").AddToken("stock")
MakeBoardTarget(myBoard)

myBoard.MergeBitmap("missions\\playerbase\\lv0.png", "missions\\playerbase\\lv0.txt")

local bed = Clutter()
bed.Glyph = 0x147
bed.XPosition = 31
bed.YPosition = 16
bed.ForegroundColor = Color.Black
bed.BackgroundColor = Color.FromArgb(86, 63, 44)
bed.ID = "Bed_playerRespawn"
bed.Name = "Bed"
bed.Description = "This is your bed. Position yourself over it and press Enter to use it."
bed.ParentBoard = myBoard
myBoard.Entities.Add(bed)

local candle = Clutter()
candle.Glyph = 0xAD
candle.XPosition = 33
candle.YPosition = 19
candle.ForegroundColor = Color.White
candle.BackgroundColor = Color.FromArgb(86, 63, 44)
candle.ID = "lifeCandle"
candle.Name = "Life candle"
candle.Description = "If you can read this, the candle wasn't reset."
candle.ParentBoard = myBoard
myBoard.Entities.Add(candle)

print("Carpenter time!")
local carpenter = town.PickBoardChar(Gender.Male)
print("Considering " .. carpenter.Character.Name)
while (carpenter.Character.HasToken("role")) do
	carpenter = town.PickBoardChar(Gender.Male)
	print("Reconsidering " .. carpenter.Character.Name)
end
carpenter.Character.AddToken("role").AddToken("vendor").AddToken("class", 0, "carpenter")
print("we're done here.")
