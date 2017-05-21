-- some code here to put Urta somewhere sensible.
if (realm ~= "Nox") then return end

local urtaBoard = PickBoard(BoardType.Town, -1, -1);

local urta = BoardChar(Character.GetUnique("urta"))
urta.XPosition = 40;
urta.YPosition = 25;
urtaBoard.Entities.Add(urta);
urta.ParentBoard = urtaBoard;
