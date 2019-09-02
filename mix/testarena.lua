TestArena = {
-- env.TestArena.Name, env.TestArena.BioGender, env.TestArena.IdentifyAs, env.TestArena.Preference, env.TestArena.Bodyplan, new Dictionary<string, string>(), env.TestArena.BonusTrait
	Name = "Testy McTestface",
	BioGender = Gender.Herm,
	IdentifyAs = Gender.Female,
	Preference = 0,
	Bodyplan = "felin",
	BonusTrait = "charismatic",
	ArenaWidth = 64,
	ArenaHeight = 16
}

function BuildTestArena(center)
	print ("lol")
	center.Clear("Grassland");
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
