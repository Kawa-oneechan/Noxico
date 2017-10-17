local ret = 0
local attackType = 0 --punch
local skinType = 0 --regular skin

if weapon == undefined then
	if target.Path("skin/type").Text == "fur" then --or perhaps they have nails?
		attackType = 1 --tear
	elseif target.HasToken("snaketail") then
		attackType = 2 --strike
	elseif target.HasToken("quadruped") or target.HasToken("taur") then
		attackType = 3 --kick
	end
	--monoceros check?
else
	local what = weapon.Path("attackType") and weapon.Path("attackType").Text or "strike"
	local attackTypes = { "punch", "tear", "strike", "kick", "stab", "pierce", "crush" }
	-- For whatever reason, we can't seem to do for k,v in loops :(
	for i = 1, #attackTypes, 1 do
		if what == attackTypes[i] then
			attackType = i
			break
		end
	end
end

local ourSkin = target.Path("skin/type").Text
if ourSkin == "carapace" then ourSkin = "scales" end
local skins = { "skin", "fur", "scales", "metal", "slime", "rubber" }
for i = 1, #skins, 1 do
	if ourSkin == skins[i] then
		skinType = i
		break
	end
end

local factors =
{ --skin   fur    scales metal slime  rubber
	{ 1,     1,     1,     0.5,  0.75,  0.5  }, --punch
	{ 1,     1,     0.5,   0.5,  0.75,  2    }, --tear
	{ 1,     1,     1,     0.5,  0.75,  0.5  }, --strike
	{ 1.1,   1.1,   1.1,   0.6,  0.78,  0.6  }, --kick (punch harder)
	{ 1.5,   1.5,   1.2,   0.5,  0.75,  1    }, --stab
	{ 1.25,  1.25,  1.5,   1,    0.75,  1    }, --pierce
	{ 1.5,   1.5,   1,     1,    2,     0.5  }, --crush
}

ret = factors[attackType][skinType]
--TODO: do something like the above for any armor or shield being carried by the defender
--TODO: involve +N modifiers on both weapon and armor/shield

return ret
