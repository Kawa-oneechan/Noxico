function SpeechFilter(s)
	-- Don't have gsub, use .Net String's Replace instead.
	s = s.Replace("R", "W")
	s = s.Replace("r", "w");
	return s
end
