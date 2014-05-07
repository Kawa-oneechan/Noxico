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

myBoard.MergeBitmap("missions\\playerbase\\lv0.png", "missions\\playerbase\\lv0.txt");

//Set up player's bedroom
var bed = new Clutter();
bed.Glyph = 0x147;
bed.UnicodeCharacter = 0x398;
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
candle.Glyph = 0xAD;
candle.UnicodeCharacter = 0xA1;
candle.XPosition = 33;
candle.YPosition = 19;
candle.ForegroundColor = Color.White;
candle.BackgroundColor = Color.FromArgb(86, 63, 44);
candle.ID = "lifeCandle";
candle.Name = "Life candle";
candle.Description = "If you can read this, the candle wasn't reset.";
candle.ParentBoard = myBoard;
myBoard.Entities.Add(candle);

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
