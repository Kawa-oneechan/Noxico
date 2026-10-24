function SpeechFilter(s)
	local squelches = { 'bfrr', 'plhbt', 'slrp', 'brlf' }
	local len = #s
	s = "";
	while len > 0 do
		s = s .. squelches[Random.Next(1, #squelches)]
		len = len - 8
	end
	return s
end
