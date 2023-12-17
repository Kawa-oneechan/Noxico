TestArena = {
-- env.TestArena.Name, env.TestArena.BioGender, env.TestArena.IdentifyAs, env.TestArena.Preference, env.TestArena.Bodyplan, new Dictionary<string, string>(), env.TestArena.BonusTrait
	Name = "Testy McTestface",
	BioGender = Gender.Male,
	IdentifyAs = Gender.Male,
	Preference = 0,
	Bodyplan = "felin",
	BonusTrait = "charismatic",
	ArenaWidth = 20,
	ArenaHeight = 20
}

function BuildTestArena(center)
	print ("lol")
	center.Clear("Grassland");
	-- center.GenerateTown(false, false);
	-- center.MergeBitmap("thepit.png", "thepit.tml");
	center.PlaceCharacter(Character.GetUnique("tentaclesan"), 4, 4)

	-- local beastie = Character.Generate("bear", Gender.RollDice);
	-- local beastebc = center.PlaceCharacter(beastie, 35, 10);
	-- beastie.AddToken("burning");

	--[[
	local hostile = Character.Generate("imp", Gender.RollDice);
	local hostilebc = center.PlaceCharacter(hostile, 35, 10);
	hostile.AddToken("team", 2, "");
	hostile.AddToken("teambehavior");

	local neutral = Character.Generate("kitsune", Gender.RollDice);
	local neutralbc = center.PlaceCharacter(neutral, 45, 10);
	neutral.AddToken("team", 0, "");
	neutral.AddToken("teambehavior");

	local guard = Character.Generate("naga", Gender.RollDice);
	local guardbc = center.PlaceCharacter(guard, 60, 10);
	guard.AddToken("team", 4, "");
	guard.AddToken("teambehavior");
	]]--

end
