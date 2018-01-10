local uncountable = {
	"money",
	"information",
	"fish",
	"deer",
	"tuna",
	"crosshairs",
	--  Japanese 
	"yaki",
	"sheep",
	"ninja",
	"ronin",
	"shito",
	"shuriken",
	"tengu",
	"sentai",
	"ki-rin",
	"yoki",
	"kunai",
}
local irregular = {
	ox = "!oxen",
	VAX = "!vaxen",
	octopus = "octopuses",
	person = "people",
	pegasus = "pegasori",
	stylus = "styli",
	goose = "geese",
	mouse = "mice",
	louse = "lice",
	human = "humans",
	man = "men",
	die = "dice",
	staff = "staves",
	foot = "feet",
	coccus = "cocci",
	convolvulus = "convolvuli",
	arsis = "arses",
	alula = "alulæ",
	alga = "algæ",
	larva = "larvæ",
	naga = "nagæ",
	ida = "idæ",
	fungus = "fungi",
	djinni = "djinn",
	mumak = "mumakil",
	nemesis = "nemeses",
	child = "children",
	--  Tolkien-specific 
	elf = "elves",
	dwarf = "dwarves",
}
local regular = {
	rtex = "rtices",
	cubus = "cubi",
	ooth = "eeth",
	oof = "ooves",
	fe = "ves",
	--  Specify sus and lus instead of just us because house -> houses -> hous 
	sus = "suses",
	lus = "luses",
	bus = "buses",
	cus = "ci",
	se = "ses",
	ch = "ches",
	oy = "oys",
	x = "xes",
	y = "ies",
}

function Pluralize(singular)
	local of = singular:find(" of ")
	if (of) then
		local key = singular:sub(1, of - 1)
		local ofWhat = singular:sub(of)
		return Pluralize(key) .. ofWhat
	end

	for _, value in pairs(uncountable) do
		if value == singular then
			return singular
		end
	end


	for left,right in pairs(irregular) do
		if right:sub(1, 1) == "!" then
			if singular == left then
				return right:sub(1)
			end
		end

		if singular:sub(-(left:len())) == left then
			return singular:sub(1, -(left:len()) - 1) .. right
		end
	end
	
	for left,right in pairs(regular) do
		if singular:sub(-(left:len())) == left then
			return singular:sub(1, -(left:len()) - 1) .. right
		end
	end

	return singular .. "s"
end

function Singularize(plural)
	local of = plural:find(" of ")
	if (of) then
		local key = plural:sub(1, of - 1)
		local ofWhat = plural:sub(of)
		return Singularize(key) .. ofWhat
	end

	for _, value in pairs(uncountable) do
		if value == plural then
			return plural
		end
	end

	for left,right in pairs(irregular) do
		if right:sub(1, 1) == "!" then
			if plural == right:sub(2) then
				return left
			end
		end

		if plural:sub(-(right:len())) == right then
			return plural:sub(1, -(right:len()) - 1) .. left
		end
	end
	
	for left,right in pairs(regular) do
		if plural:sub(-(right:len())) == right then
			return plural:sub(1, -(right:len()) - 1) .. left
		end
	end

	return plural:sub(1, -2)
end
