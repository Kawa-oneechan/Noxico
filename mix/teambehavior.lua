--[[ Team behavior actions!
	* 0 - do nothing
	* 1 - attack
	* 2 - preferential attack
	* 3 - avoid
	* 4 - flock
	* 5 - flock to similar
	* ...
	* 8 - close-by attack -- collapses to "nothing" or "preferential". IF RETURNED, SHIT'S FUCKED.
	* 9 - thiefing player check -- collapses like #8.
]]--

local teamBehaviorForAttacking =
{
	{ 0, 0, 0, 0, 0, 0, 0, 0, 0 },
	{ 0, 0, 1, 0, 0, 1, 0, 1, 1 },
	{ 1, 2, 0, 1, 1, 0, 0, 1, 0 },
	{ 0, 0, 1, 0, 0, 1, 0, 1, 0 },
	{ 0, 9, 1, 0, 0, 8, 0, 0, 0 },
	{ 0, 1, 1, 1, 1, 0, 2, 1, 2 },
	{ 0, 0, 0, 0, 0, 8, 0, 0, 0 },
	{ 0, 1, 0, 0, 0, 0, 0, 0, 0 },
	{ 0, 0, 0, 0, 0, 0, 0, 0, 0 }
}
local teamBehaviorForFlocking =
{
	{ 0, 0, 3, 0, 0, 0, 0, 0, 0 },
	{ 0, 0, 0, 0, 0, 0, 0, 0, 0 },
	{ 0, 0, 5, 0, 0, 0, 0, 0, 5 },
	{ 0, 4, 0, 0, 0, 0, 0, 0, 0 },
	{ 0, 0, 0, 0, 0, 0, 0, 0, 0 },
	{ 0, 0, 0, 0, 0, 5, 0, 0, 0 },
	{ 0, 0, 3, 0, 0, 3, 5, 0, 0 },
	{ 0, 0, 3, 0, 0, 0, 0, 0, 0 },
	{ 0, 3, 0, 3, 3, 3, 0, 3, 4 },
}

function DecideTeamBehavior(me, other, whatFor)
	local myTeam = me.Team + 1
	local theirTeam = other.Team + 1
	local action = 0

	if myTeam == 1 then -- We are the player
		return 0 -- Nothing
	end

	if whatFor == 0 then
		action = teamBehaviorForAttacking[myTeam][theirTeam]
		if action == 8 then -- close-by attack
			if me.DistanceFrom(other.BoardChar) > 4 then
				action = 0 -- do nothing
			else
				action = 1 -- attack
			end
		end
		if action == 9 then -- thiefing player
			-- if other.BoardChar is Player and other.HasToken("criminal") then
			--	action = 1 -- attack
			-- else
				action = 0 -- do nothing
			-- end
		end
		-- TODO: consider running away at low health, returning TBA.Avoid.
		if action == 1 then -- attacking... but consider the ramifications for a moment here.
			if (not me.BoardChar.ConsiderAttack(other.BoardChar)) then
				action = 3 -- get away
			end
		end
	else
		action = teamBehaviorForFlocking[myTeam][theirTeam]
		if action == 5 then -- flock to like -- TODO: check if >= 3 is any good.
			if me.GetClosestBodyplanMatch().GetHammingDistance(other.GetClosestBodyplanMatch()) >= 3 then
				action = 0 -- nothing
			else
				action = 4 -- flock
			end
		end
	end
	return action
end
