if (Realm ~= "Nox") then return end

local tentaBoard = Board.PickBoard(BoardType.Wild, GetBiomeByName("Woods"), -1);

tentaBoard.Drain()
tentaBoard.Name = "tentacle forest"
tentaBoard.ID = "tentacleforest"
tentaBoard.BoardType = BoardType.Special
tentaBoard.RemoveToken("encounters")
tentaBoard.AddToken("encounters").AddToken("stock")
tentaBoard.MakeTarget()

tentaBoard.MergeBitmap("thepit.png", "thepit.tml");
