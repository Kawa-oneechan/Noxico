stats = {
	{ name = "Health",		short = "HP",	default = 10,	panel = nil },
	{ name = "Charisma",	short = "CHA",	default = 10,	panel = { 0x2C0, 0x2C1 } },
	{ name = "Climax",		short = "CLI",	default = 0,	panel = { 0x2C2, 0x2C3 } },
	{ name = "Cunning",		short = "CUN",	default = 10,	panel = { 0x2C4, 0x2C5 } },
	{ name = "Carnality",	short = "CAR",	default = 0,	panel = { 0x2C6, 0x2C7 } },
	{ name = "Stimulation",	short = "STI",	default = 10,	panel = { 0x2C8, 0x2C9 } },
	{ name = "Sensitivity",	short = "SEN",	default = 10,	panel = { 0x2CA, 0x2CB } },
	{ name = "Speed",		short = "SPE",	default = 10,	panel = { 0x2CA, 0x2CC } },
	{ name = "Strength",	short = "STR",	default = 15,	panel = { 0x2CD, 0x2CE } }
}
statsDisplay = {
	rows = 4, cols = 2,
	labelWidth = 3,
	valueWidth = 3,
	sidePadding = 1
}
-- Character.GetStat() would take a string or int ("climax" or 2 (3?)) instead of a Stat enum.
-- Anything out of band simply returns 0 and maybe whines a little.
-- Add Character.SetStat() and IncreaseStat(), which whine and set nothing if out of band.
-- Keep the Stat enum version around for now but have it redirect to the string version. And whine.

-- Add a thing where *-init.lua files are run after the main init so mods can replace bits:
--[[
stats = {
    { name = "Health",		short = "HP",	default = 10,	panel = nil },
    { name = "Charisma",	short = "CHA",	default = 10,	panel = { 0x2C0, 0x2C1 } },
    { name = "Speed",		short = "SPE",	default = 10,	panel = { 0x2CA, 0x2CC } },
    { name = "Strength",	short = "STR",	default = 15,	panel = { 0x2CD, 0x2CE } }
}
statsDisplay.rows = 3;
statsDisplay.cols = 1;
]]--

function DrawStatus()
	HostForm.Write(string.rep(" ", 80), Color.Silver, Color.Transparent, 24, 0, true);
	
	local character = player.Character;
	local hpNow = character.Health;
	local hpMax = character.MaximumHealth;
	local hpBarLength = math.ceil((hpNow / hpMax) * 18);
	HostForm.Write(string.rep(" ", 18), Color.White, Color.FromArgb(9, 21, 39), 24, 0);
	HostForm.Write(string.rep(" ", hpBarLength), Color.White, Color.FromArgb(30, 54, 90), 24, 0);
	HostForm.Write(hpNow .. " / " .. hpMax, Color.White, Color.Transparent, 24, 1);
	
	HostForm.SetCell(24, 19, player.Glyph, player.ForegroundColor, player.BackgroundColor);
	if (character.Gender == Gender.Male) then
		HostForm.SetCell(24, 21, 0x0B, Color.FromArgb(30, 54, 90), Color.Transparent)
	elseif (character.Gender == Gender.Female) then
		HostForm.SetCell(24, 21, 0x0C, Color.FromArgb(90, 30, 30), Color.Transparent)
	else
		HostForm.SetCell(24, 21, 0x15D, Color.FromArgb(84, 30, 90), Color.Transparent)
	end
	HostForm.Write(character.Name.ToString(false), Color.White, Color.Transparent, 24, 23);
	
	local mods = "";
	if (character.HasToken("haste")) then mods = mods .. i18n.GetString("mod_haste") end
	if (character.HasToken("slow")) then mods = mods .. i18n.GetString("mod_slow") end
	if (character.HasToken("flying")) then mods = mods .. i18n.Format("mod_flying", (character.GetToken("flying").Value / 100) * 100) end
	if (character.HasToken("swimming")) then
		if (character.GetToken("swimming").Value == -1) then
			mods = mods .. i18n.GetString("mod_swimmingunl")
		else
			mods = mods .. i18n.Format("mod_swimming", (character.GetToken("swimming").Value / 100) * 100)
		end
	end
	HostForm.Write(mods, Color.Silver, Color.Transparent, 24, 79 - mods:len());
	
	local statsLength = ((statsDisplay.labelWidth + statsDisplay.valueWidth) * statsDisplay.cols) + (statsDisplay.sidePadding * (statsDisplay.cols + 1));
	local statsBack = string.rep(" ", statsLength);
	local statRow = 24 - statsDisplay.rows;
	local statCol = 80 - statsLength;
	for i = statRow, 23 do HostForm.Write(statsBack, Color.Silver, Color.Transparent, i, statCol, true) end
	local statCol1 = statCol + 1;
	local statCol2 = statCol1 + statsDisplay.labelWidth;
	for _, stat in ipairs(stats) do
		if (stat.panel ~= nil) then
			local col = statCol1;
			if (type(stat.panel) == "table") then
				for _, c in ipairs(stat.panel) do
					HostForm.SetCell(statRow, col, c, Color.Silver, Color.Transparent);
					col = col + 1;
				end
			else
				HostForm.Write(stat.panel, Color.Silver, Color.Transparent, statRow, col);
			end

			local color = Color.Gray;
			local total = "-?-";
			if character.HasToken(stat.name.ToLower()) then
				local statBonus = character.GetToken(stat.name.ToLower() .. "bonus").Value;
				local statBase = character.GetToken(stat.name.ToLower()).Value;
				total = math.ceil(statBase + statBonus);
				if (statBonus > 0) then color = Color.White
				elseif (statBonus < 0) then color = Color.Maroon end
			end
			
			col = col + 1;
			HostForm.Write(total, color, Color.Transparent, statRow, statCol2);
			statRow = statRow + 1;
			if (statRow == 24) then
				statRow = 24 - statsDisplay.rows;
				statCol1 = statCol2 + statsDisplay.valueWidth + 1;
				statCol2 = statCol1 + statsDisplay.labelWidth;
			end
		end
	end
end
