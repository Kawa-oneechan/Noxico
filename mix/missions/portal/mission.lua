
BuildPlatform = function (board, number, vague, stone, wall)
	board.MergeBitmap("missions\\portal\\platform" .. number .. ".png","missions\\portal\\platform1.txt");
	--public void Replace(Func<Tile, int, int, bool> judge, Func<Tile, int, int, Tile> brush) ?
	--board.Replace("return (tile.Character == 0x58 && tile.Foreground.R == 64)", "return vague");
	--board.Replace("return (tile.Character == 0x58 && tile.Foreground.R == 128)", "return stone");
	--board.Replace("return (tile.Character == 0x58 && tile.Foreground.R == 255)", "return wall");
end

if (realm == "Nox") then
	print ("Creating portal on Nox side...");

	local myBoard = PickBoard(BoardType.Wild, -1, -1);
	myBoard.Drain();
	myBoard.Name = "Demon Portal, Nox Side";
	myBoard.ID = "demonportal_nox";
	BuildPlatform(myBoard, 1, vague, stone, wall);
	local portalE = BoardChar(Character.GetUnique("demonportal_nox"));
	local portalC = portalE.BoardChar
	portalE.XPosition = 35;
	portalE.YPosition = 22;
	portalE.AssignScripts("demonportal_nox");
	portalE.ReassignScripts();
	myBoard.Entities.Add(portalE);
	portalE.ParentBoard = myBoard;
	
	MakeBoardTarget(myBoard); -- DEBUG! Should use LearnUnknownLocation("Demon Portal, Nox Side"); in scene to unlock.
else
	print ("Creating portal on Seradevari side...");

	myBoard = PickBoard(BoardType.Wild, -1, -1);
	myBoard.Drain();
	myBoard.Name = "Demon Portal, Sera Side";
	myBoard.ID = "demonportal_sera";
	--myBoard.AllowTravel = false;
	BuildPlatform(myBoard, 2, vague, stone, wall);

	local portalE = BoardChar(Character.GetUnique("demonportal_sera"));
	local portalC = portalE.Character
	
	portalE.XPosition = 35;
	portalE.YPosition = 22;
	portalE.AssignScripts("demonportal_sera");
	portalE.ReassignScripts();
	myBoard.Entities.Add(portalE);
	portalE.ParentBoard = myBoard;

	print ("Connecting portals...");
	local otherBoard = FindBoardByID("demonportal_nox");
	local otherPortalE = otherBoard.GetFirstBoardCharWith("Demon_Portal");
	local otherPortalC = otherPortalE.Character;
	otherPortalC.AddToken("otherSide", myBoard.BoardNum);
	portalC.AddToken("otherSide", otherBoard.BoardNum);

	MakeBoardTarget(myBoard); -- ALSO DEBUG!
end
