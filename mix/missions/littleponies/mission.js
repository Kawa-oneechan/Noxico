/*
var myBoard = AddBoard("Ponyville");
myBoard.Name = "Pony village";
MakeBoardTarget(myBoard);
MakeBoardKnown(myBoard);
myBoard.Clear(7);
myBoard.MakeTown("town", ); //use ponyville set later on
var myChar = myBoard.PickBoardChar(Gender.Female);
print("Ponyville: picked " + myChar.Character.Name + " to replace.");
var oldName = myChar.Character.Name;
myChar.Character = Character.GetUnique("PonyLeader");
myChar.AdjustView();
myChar.ID = myChar.Character.Name.ToID();
myChar.ReassignStuff(oldName);
myBoard.DumpToHTML("ponyville");
*/

var expectation = ExpectTown("Pony village", -1);
expectation.Culture = "equestrian";
//expectation.BuildingSet = "ponyville";
expectation.Characters.Add("unique=PonyLeader");