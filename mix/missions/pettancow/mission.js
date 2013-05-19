/*
var myBoard = PickBoard(BoardType.Town, -1);
myBoard.DumpToHtml("PettancowPre");
var myChar = myBoard.PickBoardChar(Gender.Female);

print("Picked " + myChar.Character.Name + " to replace.");

//  Remove any spouse the character we picked might have.
var spouse = myChar.Character.GetSpouse();
if (spouse != null)
{
	print("Character has a spouse to remove; " + spouse.Name + ".");
	myBoard.Entities.Remove(spouse.GetBoardChar());
}

//Not sure if this works. Last time I tried the wardrobe was already empty.
var spousesStuff = myBoard.GetEntitiesWith("wardrobe_" + spouse.Name.ToID(), false);
for (var i = 0; i < spousesStuff.length; i++)
	spousesStuff[i].GetToken("contents").Tokens.Clear();

// Replace this character with Pettancow.
var oldName = myChar.Character.Name;
myChar.Character = Character.GetUnique("pettancow");
myChar.AdjustView();
myChar.ID = myChar.Character.Name.ToID();
myChar.ReassignStuff(oldName);

myBoard.Type = BoardType.Special;
print("Placed Pettancow on board " + myBoard.ID + ".");
myBoard.DumpToHtml("PettancowPost");
*/

/* Should probably ensure there's a boob growth item
 * somewhere in the world, done in a similar fashion.
 */
