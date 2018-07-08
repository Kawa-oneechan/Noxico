if (Realm ~= "Nox") then return end
local myBoard = Board.PickBoard(BoardType.Town, -1, -1)
local oldName = myBoard.Name .. " (" .. myBoard.ID .. ")"
myBoard.Clear()
myBoard.ID = "Ponyville"
MakeTown(myBoard, "mlp_equestrian")
myBoard.MakeTarget()

local myChar = myBoard.PickBoardChar(Gender.Female)
-- print("Picked " .. myChar.Character.Name .. " to replace with pony mayor.")
local spouse = myChar.Character.Spouse
if (spouse) then
	-- print("Character has a spouse to remove; " .. spouse.Name .. ".")
	myBoard.Entities.Remove(spouse.BoardChar)
	local spousesStuff =  myBoard.GetEntitiesWith("wardrobe_" .. spouse.Name.ToID(), false)
	for i = 0, spousesStuff.Count-1 do
		spousesStuff[i].Token.GetToken("contents").Tokens.Clear()
	end
end
local oldCharName = myChar.Character.Name
myChar.Character = Character.GetUnique("mlp_ponyleader")
myChar.AdjustView()
myChar.ID = myChar.Character.Name.ToID()
myChar.ReassignStuff(oldCharName)

myBoard.BoardType = BoardType.Special;
-- print("Turned " .. oldName .. " into pony village " .. myBoard.Name .. ".");
