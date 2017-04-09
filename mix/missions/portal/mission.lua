--[[
var vague = new Tile();
vague.Character = 0xC5;
vague.Foreground = Color.Black;
vague.Background = Color.Black;
var stone = new Tile();
stone.Character = 0xC5;
stone.Foreground = Color.Black;
stone.Background = Color.FromCSS("#4B3027");
var wall = new Tile();
wall.Character = 0xC5;
wall.Foreground = Color.FromCSS("#58343E")
wall.Background = Color.FromCSS("#B34161");
wall.Wall = true;

function BuildPlatform(board, number, vague, stone, wall)
{
	board.MergeBitmap("missions\\portal\\platform" + number + ".png");
	board.Replace("return (tile.Character == 0x58 && tile.Foreground.R == 64)", "return vague");
	board.Replace("return (tile.Character == 0x58 && tile.Foreground.R == 128)", "return stone");
	board.Replace("return (tile.Character == 0x58 && tile.Foreground.R == 255)", "return wall");
}

if (realm == Realms.Nox)
{
	print ("Creating portal on Nox side...");

	var myBoard = PickBoard(BoardType.Wild, -1, -1);
	myBoard.Drain();
	myBoard.Name = "Demon Portal, Nox Side";
	myBoard.ID = "demonportal_nox";
	BuildPlatform(myBoard, 1, vague, stone, wall);
	var portalC = Character.GetUnique("demonportal_nox");
	var portalE = new BoardChar(portalC);
	portalE.XPosition = 35;
	portalE.YPosition = 22;
	portalE.AssignScripts("demonportal_nox");
	portalE.ReassignScripts();
	myBoard.Entities.Add(portalE);
	portalE.ParentBoard = myBoard;
	
	//MakeBoardTarget(myBoard); //<-- DEBUG! Should use LearnUnknownLocation("Demon Portal, Nox Side"); in scene to unlock.
}
else
{
	print ("Creating portal on Seradevari side...");

	myBoard = PickBoard(BoardType.Wild, -1, -1);
	myBoard.Drain();
	myBoard.Name = "Demon Portal, Sera Side";
	myBoard.ID = "demonportal_sera";
	myBoard.AllowTravel = false;
	BuildPlatform(myBoard, 2, vague, stone, wall);

	var portalC = Character.GetUnique("demonportal_sera");
	var portalE = new BoardChar(portalC);
	portalE.XPosition = 35;
	portalE.YPosition = 22;
	portalE.AssignScripts("demonportal_sera");
	portalE.ReassignScripts();
	myBoard.Entities.Add(portalE);
	portalE.ParentBoard = myBoard;

	print ("Connecting portals...");
	var otherBoard = FindBoardByID("demonportal_nox");
	var otherPortalE = otherBoard.GetFirstBoardCharWith("Demon_Portal");
	var otherPortalC = otherPortalE.Character;
	otherPortalC.AddToken("otherSide", myBoard.BoardNum);
	portalC.AddToken("otherSide", otherBoard.BoardNum);

	//MakeBoardTarget(myBoard); //<-- ALSO DEBUG!
}

/*
var vague = new Tile();
vague.Character = ' ';
vague.Foreground = Color.Black;
vague.Background = Color.FromName("Firebrick");
vague.Water = true;
*/
]]--
