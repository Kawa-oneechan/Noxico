var myBoard = PickBoard(BoardType.Wild, GetBiomeByName("Woods"), -1);
myBoard.Name = "Chelsie's Lair";
myBoard.ID = "chelsielair";
MakeBoardTarget(myBoard);
myBoard.Clear();

//Repeat for tighter packing.
for (var i = 0; i < 8; i++) {
	myBoard.AddClutter(12,11,20,18);
	myBoard.AddClutter(20, 5,60,15);
	myBoard.AddClutter(57,23,67,36);
	myBoard.AddClutter(48,34,64,45);
}
for (var i = 0; i < 5; i++)
	myBoard.AddClutter(33,36,48,42);
for (var i = 0; i < 3; i++) {
	myBoard.AddClutter(15, 7,20,11);
	myBoard.AddClutter(20,15,30,17);
	myBoard.AddClutter(60, 8,65,23);
	myBoard.AddClutter(64,34,68,43);
}

myBoard.MergeBitmap("missions\\chelsie\\lairhouse.png");

var Chelsie = new BoardChar(Character.GetUnique("chelsie"));
Chelsie.XPosition = 50;
Chelsie.YPosition = 22;
myBoard.Entities.Add(Chelsie);
Chelsie.ParentBoard = myBoard;
