if (Realm ~= "Nox") then return end
local myBoard, town
while 1 do
	local t = Board.PickBoard(BoardType.Town, -1, -1)
	local b = Board.GetBoardByNumber(t.ToEast)
	print("b.GetToken('Biome').Value == " .. b.GetToken("biome").Value)
	if (b == nil) or (b.BoardType == BoardType.Town) or (b.GetToken("biome").Value == 0) then
		b = Board.GetBoardByNumber(t.ToWest)
	end
	if (b == nil) or (b.BoardType == BoardType.Town) or (b.GetToken("biome").Value == 0) then
		b = Board.GetBoardByNumber(t.ToNorth)
	end
	if (b == nil) or (b.BoardType == BoardType.Town) or (b.GetToken("biome").Value == 0) then
		b = Board.GetBoardByNumber(t.ToSouth)
	end
	if (not (b == nil) or (b.BoardType == BoardType.Town) or (b.GetToken("biome").Value == 0)) then
		myBoard = b
		t.MakeTarget()
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
myBoard.MakeTarget()

myBoard.MergeBitmap("lv0.png", "lv0.txt")

local bed = myBoard.PlaceEntity(Clutter(), 31, 16)
bed.Glyph = 0x147
bed.ForegroundColor = Color.Black
bed.BackgroundColor = Color.FromArgb(86, 63, 44)
bed.ID = "Bed_playerRespawn"
bed.Name = "Bed"
bed.Description = "This is your bed. Position yourself over it and press Enter to use it."

local candle = myBoard.PlaceEntity(Clutter(), 33, 19)
candle.Glyph = 0xAD
candle.ForegroundColor = Color.White
candle.BackgroundColor = Color.FromArgb(86, 63, 44)
candle.ID = "lifeCandle"
candle.Name = "Life candle"
candle.Description = "If you can read this, the candle wasn't reset."

print("Carpenter time!")
local carpenter = town.PickBoardChar(Gender.Male)
print("Considering " .. carpenter.Character.Name)
while (carpenter.Character.HasToken("role")) do
	carpenter = town.PickBoardChar(Gender.Male)
	print("Reconsidering " .. carpenter.Character.Name)
end
carpenter.Character.AddToken("role").AddToken("vendor").AddToken("class", 0, "carpenter")
print("we're done here.")
 
-- add pettancow here for simplicity
local pettancow = myBoard.PlaceCharacter(Character.GetUnique("pettancow"), 57, 16)
pettancow.AdjustView();
