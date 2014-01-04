print("blah");
if (realm != Realms.Nox)
{
	print("ack!");
	return;
}
print("okay");

var myBoard, town;
while(1)
{
	var t = PickBoard(BoardType.Town, -1, -1);
	var b = GetBoard(t.ToEast);
	print("b.GetToken('Biome').Value == " + b.GetToken("biome").Value);
	if (b == null || b.BoardType == BoardType.Town || b.GetToken("biome").Value == 0)
		b = GetBoard(t.ToWest);
	if (b == null || b.BoardType == BoardType.Town || b.GetToken("biome").Value == 0)
		b = GetBoard(t.ToNorth);
	if (b == null || b.BoardType == BoardType.Town || b.GetToken("biome").Value == 0)
		b = GetBoard(t.ToSouth);
	if (b == null || b.BoardType == BoardType.Town || b.GetToken("biome").Value == 0)
		continue;
	myBoard = b;
	MakeBoardTarget(t);
	town = t;
	break;
}

myBoard.Drain();
myBoard.Name = "Home";
myBoard.ID = "home";
myBoard.BoardType = BoardType.Special;
myBoard.RemoveToken("encounters"); myBoard.AddToken("encounters").AddToken("stock");
MakeBoardTarget(myBoard);

myBoard.MergeBitmap("missions\\playerbase\\lv0.png");

//Set up player's bedroom
var bed = new Clutter();
bed.AsciiChar = 0x147;
bed.XPosition = 31;
bed.YPosition = 16;
bed.ForegroundColor = Color.Black;
bed.BackgroundColor = Color.FromArgb(86, 63, 44);
bed.ID = "Bed_playerRespawn";
bed.Name = "Bed";
bed.Description = "This is your bed. Position yourself over it and press Enter to use it.";
bed.ParentBoard = myBoard;
myBoard.Entities.Add(bed);
//wardrobe?

//Set up shrine
var candle = new Clutter();
candle.AsciiChar = 0xA1;
candle.XPosition = 33;
candle.YPosition = 19;
candle.ForegroundColor = Color.White;
candle.BackgroundColor = Color.FromArgb(86, 63, 44);
candle.ID = "lifeCandle";
candle.Name = "Life candle";
candle.Description = "If you can read this, the candle wasn't reset.";
candle.ParentBoard = myBoard;
myBoard.Entities.Add(candle);

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

/*
var testDungeon = new Warp();
testDungeon.TargetBoard = -1;
testDungeon.ID = myBoard.ID + "_Dungeon";
testDungeon.XPosition = 40;
testDungeon.YPosition = 20;
myBoard.Warps.Add(testDungeon);
myBoard.SetTile(20, 40, '>', Color.Silver, Color.Black);
*/

//Set up carpenter
var carpenter = town.PickBoardChar(Gender.Male);
while (carpenter.Character.HasToken("role"))
	carpenter = town.PickBoardChar(Gender.Male);
carpenter.Character.AddToken("role").AddToken("vendor").AddToken("class", 0, "carpenter");

//Add clutter here.
