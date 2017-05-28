if (realm ~= "Nox") then return end

local tentaBoard = PickBoard(BoardType.Wild, GetBiomeByName("Woods"), -1);

tentaBoard.Drain()
tentaBoard.Name = "tentacle forest"
tentaBoard.ID = "tentacleforest"
tentaBoard.BoardType = BoardType.Special
tentaBoard.RemoveToken("encounters")
tentaBoard.AddToken("encounters").AddToken("stock")
MakeBoardTarget(tentaBoard)

tentaBoard.MergeBitmap("missions\\consentaclepit\\thepit.png", "missions\\consentaclepit\\thepit.txt");