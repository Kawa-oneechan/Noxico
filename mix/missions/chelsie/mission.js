var myBoard = AddBoard("ChelsieLair");
myBoard.Name = "Chelsie's Lair";
MakeBoardTarget(myBoard);
MakeBoardKnown(myBoard);
myBoard.Clear("Woods");

//Repeat for tighter packing.
for (var i = 0; i < 8; i++) {
	myBoard.AddClutter(25, 1,36, 8);
	myBoard.AddClutter(34, 0,74, 4);
	myBoard.AddClutter(36, 5,43, 6);
	myBoard.AddClutter(67, 5,74,13);
}

myBoard.MergeBitmap("missions\\chelsie\\lairhouse.png");

var Chelsie = new BoardChar(Character.GetUnique("chelsie"));
Chelsie.XPosition = 64;
Chelsie.YPosition = 12;
myBoard.Entities.Add(Chelsie);
Chelsie.ParentBoard = myBoard;

myBoard.DumpToHtml("chel");
