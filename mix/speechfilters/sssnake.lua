function SpeechFilter(s)
	-- Don't have gsub, use .Net String's Replace instead.
	s = s.Replace("s", "sss")
	s = s.Replace("S", "Sss");
	return s
end
