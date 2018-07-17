TestArena = {
-- env.TestArena.Name, env.TestArena.BioGender, env.TestArena.IdentifyAs, env.TestArena.Preference, env.TestArena.Bodyplan, new Dictionary<string, string>(), env.TestArena.BonusTrait
	Name = "Testy McTestface",
	BioGender = Gender.Herm,
	IdentifyAs = Gender.Female,
	Preference = 0,
	Bodyplan = "felin",
	BonusTrait = "charismatic",
	ArenaWidth = 80,
	ArenaHeight = 80
}

function BuildTestArena(center)
	print ("lol")
	center.Clear("Grassland");
	local vendor = Character.Generate("felin", Gender.RollDice);
	local vendorbc = center.PlaceCharacter(vendor, 4, 4);
	vendor.AddToken("role").AddToken("vendor").AddToken("class", 0, "clothier");
	vendor.GetToken("money").Value = 1000;
	vendorbc.RestockVendor();
end
