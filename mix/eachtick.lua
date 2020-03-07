function Excite(this)
	local char = this.Character
	if char.HasToken("beast") or char.HasToken("sleeping") then return end
--	if player.ParentBoard ~= this.ParentBoard
--		player = nil
--	local ogled = false
	local aroundMe = this.GetCharsWithin(4)
	if #aroundMe == 0 then return end
	for i=0,#aroundMe-1,1 do
		local other = aroundMe[i]
		if other.Character.HasToken("beast") then
			-- nothing
		elseif not this.CanSee(other) then
			-- nothing
		elseif not this.Character.Likes(other.Character) then
			return -- wait
		elseif other.Character.GetStat("charisma") >= 10 then
			--print(this.ToString() .. " is maybe excited by " .. other.ToString())
			local stim = this.Character.GetStat("excitement")
			local theirChar = other.Character.GetStat("charisma")
			local distance = other.DistanceFrom(this)
			local increase = (theirChar / 20) * (distance * 0.25)
			print(this.ToString() .. " is excited for " .. increase .. " points by " .. other.ToString())
			this.Character.Raise("excitement", increase)
			if distance < 2 then
				print(this.ToString() .. " is proximity-excited for 0.25 points by " .. other.ToString())
				this.Character.Raise("excitement", 0.25)
			end
			-- TODO: ogle
		end
	end
end

--[[
public void Excite()
{
		if (other.Character.GetStat("charisma") >= 10)
		{
			/*
			if (!ogled && this != player)
			{
				var oldStim = this.Character.HasToken("oglestim") ? this.Character.GetToken("oglestim").Value : 0;
				if (stim.Value >= oldStim + 20 && player != null && this != player && player.DistanceFrom(this) < 4 && player.CanSee(this))
				{
					NoxicoGame.AddMessage(string.Format("{0} to {1}: \"{2}\"", this.Character.Name, (other == player ? "you" : other.Character.Name.ToString()), Ogle(other.Character)).SmartQuote(this.Character.GetSpeechFilter()), GetEffectiveColor());
					if (!this.Character.HasToken("oglestim"))
						this.Character.AddToken("oglestim");
					this.Character.GetToken("oglestim").Value = stim.Value;
					ogled = true;
				}
			}
			*/
		}
	}
}
]]--

function EachBoardCharTick(who, char)
	Excite(who)
end

function EndPlayerTurn()
	Excite(player)
end
