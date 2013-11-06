var myBoard = PickBoard(BoardType.Town, -1, -1);
myBoard.DumpToHtml("ponyville_pre");
myBoard.Clear();
myBoard.ID = "Ponyville";
MakeTown(myBoard, "equestrian");
MakeBoardTarget(myBoard);
var myChar = myBoard.PickBoardChar(Gender.Female);
print("Picked " + myChar.Character.Name + " to replace with pony mayor.");
var spouse = myChar.Character.Spouse;
if (spouse != null) {
	print("Character has a spouse to remove; " + spouse.Name + ".");
	myBoard.Entities.Remove(spouse.GetBoardChar());
}
var spousesStuff = myBoard.GetEntitiesWith("wardrobe_" + spouse.Name.ToID(), false);
for (var i = 0; i < spousesStuff.length; i++)
	spousesStuff[i].GetToken("contents").Tokens.Clear();
var oldName = myChar.Character.Name;
myChar.Character = Character.GetUnique("ponyleader");
myChar.AdjustView();
myChar.ID = myChar.Character.Name.ToID();
myChar.ReassignStuff(oldName);
myBoard.Type = BoardType.Special;
print("Turned " + myBoard.ID + " into pony village.");
myBoard.DumpToHtml("ponyville_post");
