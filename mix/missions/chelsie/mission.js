var myBoard = AddBoard("ChelsieLair");
myBoard.Name = "Chelsie's Lair";
MakeBoardTarget(myBoard);
MakeBoardKnown(myBoard);
myBoard.Clear(7);

var tree = new Tile();
tree.Character = '\u2660';
tree.Foreground = Color.FromArgb(36, 68, 40);
tree.Background = Color.FromArgb(83, 133, 46);
tree.CanBurn = true;
tree.Wall = true;

var lawn = new Tile();
lawn.Character = ' ';
lawn.Background = Color.FromArgb(146, 182, 131);
lawn.CanBurn = true;
lawn.Wall = false;

myBoard.Line(0, 0, 10, 5, "return tree.Noise()"); //using evalstring
myBoard.Line(0, 5, 10,10, tree); //using Tile
myBoard.Line(0,10, 10,15, tree, false); //same, but without calling Noise.

myBoard.MergeBitmap("missions/chelsie/lairhouse.png");

myBoard.Replace("return (tile.Character == 'X' && tile.Foreground.R == 128)", "return tree.Noise()");
myBoard.Replace("return (tile.Character == 'X' && tile.Foreground.R == 64)", "return lawn.Noise()");

myBoard.DumpToHTML("chel");
