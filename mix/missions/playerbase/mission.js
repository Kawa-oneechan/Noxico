var myBoard, town;
while(1)
{
	var t = PickBoard(BoardType.Town, -1, -1);
	var b = GetBoard(t.ToEast);
	if (b == null || b.BoardType == BoardType.Town)
		b = GetBoard(t.ToWest);
	if (b == null || b.BoardType == BoardType.Town)
		b = GetBoard(t.ToNorth);
	if (b == null || b.BoardType == BoardType.Town)
		b = GetBoard(t.ToSouth);
	if (b == null || b.BoardType == BoardType.Town)
		continue;
	myBoard = b;
	MakeBoardTarget(t);
	town = t;
	break;
}

myBoard.Name = "Home";
myBoard.ID = "home";
myBoard.BoardType = BoardType.Special;
myBoard.RemoveToken("encounters"); myBoard.AddToken("encounters").AddToken("stock");
MakeBoardTarget(myBoard);

//Set up carpenter
var carpenter = town.PickBoardChar(Gender.Male);
while (carpenter.Character.HasToken("role"))
	carpenter = town.PickBoardChar(Gender.Male);
carpenter.Character.AddToken("role").AddToken("vendor").AddToken("class", 0, "carpenter");

myBoard.MergeBitmap("missions\\playerbase\\lv0.png");

var fence = new Tile();
fence.Foreground = Color.FromName("DarkGoldenrod");
fence.Background = Color.FromName("Auburn");
fence.Fence = true;
fence.Character = 0x125; myBoard.Line(50, 12, 60, 12, fence); myBoard.Line(50, 20, 60, 20, fence);
fence.Character = 0x124; myBoard.Line(50, 12, 50, 20, fence); myBoard.Line(60, 12, 60, 20, fence);
fence.Character = 0x120; myBoard.SetTile(50, 12, fence);
fence.Character = 0x121; myBoard.SetTile(60, 12, fence);
fence.Character = 0x122; myBoard.SetTile(50, 20, fence);
fence.Character = 0x123; myBoard.SetTile(60, 20, fence);
fence.Character = 0x20; fence.Fence = false; myBoard.SetTile(50, 16, fence);

var dirt = new Tile();
dirt.Background = Color.FromName("Auburn");
dirt.Foreground = Color.Black;
dirt.Character = 0x157;
for (var y = 13; y < 20; y++)
	myBoard.Line(51, y, 59, y, dirt);

//Add clutter here.
