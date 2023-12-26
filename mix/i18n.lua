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

function GetArticle(topic, capitalize)
	if StartsWithVowel(topic) then
		if capitalize then
			return "An"
		else
			return "an"
		end
	else
		if capitalize then
			return "A"
		else
			return "a"
		end
	end
end

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

function Possessive(subject)
	if subject == "it" then return "its" end
	if subject:sub(-1) == "s" then return subject .. "'" end
	return subject .. "'s"
end

function Ordinal(number)
	local i = tonumber(number)
	local dd = i % 10
	if (dd == 0) or (dd > 3) or ((i % 100) / 10 == 1) then return i .. "th" end
	if (dd == 1) then return i .. "st" end
	if (dd == 2) then return i .. "nd" end
	return i .. "rd"
end

-- NOTICE THIS: Lists and arrays passed from .Net are ZERO-INDEXED.
RegisterVPTags(
{
	You = function(c, s) if isPlayer then return "You" else return c.HeSheIt() end end,
	Your = function(c, s) if isPlayer then return "Your" else return c.HisHerIts() end end,
	you = function(c, s) if isPlayer then return "you" else return c.HeSheIt(true) end end,
	your = function(c, s) if isPlayer then return "your" else return c.HisHerIts(true) end end,
	Youorname = function(c, s) if isPlayer then return "You" else return c.GetKnownName(false, false, true, true) end end,
	youorname = function(c, s) if isPlayer then return "you" else return c.GetKnownName(false, false, true) end end,
	Yourornames = function(c, s) if isPlayer then return "Your" else return (c.GetKnownName(false, false, true, true) .. "'s") end end,
	yourornames = function(c, s) if isPlayer then return "your" else return (c.GetKnownName(false, false, true, false) .. "'s") end end,

	isme = function(c, s) if isPlayer then return s[0] else return s[1] end end,
	g = function(c, s)
			local g = c.Gender
			if g == Gender.Male then return s[0] end
			if (g == Gender.Herm and not string.IsNullOrEmpty(s[2])) then return s[2] end
			return s[1]
		end,
	t = function(c, s)
			local t = c.Path(s[0])
			if t == nil then return "<404>" end
			return t.Text.ToLower()
		end,
	T = function(c, s)
			local t = c.Path(s[0])
			if t == nil then return "<404>" end
			return t.Text
		end,
	v = function(c, s)
			local t = c.Path(s[0])
			if t == nil then return "<404>" end
			return t.Value
		end,
	l = function(c, s)
			local t = c.Path(s[0])
			if t == nil then return "<404>" end
			return Descriptions.Length(t.Value)
		end,
	p = function(c, s)
			local ct = tonumber(s[0])
			if (ct == 1) then return (s[0] .. " " .. s[1]) end
			return (s[0] .. " " .. Pluralize(s[1]))
		end,
	P = function(c, s)
			local ct = tonumber(s[0])
			if (ct == 1) then return s[1] end
			return Pluralize(s[1])
		end,

	name = function(c, s) return c.GetKnownName(false, false, true) end,
	fullname = function(c, s) return c.GetKnownName(true, false, true) end,
	fullname2 = function(c, s) return c.GetKnownName(true, false, false) end,
	title = function(c, s) return c.Title end,
	a = function(c, s) return c.A end,
	gender = function(c, s) return c.Gender.ToString().ToLowerInvariant() end,
	His = function(c, s) if isPlayer then return "Your" else return c.HisHerIts() end end,	
	He = function(c, s) if isPlayer then return "You" else return c.HeSheIt() end end,	
	Him = function(c, s) if isPlayer then return "You" else return c.HimHerIt() end end,	
	his = function(c, s) if isPlayer then return "your" else return c.HisHerIts(true) end end,	
	he = function(c, s) if isPlayer then return "you" else return c.HeSheIt(true) end end,	
	him = function(c, s) if isPlayer then return "you" else return c.HimHerIt(true) end end,	
	is = function(c, s) if isPlayer then return "are" else return "is" end end,	
	has = function(c, s) if isPlayer then return "have" else return "has" end end,	
	does = function(c, s) if isPlayer then return "do" else return "does" end end,	

	breastsize = function(c, s) return Descriptions.BreastSize(c.GetToken("breasts")) end,
	breastcupsize = function(c, s) return Descriptions.BreastSize(c.GetToken("breasts"), true) end,
	nipplesize = function(c, s) return Descriptions.NippleSize(c.Path("breasts/nipples")) end,
	waistsize = function(c, s) return Descriptions.WaistSize(c.Path("waist")) end,
	buttsize = function(c, s) return Descriptions.ButtSize(c.Path("ass")) end,

	-- PillowShout's stuff
	cocktype = function(c, s)
			if s[0].Length == 0 then s[0] = "0" end
			return Descriptions.CockType(c.GetToken("penis"))
		end,
	-- cockrand = function(c, s) return Descriptions.CockRandom() end,
	-- pussyrand = function(c, s) return Descriptions.PussyRandom() end,
	-- clitrand = function(c, s) return Descriptions.ClitRandom() end,
	-- anusrand = function(c, s) return Descriptions.AnusRandom() end,
	-- buttrand = function(c, s) return Descriptions.ButtRandom() end,
	-- breastrand = function(c, s) return Descriptions.BreastRandom() end,
	-- breastsrand = function(c, s) return Descriptions.BreastRandom(true) end,
	pussywetness = function(c, s)
			if s[0].Length == 0 then s[0] = "0" end
			return Descriptions.Wetness(c.Path("vagina/wetness"))
		end,
	pussylooseness = function(c, s) return Descriptions.Looseness(c.Path("vagina/looseness")) end,
	anuslooseness = function(c, s) return Descriptions.Looseness(c.Path("ass/looseness"), true) end,
	foot = function(c, s) return Descriptions.Foot(c.GetToken("legs")) end,
	feet = function(c, s) return Descriptions.Foot(c.GetToken("legs"), true) end,
	-- cumrand = function(c, s) return Descriptions.CumRandom() end,
	equipment = function(c, s)
			local i = c.GetEquippedItemBySlot(s[0])
			if s[1] == "color" or s[1] == "c" then
				return Descriptions.Item(i, i.tempToken, s[2], true)
			end
			return Descriptions.Item(i, i.tempToken, s[1])
		end,
	tonguetype = function(c, s) return Descriptions.TongueType(c.GetToken("tongue")) end,
	tailtype = function(c, s) return Descriptions.TailType(c.GetToken("tail")) end,
	hipsize = function(c, s) return Descriptions.HipSize(c.GetToken("hips")) end,
	haircolor = function(c, s) return Descriptions.HairColor(c.GetToken("hair")) end,
	hairlength = function(c, s) return Descriptions.HairLength(c.GetToken("hair")) end,
	hairmat = function(c, s) return Descriptions.HairMaterial(c.Path("skin/type").Text) end,	
	bodymat = function(c, s) return Descriptions.BodyMaterial(c.Path("skin/type").Text) end,	
	ballsize = function(c, s) return Descriptions.BallSize(c.GetToken("balls")) end,
	-- End of PillowShout's stuff

	hand = function(c, s) return Descriptions.Hand(c) end,
	hands = function(c, s) return Descriptions.Hand(c, true) end,
})
