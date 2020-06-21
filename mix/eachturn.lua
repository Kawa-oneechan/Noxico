function Excite(this)
	print(this.ToString() .. " is at " .. this.Energy)
	if this.Energy < 5000 then
		print(this.ToString() .. " is an idle fucker")
		return
	end
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
			-- print(this.ToString() .. " is maybe excited by " .. other.ToString())
			local stim = this.Character.GetStat("excitement")
			local theirChar = other.Character.GetStat("charisma")
			local distance = other.DistanceFrom(this)
			local increase = (theirChar / 20) * (distance * 0.25)
			-- print(this.ToString() .. " is excited for " .. increase .. " points by " .. other.ToString())
			this.Character.Raise("excitement", increase)
			if distance < 2 then
				-- print(this.ToString() .. " is proximity-excited for 0.25 points by " .. other.ToString())
				this.Character.Raise("excitement", 0.25)
			end
			-- TODO: ogle
		end
	end
end

--[[
(When porting this to Lua, note that this came from the Character class, not BoardChar.
public void UpdateOviposition()
{
	if (BoardChar == null)
		return;
	if (this.HasToken("egglayer") && this.HasToken("vagina"))
	{
		var eggToken = this.GetToken("egglayer");
		eggToken.Value++;
		if (eggToken.Value == 500)
		{
			eggToken.Value = 0;
			var egg = new DroppedItem("egg")
			{
				XPosition = BoardChar.XPosition,
				YPosition = BoardChar.YPosition,
				ParentBoard = BoardChar.ParentBoard,
			};
			egg.Take(this, BoardChar.ParentBoard);
			if (BoardChar is Player)
				NoxicoGame.AddMessage(i18n.GetString("youareachicken").Viewpoint(this));
			return;
		}
	}
	return;
}
]]--

function EachBoardCharTurn(who, char)
	Excite(who)
--	UpdateOviposition(who)
end
