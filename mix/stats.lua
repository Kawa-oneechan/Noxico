stats = {
	{ name = "Health",		short = "HP",	default = 10,	onchange = false,	panel = nil },
	{ name = "Charisma",	short = "CHA",	default = 10,	onchange = false,	panel = { 0x2C0, 0x2C1 } },
	{ name = "Pleasure",	short = "PLS",	default = 0,	onchange = true,	panel = { 0x2C2, 0x2C3 } },
	{ name = "Mind",		short = "MIN",	default = 10,	onchange = false,	panel = { 0x2C4, 0x2C5 } },
	{ name = "Vice",		short = "VIC",	default = 0,	onchange = false,	panel = { 0x2C6, 0x2C7 } },
	{ name = "Excitement",	short = "EXC",	default = 10,	onchange = false,	panel = { 0x2C8, 0x2C9 } },
	{ name = "Libido",		short = "LIB",	default = 10,	onchange = false,	panel = { 0x2CA, 0x2CB } },
	{ name = "Speed",		short = "SPE",	default = 10,	onchange = false,	panel = { 0x2CC, 0x2CD } },
	{ name = "Body",		short = "BOD",	default = 15,	onchange = false,	panel = { 0x2CE, 0x2CF } }
}
statsDisplay = {
	rows = 4, cols = 2,
	labelWidth = 3,
	valueWidth = 3,
	sidePadding = 1
}

DefineStats(stats, statsDisplay);

function AdjustStat(character, stat, baseAmount)
	if stat == "Pleasure" then
		-- 0 stim gives only 50% of pleasure increase
		-- 50 stim gives 125% pleasure increase
		-- 100 stim gives 200% pleasure increase	 
		local exciteBonus = 0.5 + (character.GetStat("excitement") * 0.015);
		local viceBonus = 0.5 + (character.GetStat("vice")   * 0.015);
		return baseAmount * exciteBonus * viceBonus;
	end
	return baseAmount;
end

function TickStats(character)
	-- TODO: Every tick, have a fraction of Libido added to Excitement.
	-- TODO: Sufficiently high Excitement should add to Pleasure
end

-- Character.GetStat() would take a string or int ("pleasure" or 2 (3?)) instead of a Stat enum.
-- Anything out of band simply returns 0 and maybe whines a little.
-- Add Character.SetStat() and IncreaseStat(), which whine and set nothing if out of band.
-- Keep the Stat enum version around for now but have it redirect to the string version. And whine.

-- Add a thing where *-init.lua files are run after the main init so mods can replace bits:
--[[
stats = {
    { name = "Health",		short = "HP",	default = 10,	panel = nil },
    { name = "Charisma",	short = "CHA",	default = 10,	panel = { 0x2C0, 0x2C1 } },
    { name = "Speed",		short = "SPE",	default = 10,	panel = { 0x2CA, 0x2CC } },
    { name = "Body",		short = "BOD",	default = 15,	panel = { 0x2CD, 0x2CE } }
}
statsDisplay.rows = 3;
statsDisplay.cols = 1;
]]--

